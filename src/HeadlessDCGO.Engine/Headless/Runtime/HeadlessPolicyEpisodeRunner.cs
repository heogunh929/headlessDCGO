namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Expand into the main self-play/RL rollout runner once rules are ported.
public sealed class HeadlessPolicyEpisodeRunner
{
    private readonly Func<HeadlessRlEnvironment> _environmentFactory;
    private readonly HeadlessPolicyEpisodeRunnerOptions _options;
    private readonly HeadlessScenarioSetupApplier _setupApplier = new();

    public HeadlessPolicyEpisodeRunner(
        Func<HeadlessRlEnvironment>? environmentFactory = null,
        HeadlessPolicyEpisodeRunnerOptions? options = null)
    {
        _environmentFactory = environmentFactory ?? (() => new HeadlessRlEnvironment());
        _options = options ?? HeadlessPolicyEpisodeRunnerOptions.Default;
    }

    public async Task<HeadlessEpisodeResult> RunAsync(
        HeadlessScenario scenario,
        IHeadlessActionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(policy);

        HeadlessRlEnvironment environment = _environmentFactory();
        await environment
            .InitializeAsync(scenario.Config, cancellationToken)
            .ConfigureAwait(false);
        await _setupApplier
            .ApplyAsync(environment, scenario.Setup, cancellationToken)
            .ConfigureAwait(false);

        RlStepResult initialState = environment.Observe();
        RlStepResult currentState = initialState;
        List<HeadlessScenarioStep> steps = new();

        int maxSteps = Math.Max(0, _options.MaxSteps);
        HeadlessEpisodeStopReason stopReason = HeadlessEpisodeStopReason.MaxStepsReached;
        for (int stepIndex = 1; stepIndex <= maxSteps; stepIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.StopOnTerminal && currentState.IsTerminal)
            {
                stopReason = steps.Count == 0
                    ? HeadlessEpisodeStopReason.InitialStateTerminal
                    : HeadlessEpisodeStopReason.Terminal;
                break;
            }

            if (_options.StopWhenNoLegalActions && !currentState.ActionMask.HasAnyLegalAction)
            {
                stopReason = HeadlessEpisodeStopReason.NoLegalActions;
                break;
            }

            RlStepResult previousState = currentState;
            HeadlessActionDecision decision = await policy
                .ChooseActionAsync(previousState, cancellationToken)
                .ConfigureAwait(false);

            if (!decision.HasAction || decision.Action is null)
            {
                stopReason = HeadlessEpisodeStopReason.PolicyReturnedNoAction;
                break;
            }

            currentState = await environment
                .StepAsync(decision.Action, cancellationToken)
                .ConfigureAwait(false);
            steps.Add(new HeadlessScenarioStep(
                stepIndex,
                decision.Action.LegalAction,
                previousState,
                currentState));

            if (_options.StopOnTerminal && currentState.IsTerminal)
            {
                stopReason = HeadlessEpisodeStopReason.Terminal;
                break;
            }
        }

        return new HeadlessEpisodeResult(
            scenario.Name,
            initialState,
            steps.ToArray(),
            currentState,
            stopReason);
    }
}

public sealed record HeadlessPolicyEpisodeRunnerOptions
{
    public static HeadlessPolicyEpisodeRunnerOptions Default { get; } = new();

    public int MaxSteps { get; init; } = 1024;

    public bool StopOnTerminal { get; init; } = true;

    public bool StopWhenNoLegalActions { get; init; } = true;
}
