namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class InMemoryZoneMover : IZoneMover, IZoneStateReader, IHeadlessMatchStateResettable
{
    private readonly Dictionary<HeadlessPlayerId, Dictionary<ChoiceZone, List<HeadlessEntityId>>> _zones = new();
    private readonly List<GameEvent> _events = new();
    private readonly IRandomSource _randomSource;

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

        return cards.ToArray();
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

    // (F1-Tier1 OnAddHand) the batch/cause metadata threaded onto a ->Hand CardMoved so the OnAddHand activated
    // bridge collapses one effect's multi-card add to a single fire (batch id) and its CanTriggerOnHandAdded gate
    // reads the causing effect's source card (cause id, the AS-IS CardEffect). Null when neither is supplied.
    private static Dictionary<string, object?>? BuildAddHandMetadata(long? addHandBatchId, HeadlessEntityId? causeEffectId)
    {
        Dictionary<string, object?>? metadata = null;
        if (addHandBatchId is long batch)
        {
            (metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.AddHandBatchIdKey] = batch;
        }

        if (causeEffectId is { IsEmpty: false } cause)
        {
            (metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.AddHandCauseEffectIdKey] = cause.Value;
        }

        return metadata;
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

        Dictionary<string, object?>? metadata = null;
        if (discardBatchId is long batch)
        {
            (metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.DiscardBatchIdKey] = batch;
        }

        if (causeEffectId is { IsEmpty: false } cause)
        {
            (metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.DiscardCauseEffectIdKey] = cause.Value;
        }

        // (F1 reveal-remainder) mirror the AS-IS IsBeingRevealed=true at the trash moment so the
        // OnDiscardLibrary gate (CanTriggerWhenDiscardLibrary) filters this discard out (WhenDiscardLibrary.cs:23-26).
        if (isRevealTrash)
        {
            (metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.RevealTrashFlagKey] = true;
        }

        MoveCard(new ZoneMoveRequest(playerId, cardId, fromZone, ChoiceZone.Trash, Metadata: metadata));
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

    // (F1-Tier1 OnAddSecurity P2-1) the shared-counter per-card add-security id threaded onto a ->Security
    // CardMoved so the OnAddSecurity activated bridge sequences co-drained per-card triggers in ascending add
    // order. Null (a context-less bare add) leaves the move unstamped (the bridge reader falls back to Sequence).
    private static Dictionary<string, object?>? BuildAddSecurityMetadata(long? addSecurityBatchId)
    {
        if (addSecurityBatchId is not long batch)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Effects.MatchStateMutationSink.AddSecurityBatchIdKey] = batch,
        };
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

        // (F1-M1 P1-1) all N cards of this ONE IDestroySecurity/IReduceSecurity call share ONE security-loss
        // batch id (AS-IS: one StackSkillInfos(OnLoseSecurity) broadcast for the whole trash), so the activated
        // bridge collapses the N CardMoved events to a single OnLoseSecurity fire per reactor. (F1-Tier1) the same
        // move ALSO derives OnDiscardSecurity (Security->Trash), whose collapse reuses this security-loss id and
        // whose CardEffect!=null gate reads the CAUSE effect id stamped here — a non-effect security loss (attack
        // security-CHECK reveal, a bare zone move with neither id) fails that gate, matching AS-IS's IDestroySecurity-
        // only OnDiscardSecurity emit.
        Dictionary<string, object?>? moveMetadata = null;
        if (securityLossBatchId is long batch)
        {
            (moveMetadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.SecurityLossBatchIdKey] = batch;
        }

        if (causeEffectId is { IsEmpty: false } cause)
        {
            (moveMetadata ??= new Dictionary<string, object?>(StringComparer.Ordinal))
                [Effects.MatchStateMutationSink.DiscardCauseEffectIdKey] = cause.Value;
        }

        for (int index = 0; index < count && security.Count > 0; index++)
        {
            int securityIndex = fromTop ? 0 : security.Count - 1;
            HeadlessEntityId cardId = security[securityIndex];
            MoveCard(new ZoneMoveRequest(playerId, cardId, ChoiceZone.Security, ChoiceZone.Trash, Metadata: moveMetadata));
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

    public void Clear()
    {
        ResetMatchState();
    }

    public void ResetMatchState()
    {
        _zones.Clear();
        _events.Clear();
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

    private void RemoveFromAllZones(HeadlessPlayerId playerId, HeadlessEntityId cardId)
    {
        foreach (List<HeadlessEntityId> cards in GetPlayerZones(playerId).Values)
        {
            cards.Remove(cardId);
        }
    }

    private List<HeadlessEntityId> GetZone(HeadlessPlayerId playerId, ChoiceZone zone)
    {
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
