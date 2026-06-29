namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Globalization;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Production <see cref="IEffectMutationSink"/> that applies card effect mutations to the
/// authoritative runtime store (<see cref="ICardInstanceRepository"/> metadata), which the
/// block/battle/security processors and the observation encoder read.
///
/// W2 vocabulary (synchronous, card-instance metadata):
/// <list type="bullet">
/// <item>Keyword grants → boolean flags (Blocker/Rush/Reboot/PreventBattleDeletion/SecurityCheck/
/// Blitz/Retaliation/ArmorPurge).</item>
/// <item><see cref="AddDpModifierKind"/> → appends a typed <see cref="DpModifier"/> to the target's
/// <c>dpModifiers</c> list (read by <c>BattleResolver</c> and <c>CardObservationView</c>).</item>
/// <item><see cref="SuspendKind"/> / <see cref="UnsuspendKind"/> → the <c>isSuspended</c> flag.</item>
/// <item><see cref="SetFlagKind"/> / <see cref="ClearFlagKind"/> → an arbitrary named flag (for
/// restrictions, once-per-turn markers, custom state).</item>
/// </list>
/// Async vocabulary (zone moves, draw, memory) is W2-follow: those need a deferred flush with the
/// engine context and are not yet handled here — they are recorded as unsupported.
/// </summary>
public sealed class MatchStateMutationSink : IEffectMutationSink
{
    public const string TargetEntityIdKey = "targetEntityId";
    public const string DpModifiersKey = "dpModifiers";
    public const string SuspendedFlagKey = "isSuspended";

    // Mutation kinds (the effect→state vocabulary contract for Phase 4 card porting).
    public const string AddDpModifierKind = "AddDpModifier";
    public const string SuspendKind = "Suspend";
    public const string UnsuspendKind = "Unsuspend";
    public const string SetFlagKind = "SetFlag";
    public const string ClearFlagKind = "ClearFlag";

    // CV-B1: effect-driven deletion (destroy a Digimon). Unlike TrashCard (a raw zone move), Delete
    // honours deletion-prevention — the static `cannotBeDeleted` flag and continuous Delete/Prevent
    // replacements (same source the BattleDeletionGate consults) — and stamps `deletedByEffect` for any
    // OnDeletion triggers. (OnDeletion timing emission is wired separately in CV-A4.)
    public const string DeleteKind = "Delete";
    public const string CannotBeDeletedFlagKey = "cannotBeDeleted";
    public const string DeletedByEffectKey = "deletedByEffect";
    public const string DeletionPreventedKey = "deletionPrevented";

    // W2-follow: async / controller-backed kinds (applied on flush or via the memory controller).
    public const string TrashCardKind = "TrashCard";
    public const string ReturnToHandKind = "ReturnToHand";
    public const string ReturnToDeckTopKind = "ReturnToDeckTop";
    public const string ReturnToDeckBottomKind = "ReturnToDeckBottom";
    public const string AddToSecurityKind = "AddToSecurity";
    public const string DrawCardsKind = "DrawCards";
    // B-6: effect-driven security operations (player-scoped batches over IZoneMover primitives).
    public const string RecoverKind = "Recover";              // top N library -> security (AS-IS IRecovery/IAddSecurityFromLibrary)
    public const string TrashSecurityKind = "TrashSecurity";  // N security -> trash (AS-IS IDestroySecurity), emits OnDiscardSecurity
    // B-9: create N token Digimon on the controller's battle area (AS-IS CardEffectCommons.PlayToken).
    public const string CreateTokenKind = "CreateToken";
    public const string TokenDefinitionIdKey = "tokenDefinitionId";
    public const string TokenInstanceIdKey = "tokenInstanceId";
    public const string TokenTappedKey = "tokenTapped";
    public const string AddMemoryKind = "AddMemory";
    public const string SetMemoryKind = "SetMemory";
    // F-3.7: effect-driven play — moves the target from its source zone onto the battle area face up
    // and marks it as having entered this turn (summoning sickness). "Play for free"; a memory cost, if
    // any, is paid by the effect before emitting this mutation.
    public const string PlayCardKind = "PlayCard";
    // B-10: effect-driven trash / return of a Digimon's digivolution sources, and trash of its link cards.
    public const string TrashDigivolutionCardsKind = "TrashDigivolutionCards"; // target = host; count, fromBottom
    public const string ReturnDigivolutionCardsKind = "ReturnDigivolutionCards"; // target = host; count, toDeck
    public const string TrashLinkCardsKind = "TrashLinkCards";                   // target = host; count (default all)
    public const string FromBottomKey = "fromBottom";
    public const string ToDeckKey = "toDeck";

    // Value keys.
    public const string DpValueKey = "value";
    public const string DpAbsoluteKey = "absolute";
    public const string DpActivatedOrderKey = "activatedOrder";
    public const string FlagKeyKey = "flagKey";
    public const string PlayerIdKey = "playerId";
    public const string CountKey = "count";
    public const string FaceUpKey = "faceUp";
    public const string FromTopKey = "fromTop";
    // N-3: optional override to insert a returned card at the security BOTTOM instead of the default top.
    public const string ToBottomKey = "toBottom";
    public const string AmountKey = "amount";
    // F-3.7: the zone the played card comes from (defaults to Hand).
    public const string FromZoneKey = "fromZone";
    public const string EnteredThisTurnKey = "enteredThisTurn";
    // D-8: optional memory cost paid when an effect plays a card "for cost" (PlayForCost). The effect
    // resolves the (reduced) cost via the cost pipeline and passes it; absent/0 = play for free.
    public const string MemoryCostKey = "memoryCost";

    private static readonly IReadOnlyDictionary<string, string> KindToFlag =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GrantBlocker"] = "hasBlocker",
            ["GrantRush"] = "hasRush",
            ["ScheduleRebootUnsuspend"] = "scheduleRebootUnsuspend",
            ["PreventBattleDeletion"] = "preventBattleDeletion",
            ["SetSecurityCheck"] = "pendingSecurityCheck",
            // W2: previously-dropped keyword kinds now write a flag (consumers wired per keyword).
            ["RequestBlitzAttack"] = "hasBlitz",
            ["DeleteRetaliationTarget"] = "hasRetaliation",
            ["ApplyArmorPurge"] = "hasArmorPurge",
            // C-4 Decoy / C-5 Barrier / C-7 Evade: defense-keyword grants consumed by DeletionReplacementGate.
            ["GrantEvade"] = "hasEvade",
            ["GrantBarrier"] = "hasBarrier",
            ["GrantDecoy"] = "hasDecoy",
            ["GrantFortitude"] = "hasFortitude",
            // C-3 Raid: switch-defender keyword grant consumed by RaidAttackSwitch.
            ["GrantRaid"] = "hasRaid",
            // C-10 Collision: forced-block keyword grant consumed by BlockTiming.
            ["GrantCollision"] = "hasCollision",
            // C-9 Execute: grants consumed by AttackPermanentAction (attack unsuspended) + AttackPipeline
            // (self-delete at end of attack).
            ["GrantAttackUnsuspended"] = "canAttackUnsuspendedDigimon",
            ["GrantDeleteSelfAtEndOfAttack"] = "deleteSelfAtEndOfAttack",
            // C-11 Fragment / C-17 Ascension / C-19 Scapegoat: deletion-family grants consumed by
            // DeletionReplacementGate.
            ["GrantFragment"] = "hasFragment",
            ["GrantAscension"] = "hasAscension",
            ["GrantScapegoat"] = "hasScapegoat",
            // C-22 Save: post-deletion attach-to-stack grant consumed by DeletionReplacementGate.
            ["GrantSave"] = "hasSave",
            // C-12 Iceclad: battle-comparison keyword grant consumed by BattleResolver (compare by
            // digivolution-source count instead of DP when either combatant has it).
            ["GrantIceclad"] = "hasIceclad",
            // C-13 Decode: post-(effect)-removal play-a-source-for-free grant consumed by DeletionReplacementTiming.
            ["GrantDecode"] = "hasDecode",
            // C-18 Alliance: on-attack suspend-an-ally boost grant consumed by AllianceAttackBoost.
            ["GrantAlliance"] = "hasAlliance",
            // C-20 Vortex (S1): effect-driven attack grant consumed by EffectDrivenAttack.
            ["GrantVortex"] = "hasVortex",
            // C-16 Overclock (S3+S1): end-of-turn delete-trait-ally + untapped attack grant consumed by OverclockEffect.
            ["GrantOverclock"] = "hasOverclock",
            // C-14 Partition (S4): post-(effect)-removal play-two-sources-free grant consumed by DeletionReplacementTiming.
            ["GrantPartition"] = "hasPartition",
            // C-15 Progress (S2): attack-time opponent-effect immunity grant consumed by ProgressImmunity/ContinuousImmunityGate.
            ["GrantProgress"] = "hasProgress",
        };

    private readonly ICardInstanceRepository _repository;
    private readonly IZoneMover? _zoneMover;
    private readonly IHeadlessMemoryController? _memory;
    private readonly EffectRegistry? _effectRegistry;
    private readonly GameEventQueue? _gameEventQueue;
    private readonly ILogSink? _log;
    private readonly List<AppliedMutation> _applied = new();
    private readonly List<EffectMutation> _unsupported = new();
    private readonly List<EffectMutation> _skipped = new();
    private readonly List<Func<CancellationToken, Task>> _pendingAsync = new();

    public MatchStateMutationSink(
        ICardInstanceRepository repository,
        ILogSink? log = null,
        IZoneMover? zoneMover = null,
        IHeadlessMemoryController? memory = null,
        EffectRegistry? effectRegistry = null,
        GameEventQueue? gameEventQueue = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _log = log;
        _zoneMover = zoneMover;
        _memory = memory;
        _effectRegistry = effectRegistry;
        _gameEventQueue = gameEventQueue;
    }

    public int AppliedCount => _applied.Count;

    public int UnsupportedCount => _unsupported.Count;

    public int SkippedCount => _skipped.Count;

    public IReadOnlyList<AppliedMutation> Applied => _applied.ToArray();

    public IReadOnlyList<EffectMutation> Unsupported => _unsupported.ToArray();

    public IReadOnlyList<EffectMutation> Skipped => _skipped.ToArray();

    public void Apply(EffectMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        // Unknown kinds are reported as unsupported BEFORE the target is checked, so an effect that
        // emits a mutation this sink does not understand is surfaced regardless of its target.
        if (!IsKnownKind(mutation.Kind))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
            return;
        }

        // Player-scoped / global mutations (no specific card target).
        switch (mutation.Kind)
        {
            case AddMemoryKind:
                ApplyMemory(mutation, isSet: false);
                return;
            case SetMemoryKind:
                ApplyMemory(mutation, isSet: true);
                return;
            case DrawCardsKind:
                ApplyDraw(mutation);
                return;
            case RecoverKind:
                ApplyRecover(mutation);
                return;
            case TrashSecurityKind:
                ApplyTrashSecurity(mutation);
                return;
            case CreateTokenKind:
                ApplyCreateToken(mutation);
                return;
        }

        HeadlessEntityId targetId = ResolveTargetId(mutation);
        if (targetId.IsEmpty
            || !_repository.TryGetInstance(targetId, out CardInstanceRecord? record)
            || record is null)
        {
            _skipped.Add(mutation);
            _log?.Warn(
                $"Effect mutation '{mutation.Kind}' targeted card '{targetId.Value}' which is not in the instance repository.");
            return;
        }

        // S2 (C-15 Progress): an active opponent-only immunity on the target prevents an opponent-sourced
        // effect mutation. No-op unless an immunity is registered (source-relativity skips own/ally effects).
        if (Runtime.ContinuousImmunityGate.BlocksOpponentEffect(_effectRegistry, _repository, targetId, mutation.SourceEntityId))
        {
            _skipped.Add(mutation);
            _log?.Warn($"Effect mutation '{mutation.Kind}' on '{targetId.Value}' was prevented by immunity (opponent effect).");
            return;
        }

        if (KindToFlag.TryGetValue(mutation.Kind, out string? flagKey))
        {
            WriteMetadata(record, targetId, mutation.Kind, flagKey, true);
            return;
        }

        switch (mutation.Kind)
        {
            case AddDpModifierKind:
                ApplyDpModifier(mutation, record, targetId);
                break;
            case SuspendKind:
                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, true);
                EmitTiming(TriggerTimings.OnTapped, record.OwnerId);
                break;
            case UnsuspendKind:
                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, false);
                EmitTiming(TriggerTimings.OnUntapped, record.OwnerId);
                break;
            case SetFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: true);
                break;
            case ClearFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: false);
                break;
            case DeleteKind:
                ApplyDelete(mutation, record, targetId);
                break;
            case TrashCardKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToTrashAsync(owner, id, ct));
                break;
            case ReturnToHandKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToHandAsync(owner, id, ct));
                break;
            case ReturnToDeckTopKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckTopAsync(owner, id, ct));
                break;
            case ReturnToDeckBottomKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckBottomAsync(owner, id, ct));
                break;
            case AddToSecurityKind:
                bool faceUp = ReadBool(mutation.Values, FaceUpKey);
                // N-3: default to the security TOP (original AddSecurityCard toTop:true). An effect that
                // needs a bottom insert sets the "toBottom" flag on the mutation.
                bool toTop = !ReadBool(mutation.Values, ToBottomKey);
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToSecurityAsync(owner, id, faceUp, toTop, ct));
                // F-6.4: a face-up add raises the face-up security count — open that timing window.
                if (faceUp)
                {
                    EmitTiming(TriggerTimings.OnFaceUpSecurityIncreased, record.OwnerId);
                }
                break;
            case PlayCardKind:
                ApplyPlayCard(mutation, record, targetId);
                break;
            case TrashDigivolutionCardsKind:
                ApplyDigivolutionSourceRemoval(mutation, targetId, returnToZone: null);
                break;
            case ReturnDigivolutionCardsKind:
                ApplyDigivolutionSourceRemoval(mutation, targetId,
                    returnToZone: ReadBool(mutation.Values, ToDeckKey) ? ChoiceZone.Library : ChoiceZone.Hand);
                break;
            case TrashLinkCardsKind:
                ApplyTrashLinkCards(mutation, targetId);
                break;
            default:
                _unsupported.Add(mutation);
                _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
                break;
        }
    }

    /// <summary>Applies pending asynchronous zone moves / draws deferred by <see cref="Apply"/>.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingAsync.Count == 0)
        {
            return;
        }

        Func<CancellationToken, Task>[] operations = _pendingAsync.ToArray();
        _pendingAsync.Clear();
        foreach (Func<CancellationToken, Task> operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await operation(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ApplyMemory(EffectMutation mutation, bool isSet)
    {
        if (_memory is null)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a memory controller; none is wired.");
            return;
        }

        int amount = ReadInt(mutation.Values, AmountKey) ?? 0;
        if (isSet)
        {
            _memory.Set(amount);
        }
        else
        {
            _memory.Add(amount);
        }

        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "memory"));
    }

    private void ApplyDraw(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        _pendingAsync.Add(ct => zoneMover.DrawAsync(player, count, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "draw"));
    }

    // B-6 Recovery: move the top N library cards into the player's security stack (AS-IS IRecovery →
    // IAddSecurityFromLibrary; face down by default). Player-scoped batch over the zone mover.
    private void ApplyRecover(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool faceUp = ReadBool(mutation.Values, FaceUpKey);
        _pendingAsync.Add(ct => zoneMover.AddSecurityFromLibraryAsync(player, count, faceUp, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "recover"));
        if (faceUp)
        {
            EmitTiming(TriggerTimings.OnFaceUpSecurityIncreased, player);
        }
    }

    // B-6 Trash Security: trash N security cards from the top (or bottom) (AS-IS IDestroySecurity), then emit
    // OnDiscardSecurity so security-discard triggers fire. Player-scoped batch over the zone mover.
    private void ApplyTrashSecurity(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool fromTop = !mutation.Values.ContainsKey(FromTopKey) || ReadBool(mutation.Values, FromTopKey);
        _pendingAsync.Add(ct => zoneMover.TrashSecurityAsync(player, count, fromTop, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "trashSecurity"));
        EmitTiming(TriggerTimings.OnDiscardSecurity, player);
    }

    // B-9 PlayToken: create N token Digimon (IsToken instances of the given token definition) on the
    // controller's battle area, summoning-sick (AS-IS CardEffectCommons.PlayToken). The token definition is
    // supplied by the effect (the porting layer registers token card data); ids are derived from the base
    // instance id (suffixed for quantity > 1).
    private void ApplyCreateToken(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        string? definitionId = ReadString(mutation.Values, TokenDefinitionIdKey);
        string? baseInstanceId = ReadString(mutation.Values, TokenInstanceIdKey);
        if (player.IsEmpty || string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(baseInstanceId))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires '{PlayerIdKey}', '{TokenDefinitionIdKey}' and '{TokenInstanceIdKey}' values.");
            return;
        }

        int count = Math.Max(1, ReadInt(mutation.Values, CountKey) ?? 1);
        bool tapped = ReadBool(mutation.Values, TokenTappedKey);
        for (int index = 1; index <= count; index++)
        {
            var tokenId = new HeadlessEntityId(index == 1 ? baseInstanceId : $"{baseInstanceId}#{index}");
            _repository.Upsert(new CardInstanceRecord(
                tokenId,
                new HeadlessEntityId(definitionId),
                player,
                IsToken: true,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["enteredThisTurn"] = true,
                    [SuspendedFlagKey] = tapped,
                }));
            _pendingAsync.Add(ct => zoneMover.MoveAsync(
                new ZoneMoveRequest(player, tokenId, ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true), ct));
            _applied.Add(new AppliedMutation(mutation.Kind, tokenId, "createToken"));
        }
    }

    private void ApplyZoneMove(
        EffectMutation mutation,
        CardInstanceRecord record,
        HeadlessEntityId targetId,
        Func<IZoneMover, HeadlessPlayerId, HeadlessEntityId, CancellationToken, Task> move)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId owner = record.OwnerId;
        _pendingAsync.Add(ct => move(zoneMover, owner, targetId, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, "pendingMove"));
    }

    private void ApplyDelete(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        // Deletion-prevention: the static cannotBeDeleted flag (card/instance) OR a continuous
        // Delete/Prevent replacement (the same source BattleDeletionGate consults). When prevented, the
        // card stays on the field and the mutation is recorded as skipped with a deletionPrevented marker.
        if (ReadFlag(record.Metadata, CannotBeDeletedFlagKey) || IsDeletionPreventedByContinuous(targetId))
        {
            _skipped.Add(mutation);
            _applied.Add(new AppliedMutation(mutation.Kind, targetId, DeletionPreventedKey));
            return;
        }

        // F-6.8: an OPTIONAL would-be-deleted replacement (Evade / Scapegoat / Fragment / Decoy) is the
        // owner's decision, not an auto-apply (auto would change game rules). DEFER the deletion (flag
        // pendingDeletion) so the common loop surfaces the replacement window; the state-based sweep
        // finishes it only if declined. Decoy is by-ENEMY-effect, checked here while the deleter is known
        // and recorded as a marker the window reads.
        if (_zoneMover is IZoneStateReader preZones)
        {
            bool hasPre = DeletionReplacementTiming.HasPreOption(_repository, preZones, record, byBattle: false);
            bool decoyEligible = DeletionReplacementGate
                .FindDecoyRedirect(_repository, preZones, record, mutation.SourceEntityId) is not null;
            if (hasPre || decoyEligible)
            {
                var deferMetadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [GameFlowProcessor.PendingDeletionKey] = true,
                    [DeletedByEffectKey] = true,
                };
                if (decoyEligible)
                {
                    deferMetadata[DeletionReplacementTiming.DecoyEligibleKey] = true;
                }

                _repository.Upsert(record with { Metadata = deferMetadata });
                _skipped.Add(mutation);
                _applied.Add(new AppliedMutation(mutation.Kind, targetId, GameFlowProcessor.PendingDeletionKey));
                return;
            }
        }

        // (Fragment / Scapegoat / Decoy auto-resolve removed — all are F-6.8 agent choices via the window.)

        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        // Stamp the deletion marker before the move so OnDeletion-scoped triggers can read it.
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DeletedByEffectKey] = true,
        };
        _repository.Upsert(record with { Metadata = metadata });

        HeadlessPlayerId owner = record.OwnerId;
        _pendingAsync.Add(ct => zoneMover.AddToTrashAsync(owner, targetId, ct));
        // C-6 Fortitude: after the deletion completes (card now in trash), a Digimon that had >= 1
        // digivolution source replays itself from the trash for free (OnDestroyed). Enqueued after the
        // trash move so it runs once the card has actually arrived there.
        _pendingAsync.Add(async ct =>
            await DeletionReplacementGate.TryFortitudeReplayAsync(_repository, zoneMover, targetId, ct).ConfigureAwait(false));
        // C-21 Armor Purge is now an OPTIONAL post-deletion agent choice (F-6.8 DeletionReplacementTiming),
        // opened by the common loop once the top is in the trash — no longer auto-applied here.
        // C-17 Ascension / C-22 Save are now OPTIONAL post-deletion agent choices (F-6.8
        // DeletionReplacementTiming), opened by the common loop once the card is in the trash — no longer
        // auto-applied here.
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, DeletedByEffectKey));
    }

    private bool IsDeletionPreventedByContinuous(HeadlessEntityId cardId)
    {
        if (_effectRegistry is null)
        {
            return false;
        }

        ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(
            _effectRegistry,
            new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: cardId));

        foreach (ReplacementEffect replacement in result.Replacements)
        {
            if (replacement.EventKind == ReplacementEventKind.Delete &&
                replacement.ActionKind == ReplacementActionKind.Prevent)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private void ApplyDpModifier(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        int value = ReadInt(mutation.Values, DpValueKey) ?? 0;
        bool absolute = ReadBool(mutation.Values, DpAbsoluteKey);
        long order = ReadLong(mutation.Values, DpActivatedOrderKey) ?? 0;
        string source = mutation.SourceEntityId.Value;

        DpModifier modifier = absolute
            ? DpModifier.Absolute(value, order, source)
            : DpModifier.Relative(value, order, source);

        DpModifier[] existing = record.Metadata.TryGetValue(DpModifiersKey, out object? raw) &&
            raw is IEnumerable<DpModifier> mods
            ? mods.ToArray()
            : Array.Empty<DpModifier>();

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DpModifiersKey] = existing.Append(modifier).ToArray(),
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, DpModifiersKey));
    }

    private void ApplyNamedFlag(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId, bool value)
    {
        string? key = ReadString(mutation.Values, FlagKeyKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{FlagKeyKey}' value.");
            return;
        }

        WriteMetadata(record, targetId, mutation.Kind, key.Trim(), value);
    }

    private void WriteMetadata(CardInstanceRecord record, HeadlessEntityId targetId, string kind, string key, object? value)
    {
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [key] = value,
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(kind, targetId, key));
    }

    /// <summary>(F-3.7) Effect-driven play: move the card from its source zone (default Hand) onto the
    /// battle area face up and mark it entered-this-turn (summoning sickness). The actual move is
    /// deferred to the flush, like the other zone-move kinds.</summary>
    private void ApplyPlayCard(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        ChoiceZone fromZone = ReadZone(mutation.Values, FromZoneKey, ChoiceZone.Hand);
        bool faceUp = !mutation.Values.ContainsKey(FaceUpKey) || ReadBool(mutation.Values, FaceUpKey);
        HeadlessPlayerId owner = record.OwnerId;

        // D-8: pay the (already cost-pipeline-resolved) memory cost for a "play for cost" effect. 0 / no
        // memory controller = play for free.
        int memoryCost = ReadInt(mutation.Values, MemoryCostKey) ?? 0;
        if (memoryCost > 0 && _memory is not null)
        {
            _memory.Pay(memoryCost);
        }

        // Mark summoning sickness synchronously (same metadata flag PlayCardAction sets).
        WriteMetadata(record, targetId, mutation.Kind, EnteredThisTurnKey, true);
        _pendingAsync.Add(ct => zoneMover.MoveAsync(
            new ZoneMoveRequest(owner, targetId, fromZone, ChoiceZone.BattleArea, faceUp), ct));
    }

    /// <summary>(B-10) Trash (returnToZone null) or return the host's digivolution sources. Deferred to
    /// flush like the other zone moves.</summary>
    private void ApplyDigivolutionSourceRemoval(EffectMutation mutation, HeadlessEntityId hostId, ChoiceZone? returnToZone)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        bool fromBottom = !mutation.Values.ContainsKey(FromBottomKey) || ReadBool(mutation.Values, FromBottomKey);
        if (returnToZone is ChoiceZone destination)
        {
            _pendingAsync.Add(ct => DigivolutionStackHelpers.ReturnSourcesAsync(_repository, zoneMover, hostId, count, destination, fromBottom, ct));
        }
        else
        {
            _pendingAsync.Add(ct => DigivolutionStackHelpers.TrashSourcesAsync(_repository, zoneMover, hostId, count, fromBottom, ct));
        }

        _applied.Add(new AppliedMutation(mutation.Kind, hostId, "sourceRemoval"));
    }

    /// <summary>(B-10) Trash up to <c>count</c> (default all) of the host's link cards via LinkHelpers.</summary>
    private void ApplyTrashLinkCards(EffectMutation mutation, HeadlessEntityId hostId)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? int.MaxValue;
        GameEventQueue? queue = _gameEventQueue;
        _pendingAsync.Add(async ct =>
        {
            if (!_repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
            {
                return;
            }

            // Newest-first list; trash from the front up to count.
            foreach (HeadlessEntityId linkCardId in LinkHelpers.ReadLinkedCardIds(host.Metadata).Take(count).ToArray())
            {
                await LinkHelpers.RemoveLinkCardAsync(_repository, zoneMover, hostId, linkCardId, trash: true, queue, ct).ConfigureAwait(false);
            }
        });
        _applied.Add(new AppliedMutation(mutation.Kind, hostId, "trashLinkCards"));
    }

    private static ChoiceZone ReadZone(IReadOnlyDictionary<string, object?> values, string key, ChoiceZone fallback)
    {
        if (values.TryGetValue(key, out object? raw) && raw is not null)
        {
            switch (raw)
            {
                case ChoiceZone zone:
                    return zone;
                case string text when Enum.TryParse(text, ignoreCase: true, out ChoiceZone parsed) && Enum.IsDefined(parsed):
                    return parsed;
            }
        }

        return fallback;
    }

    /// <summary>(CV-A4) Open a global timing window for a state change that is not a zone move (so it is
    /// not derived from a CardMoved event). No-op when the sink was built without a game-event queue.</summary>
    private void EmitTiming(string timing, HeadlessPlayerId actor)
    {
        if (_gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(_gameEventQueue, timing, actor: actor);
        }
    }

    private static bool IsKnownKind(string kind)
    {
        return KindToFlag.ContainsKey(kind)
            || kind is AddDpModifierKind or SuspendKind or UnsuspendKind or SetFlagKind or ClearFlagKind
            || kind is TrashCardKind or ReturnToHandKind or ReturnToDeckTopKind or ReturnToDeckBottomKind
                or AddToSecurityKind or DrawCardsKind or AddMemoryKind or SetMemoryKind
                or DeleteKind or PlayCardKind or RecoverKind or TrashSecurityKind or CreateTokenKind
                or TrashDigivolutionCardsKind or ReturnDigivolutionCardsKind or TrashLinkCardsKind;
    }

    private static HeadlessPlayerId ReadPlayer(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return default;
        }

        return raw switch
        {
            HeadlessPlayerId p => p,
            int i => new HeadlessPlayerId(i),
            long l when l >= int.MinValue && l <= int.MaxValue => new HeadlessPlayerId((int)l),
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => new HeadlessPlayerId(p),
            _ => default,
        };
    }

    private static HeadlessEntityId ResolveTargetId(EffectMutation mutation)
    {
        if (mutation.Values.TryGetValue(TargetEntityIdKey, out object? raw))
        {
            switch (raw)
            {
                case HeadlessEntityId typed when !typed.IsEmpty:
                    return typed;
                case string text when !string.IsNullOrWhiteSpace(text):
                    return new HeadlessEntityId(text.Trim());
            }
        }

        return mutation.SourceEntityId;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            double d when d % 1 == 0 && d is >= int.MinValue and <= int.MaxValue => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
            _ => null,
        };
    }

    private static long? ReadLong(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            long l => l,
            int i => i,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long p) => p,
            _ => null,
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is bool b && b;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is string s ? s : null;
    }
}

public sealed record AppliedMutation(string Kind, HeadlessEntityId TargetId, string FlagKey);
