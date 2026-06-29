namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class BattleResolver
{
    public const string DpKey = "dp";
    public const string DpModifiersKey = "dpModifiers";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DpBeforeBattleKey = "dpBeforeBattle";
    public const string PreventBattleDeletionKey = "preventBattleDeletion";
    public const string HasPiercingKey = "hasPiercing";
    public const string HasRetaliationKey = "hasRetaliation";
    public const string HasIcecladKey = "hasIceclad";
    public const string SourceIdsKey = "sourceIds";

    public async Task<BattleResolutionResult> ResolveAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        HeadlessAttackState attack = context.AttackController.Current;
        BattleParticipant? attacker = null;
        BattleParticipant? defender = null;

        string? validationFailure = ValidateBattle(
            context,
            attack,
            out attacker,
            out defender);
        if (validationFailure is not null)
        {
            return BattleResolutionResult.Failure(validationFailure, attack);
        }

        // G8-003: SYNCHRONOUS OnStartBattle window. Only engaged when such effects are registered (so the
        // common no-effect battle is byte-for-byte unchanged): resolve each participant's
        // [On Start of Battle] effect BEFORE the DP comparison, then recompute participant DP, so a
        // battle-start DP change actually affects the outcome.
        if (context.EffectRegistry.GetEffectsForTiming(TriggerTimings.OnStartBattle).Count > 0)
        {
            await ResolveStartBattleWindowAsync(context, attacker!.OwnerId, attacker!.InstanceId, cancellationToken).ConfigureAwait(false);
            await ResolveStartBattleWindowAsync(context, defender!.OwnerId, defender!.InstanceId, cancellationToken).ConfigureAwait(false);
            if (context.ZoneMover is IZoneStateReader startReader)
            {
                if (TryReadParticipant(context, startReader, attacker!.OwnerId, attacker!.InstanceId, "Attacker", out BattleParticipant? a2) is null && a2 is not null) attacker = a2;
                if (TryReadParticipant(context, startReader, defender!.OwnerId, defender!.InstanceId, "Defender", out BattleParticipant? d2) is null && d2 is not null) defender = d2;
            }
        }

        // G3.5-RL-C2: who is deleted is the DP comparison adjusted by battle keywords.
        int comparison = CompareBattleStats(attacker!, defender!);
        var deleted = new List<BattleParticipant>();
        switch (comparison)
        {
            case > 0:
                deleted.Add(defender);
                break;
            case < 0:
                deleted.Add(attacker);
                break;
            default:
                deleted.Add(attacker);
                deleted.Add(defender);
                break;
        }

        // NOTE: Jamming is intentionally NOT a mutual-deletion rule here. In the original, Jamming is a
        // conditional CanNotBeDestroyedByBattle that protects the ATTACKER only when it battles a
        // Security Digimon. That surface lives in SecurityResolver (W5): the security-Digimon battle
        // honours PreventBattleDeletion on the attacker, which is what Jamming applies. This field
        // battle (attacker vs a battle-area Digimon) is unaffected by Jamming.

        // PreventBattleDeletion (CanNotBeDeletedByBattle): flagged participants survive the battle.
        // R2-1/N-2: also honour continuous deletion-prevention REPLACEMENTS from other cards.
        deleted.RemoveAll(participant =>
            HasFlag(participant, PreventBattleDeletionKey) ||
            BattleDeletionGate.PreventsBattleDeletion(context, participant.InstanceId));

        // F-6.8: a battle deletion is OPTIONAL-replaceable. Flag the DP-losers as pending battle deletions,
        // then resolve the battle in ROUNDS (ResolveRoundAsync): each round applies confirmed Retaliation
        // and either parks for a would-be-deleted window (any flagged participant with an undeclined
        // replacement) or finalizes. A vanilla battle (no keyword, no retaliation) finalizes in this call.
        foreach (BattleParticipant participant in deleted)
        {
            FlagPendingBattleDeletion(context, participant);
        }

        return await ResolveRoundAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(F-6.8) Re-entered each time the pipeline advances <see cref="AttackPhase.DeletionReplacement"/>
    /// (a window resolved). Runs one battle-deletion round.</summary>
    public Task<BattleResolutionResult> FinalizeDeferredAsync(EngineContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveRoundAsync(context, cancellationToken);
    }

    private async Task<BattleResolutionResult> ResolveRoundAsync(EngineContext context, CancellationToken cancellationToken)
    {
        HeadlessAttackState attack = context.AttackController.Current;
        string? validationFailure = ValidateBattle(context, attack, out BattleParticipant? attacker, out BattleParticipant? defender);
        if (validationFailure is not null)
        {
            return BattleResolutionResult.Failure(validationFailure, attack);
        }

        // C-8 Retaliation: a Digimon whose battle death is CONFIRMED (pending + no undeclined replacement)
        // drags its battle opponent down — the opponent becomes a fresh would-be-deleted, so it may
        // Evade/Barrier the retaliation. Fires once per holder; a tie already deletes both.
        foreach ((BattleParticipant dead, BattleParticipant opponent) in new[] { (attacker!, defender!), (defender!, attacker!) })
        {
            if (IsConfirmedDoomed(context, dead.InstanceId) &&
                HasFlag(dead, HasRetaliationKey) &&
                !ReadInstanceFlag(context, dead.InstanceId, RetaliationFiredKey) &&
                !IsStillPendingDeletion(context, opponent.InstanceId) &&
                IsOnBattleArea(context, opponent))
            {
                MarkInstance(context, dead.InstanceId, RetaliationFiredKey);
                FlagPendingBattleDeletion(context, opponent);
                ClearInstanceFlag(context, opponent.InstanceId, DeletionReplacementTiming.ReplacementDeclinedKey);
            }
        }

        // If any flagged participant still needs a would-be-deleted decision, park for its window.
        if (context.ZoneMover is IZoneStateReader zones &&
            new[] { attacker!, defender! }.Any(p => NeedsWindow(context, zones, p.InstanceId)))
        {
            return BattleResolutionResult.Deferred(attack);
        }

        // No more windows — finalize: the still-pending participants are the casualties.
        var deleted = new[] { attacker!, defender! }.Where(p => IsStillPendingDeletion(context, p.InstanceId)).ToList();
        return await FinalizeAsync(context, attacker!, defender!, deleted, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BattleResolutionResult> FinalizeAsync(
        EngineContext context,
        BattleParticipant attacker,
        BattleParticipant defender,
        List<BattleParticipant> deleted,
        CancellationToken cancellationToken)
    {
        bool defenderDeletedNow = deleted.Any(p => p.InstanceId == defender.InstanceId);
        bool attackerSurvives = !deleted.Any(p => p.InstanceId == attacker.InstanceId);

        var movementResults = new List<ZoneMoveResult>();
        foreach (BattleParticipant participant in deleted)
        {
            MarkDeletedByBattle(context, participant);
            movementResults.Add(await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(participant.OwnerId, participant.InstanceId, ChoiceZone.BattleArea, ChoiceZone.Trash),
                cancellationToken).ConfigureAwait(false));
            // F-6.3: open the knock-out window for the Digimon deleted by battle (subject = the card).
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnKnockOut, actor: participant.OwnerId, subject: participant.InstanceId);
        }

        // C-6 Fortitude: mandatory post-deletion replay. (Armor Purge / Ascension / Save are OPTIONAL POST
        // agent choices opened by the common loop once the card is in the trash.)
        foreach (BattleParticipant participant in deleted)
        {
            await DeletionReplacementGate.TryFortitudeReplayAsync(
                context.CardInstanceRepository, context.ZoneMover, participant.InstanceId, cancellationToken).ConfigureAwait(false);
        }

        // Piercing: a surviving attacker that deleted the defender also checks the defending player's
        // security (the AttackPipeline performs the follow-up check).
        bool piercing = attackerSurvives && defenderDeletedNow && HasFlag(attacker, HasPiercingKey);

        EffectDurationExpiry.ExpireBattleEnd(context.EffectRegistry);
        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Battle resolved by DP comparison.");
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndBattle, actor: attacker.OwnerId);

        return BattleResolutionResult.Success(
            resolvedAttack,
            attacker.Dp,
            defender.Dp,
            deleted.Select(p => p.InstanceId).ToArray(),
            movementResults,
            attackerDeleted: deleted.Any(p => p.InstanceId == attacker.InstanceId),
            defenderDeleted: defenderDeletedNow,
            triggersPiercingSecurityCheck: piercing);
    }

    private static void FlagPendingBattleDeletion(EngineContext context, BattleParticipant participant)
    {
        var metadata = new Dictionary<string, object?>(participant.Instance.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.PendingDeletionKey] = true,
            [DeletedByBattleKey] = true,
            [DpBeforeBattleKey] = participant.Dp,
        };
        context.CardInstanceRepository.Upsert(participant.Instance with { Metadata = metadata });
    }

    private static bool IsStillPendingDeletion(EngineContext context, HeadlessEntityId cardId) =>
        ReadInstanceFlag(context, cardId, GameFlowProcessor.PendingDeletionKey);

    public const string RetaliationFiredKey = "retaliationFired";

    /// <summary>A pending battle deletion that still has an UNDECLINED would-be-deleted replacement — the
    /// owner has not yet decided, so a window must open before finalizing.</summary>
    private static bool NeedsWindow(EngineContext context, IZoneStateReader zones, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null ||
            !ReadFlag(record.Metadata, GameFlowProcessor.PendingDeletionKey) ||
            ReadFlag(record.Metadata, DeletionReplacementTiming.ReplacementDeclinedKey))
        {
            return false;
        }

        return DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, record, byBattle: true);
    }

    /// <summary>A pending battle deletion whose death is FINAL — no undeclined replacement remains.</summary>
    private static bool IsConfirmedDoomed(EngineContext context, HeadlessEntityId cardId) =>
        IsStillPendingDeletion(context, cardId) &&
        (context.ZoneMover is not IZoneStateReader zones || !NeedsWindow(context, zones, cardId));

    private static bool IsOnBattleArea(EngineContext context, BattleParticipant participant) =>
        context.ZoneMover is IZoneStateReader zones &&
        zones.GetCards(participant.OwnerId, ChoiceZone.BattleArea).Contains(participant.InstanceId);

    private static bool ReadInstanceFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null &&
        ReadFlag(record.Metadata, key);

    private static void MarkInstance(EngineContext context, HeadlessEntityId cardId, string key)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        context.CardInstanceRepository.Upsert(record with
        {
            Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = true }
        });
    }

    private static void ClearInstanceFlag(EngineContext context, HeadlessEntityId cardId, string key)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null ||
            !record.Metadata.ContainsKey(key))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata.Remove(key);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    /// <summary>(C-12 Iceclad) Mirrors AS-IS <c>CardController.CompareStats</c>: if EITHER combatant has
    /// Iceclad the battle is decided by digivolution-source count instead of DP; otherwise by DP. The
    /// result is clamped to [-1, 1] (attacker-relative: &gt;0 defender loses, &lt;0 attacker loses, 0 tie).</summary>
    private static int CompareBattleStats(BattleParticipant attacker, BattleParticipant defender)
    {
        if (HasFlag(attacker, HasIcecladKey) || HasFlag(defender, HasIcecladKey))
        {
            return Math.Clamp(SourceCount(attacker).CompareTo(SourceCount(defender)), -1, 1);
        }

        return Math.Clamp(attacker.Dp.CompareTo(defender.Dp), -1, 1);
    }

    /// <summary>The number of digivolution sources under a participant (AS-IS <c>DigivolutionCards.Count</c>).</summary>
    private static int SourceCount(BattleParticipant participant)
    {
        return participant.Instance.Metadata.TryGetValue(SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
            ? ids.Count()
            : 0;
    }

    private static bool HasFlag(BattleParticipant participant, string key)
    {
        return ReadFlag(participant.Instance.Metadata, key) || ReadFlag(participant.Definition.Metadata, key);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static string? ValidateBattle(
        EngineContext context,
        HeadlessAttackState attack,
        out BattleParticipant? attacker,
        out BattleParticipant? defender)
    {
        attacker = null;
        defender = null;

        if (!attack.IsPending)
        {
            return "No pending attack exists.";
        }

        if (!attack.AttackingPlayerId.HasValue || !attack.AttackerId.HasValue)
        {
            return "Pending attack has no attacker.";
        }

        if (!attack.DefendingPlayerId.HasValue || !attack.TargetId.HasValue || attack.IsDirectAttack)
        {
            return "Battle resolution requires a non-direct attack target.";
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return "Zone mover does not expose readable zone state.";
        }

        string? attackerFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.AttackingPlayerId.Value,
            attack.AttackerId.Value,
            "Attacker",
            out attacker);
        if (attackerFailure is not null)
        {
            return attackerFailure;
        }

        string? defenderFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.DefendingPlayerId.Value,
            attack.TargetId.Value,
            "Defender",
            out defender);
        if (defenderFailure is not null)
        {
            return defenderFailure;
        }

        return null;
    }

    // (G8-003) Resolve the subject's OnStartBattle effects synchronously through the scheduler (the same
    // collector path the game loop uses), scoped to the subject so only its window fires.
    private static async Task ResolveStartBattleWindowAsync(
        EngineContext context, HeadlessPlayerId actor, HeadlessEntityId subject, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = TriggerTimings.OnStartBattle,
            [AutoProcessingTriggerCollector.SourceEntityIdKey] = subject,
        };
        var gameEvent = new GameEvent(0, GameEventType.StateChanged, $"Timing window: {TriggerTimings.OnStartBattle}", metadata)
        {
            Actor = actor,
            Subject = subject,
            Cause = TriggerTimings.OnStartBattle,
        };
        new AutoProcessingTriggerCollector(context.EffectRegistry).CollectAndEnqueueAll(gameEvent, context.EffectScheduler);
        await context.EffectScheduler.ResolveAllAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? TryReadParticipant(
        EngineContext context,
        IZoneStateReader zoneReader,
        HeadlessPlayerId expectedOwner,
        HeadlessEntityId instanceId,
        string role,
        out BattleParticipant? participant)
    {
        participant = null;

        if (!context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return $"{role} '{instanceId}' was not found.";
        }

        if (instance.OwnerId != expectedOwner)
        {
            return $"{role} '{instanceId}' is not owned by player '{expectedOwner}'.";
        }

        if (!zoneReader.GetCards(expectedOwner, ChoiceZone.BattleArea).Contains(instanceId))
        {
            return $"{role} '{instanceId}' is not in the battle area.";
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null)
        {
            return $"{role} definition '{instance.DefinitionId}' was not found.";
        }

        if (!IsDigimon(definition))
        {
            return $"{role} '{instanceId}' is not a Digimon.";
        }

        if (!TryReadDp(instance.Metadata, definition.Metadata, out int baseDp))
        {
            return $"{role} '{instanceId}' has no battle DP.";
        }

        // G3.5-RL-B1: effective DP = base (printed) DP combined with typed DP modifiers using the
        // original accumulation order. With no modifiers this equals the base DP (no behavior change).
        int staticDp = DpCalculator.ComputeDp(baseDp, ReadDpModifiers(instance.Metadata));

        // N-2 / D-A1: layer continuous DP effects from other cards on top of the static DP (the original
        // GetDP rescans every field/security/player effect each access). No-op until such effects are
        // registered; the gate also honours DP-reduction immunity (D-A3).
        int dp = ContinuousDpGate.ResolveDp(context, instanceId, staticDp);

        participant = new BattleParticipant(instanceId, instance.OwnerId, instance, definition, dp);
        return null;
    }

    private static IReadOnlyList<DpModifier> ReadDpModifiers(IReadOnlyDictionary<string, object?> metadata)
    {
        return metadata.TryGetValue(DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> modifiers
            ? modifiers.ToArray()
            : Array.Empty<DpModifier>();
    }

    private static bool IsDigimon(CardRecord definition)
    {
        return string.Equals(definition.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadDp(
        IReadOnlyDictionary<string, object?> instanceMetadata,
        IReadOnlyDictionary<string, object?> cardMetadata,
        out int dp)
    {
        return TryReadInt(instanceMetadata, DpKey, out dp) ||
            TryReadInt(cardMetadata, DpKey, out dp);
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue % 1 == 0:
                value = (int)doubleValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static void MarkDeletedByBattle(
        EngineContext context,
        BattleParticipant participant)
    {
        var metadata = new Dictionary<string, object?>(participant.Instance.Metadata, StringComparer.Ordinal)
        {
            [DeletedByBattleKey] = true,
            [DpBeforeBattleKey] = participant.Dp,
            // F-6.8: the deletion is now actually applied -> clear any deferred-deletion flag so the
            // state-based sweep does not double-handle it.
            [GameFlowProcessor.PendingDeletionKey] = false,
        };
        // Clear the per-attack F-6.8 markers so a Fortitude-replayed card starts clean.
        metadata.Remove(RetaliationFiredKey);
        metadata.Remove(DeletionReplacementTiming.ReplacementDeclinedKey);

        context.CardInstanceRepository.Upsert(participant.Instance with { Metadata = metadata });
    }

    private sealed record BattleParticipant(
        HeadlessEntityId InstanceId,
        HeadlessPlayerId OwnerId,
        CardInstanceRecord Instance,
        CardRecord Definition,
        int Dp);
}

public sealed record BattleResolutionResult(
    bool IsSuccess,
    string FailureReason,
    HeadlessAttackState Attack,
    int? AttackerDp,
    int? DefenderDp,
    IReadOnlyList<HeadlessEntityId> DeletedCardIds,
    IReadOnlyList<ZoneMoveResult> MovementResults,
    bool AttackerDeleted,
    bool DefenderDeleted,
    bool TriggersPiercingSecurityCheck = false,
    bool RequiresDeletionReplacement = false)
{
    /// <summary>(F-6.8) Battle deletion was deferred for an optional would-be-deleted replacement window;
    /// the pipeline parks at <see cref="AttackPhase.DeletionReplacement"/> and later calls
    /// <see cref="BattleResolver.FinalizeDeferredAsync"/> to finish the battle.</summary>
    public static BattleResolutionResult Deferred(HeadlessAttackState attack) =>
        new(true, string.Empty, attack, null, null, Array.Empty<HeadlessEntityId>(), Array.Empty<ZoneMoveResult>(),
            AttackerDeleted: false, DefenderDeleted: false, TriggersPiercingSecurityCheck: false, RequiresDeletionReplacement: true);

    public static BattleResolutionResult Failure(
        string failureReason,
        HeadlessAttackState attack)
    {
        return new BattleResolutionResult(
            false,
            failureReason,
            attack,
            null,
            null,
            Array.Empty<HeadlessEntityId>(),
            Array.Empty<ZoneMoveResult>(),
            AttackerDeleted: false,
            DefenderDeleted: false);
    }

    public static BattleResolutionResult Success(
        HeadlessAttackState attack,
        int attackerDp,
        int defenderDp,
        IReadOnlyList<HeadlessEntityId> deletedCardIds,
        IReadOnlyList<ZoneMoveResult> movementResults,
        bool attackerDeleted,
        bool defenderDeleted,
        bool triggersPiercingSecurityCheck = false)
    {
        return new BattleResolutionResult(
            true,
            string.Empty,
            attack,
            attackerDp,
            defenderDp,
            deletedCardIds.ToArray(),
            movementResults.ToArray(),
            AttackerDeleted: attackerDeleted,
            DefenderDeleted: defenderDeleted,
            TriggersPiercingSecurityCheck: triggersPiercingSecurityCheck);
    }
}
