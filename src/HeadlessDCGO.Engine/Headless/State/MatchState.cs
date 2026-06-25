namespace HeadlessDCGO.Engine.Headless.State;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record MatchState
{
    private IReadOnlyList<PlayerState> _players = Array.Empty<PlayerState>();
    private IReadOnlyDictionary<HeadlessEntityId, CardInstanceState> _cardInstances =
        new ReadOnlyDictionary<HeadlessEntityId, CardInstanceState>(
            new Dictionary<HeadlessEntityId, CardInstanceState>());
    private IReadOnlyList<GameEvent> _events = Array.Empty<GameEvent>();

    public MatchState(
        IReadOnlyList<PlayerState> Players,
        IReadOnlyDictionary<HeadlessEntityId, CardInstanceState>? CardInstances = null,
        long Version = 0,
        bool IsTerminal = false,
        IReadOnlyList<GameEvent>? Events = null)
    {
        if (Version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Version), "State version must not be negative.");
        }

        this.Players = Players;
        this.CardInstances = CardInstances ?? new Dictionary<HeadlessEntityId, CardInstanceState>();
        this.Version = Version;
        this.IsTerminal = IsTerminal;
        this.Events = Events ?? Array.Empty<GameEvent>();
    }

    public IReadOnlyList<PlayerState> Players
    {
        get => _players;
        init => _players = CopyPlayers(value);
    }

    public IReadOnlyDictionary<HeadlessEntityId, CardInstanceState> CardInstances
    {
        get => _cardInstances;
        init => _cardInstances = CopyCardInstances(value);
    }

    public long Version { get; init; }

    public bool IsTerminal { get; init; }

    public IReadOnlyList<GameEvent> Events
    {
        get => _events;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _events = Array.AsReadOnly(value.ToArray());
        }
    }

    public static MatchState CreateInitial(IEnumerable<HeadlessPlayerId> playerIds, int initialMemory = 0)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        HeadlessPlayerId[] ids = playerIds.ToArray();
        if (ids.Length == 0)
        {
            throw new ArgumentException("At least one player id is required.", nameof(playerIds));
        }

        if (ids.Any(id => id.IsEmpty))
        {
            throw new ArgumentException("Player ids must not contain empty ids.", nameof(playerIds));
        }

        if (ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidOperationException("Player ids must be unique.");
        }

        PlayerState[] players = ids
            .OrderBy(id => id.Value)
            .Select(id => new PlayerState(id, Memory: initialMemory))
            .ToArray();

        return new MatchState(players);
    }

    public PlayerState GetPlayer(HeadlessPlayerId playerId)
    {
        return Players.FirstOrDefault(player => player.PlayerId == playerId)
            ?? throw new InvalidOperationException($"Player id '{playerId}' is not in the match state.");
    }

    public CardInstanceState GetCardInstance(HeadlessEntityId instanceId)
    {
        return CardInstances.TryGetValue(instanceId, out CardInstanceState? instance)
            ? instance
            : throw new InvalidOperationException($"Card instance id '{instanceId}' is not in the match state.");
    }

    public MatchState WithCardInstance(CardInstanceState instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsurePlayerExists(instance.OwnerId);

        Dictionary<HeadlessEntityId, CardInstanceState> instances = new(CardInstances)
        {
            [instance.InstanceId] = instance
        };

        return this with { CardInstances = instances };
    }

    public MatchState PlaceCard(HeadlessEntityId instanceId, ChoiceZone zone)
    {
        CardInstanceState instance = GetCardInstance(instanceId);
        PlayerState player = GetPlayer(instance.OwnerId);
        PlayerState updatedPlayer = player.AddToZone(zone, instanceId);
        return ReplacePlayer(updatedPlayer);
    }

    public MatchState MoveCard(ZoneMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CardInstanceState instance = GetCardInstance(request.CardId);
        if (instance.OwnerId != request.PlayerId)
        {
            throw new InvalidOperationException(
                $"Card id '{request.CardId}' is owned by player '{instance.OwnerId}', not player '{request.PlayerId}'.");
        }

        PlayerState player = GetPlayer(request.PlayerId);
        PlayerState removed = request.FromZone is ChoiceZone.None or ChoiceZone.Custom
            ? player
            : player.RemoveFromZone(request.FromZone, request.CardId);
        PlayerState moved = removed.AddToZone(request.ToZone, request.CardId);

        GameEvent cardMoved = new(
            Version + 1,
            GameEventType.CardMoved,
            $"Card moved: {request.CardId}",
            new Dictionary<string, object?>
            {
                ["playerId"] = request.PlayerId.Value,
                ["cardId"] = request.CardId.Value,
                ["fromZone"] = request.FromZone.ToString(),
                ["toZone"] = request.ToZone.ToString(),
                ["faceUp"] = request.FaceUp
            });

        return ReplacePlayer(moved) with
        {
            Version = Version + 1,
            Events = Events.Concat(new[] { cardMoved }).ToArray()
        };
    }

    public MatchStateSnapshot Snapshot()
    {
        return new MatchStateSnapshot(
            Version,
            IsTerminal,
            Players,
            CardInstances.Values
                .OrderBy(instance => instance.InstanceId.Value, StringComparer.Ordinal)
                .ToArray(),
            Events);
    }

    public string ComputeFingerprint()
    {
        return StateFingerprintService.Default.ComputeFingerprint(this);
    }

    private MatchState ReplacePlayer(PlayerState player)
    {
        PlayerState[] players = Players
            .Select(current => current.PlayerId == player.PlayerId ? player : current)
            .ToArray();
        return this with { Players = players };
    }

    private void EnsurePlayerExists(HeadlessPlayerId playerId)
    {
        _ = GetPlayer(playerId);
    }

    private static IReadOnlyList<PlayerState> CopyPlayers(IReadOnlyList<PlayerState>? players)
    {
        ArgumentNullException.ThrowIfNull(players);

        PlayerState[] snapshot = players.ToArray();
        if (snapshot.Any(player => player is null))
        {
            throw new ArgumentException("Players must not contain null entries.", nameof(players));
        }

        if (snapshot.Select(player => player.PlayerId).Distinct().Count() != snapshot.Length)
        {
            throw new InvalidOperationException("Player states must have unique player ids.");
        }

        return Array.AsReadOnly(snapshot
            .OrderBy(player => player.PlayerId.Value)
            .ToArray());
    }

    private static IReadOnlyDictionary<HeadlessEntityId, CardInstanceState> CopyCardInstances(
        IReadOnlyDictionary<HeadlessEntityId, CardInstanceState>? cardInstances)
    {
        ArgumentNullException.ThrowIfNull(cardInstances);

        Dictionary<HeadlessEntityId, CardInstanceState> copy = new();
        foreach (KeyValuePair<HeadlessEntityId, CardInstanceState> pair in cardInstances)
        {
            if (pair.Key.IsEmpty)
            {
                throw new ArgumentException("Card instance id must not be empty.", nameof(cardInstances));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            if (pair.Key != pair.Value.InstanceId)
            {
                throw new ArgumentException("Card instance dictionary key must match the instance id.", nameof(cardInstances));
            }

            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<HeadlessEntityId, CardInstanceState>(copy);
    }
}

public sealed record MatchStateSnapshot(
    long Version,
    bool IsTerminal,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<CardInstanceState> CardInstances,
    IReadOnlyList<GameEvent> Events);
