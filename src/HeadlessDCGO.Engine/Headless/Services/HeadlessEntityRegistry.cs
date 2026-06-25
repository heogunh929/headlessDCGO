namespace HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessEntityRegistry
{
    private readonly HashSet<HeadlessPlayerId> _players = new();
    private readonly Dictionary<HeadlessEntityId, CardRecord> _cardDefinitions = new();
    private readonly Dictionary<HeadlessEntityId, CardInstanceRecord> _cardInstances = new();

    public int PlayerCount => _players.Count;

    public int CardDefinitionCount => _cardDefinitions.Count;

    public int CardInstanceCount => _cardInstances.Count;

    public void RegisterPlayer(HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        if (!_players.Add(playerId))
        {
            throw new InvalidOperationException($"Player id '{playerId}' is already registered.");
        }
    }

    public void RegisterPlayers(IEnumerable<HeadlessPlayerId> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        foreach (HeadlessPlayerId playerId in playerIds)
        {
            RegisterPlayer(playerId);
        }
    }

    public bool ContainsPlayer(HeadlessPlayerId playerId)
    {
        return _players.Contains(playerId);
    }

    public void RegisterCardDefinition(CardRecord card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (_cardDefinitions.ContainsKey(card.Id))
        {
            throw new InvalidOperationException($"Card definition id '{card.Id}' is already registered.");
        }

        _cardDefinitions.Add(card.Id, card);
    }

    public bool TryGetCardDefinition(HeadlessEntityId definitionId, out CardRecord? card)
    {
        return _cardDefinitions.TryGetValue(definitionId, out card);
    }

    public void RegisterCardInstance(CardInstanceRecord instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!_players.Contains(instance.OwnerId))
        {
            throw new InvalidOperationException($"Owner player id '{instance.OwnerId}' is not registered.");
        }

        if (!_cardDefinitions.ContainsKey(instance.DefinitionId))
        {
            throw new InvalidOperationException($"Card definition id '{instance.DefinitionId}' is not registered.");
        }

        if (_cardInstances.ContainsKey(instance.InstanceId))
        {
            throw new InvalidOperationException($"Card instance id '{instance.InstanceId}' is already registered.");
        }

        _cardInstances.Add(instance.InstanceId, instance);
    }

    public bool TryGetCardInstance(HeadlessEntityId instanceId, out CardInstanceRecord? instance)
    {
        return _cardInstances.TryGetValue(instanceId, out instance);
    }

    public HeadlessEntityRegistrySnapshot Snapshot()
    {
        return new HeadlessEntityRegistrySnapshot(
            _players
                .OrderBy(playerId => playerId.Value)
                .ToArray(),
            _cardDefinitions
                .Values
                .OrderBy(card => card.Id.Value, StringComparer.Ordinal)
                .ToArray(),
            _cardInstances
                .Values
                .OrderBy(instance => instance.InstanceId.Value, StringComparer.Ordinal)
                .ToArray());
    }

    public void Clear()
    {
        _players.Clear();
        _cardDefinitions.Clear();
        _cardInstances.Clear();
    }
}

public sealed record HeadlessEntityRegistrySnapshot
{
    public HeadlessEntityRegistrySnapshot(
        IReadOnlyList<HeadlessPlayerId> Players,
        IReadOnlyList<CardRecord> CardDefinitions,
        IReadOnlyList<CardInstanceRecord> CardInstances)
    {
        ArgumentNullException.ThrowIfNull(Players);
        ArgumentNullException.ThrowIfNull(CardDefinitions);
        ArgumentNullException.ThrowIfNull(CardInstances);

        this.Players = Array.AsReadOnly(Players.ToArray());
        this.CardDefinitions = Array.AsReadOnly(CardDefinitions.ToArray());
        this.CardInstances = Array.AsReadOnly(CardInstances.ToArray());
    }

    public IReadOnlyList<HeadlessPlayerId> Players { get; }

    public IReadOnlyList<CardRecord> CardDefinitions { get; }

    public IReadOnlyList<CardInstanceRecord> CardInstances { get; }
}
