namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Expand reward shaping once full scoring and terminal cause logic is ported.
public sealed record RlStepResult(
    bool IsTerminal,
    bool HasPendingChoice,
    EncodedObservation Observation,
    EncodedActionMask ActionMask,
    IReadOnlyList<GameEvent> Events,
    MatchResult? Result,
    double Reward,
    double Discount)
{
    public double[] ObservationVector => Observation.ToVector();

    public double[] ActionMaskVector => ActionMask.ToMaskVector();

    public double[] ActionCountVector => ActionMask.ToCountVector();

    public RlActionOutcome ActionOutcome => RlActionOutcome.FromEvents(Events);
}
