namespace HeadlessDCGO.Engine.Headless.Runtime;

public sealed record StepResult
{
    private IReadOnlyList<GameEvent> _events = Array.Empty<GameEvent>();
    private ObservationSnapshot _observation = ObservationSnapshot.Empty;
    private ActionMask _actionMask = ActionMask.Empty;

    public StepResult(
        bool IsTerminal,
        bool HasPendingChoice,
        IReadOnlyList<GameEvent> Events,
        ObservationSnapshot Observation,
        ActionMask ActionMask)
    {
        this.IsTerminal = IsTerminal;
        this.HasPendingChoice = HasPendingChoice;
        this.Events = Events;
        this.Observation = Observation;
        this.ActionMask = ActionMask;
    }

    public bool IsTerminal { get; init; }

    public bool HasPendingChoice { get; init; }

    public IReadOnlyList<GameEvent> Events
    {
        get => _events;
        init => _events = CopyEvents(value);
    }

    public ObservationSnapshot Observation
    {
        get => _observation;
        init => _observation = value ?? throw new ArgumentNullException(nameof(value));
    }

    public ActionMask ActionMask
    {
        get => _actionMask;
        init => _actionMask = value ?? throw new ArgumentNullException(nameof(value));
    }

    private static IReadOnlyList<GameEvent> CopyEvents(IEnumerable<GameEvent>? events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.ToArray();
    }
}
