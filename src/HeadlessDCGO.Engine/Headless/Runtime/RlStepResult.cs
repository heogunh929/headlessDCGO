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
    double Discount,
    // G3.5-RL-A5: the fixed factored action mask (A3) is carried on every step result so a
    // MaskablePPO / MultiDiscrete trainer gets the per-position legality vector directly, without a
    // separate EncodeFactoredActionMask() call. Built from the same legal-action set as ActionMask.
    FactoredActionMask FactoredActionMask)
{
    public double[] ObservationVector => Observation.ToVector();

    public double[] ActionMaskVector => ActionMask.ToMaskVector();

    public double[] ActionCountVector => ActionMask.ToCountVector();

    /// <summary>The fixed-size factored action mask vector (1 at each legal factored index).</summary>
    public double[] FactoredActionMaskVector => FactoredActionMask.ToMaskVector();

    public RlActionOutcome ActionOutcome => RlActionOutcome.FromEvents(Events);
}
