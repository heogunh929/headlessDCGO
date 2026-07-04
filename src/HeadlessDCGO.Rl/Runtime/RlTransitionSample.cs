namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace this neutral sample DTO with the final trainer/export schema.
public sealed record RlTransitionSample(
    int StepIndex,
    string ActionId,
    int PlayerId,
    string ActionType,
    int ActionIndex,
    string EncodedActionKey,
    double Reward,
    double Discount,
    bool IsTerminal,
    bool ActionProcessed,
    bool ActionRejected,
    double[] Observation,
    double[] ActionMask,
    double[] ActionCounts,
    double[] NextObservation,
    double[] NextActionMask,
    double[] NextActionCounts)
{
    public static RlTransitionSample FromTransition(RlTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        EncodedAction? encodedAction = transition.PreviousState
            .ActionMask
            .FindByActionId(transition.Action.Id);
        RlActionOutcome outcome = transition.ActionOutcome;

        return new RlTransitionSample(
            transition.StepIndex,
            transition.Action.Id.Value,
            transition.Action.PlayerId.Value,
            transition.Action.ActionType,
            encodedAction?.ActionIndex ?? -1,
            encodedAction?.EncodedKey ?? string.Empty,
            transition.Reward,
            transition.Discount,
            transition.IsTerminal,
            outcome.WasProcessed,
            outcome.WasRejected,
            Copy(transition.PreviousState.ObservationVector),
            Copy(transition.PreviousState.ActionMaskVector),
            Copy(transition.PreviousState.ActionCountVector),
            Copy(transition.NextState.ObservationVector),
            Copy(transition.NextState.ActionMaskVector),
            Copy(transition.NextState.ActionCountVector));
    }

    private static double[] Copy(double[] values)
    {
        return values.ToArray();
    }
}

public sealed record RlEpisodeSampleBatch(
    string ScenarioName,
    bool IsTerminal,
    double TotalReward,
    RlVectorSchema Schema,
    IReadOnlyList<RlTransitionSample> Samples)
{
    public int Count => Samples.Count;

    public static RlEpisodeSampleBatch FromEpisode(HeadlessEpisodeResult episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        RlTransitionSample[] samples = episode
            .Transitions()
            .Select(RlTransitionSample.FromTransition)
            .ToArray();

        return new RlEpisodeSampleBatch(
            episode.ScenarioName,
            episode.IsTerminal,
            episode.TotalReward,
            RlVectorSchema.FromState(episode.InitialState),
            samples);
    }
}
