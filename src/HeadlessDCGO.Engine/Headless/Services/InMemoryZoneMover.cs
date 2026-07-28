namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class InMemoryZoneMover : IZoneMover, IZoneStateReader, IHeadlessMatchStateResettable
{
    private readonly Dictionary<HeadlessPlayerId, Dictionary<ChoiceZone, List<HeadlessEntityId>>> _zones = new();
    private readonly List<GameEvent> _events = new();
    private readonly IRandomSource _randomSource;

    // (RD-RLENV-03 / B2 substrate-only) Per-(player, zone) snapshot cache for GetCards. The mirror
    // effect scans call GetCards at enormous frequency (every PermanentOfThisCard / IsExistInSecurity
    // hits it), and the former unconditional cards.ToArray() per call was a top-3 allocation source in
    // the B2 profile. Invariant that keeps this SEMANTICS-INVARIANT: every mutation of a live zone list
    // flows through GetZone (the single accessor that hands out the mutable List) or RemoveFromAllZones
    // — both invalidate the affected cache entries BEFORE the caller can mutate, so a cached array is
    // only ever served while its list is untouched. Served arrays are never mutated by this class
    // (invalidation replaces them), so callers keep the same frozen-snapshot semantics as before.
    private readonly Dictionary<(HeadlessPlayerId PlayerId, ChoiceZone Zone), IReadOnlyList<HeadlessEntityId>> _cardsSnapshotCache = new();

    /// <summary>(RD-R3-02) The repository that keys the
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore"/> for this match — wired by the EngineContext constructor. When set,
    /// <see cref="MoveCard"/> is the AS-IS Permanent-object lifetime chokepoint: ANY move that changes a
    /// card's field-zone membership (enters/leaves BattleArea/BreedingArea) is a permanent CREATE/DIE and
    /// resets the card's bookkeeping entry, unless the move carries
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.PermanentContinuityKey"/>
    /// (a top-swap half whose owning op ReKeys instead). Null (a bare mover outside an EngineContext) skips
    /// the lifetime handling — the store is unreachable without a repository anyway.</summary>
    public ICardInstanceRepository? BookkeepingRepository { get; set; }

    public InMemoryZoneMover()
        : this(new GameRandomSource())
    {
    }

    public InMemoryZoneMover(IRandomSource randomSource)
    {
        _randomSource = randomSource;
    }

    public IReadOnlyList<GameEvent> Events => _events.ToArray();

    public IReadOnlyList<HeadlessEntityId> GetCards(HeadlessPlayerId playerId, ChoiceZone zone)
    {
        ValidatePlayerId(playerId);
        ValidateReadableZone(zone);

        if (!_zones.TryGetValue(playerId, out Dictionary<ChoiceZone, List<HeadlessEntityId>>? playerZones) ||
            !playerZones.TryGetValue(zone, out List<HeadlessEntityId>? cards))
        {
            return Array.Empty<HeadlessEntityId>();
        }

        if (_cardsSnapshotCache.TryGetValue((playerId, zone), out IReadOnlyList<HeadlessEntityId>? cachedSnapshot))
        {
            return cachedSnapshot;
        }

        HeadlessEntityId[] snapshot = cards.ToArray();
        _cardsSnapshotCache[(playerId, zone)] = snapshot;
        return snapshot;
    }

    public IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> Snapshot(HeadlessPlayerId playerId)
    {
        ValidatePlayerId(playerId);

        if (!_zones.TryGetValue(playerId, out Dictionary<ChoiceZone, List<HeadlessEntityId>>? playerZones))
        {
            return new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>();
        }

        return playerZones
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<HeadlessEntityId>)pair.Value.ToArray());
    }

    public Task<ZoneMoveResult> MoveAsync(ZoneMoveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(MoveCard(request));
    }

    public Task AddToHandAsync(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        long? addHandBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Hand, metadata: BuildAddHandMetadata(addHandBatchId, causeEffectId));
        return Task.CompletedTask;
    }

    // (sink re-migration / RDW) The former F1-Tier1 OnAddHand batch/cause stamp is RETIRED: it fed the
    // never-built WindowResolverWiring.CollectActivatedBridgeTriggers collapse (no live reader of
    // addHandBatchId / addHandCauseEffectId anywhere), so it was a dead write. The batch/cause params are
    // kept on the zone-mover surface (callers still compute them) but no longer stamped — always null.
    private static Dictionary<string, object?>? BuildAddHandMetadata(long? addHandBatchId, HeadlessEntityId? causeEffectId)
    {
        _ = addHandBatchId;
        _ = causeEffectId;
        return null;
    }

    public Task AddToTrashAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Trash);
        return Task.CompletedTask;
    }

    public Task TrashCardAsync(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        long? discardBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        bool isRevealTrash = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);

        // (F1-Tier1 OnDiscard*) preserve the REAL source zone so a Hand->Trash / Library->Trash derives
        // OnDiscardHand / OnDiscardLibrary (TriggerTimingMap). If the card is not found in a concrete zone
        // (already removed / a token), fall back to From=None (identical to AddToTrashAsync) — a None->Trash move
        // derives no source-zone discard timing, exactly as before.
        ChoiceZone fromZone = FindZoneOf(playerId, cardId) ?? ChoiceZone.None;

        // (sink re-migration / RDW) The discard batch/cause + reveal-trash stamps are RETIRED — all three keys
        // (discardBatchId / discardCauseEffectId / revealTrash) had no live reader (the OnDiscard* activated-bridge
        // collapse consumer was never built, and the reveal-suppression gate reads the SEPARATE, still-unported
        // "isBeingRevealed" flag — design item RD-P6C3-A2 — not "revealTrash"). Params kept, no longer stamped.
        _ = discardBatchId;
        _ = causeEffectId;
        _ = isRevealTrash;

        MoveCard(new ZoneMoveRequest(playerId, cardId, fromZone, ChoiceZone.Trash, Metadata: null));
        return Task.CompletedTask;
    }

    // (F1-Tier1) The concrete zone a card currently sits in for this player, or null if it is in none.
    private ChoiceZone? FindZoneOf(HeadlessPlayerId playerId, HeadlessEntityId cardId)
    {
        foreach (KeyValuePair<ChoiceZone, List<HeadlessEntityId>> pair in GetPlayerZones(playerId))
        {
            if (pair.Value.Contains(cardId))
            {
                return pair.Key;
            }
        }

        return null;
    }

    public Task AddToSecurityAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, bool faceUp, bool toTop = true, long? addSecurityBatchId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        // N-3: index 0 is the security top (consumed first by SecurityResolver / TrashSecurity fromTop),
        // so toTop maps to a top insert — matching the original AddSecurityCard(toTop: true) default.
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Security, faceUp, insertTop: toTop, metadata: BuildAddSecurityMetadata(addSecurityBatchId));
        return Task.CompletedTask;
    }

    // (sink re-migration / RDW) The F1-Tier1 OnAddSecurity per-card add id stamp is RETIRED — addSecurityBatchId
    // had no live reader (the OnAddSecurity activated-bridge sequencing consumer was never built). Param kept on
    // the surface (callers still compute it) but no longer stamped — always null.
    private static Dictionary<string, object?>? BuildAddSecurityMetadata(long? addSecurityBatchId)
    {
        _ = addSecurityBatchId;
        return null;
    }

    public Task MoveToDeckTopAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Library, insertTop: true);
        return Task.CompletedTask;
    }

    public Task MoveToDeckBottomAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Library);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HeadlessEntityId>> DrawAsync(
        HeadlessPlayerId playerId,
        int count,
        long? addHandBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        // (F1-Tier1 OnAddHand) all N drawn cards share ONE add-hand batch id + cause (AS-IS one AddHandCards call
        // over the whole DrawCards list), so the bridge collapses them to a single OnAddHand fire per reactor.
        return Task.FromResult(MoveFromLibraryTop(
            playerId, ChoiceZone.Hand, count, metadata: BuildAddHandMetadata(addHandBatchId, causeEffectId)));
    }

    public Task<IReadOnlyList<HeadlessEntityId>> AddSecurityFromLibraryAsync(
        HeadlessPlayerId playerId,
        int count,
        bool faceUp = false,
        Func<long?>? batchIdFactory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        // N-3: the original AddSecurity deals each library-top card via AddSecurityCard(toTop: true),
        // i.e. Insert(0). Inserting each at the security top reproduces that stacking order (last dealt
        // ends up on top) instead of the previous bottom-append (which reversed the stack).
        // (F1-Tier1 OnAddSecurity P2-1) each recovered card gets its OWN shared-counter add-security id
        // (OnAddSecurity is per-card, not collapsed) via the per-card factory, stamped on each ->Security move.
        return Task.FromResult(MoveFromLibraryTop(
            playerId, ChoiceZone.Security, count, faceUp, insertTop: true,
            metadataFactory: batchIdFactory is null ? null : () => BuildAddSecurityMetadata(batchIdFactory())));
    }

    public Task<IReadOnlyList<HeadlessEntityId>> TrashSecurityAsync(
        HeadlessPlayerId playerId,
        int count,
        bool fromTop = true,
        long? securityLossBatchId = null,
        HeadlessEntityId? causeEffectId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        List<HeadlessEntityId> security = GetZone(playerId, ChoiceZone.Security);
        List<HeadlessEntityId> trash = GetZone(playerId, ChoiceZone.Trash);
        List<HeadlessEntityId> trashedCards = new();

        // (sink re-migration / RDW) The F1-M1 security-loss batch id + cause stamp is RETIRED — both keys
        // (securityLossBatchId / discardCauseEffectId) had no live reader (the OnLoseSecurity / OnDiscardSecurity
        // activated-bridge collapse consumer was never built). Params kept, no longer stamped.
        _ = securityLossBatchId;
        _ = causeEffectId;

        for (int index = 0; index < count && security.Count > 0; index++)
        {
            int securityIndex = fromTop ? 0 : security.Count - 1;
            HeadlessEntityId cardId = security[securityIndex];
            MoveCard(new ZoneMoveRequest(playerId, cardId, ChoiceZone.Security, ChoiceZone.Trash, Metadata: null));
            trashedCards.Add(cardId);
        }

        return Task.FromResult((IReadOnlyList<HeadlessEntityId>)trashedCards.ToArray());
    }

    public Task<HeadlessEntityId?> HatchDigitamaAsync(
        HeadlessPlayerId playerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        IReadOnlyList<HeadlessEntityId> hatchedCards = MoveFromZoneTop(
            playerId,
            ChoiceZone.DigitamaLibrary,
            ChoiceZone.BreedingArea,
            count: 1);

        return Task.FromResult<HeadlessEntityId?>(hatchedCards.Count == 0
            ? null
            : hatchedCards[0]);
    }

    public Task<IReadOnlyList<HeadlessEntityId>> MoveBreedingToBattleAsync(
        HeadlessPlayerId playerId,
        int count = 1,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        return Task.FromResult(MoveFromZoneTop(
            playerId,
            ChoiceZone.BreedingArea,
            ChoiceZone.BattleArea,
            count));
    }

    public Task ShuffleAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        _randomSource.Shuffle(GetZone(playerId, ChoiceZone.Library));
        RecordEvent(
            GameEventType.StateChanged,
            $"Zone shuffled: player={playerId}, zone={ChoiceZone.Library}",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId.Value,
                ["zone"] = ChoiceZone.Library.ToString(),
                ["operation"] = "Shuffle",
                ["count"] = GetZone(playerId, ChoiceZone.Library).Count
            });
        return Task.CompletedTask;
    }

    // (BT1_087) Shuffle the SECURITY zone in place with the deterministic RNG — mirror of ShuffleAsync but
    // scoped to ChoiceZone.Security (AS-IS SecurityCards = RandomUtility.ShuffledDeckCards(SecurityCards)).
    public Task ShuffleSecurityAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        _randomSource.Shuffle(GetZone(playerId, ChoiceZone.Security));
        RecordEvent(
            GameEventType.StateChanged,
            $"Zone shuffled: player={playerId}, zone={ChoiceZone.Security}",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId.Value,
                ["zone"] = ChoiceZone.Security.ToString(),
                ["operation"] = "Shuffle",
                ["count"] = GetZone(playerId, ChoiceZone.Security).Count
            });
        return Task.CompletedTask;
    }

    // (BT13_033) Shuffle the HAND zone in place with the deterministic RNG — mirror of ShuffleSecurityAsync but
    // scoped to ChoiceZone.Hand (AS-IS `HandCards = RandomUtility.ShuffledDeckCards(HandCards)`, an in-place hand
    // reorder feeding a blind select-to-deck-bottom). New op; unused by starter decks so the digest is unmoved.
    public Task ShuffleHandAsync(HeadlessPlayerId playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        _randomSource.Shuffle(GetZone(playerId, ChoiceZone.Hand));
        RecordEvent(
            GameEventType.StateChanged,
            $"Zone shuffled: player={playerId}, zone={ChoiceZone.Hand}",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId.Value,
                ["zone"] = ChoiceZone.Hand.ToString(),
                ["operation"] = "Shuffle",
                ["count"] = GetZone(playerId, ChoiceZone.Hand).Count
            });
        return Task.CompletedTask;
    }

    public void Clear()
    {
        ResetMatchState();
    }

    public void ResetMatchState()
    {
        _zones.Clear();
        _events.Clear();
        _cardsSnapshotCache.Clear();
    }

    private void MoveCardToSingleZone(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        ChoiceZone zone,
        bool faceUp = false,
        bool insertTop = false,
        Dictionary<string, object?>? metadata = null)
    {
        MoveCard(
            new ZoneMoveRequest(playerId, cardId, ChoiceZone.None, zone, faceUp, Metadata: metadata),
            insertTop ? ZoneInsertion.Top : ZoneInsertion.Bottom);
    }

    private IReadOnlyList<HeadlessEntityId> MoveFromLibraryTop(
        HeadlessPlayerId playerId,
        ChoiceZone toZone,
        int count,
        bool faceUp = false,
        bool insertTop = false,
        Dictionary<string, object?>? metadata = null,
        Func<Dictionary<string, object?>?>? metadataFactory = null)
    {
        return MoveFromZoneTop(playerId, ChoiceZone.Library, toZone, count, faceUp, insertTop, metadata, metadataFactory);
    }

    // metadataFactory (when supplied) is invoked ONCE PER moved card to build a FRESH per-card metadata dict
    // (e.g. a distinct OnAddSecurity batch id per card); it takes precedence over the shared `metadata`.
    private IReadOnlyList<HeadlessEntityId> MoveFromZoneTop(
        HeadlessPlayerId playerId,
        ChoiceZone fromZone,
        ChoiceZone toZone,
        int count,
        bool faceUp = false,
        bool insertTop = false,
        Dictionary<string, object?>? metadata = null,
        Func<Dictionary<string, object?>?>? metadataFactory = null)
    {
        List<HeadlessEntityId> sourceZone = GetZone(playerId, fromZone);
        List<HeadlessEntityId> movedCards = new();
        ZoneInsertion insertion = insertTop ? ZoneInsertion.Top : ZoneInsertion.Bottom;

        for (int index = 0; index < count && sourceZone.Count > 0; index++)
        {
            HeadlessEntityId cardId = sourceZone[0];
            Dictionary<string, object?>? moveMetadata = metadataFactory is null ? metadata : metadataFactory();
            MoveCard(new ZoneMoveRequest(playerId, cardId, fromZone, toZone, faceUp, Metadata: moveMetadata), insertion);
            movedCards.Add(cardId);
        }

        return movedCards.ToArray();
    }

    private ZoneMoveResult MoveCard(ZoneMoveRequest request, ZoneInsertion insertion = ZoneInsertion.Bottom)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool hasSource = request.FromZone != ChoiceZone.None;
        bool hasDestination = request.ToZone != ChoiceZone.None;

        // (RD-R3-02) field-zone membership BEFORE the move — for a From=None request the card may still be
        // physically withdrawn from a field zone by RemoveFromAllZones below, so scan its actual zone first.
        bool wasOnField = hasSource
            ? IsFieldZone(request.FromZone)
            : FindZoneOf(request.PlayerId, request.CardId) is { } currentZone && IsFieldZone(currentZone);

        if (hasSource)
        {
            List<HeadlessEntityId> sourceZone = GetZone(request.PlayerId, request.FromZone);
            if (!sourceZone.Remove(request.CardId))
            {
                throw new InvalidOperationException(
                    $"Card id '{request.CardId}' is not in player '{request.PlayerId}' zone '{request.FromZone}'.");
            }
        }
        else
        {
            RemoveFromAllZones(request.PlayerId, request.CardId);
        }

        if (hasDestination)
        {
            AddToZone(request.PlayerId, request.ToZone, request.CardId, insertion);
        }

        // (RD-R3-02) the AS-IS Permanent-object lifetime chokepoint: a change of field-zone membership is a
        // permanent CREATE (fresh `new Permanent(...)` — a re-played card reads AS-IS field defaults) or DIE
        // (the object is dropped with the field slot) — either way the card's bookkeeping entry resets. A
        // field→field move (breeding promote) keeps the same AS-IS object, and a top-swap half (marked
        // PermanentContinuityKey; digivolve / de-digivolve promote) is owned by its op's ReKey.
        bool entersField = hasDestination && IsFieldZone(request.ToZone);
        if (wasOnField != entersField
            && BookkeepingRepository is { } bookkeepingRepository
            && !(request.Metadata?.ContainsKey(
                    HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.PermanentContinuityKey) ?? false))
        {
            HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.Reset(bookkeepingRepository, request.CardId);
        }

        GameEvent cardMoved = RecordCardMoved(request);
        return new ZoneMoveResult(
            request,
            cardMoved,
            hasSource ? GetCards(request.PlayerId, request.FromZone) : Array.Empty<HeadlessEntityId>(),
            hasDestination ? GetCards(request.PlayerId, request.ToZone) : Array.Empty<HeadlessEntityId>());
    }

    private void AddToZone(
        HeadlessPlayerId playerId,
        ChoiceZone zone,
        HeadlessEntityId cardId,
        ZoneInsertion insertion)
    {
        ValidateConcreteZone(zone, nameof(zone));

        List<HeadlessEntityId> cards = GetZone(playerId, zone);
        if (cards.Contains(cardId))
        {
            return;
        }

        // N-6: the original always inserts into the trash at index 0 (most recent on top,
        // TrashCards.Insert(0)), regardless of the move path. Centralise that here so every trash
        // insertion (AddToTrash / battle deletion / TrashSecurity / generic MoveAsync) is consistent.
        if (insertion == ZoneInsertion.Top || zone == ChoiceZone.Trash)
        {
            cards.Insert(0, cardId);
            return;
        }

        cards.Add(cardId);
    }

    // (RD-R3-02) the zones whose residency defines "the card heads a field permanent".
    private static bool IsFieldZone(ChoiceZone zone) => zone is ChoiceZone.BattleArea or ChoiceZone.BreedingArea;

    private void RemoveFromAllZones(HeadlessPlayerId playerId, HeadlessEntityId cardId)
    {
        // (RD-RLENV-03 / B2) This is the one mutation path that bypasses GetZone — invalidate the
        // player's cached snapshots before touching the live lists.
        Dictionary<ChoiceZone, List<HeadlessEntityId>> playerZones = GetPlayerZones(playerId);
        foreach (ChoiceZone zone in playerZones.Keys)
        {
            _cardsSnapshotCache.Remove((playerId, zone));
        }

        foreach (List<HeadlessEntityId> cards in playerZones.Values)
        {
            cards.Remove(cardId);
        }
    }

    private List<HeadlessEntityId> GetZone(HeadlessPlayerId playerId, ChoiceZone zone)
    {
        // (RD-RLENV-03 / B2) GetZone hands out the LIVE mutable list — every in-class mutation flows
        // through here (audited: MoveCard/AddToZone/RemoveCardFromZone/Shuffle*/TrashSecurity/
        // MoveFromZoneTop), so invalidating the snapshot at hand-out keeps GetCards' cache coherent.
        _cardsSnapshotCache.Remove((playerId, zone));

        Dictionary<ChoiceZone, List<HeadlessEntityId>> playerZones = GetPlayerZones(playerId);

        if (!playerZones.TryGetValue(zone, out List<HeadlessEntityId>? cards))
        {
            cards = new List<HeadlessEntityId>();
            playerZones[zone] = cards;
        }

        return cards;
    }

    private Dictionary<ChoiceZone, List<HeadlessEntityId>> GetPlayerZones(HeadlessPlayerId playerId)
    {
        ValidatePlayerId(playerId);

        if (!_zones.TryGetValue(playerId, out Dictionary<ChoiceZone, List<HeadlessEntityId>>? playerZones))
        {
            playerZones = new Dictionary<ChoiceZone, List<HeadlessEntityId>>();
            _zones[playerId] = playerZones;
        }

        return playerZones;
    }

    private static void ValidateCardMutation(HeadlessPlayerId playerId, HeadlessEntityId cardId)
    {
        ValidatePlayerId(playerId);

        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }
    }

    private static void ValidatePlayerId(HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }
    }

    private static void ValidateReadableZone(ChoiceZone zone)
    {
        if (zone == ChoiceZone.Custom)
        {
            throw new ArgumentException("Zone must not be Custom.", nameof(zone));
        }
    }

    private static void ValidateConcreteZone(ChoiceZone zone, string parameterName)
    {
        if (zone is ChoiceZone.None or ChoiceZone.Custom)
        {
            throw new ArgumentException("Zone must be a concrete gameplay zone.", parameterName);
        }
    }

    private GameEvent RecordCardMoved(ZoneMoveRequest request)
    {
        string operation = request.FromZone == ChoiceZone.None
            ? "Insert"
            : request.ToZone == ChoiceZone.None
                ? "Remove"
                : "Move";

        var metadata = new Dictionary<string, object?>
        {
            ["playerId"] = request.PlayerId.Value,
            ["cardId"] = request.CardId.Value,
            ["fromZone"] = request.FromZone.ToString(),
            ["toZone"] = request.ToZone.ToString(),
            ["faceUp"] = request.FaceUp,
            ["operation"] = operation
        };
        // (G3) merge any caller-supplied extra metadata (e.g. suppressOnPlay) into the event.
        if (request.Metadata is { } extra)
        {
            foreach (KeyValuePair<string, object?> pair in extra)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        GameEvent gameEvent = new(
            _events.Count + 1,
            GameEventType.CardMoved,
            $"Card moved: {request.CardId} {request.FromZone}->{request.ToZone}",
            metadata)
        {
            // G3.5-RL-B2: structured fields alongside the legacy metadata.
            Actor = request.PlayerId,
            Subject = request.CardId,
            ZoneFrom = request.FromZone,
            ZoneTo = request.ToZone,
            Cause = operation
        };

        _events.Add(gameEvent);
        return gameEvent;
    }

    private GameEvent RecordEvent(
        GameEventType type,
        string message,
        IReadOnlyDictionary<string, object?> metadata)
    {
        GameEvent gameEvent = new(
            _events.Count + 1,
            type,
            message,
            metadata);
        _events.Add(gameEvent);
        return gameEvent;
    }

    private enum ZoneInsertion
    {
        Bottom,
        Top
    }
}
