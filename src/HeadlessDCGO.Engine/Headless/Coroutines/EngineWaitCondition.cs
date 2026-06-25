namespace HeadlessDCGO.Engine.Headless.Coroutines;

public sealed record EngineWaitCondition
{
    private readonly Func<bool>? _predicate;

    private EngineWaitCondition(
        EngineWaitConditionKind kind,
        TimeSpan duration,
        Func<bool>? predicate)
    {
        Kind = kind;
        Duration = duration;
        _predicate = predicate;
    }

    public EngineWaitConditionKind Kind { get; }

    public TimeSpan Duration { get; }

    public bool IsTimeBased => Kind == EngineWaitConditionKind.Seconds;

    public bool IsPredicateBased => Kind == EngineWaitConditionKind.Until;

    public static EngineWaitCondition Seconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be a finite value.");
        }

        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must not be negative.");
        }

        return new EngineWaitCondition(
            EngineWaitConditionKind.Seconds,
            TimeSpan.FromSeconds(seconds),
            predicate: null);
    }

    public static EngineWaitCondition Seconds(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must not be negative.");
        }

        return new EngineWaitCondition(
            EngineWaitConditionKind.Seconds,
            duration,
            predicate: null);
    }

    public static EngineWaitCondition Until(Func<bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new EngineWaitCondition(
            EngineWaitConditionKind.Until,
            TimeSpan.Zero,
            predicate);
    }

    public bool IsSatisfied()
    {
        return IsSatisfied(TimeSpan.Zero);
    }

    public bool IsSatisfied(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time must not be negative.");
        }

        if (Kind == EngineWaitConditionKind.Seconds && elapsed < Duration)
        {
            return false;
        }

        return _predicate?.Invoke() ?? true;
    }
}

public enum EngineWaitConditionKind
{
    Seconds,
    Until
}
