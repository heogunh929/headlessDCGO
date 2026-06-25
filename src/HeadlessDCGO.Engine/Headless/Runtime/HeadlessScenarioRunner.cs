namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Move this into a dedicated test/benchmark harness once project test infrastructure exists.
public sealed class HeadlessScenarioRunner
{
    private readonly Func<HeadlessRlEnvironment> _environmentFactory;
    private readonly HeadlessScenarioRunnerOptions _options;
    private readonly HeadlessScenarioSetupApplier _setupApplier = new();

    public HeadlessScenarioRunner(
        Func<HeadlessRlEnvironment>? environmentFactory = null,
        HeadlessScenarioRunnerOptions? options = null)
    {
        _environmentFactory = environmentFactory ?? (() => new HeadlessRlEnvironment());
        _options = options ?? HeadlessScenarioRunnerOptions.Default;
    }

    public async Task<HeadlessEpisodeResult> RunAsync(
        HeadlessScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(scenario.Config);
        ArgumentNullException.ThrowIfNull(scenario.Actions);

        HeadlessRlEnvironment environment = _environmentFactory();
        await environment
            .InitializeAsync(scenario.Config, cancellationToken)
            .ConfigureAwait(false);
        await _setupApplier
            .ApplyAsync(environment, scenario.Setup, cancellationToken)
            .ConfigureAwait(false);

        RlStepResult initialState = environment.Observe();

        List<HeadlessScenarioStep> steps = new();
        RlStepResult currentState = initialState;
        HeadlessEpisodeStopReason stopReason = HeadlessEpisodeStopReason.ActionListExhausted;
        int stepIndex = 0;

        if (_options.StopOnTerminal && currentState.IsTerminal)
        {
            stopReason = HeadlessEpisodeStopReason.InitialStateTerminal;
            return new HeadlessEpisodeResult(
                scenario.Name,
                initialState,
                steps,
                currentState,
                stopReason);
        }

        foreach (LegalAction action in scenario.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.StopOnTerminal && currentState.IsTerminal)
            {
                break;
            }

            RlStepResult previousState = currentState;
            RlStepResult result = await StepActionAsync(environment, action, cancellationToken)
                .ConfigureAwait(false);

            currentState = result;
            steps.Add(new HeadlessScenarioStep(++stepIndex, action, previousState, result));

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

    private async Task<RlStepResult> StepActionAsync(
        HeadlessRlEnvironment environment,
        LegalAction action,
        CancellationToken cancellationToken)
    {
        return _options.ActionSelectionMode switch
        {
            HeadlessScenarioActionSelectionMode.ActionId => await environment
                .StepByActionIdAsync(action.Id, cancellationToken)
                .ConfigureAwait(false),
            HeadlessScenarioActionSelectionMode.EncodedKey => await StepByEncodedKeyAsync(
                environment,
                action,
                cancellationToken).ConfigureAwait(false),
            HeadlessScenarioActionSelectionMode.ActionIndex => await StepByActionIndexAsync(
                environment,
                action,
                cancellationToken).ConfigureAwait(false),
            _ => await environment
                .StepAsync(action, cancellationToken)
                .ConfigureAwait(false)
        };
    }

    private static async Task<RlStepResult> StepByEncodedKeyAsync(
        HeadlessRlEnvironment environment,
        LegalAction action,
        CancellationToken cancellationToken)
    {
        EncodedAction? encodedAction = environment
            .Observe()
            .ActionMask
            .FindByActionId(action.Id);

        string encodedKey = encodedAction?.EncodedKey ?? action.Id.Value;
        return await environment
            .StepByEncodedKeyAsync(encodedKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<RlStepResult> StepByActionIndexAsync(
        HeadlessRlEnvironment environment,
        LegalAction action,
        CancellationToken cancellationToken)
    {
        EncodedAction? encodedAction = environment
            .Observe()
            .ActionMask
            .FindByActionId(action.Id);

        int actionIndex = encodedAction?.ActionIndex ?? -1;
        return await environment
            .StepByActionIndexAsync(actionIndex, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record HeadlessScenario(
    string Name,
    MatchConfig Config,
    IReadOnlyList<LegalAction> Actions)
{
    public HeadlessScenarioSetup Setup { get; init; } = HeadlessScenarioSetup.Empty;

    public static HeadlessScenario Empty { get; } = new(
        Name: "empty",
        Config: new MatchConfig(),
        Actions: Array.Empty<LegalAction>());
}

public sealed record HeadlessScenarioStep(
    int StepIndex,
    LegalAction Action,
    RlStepResult PreviousState,
    RlStepResult Result)
{
    public RlTransition Transition => new(
        StepIndex,
        PreviousState,
        Action,
        Result);
}

public sealed record HeadlessEpisodeResult(
    string ScenarioName,
    RlStepResult InitialState,
    IReadOnlyList<HeadlessScenarioStep> Steps,
    RlStepResult FinalState,
    HeadlessEpisodeStopReason StopReason = HeadlessEpisodeStopReason.Unknown)
{
    public bool IsTerminal => FinalState.IsTerminal;

    public int StepCount => Steps.Count;

    public double TotalReward => Steps.Sum(step => step.Result.Reward);

    public IReadOnlyList<RlTransition> Transitions()
    {
        return Steps
            .Select(step => step.Transition)
            .ToArray();
    }

    public RlEpisodeSampleBatch ToSampleBatch()
    {
        return RlEpisodeSampleBatch.FromEpisode(this);
    }

    public HeadlessEpisodeFingerprint ToFingerprint()
    {
        return HeadlessEpisodeFingerprint.FromEpisode(this);
    }
}

public enum HeadlessEpisodeStopReason
{
    Unknown = 0,
    ActionListExhausted,
    InitialStateTerminal,
    Terminal,
    NoLegalActions,
    PolicyReturnedNoAction,
    MaxStepsReached
}

public sealed record HeadlessScenarioRunnerOptions
{
    public static HeadlessScenarioRunnerOptions Default { get; } = new();

    public bool StopOnTerminal { get; init; } = true;

    public HeadlessScenarioActionSelectionMode ActionSelectionMode { get; init; } =
        HeadlessScenarioActionSelectionMode.Direct;
}

public enum HeadlessScenarioActionSelectionMode
{
    Direct = 0,
    ActionId,
    EncodedKey,
    ActionIndex
}
