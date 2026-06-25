namespace HeadlessDCGO.Engine.Headless.Bridge;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record ContinuousContext
{
    private IReadOnlyList<HeadlessPlayerId> _playerIds = Array.Empty<HeadlessPlayerId>();
    private IReadOnlyDictionary<HeadlessPlayerId, DeckList> _decks =
        new ReadOnlyDictionary<HeadlessPlayerId, DeckList>(new Dictionary<HeadlessPlayerId, DeckList>());
    private string _sessionId = "local";
    private int _minimumMemory = -10;
    private int _maximumMemory = 10;
    private int _initialMemory;

    public long RandomSeed { get; init; }

    public bool IsHeadless { get; init; } = true;

    public bool IsAiSimulation { get; init; }

    public bool UseDeterministicChoices { get; init; } = true;

    public bool CanSetRandom { get; init; }

    public string SessionId
    {
        get => _sessionId;
        init => _sessionId = string.IsNullOrWhiteSpace(value) ? "local" : value.Trim();
    }

    public IReadOnlyList<HeadlessPlayerId> PlayerIds
    {
        get => _playerIds;
        init => _playerIds = CopyPlayerIds(value);
    }

    public IReadOnlyDictionary<HeadlessPlayerId, DeckList> Decks
    {
        get => _decks;
        init => _decks = CopyDecks(value);
    }

    public int InitialMemory
    {
        get => _initialMemory;
        init
        {
            if (value < _minimumMemory || value > _maximumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "InitialMemory must be inside the configured memory range.");
            }

            _initialMemory = value;
        }
    }

    public int MinimumMemory
    {
        get => _minimumMemory;
        init
        {
            if (value > _maximumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MinimumMemory must be less than or equal to MaximumMemory.");
            }

            if (_initialMemory < value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MinimumMemory must not exceed InitialMemory.");
            }

            _minimumMemory = value;
        }
    }

    public int MaximumMemory
    {
        get => _maximumMemory;
        init
        {
            if (value < _minimumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaximumMemory must be greater than or equal to MinimumMemory.");
            }

            if (_initialMemory > value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaximumMemory must not be less than InitialMemory.");
            }

            _maximumMemory = value;
        }
    }

    public static ContinuousContext Create(
        IEnumerable<HeadlessPlayerId> playerIds,
        long randomSeed = 0,
        bool useDeterministicChoices = true,
        bool isAiSimulation = false,
        bool canSetRandom = false,
        string? sessionId = null,
        IReadOnlyDictionary<HeadlessPlayerId, DeckList>? decks = null,
        int initialMemory = 0,
        int minimumMemory = -10,
        int maximumMemory = 10)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        return new ContinuousContext
        {
            PlayerIds = playerIds.ToArray(),
            RandomSeed = randomSeed,
            UseDeterministicChoices = useDeterministicChoices,
            IsAiSimulation = isAiSimulation,
            CanSetRandom = canSetRandom,
            SessionId = sessionId ?? "local",
            Decks = decks ?? new Dictionary<HeadlessPlayerId, DeckList>(),
            InitialMemory = initialMemory,
            MinimumMemory = minimumMemory,
            MaximumMemory = maximumMemory
        }.Validate();
    }

    public static ContinuousContext FromMatchConfig(
        MatchConfig config,
        string? sessionId = null,
        bool isAiSimulation = false,
        bool canSetRandom = false,
        IReadOnlyDictionary<HeadlessPlayerId, DeckList>? decks = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Create(
            config.PlayerIds,
            config.RandomSeed,
            config.UseDeterministicChoices,
            isAiSimulation,
            canSetRandom,
            sessionId,
            decks,
            config.InitialMemory,
            config.MinimumMemory,
            config.MaximumMemory);
    }

    public MatchConfig ToMatchConfig()
    {
        return MatchConfig.Create(
            PlayerIds,
            randomSeed: ToMatchConfigSeed(RandomSeed),
            useDeterministicChoices: UseDeterministicChoices,
            initialMemory: InitialMemory,
            minimumMemory: MinimumMemory,
            maximumMemory: MaximumMemory);
    }

    public ContinuousContext WithDeck(HeadlessPlayerId playerId, DeckList deck)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        ArgumentNullException.ThrowIfNull(deck);
        if (!PlayerIds.Contains(playerId))
        {
            throw new InvalidOperationException($"Deck owner '{playerId}' is not in PlayerIds.");
        }

        Dictionary<HeadlessPlayerId, DeckList> decks = new(Decks)
        {
            [playerId] = deck
        };
        return this with { Decks = decks };
    }

    public DeckList GetDeck(HeadlessPlayerId playerId)
    {
        return Decks.TryGetValue(playerId, out DeckList? deck)
            ? deck
            : throw new InvalidOperationException($"Deck for player '{playerId}' is not configured.");
    }

    public bool TryGetDeck(HeadlessPlayerId playerId, out DeckList? deck)
    {
        return Decks.TryGetValue(playerId, out deck);
    }

    public ContinuousContext Validate()
    {
        _ = ToMatchConfig();

        foreach (HeadlessPlayerId deckOwner in Decks.Keys)
        {
            if (!PlayerIds.Contains(deckOwner))
            {
                throw new InvalidOperationException($"Deck owner '{deckOwner}' is not in PlayerIds.");
            }
        }

        return this;
    }

    private static IReadOnlyList<HeadlessPlayerId> CopyPlayerIds(IEnumerable<HeadlessPlayerId>? playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        HeadlessPlayerId[] snapshot = playerIds.ToArray();
        if (snapshot.Any(playerId => playerId.IsEmpty))
        {
            throw new ArgumentException("Player ids must not contain empty ids.", nameof(playerIds));
        }

        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new InvalidOperationException("Player ids must not contain duplicate values.");
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyDictionary<HeadlessPlayerId, DeckList> CopyDecks(
        IReadOnlyDictionary<HeadlessPlayerId, DeckList>? decks)
    {
        ArgumentNullException.ThrowIfNull(decks);

        Dictionary<HeadlessPlayerId, DeckList> copy = new();
        foreach (KeyValuePair<HeadlessPlayerId, DeckList> pair in decks)
        {
            if (pair.Key.IsEmpty)
            {
                throw new ArgumentException("Deck owner id must not be empty.", nameof(decks));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<HeadlessPlayerId, DeckList>(copy);
    }

    private static int ToMatchConfigSeed(long randomSeed)
    {
        if (randomSeed < int.MinValue || randomSeed > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(randomSeed), "RandomSeed must fit inside MatchConfig's Int32 seed.");
        }

        return (int)randomSeed;
    }
}
