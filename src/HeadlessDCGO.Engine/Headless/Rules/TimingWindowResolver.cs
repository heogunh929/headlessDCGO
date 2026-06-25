namespace HeadlessDCGO.Engine.Headless.Rules;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public interface TimingWindowResolver
{
    IReadOnlyList<TimingWindowTrigger> CollectTriggers(TimingWindow window);

    IReadOnlyList<TimingWindowTrigger> SortTriggers(IEnumerable<TimingWindowTrigger> triggers);

    IReadOnlyList<PendingEffect> OpenWindow(TimingWindow window);
}

public sealed class DefaultTimingWindowResolver : TimingWindowResolver
{
    private readonly IEffectQueryService _effectQueryService;

    public DefaultTimingWindowResolver(IEffectQueryService effectQueryService)
    {
        _effectQueryService = effectQueryService ?? throw new ArgumentNullException(nameof(effectQueryService));
    }

    public IReadOnlyList<TimingWindowTrigger> CollectTriggers(TimingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IReadOnlyList<EffectRequest> effects = _effectQueryService.GetEffectsForTiming(window.Timing);
        var triggers = new List<TimingWindowTrigger>(effects.Count);
        for (var i = 0; i < effects.Count; i++)
        {
            triggers.Add(new TimingWindowTrigger(
                effects[i],
                EffectResolutionMode.MainStack,
                TimingWindowTriggerKind.Mandatory,
                priority: 0,
                sequence: i));
        }

        return triggers;
    }

    public IReadOnlyList<TimingWindowTrigger> SortTriggers(IEnumerable<TimingWindowTrigger> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        return triggers
            .Select((trigger, index) => new IndexedTrigger(trigger, index))
            .OrderBy(item => item.Trigger.Kind)
            .ThenBy(item => item.Trigger.Priority)
            .ThenBy(item => item.Trigger.Sequence)
            .ThenBy(item => item.Index)
            .Select(item => item.Trigger)
            .ToArray();
    }

    public IReadOnlyList<PendingEffect> OpenWindow(TimingWindow window)
    {
        return SortTriggers(CollectTriggers(window))
            .Select(trigger => new PendingEffect(trigger.Request, trigger.Mode))
            .ToArray();
    }

    private readonly record struct IndexedTrigger(
        TimingWindowTrigger Trigger,
        int Index);
}

public sealed record TimingWindow
{
    public TimingWindow(string timing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        Timing = timing.Trim();
    }

    public string Timing { get; }
}

public sealed record TimingWindowTrigger
{
    public TimingWindowTrigger(
        EffectRequest request,
        EffectResolutionMode mode,
        TimingWindowTriggerKind kind,
        int priority,
        long sequence)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Effect resolution mode must be a known value.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Timing window trigger kind must be a known value.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Timing window trigger sequence must not be negative.");
        }

        Request = request;
        Mode = mode;
        Kind = kind;
        Priority = priority;
        Sequence = sequence;
    }

    public EffectRequest Request { get; }

    public EffectResolutionMode Mode { get; }

    public TimingWindowTriggerKind Kind { get; }

    public int Priority { get; }

    public long Sequence { get; }
}

public enum TimingWindowTriggerKind
{
    Mandatory = 0,
    Optional = 1,
}
