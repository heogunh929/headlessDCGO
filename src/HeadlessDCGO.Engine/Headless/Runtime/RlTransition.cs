namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace this generic transition with the final trainer-facing sample format.
public sealed record RlTransition(
    int StepIndex,
    RlStepResult PreviousState,
    LegalAction Action,
    RlStepResult NextState)
{
    public bool IsTerminal => NextState.IsTerminal;

    public double Reward => NextState.Reward;

    public double Discount => NextState.Discount;

    public RlActionOutcome ActionOutcome => NextState.ActionOutcome;

    public double[] PreviousObservationVector => PreviousState.ObservationVector;

    public double[] NextObservationVector => NextState.ObservationVector;

    public double[] PreviousActionMaskVector => PreviousState.ActionMaskVector;

    public double[] NextActionMaskVector => NextState.ActionMaskVector;
}
