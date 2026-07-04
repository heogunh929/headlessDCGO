namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace with test-runner output once executable test infrastructure exists.
public sealed class HeadlessSmokeSuiteReporter
{
    public HeadlessSmokeSuiteReport Create(HeadlessSmokeSuiteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        HeadlessSmokeSuiteCaseReport[] cases = result.Cases
            .Select(CreateCaseReport)
            .ToArray();

        return new HeadlessSmokeSuiteReport(
            result.Passed,
            result.CaseCount,
            result.FailedCaseCount,
            result.Cases.Sum(item => item.Episode.StepCount),
            result.Cases.Sum(item => item.Episode.ToSampleBatch().Count),
            result.Cases
                .GroupBy(item => item.Episode.StopReason)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => group.Count()),
            cases,
            CreateSummary(result));
    }

    private static HeadlessSmokeSuiteCaseReport CreateCaseReport(HeadlessSmokeSuiteCaseResult result)
    {
        RlEpisodeSampleBatch sampleBatch = result.Episode.ToSampleBatch();
        HeadlessSmokeSuiteFailureReport[] failures = result.Verification.Failures
            .Select(failure => new HeadlessSmokeSuiteFailureReport(
                failure.Field,
                failure.Expected,
                failure.Actual))
            .ToArray();

        return new HeadlessSmokeSuiteCaseReport(
            result.Case.Name,
            result.Passed,
            result.Case.ActionPolicy is null ? "Scripted" : "Policy",
            result.Episode.ToFingerprint().Value,
            result.Episode.StepCount,
            result.Episode.Transitions().Count,
            sampleBatch.Count,
            result.Episode.TotalReward,
            result.Episode.IsTerminal,
            result.Episode.StopReason.ToString(),
            sampleBatch.Schema.ObservationLength,
            sampleBatch.Schema.ActionMaskLength,
            result.Episode.InitialState.Observation.Length,
            result.Episode.FinalState.Observation.Length,
            result.Episode.InitialState.ActionMask.Length,
            result.Episode.FinalState.ActionMask.Length,
            result.Episode.FinalState.ActionOutcome.ActionType,
            result.Episode.FinalState.ActionOutcome.WasProcessed,
            result.Episode.FinalState.ActionOutcome.WasRejected,
            failures);
    }

    private static string CreateSummary(HeadlessSmokeSuiteResult result)
    {
        string status = result.Passed ? "PASS" : "FAIL";
        return $"{status}: {result.CaseCount - result.FailedCaseCount}/{result.CaseCount} smoke cases passed.";
    }
}

public sealed record HeadlessSmokeSuiteReport(
    bool Passed,
    int CaseCount,
    int FailedCaseCount,
    int TotalStepCount,
    int TotalSampleCount,
    IReadOnlyDictionary<string, int> StopReasonCounts,
    IReadOnlyList<HeadlessSmokeSuiteCaseReport> Cases,
    string Summary)
{
    public IReadOnlyList<HeadlessSmokeSuiteCaseReport> FailedCases()
    {
        return Cases
            .Where(result => !result.Passed)
            .ToArray();
    }
}

public sealed record HeadlessSmokeSuiteCaseReport(
    string Name,
    bool Passed,
    string ExecutionMode,
    string Fingerprint,
    int StepCount,
    int TransitionCount,
    int SampleCount,
    double TotalReward,
    bool IsTerminal,
    string StopReason,
    int SchemaObservationLength,
    int SchemaActionMaskLength,
    int InitialObservationLength,
    int FinalObservationLength,
    int InitialActionMaskLength,
    int FinalActionMaskLength,
    string? LastActionType,
    bool LastActionProcessed,
    bool LastActionRejected,
    IReadOnlyList<HeadlessSmokeSuiteFailureReport> Failures);

public sealed record HeadlessSmokeSuiteFailureReport(
    string Field,
    string Expected,
    string Actual);
