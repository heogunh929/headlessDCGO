namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Choices;
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

    public Task AddToHandAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Hand);
        return Task.CompletedTask;
    }

    public Task AddToTrashAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Trash);
        return Task.CompletedTask;
    }

    public Task AddToSecurityAsync(HeadlessPlayerId playerId, HeadlessEntityId cardId, bool faceUp, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCardMutation(playerId, cardId);
        MoveCardToSingleZone(playerId, cardId, ChoiceZone.Security, faceUp);
        return Task.CompletedTask;
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
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        return Task.FromResult(MoveFromLibraryTop(playerId, ChoiceZone.Hand, count));
    }

    public Task<IReadOnlyList<HeadlessEntityId>> AddSecurityFromLibraryAsync(
        HeadlessPlayerId playerId,
        int count,
        bool faceUp = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        if (count <= 0)
        {
            return Task.FromResult((IReadOnlyList<HeadlessEntityId>)Array.Empty<HeadlessEntityId>());
        }

        return Task.FromResult(MoveFromLibraryTop(playerId, ChoiceZone.Security, count, faceUp));
    }

    public Task<IReadOnlyList<HeadlessEntityId>> TrashSecurityAsync(
        HeadlessPlayerId playerId,
        int count,
        bool fromTop = true,
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

        for (int index = 0; index < count && security.Count > 0; index++)
        {
            int securityIndex = fromTop ? 0 : security.Count - 1;
            HeadlessEntityId cardId = security[securityIndex];
            MoveCard(new ZoneMoveRequest(playerId, cardId, ChoiceZone.Security, ChoiceZone.Trash));
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
        bool insertTop = false)
    {
        MoveCard(
            new ZoneMoveRequest(playerId, cardId, ChoiceZone.None, zone, faceUp),
            insertTop ? ZoneInsertion.Top : ZoneInsertion.Bottom);
    }

    private IReadOnlyList<HeadlessEntityId> MoveFromLibraryTop(
        HeadlessPlayerId playerId,
        ChoiceZone toZone,
        int count,
        bool faceUp = false)
    {
        return MoveFromZoneTop(playerId, ChoiceZone.Library, toZone, count, faceUp);
    }

    private IReadOnlyList<HeadlessEntityId> MoveFromZoneTop(
        HeadlessPlayerId playerId,
        ChoiceZone fromZone,
        ChoiceZone toZone,
        int count,
        bool faceUp = false)
    {
        List<HeadlessEntityId> sourceZone = GetZone(playerId, fromZone);
        List<HeadlessEntityId> movedCards = new();

        for (int index = 0; index < count && sourceZone.Count > 0; index++)
        {
            HeadlessEntityId cardId = sourceZone[0];
            MoveCard(new ZoneMoveRequest(playerId, cardId, fromZone, toZone, faceUp));
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

        if (insertion == ZoneInsertion.Top)
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

        return RecordEvent(
            GameEventType.CardMoved,
            $"Card moved: {request.CardId} {request.FromZone}->{request.ToZone}",
            new Dictionary<string, object?>
            {
                ["playerId"] = request.PlayerId.Value,
                ["cardId"] = request.CardId.Value,
                ["fromZone"] = request.FromZone.ToString(),
                ["toZone"] = request.ToZone.ToString(),
                ["faceUp"] = request.FaceUp,
                ["operation"] = operation
            });
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
