namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record MatchConfig
{
    private IReadOnlyList<HeadlessPlayerId> _playerIds = Array.Empty<HeadlessPlayerId>();
    private int _initialMemory;
    private int _minimumMemory = -10;
    private int _maximumMemory = 10;

    public IReadOnlyList<HeadlessPlayerId> PlayerIds
    {
        get => _playerIds;
        init => _playerIds = CopyPlayerIds(value);
    }

    public int RandomSeed { get; init; }

    public bool UseDeterministicChoices { get; init; } = true;

    public MatchSetupConfig? Setup { get; init; }

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

    public static MatchConfig Create(
        IEnumerable<HeadlessPlayerId> playerIds,
        int randomSeed = 0,
        bool useDeterministicChoices = true,
        int initialMemory = 0,
        int minimumMemory = -10,
        int maximumMemory = 10,
        MatchSetupConfig? setup = null)
    {
        return new MatchConfig
        {
            PlayerIds = CopyPlayerIds(playerIds),
            RandomSeed = randomSeed,
            UseDeterministicChoices = useDeterministicChoices,
            InitialMemory = initialMemory,
            MinimumMemory = minimumMemory,
            MaximumMemory = maximumMemory,
            Setup = setup
        }.Validate();
    }

    public MatchConfig Validate()
    {
        if (MinimumMemory > MaximumMemory)
        {
            throw new InvalidOperationException("MinimumMemory must be less than or equal to MaximumMemory.");
        }

        if (InitialMemory < MinimumMemory || InitialMemory > MaximumMemory)
        {
            throw new InvalidOperationException("InitialMemory must be inside the configured memory range.");
        }

        if (PlayerIds.Count != PlayerIds.Distinct().Count())
        {
            throw new InvalidOperationException("PlayerIds must not contain duplicate values.");
        }

        Setup?.Validate(PlayerIds);

        return this;
    }

    private static IReadOnlyList<HeadlessPlayerId> CopyPlayerIds(IEnumerable<HeadlessPlayerId>? playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        return playerIds.ToArray();
    }
}
