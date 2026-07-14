namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-22 Save / C-23 Material Save / C-24 Training) Shared digivolution-stack mutations. All three
/// keywords build on the AS-IS <c>Permanent.AddDigivolutionCardsBottom</c> primitive — placing a card
/// under a permanent as a digivolution source (bottom of the stack). In the headless model the sources
/// live off-field (<see cref="ChoiceZone.None"/>) and are tracked by the top card's <c>sourceIds</c>
/// metadata (ordered top→bottom), so "add to bottom" appends to that list.
///
/// Save is wired as a post-deletion response (<see cref="DeletionReplacementGate.TrySaveAsync"/>);
/// Material Save and Training are ACTIVATED effects with no passive trigger point, so the engine exposes
/// the primitive here and the card-facing activation is authored at porting time.
/// </summary>
public static class DigivolutionStackHelpers
{
    public const string SourceIdsKey = "sourceIds";
    public const string IsSuspendedKey = "isSuspended";
    public const string CanSuspendKey = "canSuspend";

    /// <summary>Moves <paramref name="cards"/> from <paramref name="fromZone"/> off-field and appends them
    /// (in order) to the BOTTOM of <paramref name="targetId"/>'s digivolution stack.
    /// (B-3 tuck reset) When <paramref name="onceFlags"/> is supplied, each tucked card's per-turn use counts are
    /// cleared — AS-IS clears them on EVERY path that puts a card under another permanent:
    /// PlacePermanentToDigivolutionCards runs InitUseCountThisTurn on the tucked card (CardController.cs:3093)
    /// after RemoveField already Init()-reset the whole leaving stack (CardObjectController.cs:546-553); DigiXros
    /// materials reset per card (SelectDigiXrosClass.cs:923); hand/deck/trash-origin cards were reset when they
    /// entered that zone, so the uniform reset is a no-op for them.</summary>
    public static async Task AddSourcesBottomAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId targetId,
        IReadOnlyList<HeadlessEntityId> cards,
        ChoiceZone fromZone,
        CancellationToken cancellationToken = default,
        Effects.OnceFlagController? onceFlags = null,
        GameEventQueue? gameEventQueue = null,
        HeadlessEntityId causeSourceId = default,
        bool skipEffectAndActivateSkill = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0 || !repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null)
        {
            return;
        }

        // (C1d RDW-02) compute the AS-IS from-flags BEFORE the cards move (post-move they read wrong).
        (bool isFromSame, bool isFromDigimon) = ComputeAddDigivolutionFromFlags(repository, zoneMover, target, cards);
        var appended = new List<string>();
        foreach (HeadlessEntityId cardId in cards)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            await zoneMover.MoveAsync(
                new ZoneMoveRequest(card.OwnerId, cardId, fromZone, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
            appended.Add(cardId.Value);
            onceFlags?.ResetForCard(card.OwnerId, cardId);
        }

        AppendSources(repository, target, appended);
        // (F1-Tier2 OnAddDigivolutionCards) AS-IS AddDigivolutionCardsBottom (Permanent.cs:1203-1223) fires
        // OnAddDigivolutionCards when an EFFECT places >=1 card under a permanent, EXCEPT when skipEffectAndActivateSkill.
        // Natural digivolution does NOT reach this helper (DigivolveAction attaches sources itself), so this is purely
        // the effect place-under path. Emit only when a caller supplies the queue + causing-effect source.
        EmitAddDigivolutionCards(gameEventQueue, target.OwnerId, targetId, appended, causeSourceId, skipEffectAndActivateSkill, isFromSame, isFromDigimon);
    }

    /// <summary>(G8 / BT3_019 <c>AddDigivolutionCardsTop</c>) Moves <paramref name="cards"/> from
    /// <paramref name="fromZone"/> off-field and inserts them (in order) at the TOP of
    /// <paramref name="targetId"/>'s digivolution stack (just below the top card). The sourceIds list is
    /// top→bottom, so "add to top" prepends.</summary>
    public static async Task AddSourcesTopAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId targetId,
        IReadOnlyList<HeadlessEntityId> cards,
        ChoiceZone fromZone,
        CancellationToken cancellationToken = default,
        Effects.OnceFlagController? onceFlags = null,
        GameEventQueue? gameEventQueue = null,
        HeadlessEntityId causeSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0 || !repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null)
        {
            return;
        }

        // (C1d RDW-02) compute the AS-IS from-flags BEFORE the cards move.
        (bool isFromSame, bool isFromDigimon) = ComputeAddDigivolutionFromFlags(repository, zoneMover, target, cards);
        var moved = new List<string>();
        foreach (HeadlessEntityId cardId in cards)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            await zoneMover.MoveAsync(
                new ZoneMoveRequest(card.OwnerId, cardId, fromZone, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
            moved.Add(cardId.Value);
            // (B-3 tuck reset) same AS-IS InitUseCountThisTurn mirror as AddSourcesBottomAsync.
            onceFlags?.ResetForCard(card.OwnerId, cardId);
        }

        PrependSources(repository, target, moved);
        // (F1-Tier2 OnAddDigivolutionCards) AS-IS AddDigivolutionCardsTop (Permanent.cs:1064-1119) has NO skip flag —
        // an effect placing >=1 card on top always fires OnAddDigivolutionCards. Effect place-under path only.
        EmitAddDigivolutionCards(gameEventQueue, target.OwnerId, targetId, moved, causeSourceId, skip: false, isFromSame, isFromDigimon);
    }

    /// <summary>(C-23 Material Save) Moves the first <paramref name="count"/> digivolution sources of
    /// <paramref name="fromId"/> to the bottom of <paramref name="toId"/>'s stack (pure re-parent — the
    /// source cards already live off-field). Returns true when at least one source moved.
    /// (B-3) Pass <paramref name="onceFlags"/> ONLY when the AS-IS analog resets the moved cards — e.g. MindLink,
    /// where the whole leaving Tamer stack rode a RemoveField Init() reset. Material Save's AS-IS path has no
    /// witnessed InitUseCountThisTurn, so its sink call leaves this null.</summary>
    public static bool MoveSourcesBottom(
        ICardInstanceRepository repository,
        HeadlessEntityId fromId,
        HeadlessEntityId toId,
        int count,
        Effects.OnceFlagController? onceFlags = null,
        GameEventQueue? gameEventQueue = null,
        HeadlessEntityId causeSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (count < 1 ||
            fromId == toId ||
            !repository.TryGetInstance(fromId, out CardInstanceRecord? source) || source is null ||
            !repository.TryGetInstance(toId, out CardInstanceRecord? destination) || destination is null)
        {
            return false;
        }

        List<string> fromSources = ReadSourceIds(source.Metadata).Select(id => id.Value).ToList();
        if (fromSources.Count == 0)
        {
            return false;
        }

        List<string> moved = fromSources.Take(Math.Min(count, fromSources.Count)).ToList();
        List<string> remaining = fromSources.Skip(moved.Count).ToList();

        // (C1d RDW-02) from-flags BEFORE the re-parent append. Re-parented cards are off-field digivolution
        // sources, so isFromDigimon (a battle-area top) is false; isFromSameDigimon = already a source of the dest.
        var destSources = ReadSourceIds(destination.Metadata).Select(id => id.Value).ToHashSet(StringComparer.Ordinal);
        bool isFromSame = moved.Any(m => destSources.Contains(m));

        repository.Upsert(source with { Metadata = WithSources(source.Metadata, remaining) });
        AppendSources(repository, destination, moved);
        if (onceFlags is not null)
        {
            foreach (string movedValue in moved)
            {
                var movedId = new HeadlessEntityId(movedValue);
                HeadlessPlayerId owner = repository.TryGetInstance(movedId, out CardInstanceRecord? movedCard) && movedCard is not null
                    ? movedCard.OwnerId
                    : destination.OwnerId;
                onceFlags.ResetForCard(owner, movedId);
            }
        }

        // (F1-Tier2 OnAddDigivolutionCards) caller-gated: Material Save's AddDigivolutionCardsBottom(selectedCards)
        // is a real effect place-under and fires (queue + cause supplied); MindLink's re-parent tail (after the
        // Tamer itself was already added via AddSourcesBottomAsync, which emitted) supplies neither, so it stays silent.
        EmitAddDigivolutionCards(gameEventQueue, destination.OwnerId, toId, moved, causeSourceId, skip: false, isFromSame, isFromDigimon: false);
        return true;
    }

    /// <summary>(C-24 Training) Suspends <paramref name="permanentId"/> (the cost) and places the top card
    /// of its owner's library at the bottom of its own stack. Returns true when trained.</summary>
    public static async Task<bool> TrainAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId permanentId,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(permanentId, out CardInstanceRecord? permanent) || permanent is null ||
            ReadFlag(permanent.Metadata, IsSuspendedKey) ||
            !ReadFlag(permanent.Metadata, CanSuspendKey, defaultValue: true) ||
            zoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> library = zones.GetCards(permanent.OwnerId, ChoiceZone.Library);
        if (library.Count == 0)
        {
            return false;
        }

        // Cost: suspend self.
        repository.Upsert(permanent with
        {
            Metadata = new Dictionary<string, object?>(permanent.Metadata, StringComparer.Ordinal)
            {
                [IsSuspendedKey] = true,
            }
        });

        // (F1-Tier2 OnAddDigivolutionCards) Training is an effect place-under — the trained Digimon (permanentId) is the
        // causing effect's source, so the add fires OnAddDigivolutionCards for it.
        await AddSourcesBottomAsync(repository, zoneMover, permanentId, new[] { library[0] }, ChoiceZone.Library, cancellationToken,
            gameEventQueue: gameEventQueue, causeSourceId: permanentId)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>(B-10) Trash <paramref name="count"/> of <paramref name="hostId"/>'s digivolution sources
    /// (from the bottom/DigiEgg end by default) — move them off-field to the trash and drop them from the
    /// host's stack. Returns the number trashed.</summary>
    /// <param name="honorProtection">(C-3) AS-IS effect-trash (<c>ITrashDigivolutionCards</c>) filters
    /// <c>CanNotTrashFromDigivolutionCards</c>-protected sources OUT of the window; the DELETION path
    /// (<c>DiscardEvoRoots</c>) does NOT (no keyword check) and passes false. The scan args
    /// (<paramref name="effectRegistry"/>/<paramref name="context"/>/<paramref name="causingEffectSourceId"/>)
    /// are the AS-IS <c>CanNotTrashFromDigivolutionCards(source, _cardEffect)</c> inputs; supplied only on the
    /// effect-trash path.</param>
    public static Task<int> TrashSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom = true,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null,
        bool honorProtection = true,
        IEffectQueryService? effectRegistry = null,
        Bridge.EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default) =>
        RemoveSourcesAsync(repository, zoneMover, hostId, count, fromBottom, ChoiceZone.Trash, cancellationToken, gameEventQueue, honorProtection, effectRegistry, context, causingEffectSourceId);

    /// <summary>(W6 process) Trash SPECIFIC digivolution sources of <paramref name="hostId"/> (AS-IS
    /// <c>ITrashDigivolutionCards(permanent, selectedCards, …)</c>). Returns the count trashed.</summary>
    public static async Task<int> TrashSpecificSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        IReadOnlyList<HeadlessEntityId> cardIds,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null,
        IEffectQueryService? effectRegistry = null,
        Bridge.EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cardIds);
        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null ||
            !host.Metadata.TryGetValue("sourceIds", out object? raw) || raw is not IEnumerable<string> existing)
        {
            return 0;
        }

        List<string> sources = existing.ToList();
        // First determine which requested cards are real sources (and their owners) WITHOUT moving them yet, so
        // the OnDigivolutionCardDiscarded trigger can fire BEFORE the cards physically leave (AS-IS ordering).
        var discarded = new List<(HeadlessEntityId Id, HeadlessPlayerId Owner)>();
        foreach (HeadlessEntityId cardId in cardIds)
        {
            // (C-3) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards() re-filters CanNotTrashFromDigivolutionCards-
            // protected sources (CardController.cs:5158-5160) — a defensive second filter over the already-narrowed
            // DigiBurst candidate pool. This is an EFFECT-trash path (no deletion caller), so protection is honoured.
            if (IsTrashProtected(repository, cardId.Value, effectRegistry, context, causingEffectSourceId))
            {
                continue;
            }

            if (!sources.Remove(cardId.Value))
            {
                continue;
            }

            HeadlessPlayerId owner = repository.TryGetInstance(cardId, out CardInstanceRecord? card) && card is not null
                ? card.OwnerId
                : host.OwnerId;
            discarded.Add((cardId, owner));
        }

        if (discarded.Count == 0)
        {
            return 0;
        }

        repository.TryGetInstance(hostId, out CardInstanceRecord? refreshed);
        repository.Upsert(refreshed! with
        {
            Metadata = new Dictionary<string, object?>(refreshed!.Metadata, StringComparer.Ordinal) { ["sourceIds"] = sources.ToArray() }
        });

        // (PRIM-P0-timing) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards fires OnDigivolutionCardDiscarded
        // just BEFORE the cards physically move to the trash (CardController.cs:5215, move at :5219).
        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(
                gameEventQueue,
                TriggerTimings.OnDigivolutionCardDiscarded,
                actor: host.OwnerId,
                subject: hostId,
                extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["discardedCardIds"] = string.Join(",", discarded.Select(d => d.Id.Value)) });
        }

        // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards runs `new AceOverflowClass(_trashTargetCards).Overflow()`
        // IMMEDIATELY BEFORE the removal loop (CardController.cs:5219) — an un-flipped ACE source leaving by an
        // effect-trash costs its owner the printed Overflow memory. Same shared pass as the sink's link-card trash.
        ApplyEffectTrashAceOverflow(repository, zoneMover, context, hostId, host.OwnerId, discarded.Select(d => d.Id).ToArray());

        foreach ((HeadlessEntityId id, HeadlessPlayerId owner) in discarded)
        {
            // (MIG3 review P2-2) AS-IS ITrashDigivolutionCards skips AddTrashCard for a TOKEN source
            // (CardController.cs:5227-5230 `if (!cardSource.IsToken)`) — a trashed token vanishes instead of
            // becoming a real trash card. Same isToken handling as DeDigivolveHelpers.ArmorPurgeTopAsync.
            bool isToken = repository.TryGetInstance(id, out CardInstanceRecord? tokenRecord) && tokenRecord is not null
                && tokenRecord.Metadata.TryGetValue("isToken", out object? tokenRaw) && tokenRaw is true;
            if (isToken)
            {
                continue; // already off-stack (ChoiceZone.None) — vanishes, no trash move.
            }

            await zoneMover.MoveAsync(
                new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Trash), cancellationToken).ConfigureAwait(false);
        }

        return discarded.Count;
    }

    /// <summary>(B-10) Return <paramref name="count"/> of <paramref name="hostId"/>'s digivolution sources
    /// to <paramref name="destination"/> (Hand / Library top via MoveToDeck-style zones). Returns the count moved.</summary>
    public static Task<int> ReturnSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        ChoiceZone destination,
        bool fromBottom = true,
        CancellationToken cancellationToken = default,
        GameEventQueue? gameEventQueue = null) =>
        RemoveSourcesAsync(repository, zoneMover, hostId, count, fromBottom, destination, cancellationToken, gameEventQueue);

    /// <summary>(G10-007) Remove a SPECIFIC digivolution source from <paramref name="hostId"/> and move it to
    /// <paramref name="destination"/> (e.g. the battle area, to play it as another Digimon). Returns true if
    /// the source belonged to the host and was moved.</summary>
    public static async Task<bool> PlaySpecificSourceAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId sourceId,
        ChoiceZone destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (sourceId.IsEmpty || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> sources = ReadSourceIds(host.Metadata).Select(id => id.Value).ToList();
        if (!sources.Remove(sourceId.Value))
        {
            return false;
        }

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, sources) });
        HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
            ? src.OwnerId
            : host.OwnerId;
        // (BT22_035) destination == None ⇒ DETACH-only: the source is already off-field (ChoiceZone.None as a
        // digivolution source); removing it from sourceIds leaves it off-field for an immediate re-attach (e.g.
        // as a LINK card). A None → None ZoneMoveRequest is rejected (both zones abstract), so skip the move.
        if (destination != ChoiceZone.None)
        {
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task<int> RemoveSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom,
        ChoiceZone destination,
        CancellationToken cancellationToken,
        GameEventQueue? gameEventQueue = null,
        bool honorProtection = true,
        IEffectQueryService? effectRegistry = null,
        Bridge.EngineContext? context = null,
        HeadlessEntityId causingEffectSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (count < 1 || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return 0;
        }

        List<string> sources = ReadSourceIds(host.Metadata).Select(id => id.Value).ToList();
        if (sources.Count == 0)
        {
            return 0;
        }

        int take = Math.Min(count, sources.Count);
        // Bottom (DigiEgg) is the end of the top→bottom list; top is the start.
        List<string> window = fromBottom
            ? sources.Skip(sources.Count - take).ToList()
            : sources.Take(take).ToList();
        // (fidelity) AS-IS ITrashDigivolutionCards.TrashDigivolutionCards() filters CanNotTrashFromDigivolutionCards-
        // protected sources OUT of the selected window before trashing — the protection applies to TRASH only, NOT
        // return-to-hand/deck. A protected source in the window is skipped and stays in the stack; the trash does
        // NOT reach deeper to make up the count (AS-IS collects the window by position, then filters).
        List<string> removed = destination == ChoiceZone.Trash && honorProtection
            ? window.Where(value => !IsTrashProtected(repository, value, effectRegistry, context, causingEffectSourceId)).ToList()
            : window;
        List<string> remaining = sources.Where(value => !removed.Contains(value)).ToList();

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, remaining) });

        // (PRIM-P0-timing) AS-IS ITrashDigivolutionCards fires OnDigivolutionCardDiscarded just BEFORE the source
        // cards physically move to the trash. Only for the Trash destination (ReturnSourcesAsync -> Hand/Library
        // is not a discard).
        if (gameEventQueue is not null && destination == ChoiceZone.Trash && removed.Count > 0)
        {
            TriggerEventEmitter.Emit(
                gameEventQueue,
                TriggerTimings.OnDigivolutionCardDiscarded,
                actor: host.OwnerId,
                subject: hostId,
                extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["discardedCardIds"] = string.Join(",", removed) });
        }

        // (c-remediation) AS-IS ReturnToLibraryBottomDigivolutionCards fires OnDigivolutionCardReturnToDeckBottom
        // (via global StackSkillInfos) when the sources are returned to the DECK — mirror it for the Library
        // destination (the only return-to-deck path). Fires BEFORE the cards physically move, like the discard.
        if (gameEventQueue is not null && destination == ChoiceZone.Library && removed.Count > 0)
        {
            TriggerEventEmitter.Emit(
                gameEventQueue,
                TriggerTimings.OnDigivolutionCardReturnToDeckBottom,
                actor: host.OwnerId,
                subject: hostId,
                extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["deckBottomCardIds"] = string.Join(",", removed) });
        }

        // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards applies the ACE-Overflow pass to the trash targets just
        // before moving them (CardController.cs:5219) — TRASH destination only (return-to-hand/deck is not this
        // AS-IS path). The deletion path (DeletionSourceTrash) applies its own DiscardEvoRoots overflow pass and
        // calls in with context:null, so it never double-charges here.
        if (destination == ChoiceZone.Trash && removed.Count > 0)
        {
            ApplyEffectTrashAceOverflow(
                repository, zoneMover, context, hostId, host.OwnerId,
                removed.Select(value => new HeadlessEntityId(value)).ToArray());
        }

        foreach (string sourceValue in removed)
        {
            var sourceId = new HeadlessEntityId(sourceValue);
            HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
                ? src.OwnerId
                : host.OwnerId;
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        }

        return removed.Count;
    }

    // (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards' `new AceOverflowClass(trashTargets).Overflow()`
    // (CardController.cs:5219): the effect-trash counterpart of DeletionSourceTrash's DiscardEvoRoots overflow.
    // Reuses DeletionSourceTrash.ApplyAceOverflow with the EngineContext's memory/turn services; context is
    // supplied only on effect-trash paths (the deletion path applies its own pass and calls with context:null,
    // and gameEventQueue-less bare/unit callers carry no memory model). The host-on-field guard mirrors the AS-IS
    // AceOverflowClass existence test (each source's permanent IsExistOnBattleArea/IsExistOnBreedingAreaDigimon),
    // which for a source-trash is its still-fielded host — same guard as the sink's link-card trash.
    private static void ApplyEffectTrashAceOverflow(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        Bridge.EngineContext? context,
        HeadlessEntityId hostId,
        HeadlessPlayerId hostOwner,
        IReadOnlyList<HeadlessEntityId> trashTargets)
    {
        if (context is null || trashTargets.Count == 0 || zoneMover is not IZoneStateReader zones
            || !(zones.GetCards(hostOwner, ChoiceZone.BattleArea).Contains(hostId)
                || zones.GetCards(hostOwner, ChoiceZone.BreedingArea).Contains(hostId)))
        {
            return;
        }

        DeletionSourceTrash.ApplyAceOverflow(
            repository, trashTargets, context.MemoryController, context.TurnController.Current.TurnPlayerId);
    }

    // (C-3, fidelity) AS-IS CardSource.CanNotTrashFromDigivolutionCards = the legacy per-source stamp
    // (willBeRemoveSources-style flag) OR a field-effect SCAN (ICanNotTrashFromDigivolutionCardsEffect). The
    // EFFECT-trash path passes the registry/context/causing-source so BT9_109's conditional continuous protection
    // (X-Antibody sources under a live host) is honoured via TrashProtectionScan; the DELETION path passes
    // honorProtection: false and never reaches here, exactly AS-IS DiscardEvoRoots (no keyword check).
    private static bool IsTrashProtected(
        ICardInstanceRepository repository,
        string sourceValue,
        IEffectQueryService? effectRegistry,
        Bridge.EngineContext? context,
        HeadlessEntityId causingEffectSourceId)
    {
        if (string.IsNullOrEmpty(sourceValue))
        {
            return false;
        }

        var sourceId = new HeadlessEntityId(sourceValue);
        if (repository.TryGetInstance(sourceId, out CardInstanceRecord? source) && source is not null
            && source.Metadata.TryGetValue(CardEffectCommons.TrashProtectedKey, out object? raw) && raw is true)
        {
            return true;
        }

        return TrashProtectionScan.IsProtected(effectRegistry, repository, context, sourceId, causingEffectSourceId);
    }

    // (F1-Tier2 OnAddDigivolutionCards) AS-IS AddDigivolutionCardsTop/Bottom (Permanent.cs:1109-1116 / 1213-1220)
    // emit OnAddDigivolutionCards with payload {Permanent=host, CardEffect=cause, CardSources=added}. Headless mirror:
    // subject=host (so the emit also stamps SourceEntityIdKey=host, letting a self-scoped scheduler-half reactor be
    // collected), addedCardIds (the batch, one emit — NOT per-card), and causeSourceId (the causing effect's source).
    // The gate CanTriggerOnAddDigivolutionCard requires a NON-EMPTY cause (AS-IS OnAddDigivolutionCards.cs:24
    // `CardEffect != null`) — an Assembly-style add (AS-IS `AddDigivolutionCardsBottom(card, null)`) passes a default
    // (empty) cause here and every reactor rejects it. No queue / no added cards / skip => no emit.
    // (C1d RDW-02, resolving F1-ADDDIGI-FROMFLAGS) AS-IS payload carries isFromSameDigimon / isFromDigimon (whether
    // an added card was already among THIS permanent's own sources / came from a Digimon on the battle area with >=1
    // sources — Permanent.cs:1071-1085). They gate 3 AS-IS reactors (BT22_006 / EX5_065 / EX5_001, unported today).
    // Computed PRE-move by the caller (post-move the flags read wrong: the card is now under the host / off-field)
    // and threaded here into the emit so the DORMANT SkillWindowSupply can build the full AS-IS key set.
    private static void EmitAddDigivolutionCards(
        GameEventQueue? gameEventQueue,
        HeadlessPlayerId hostOwner,
        HeadlessEntityId hostId,
        IReadOnlyList<string> addedCardIds,
        HeadlessEntityId causeSourceId,
        bool skip,
        bool isFromSameDigimon,
        bool isFromDigimon)
    {
        if (gameEventQueue is null || addedCardIds.Count == 0 || skip)
        {
            return;
        }

        TriggerEventEmitter.Emit(
            gameEventQueue,
            TriggerTimings.OnAddDigivolutionCards,
            actor: hostOwner,
            subject: hostId,
            extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [SkillWindowSupply.AddDigivolutionAddedCardIdsKey] = string.Join(",", addedCardIds),
                ["causeSourceId"] = causeSourceId.Value,
                [SkillWindowSupply.AddDigivolutionIsFromSameDigimonKey] = isFromSameDigimon,
                [SkillWindowSupply.AddDigivolutionIsFromDigimonKey] = isFromDigimon,
            });
    }

    // (C1d RDW-02) AS-IS AddDigivolutionCardsTop/Bottom (Permanent.cs:1071-1085): isFromSameDigimon == an added
    // card was already in THIS permanent's cardSources; isFromDigimon == an added card exists on the battle area
    // and its permanent has >=1 digivolution sources. Evaluated PRE-move (target + cards still in their prior
    // state). No zone reader (bare re-parent) => isFromDigimon can only be a battle-area top, so it is false.
    private static (bool isFromSame, bool isFromDigimon) ComputeAddDigivolutionFromFlags(
        ICardInstanceRepository repository, IZoneMover? zoneMover, CardInstanceRecord target, IReadOnlyList<HeadlessEntityId> cards)
    {
        bool isFromSame = false;
        bool isFromDigimon = false;
        var hostSources = ReadSourceIds(target.Metadata).Select(id => id.Value).ToHashSet(StringComparer.Ordinal);
        IZoneStateReader? reader = zoneMover as IZoneStateReader;
        foreach (HeadlessEntityId cardId in cards)
        {
            if (hostSources.Contains(cardId.Value))
            {
                isFromSame = true;
            }

            if (reader is not null
                && repository.TryGetInstance(cardId, out CardInstanceRecord? card) && card is not null
                && reader.GetCards(card.OwnerId, ChoiceZone.BattleArea).Contains(cardId)
                && ReadSourceIds(card.Metadata).Count >= 1)
            {
                isFromDigimon = true;
            }
        }

        return (isFromSame, isFromDigimon);
    }

    private static void AppendSources(ICardInstanceRepository repository, CardInstanceRecord target, IReadOnlyList<string> add)
    {
        if (add.Count == 0)
        {
            return;
        }

        // Re-read the target so an earlier append in the same operation is preserved.
        CardInstanceRecord current = repository.TryGetInstance(target.InstanceId, out CardInstanceRecord? latest) && latest is not null
            ? latest
            : target;
        List<string> sources = ReadSourceIds(current.Metadata).Select(id => id.Value).ToList();
        sources.AddRange(add);
        repository.Upsert(current with { Metadata = WithSources(current.Metadata, sources) });
    }

    private static void PrependSources(ICardInstanceRepository repository, CardInstanceRecord target, IReadOnlyList<string> add)
    {
        if (add.Count == 0)
        {
            return;
        }

        CardInstanceRecord current = repository.TryGetInstance(target.InstanceId, out CardInstanceRecord? latest) && latest is not null
            ? latest
            : target;
        List<string> sources = ReadSourceIds(current.Metadata).Select(id => id.Value).ToList();
        sources.InsertRange(0, add);
        repository.Upsert(current with { Metadata = WithSources(current.Metadata, sources) });
    }

    private static Dictionary<string, object?> WithSources(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> sources)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (sources.Count > 0)
        {
            copy[SourceIdsKey] = sources.ToArray();
        }
        else
        {
            copy.Remove(SourceIdsKey);
        }

        return copy;
    }

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>()
        };
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key, bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return defaultValue;
        }

        return raw is bool value ? value : defaultValue;
    }
}
