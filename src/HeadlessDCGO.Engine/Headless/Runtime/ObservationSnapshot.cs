namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record ObservationSnapshot
{
    private long _stepIndex;
    private int _pendingActionCount;
    private int _cardInstanceCount;
    private HeadlessTurnState _turn = HeadlessTurnState.Empty;
    private HeadlessChoiceState _choice = HeadlessChoiceState.Empty;
    private HeadlessAttackState _attack = HeadlessAttackState.Empty;
    private HeadlessEffectState _effects = HeadlessEffectState.Empty;
    private HeadlessMemoryState _memory = HeadlessMemoryState.Default;
    private IReadOnlyList<PlayerObservation> _players = Array.Empty<PlayerObservation>();

    public ObservationSnapshot(
        long StepIndex,
        bool IsTerminal,
        int PendingActionCount,
        bool HasPendingEffects,
        int CardInstanceCount,
        int? RandomSeed,
        string? LastActionType,
        bool? LastActionSucceeded,
        string? LastActionMessage,
        HeadlessTurnState Turn,
        HeadlessChoiceState Choice,
        HeadlessAttackState Attack,
        HeadlessEffectState Effects,
        HeadlessMemoryState Memory,
        IReadOnlyList<PlayerObservation> Players)
    {
        this.StepIndex = StepIndex;
        this.IsTerminal = IsTerminal;
        this.PendingActionCount = PendingActionCount;
        this.HasPendingEffects = HasPendingEffects;
        this.CardInstanceCount = CardInstanceCount;
        this.RandomSeed = RandomSeed;
        this.LastActionType = LastActionType;
        this.LastActionSucceeded = LastActionSucceeded;
        this.LastActionMessage = LastActionMessage;
        this.Turn = Turn;
        this.Choice = Choice;
        this.Attack = Attack;
        this.Effects = Effects;
        this.Memory = Memory;
        this.Players = Players;
    }

    public long StepIndex
    {
        get => _stepIndex;
        init => _stepIndex = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "StepIndex must not be negative.");
    }

    public bool IsTerminal { get; init; }

    public int PendingActionCount
    {
        get => _pendingActionCount;
        init => _pendingActionCount = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "PendingActionCount must not be negative.");
    }

    public bool HasPendingEffects { get; init; }

    public int CardInstanceCount
    {
        get => _cardInstanceCount;
        init => _cardInstanceCount = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "CardInstanceCount must not be negative.");
    }

    public int? RandomSeed { get; init; }

    public string? LastActionType { get; init; }

    public bool? LastActionSucceeded { get; init; }

    public string? LastActionMessage { get; init; }

    public HeadlessTurnState Turn
    {
        get => _turn;
        init => _turn = value ?? throw new ArgumentNullException(nameof(value));
    }

    public HeadlessChoiceState Choice
    {
        get => _choice;
        init => _choice = value ?? throw new ArgumentNullException(nameof(value));
    }

    public HeadlessAttackState Attack
    {
        get => _attack;
        init => _attack = value ?? throw new ArgumentNullException(nameof(value));
    }

    public HeadlessEffectState Effects
    {
        get => _effects;
        init => _effects = value ?? throw new ArgumentNullException(nameof(value));
    }

    public HeadlessMemoryState Memory
    {
        get => _memory;
        init => _memory = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IReadOnlyList<PlayerObservation> Players
    {
        get => _players;
        init => _players = CopyRequiredItems(value, nameof(Players));
    }

    public int PlayerCount => Players.Count;

    public static ObservationSnapshot Empty { get; } = new(
        StepIndex: 0,
        IsTerminal: false,
        PendingActionCount: 0,
        HasPendingEffects: false,
        CardInstanceCount: 0,
        RandomSeed: null,
        LastActionType: null,
        LastActionSucceeded: null,
        LastActionMessage: null,
        Turn: HeadlessTurnState.Empty,
        Choice: HeadlessChoiceState.Empty,
        Attack: HeadlessAttackState.Empty,
        Effects: HeadlessEffectState.Empty,
        Memory: HeadlessMemoryState.Default,
        Players: Array.Empty<PlayerObservation>());

    private static IReadOnlyList<T> CopyRequiredItems<T>(
        IEnumerable<T>? items,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        T[] snapshot = items.ToArray();
        if (snapshot.Any(item => item is null))
        {
            throw new ArgumentException("Observation collections must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record PlayerObservation
{
    private IReadOnlyList<ZoneObservation> _zones = Array.Empty<ZoneObservation>();

    public PlayerObservation(
        HeadlessPlayerId PlayerId,
        IReadOnlyList<ZoneObservation> Zones)
    {
        this.PlayerId = PlayerId;
        this.Zones = Zones;
    }

    public HeadlessPlayerId PlayerId { get; init; }

    public IReadOnlyList<ZoneObservation> Zones
    {
        get => _zones;
        init => _zones = CopyRequiredItems(value, nameof(Zones));
    }

    public int TotalCardCount => Zones.Sum(zone => zone.Count);

    public ZoneObservation? FindZone(ChoiceZone zone)
    {
        return Zones.FirstOrDefault(snapshot => snapshot.Zone == zone);
    }

    private static IReadOnlyList<T> CopyRequiredItems<T>(
        IEnumerable<T>? items,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        T[] snapshot = items.ToArray();
        if (snapshot.Any(item => item is null))
        {
            throw new ArgumentException("Player observation collections must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record ZoneObservation
{
    private int _count;
    private IReadOnlyList<HeadlessEntityId> _cardIds = Array.Empty<HeadlessEntityId>();
    private IReadOnlyList<CardObservation> _cards = Array.Empty<CardObservation>();

    public ZoneObservation(
        ChoiceZone Zone,
        int Count,
        IReadOnlyList<HeadlessEntityId> CardIds,
        IReadOnlyList<CardObservation>? Cards = null)
    {
        this.Zone = Zone;
        this.CardIds = CardIds;
        this.Count = Count;
        this.Cards = Cards ?? Array.Empty<CardObservation>();
    }

    public ChoiceZone Zone { get; init; }

    // G3.5-RL-A4b: typed per-card features for cards visible to the observer (empty for hidden /
    // count-only zones). The flat encoder draws from this; embedding policies can use it directly.
    public IReadOnlyList<CardObservation> Cards
    {
        get => _cards;
        init => _cards = (value ?? Array.Empty<CardObservation>()).ToArray();
    }

    public int Count
    {
        get => _count;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Zone card count must not be negative.");
            }

            if (value < CardIds.Count)
            {
                throw new ArgumentException("Zone card count must be greater than or equal to visible card ids.", nameof(value));
            }

            _count = value;
        }
    }

    public IReadOnlyList<HeadlessEntityId> CardIds
    {
        get => _cardIds;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            HeadlessEntityId[] snapshot = value.ToArray();
            if (Count > 0 && Count < snapshot.Length)
            {
                throw new ArgumentException("Zone card count must be greater than or equal to visible card ids.", nameof(value));
            }

            _cardIds = Array.AsReadOnly(snapshot);
        }
    }
}
