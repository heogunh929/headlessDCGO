namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with executable test assertions once test infrastructure exists.
public sealed class HeadlessScenarioVerifier
{
    public HeadlessScenarioVerificationResult Verify(
        HeadlessEpisodeResult result,
        HeadlessScenarioExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(expectation);

        List<HeadlessScenarioVerificationFailure> failures = new();

        VerifyNullable("IsTerminal", expectation.IsTerminal, result.IsTerminal, failures);
        VerifyNullable("StepCount", expectation.StepCount, result.StepCount, failures);
        VerifyStopReason(expectation.StopReason, result.StopReason, failures);
        VerifyNullable("IsDraw", expectation.IsDraw, result.FinalState.Result?.IsDraw, failures);
        VerifyNullable("IsSurrender", expectation.IsSurrender, result.FinalState.Result?.IsSurrender, failures);
        VerifyNullable("FinalReward", expectation.FinalReward, result.FinalState.Reward, failures, expectation.Tolerance);
        VerifyNullable("TotalReward", expectation.TotalReward, result.TotalReward, failures, expectation.Tolerance);
        VerifyNullable("FinalDiscount", expectation.FinalDiscount, result.FinalState.Discount, failures, expectation.Tolerance);
        VerifyNullable("LegalActionCount", expectation.LegalActionCount, result.FinalState.ActionMask.LegalActions.Count, failures);
        VerifyPlayerId(expectation.WinnerId, result.FinalState.Result?.WinnerId, failures);
        VerifyString("ResultReason", expectation.ResultReason, result.FinalState.Result?.Reason, failures);
        VerifyActionOutcome(expectation, result.FinalState.ActionOutcome, failures);
        VerifyTransitionIntegrity(result, failures);
        VerifySampleBatch(result, failures);
        VerifyFingerprint(result, failures);
        VerifyFeatures(expectation.Features, result.FinalState.Observation, failures);
        VerifyZoneCounts(expectation.ZoneCounts, result.FinalState.Observation, failures);
        VerifyEventTypes(expectation.EventTypes, result, failures);

        return new HeadlessScenarioVerificationResult(result.ScenarioName, failures.ToArray());
    }

    private static void VerifyNullable<T>(
        string field,
        T? expected,
        T? actual,
        List<HeadlessScenarioVerificationFailure> failures)
        where T : struct, IEquatable<T>
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (!actual.HasValue || !expected.Value.Equals(actual.Value))
        {
            string actualValue = actual.HasValue
                ? actual.Value.ToString() ?? string.Empty
                : "<null>";

            failures.Add(new HeadlessScenarioVerificationFailure(
                field,
                expected.Value.ToString() ?? string.Empty,
                actualValue));
        }
    }

    private static void VerifyNullable(
        string field,
        double? expected,
        double actual,
        List<HeadlessScenarioVerificationFailure> failures,
        double tolerance)
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (Math.Abs(expected.Value - actual) > tolerance)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                field,
                expected.Value.ToString("R"),
                actual.ToString("R")));
        }
    }

    private static void VerifyPlayerId(
        HeadlessPlayerId? expected,
        HeadlessPlayerId? actual,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (!actual.HasValue || expected.Value != actual.Value)
        {
            string actualValue = actual.HasValue
                ? actual.Value.Value.ToString()
                : "<null>";

            failures.Add(new HeadlessScenarioVerificationFailure(
                "WinnerId",
                expected.Value.Value.ToString(),
                actualValue));
        }
    }

    private static void VerifyString(
        string field,
        string? expected,
        string? actual,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        if (expected is null)
        {
            return;
        }

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                field,
                expected,
                actual ?? "<null>"));
        }
    }

    private static void VerifyStopReason(
        HeadlessEpisodeStopReason? expected,
        HeadlessEpisodeStopReason actual,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (expected.Value != actual)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "StopReason",
                expected.Value.ToString(),
                actual.ToString()));
        }
    }

    private static void VerifyZoneCounts(
        IReadOnlyList<HeadlessZoneCountExpectation> expectations,
        EncodedObservation observation,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        foreach (HeadlessZoneCountExpectation expectation in expectations)
        {
            string featureName = $"player.{expectation.PlayerId.Value}.zone.{expectation.Zone}.count";
            ObservationFeature? feature = observation.Features.FirstOrDefault(item =>
                string.Equals(item.Name, featureName, StringComparison.Ordinal));

            if (feature is null)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    featureName,
                    expectation.Count.ToString(),
                    "<missing>"));
                continue;
            }

            if (Math.Abs(feature.Value - expectation.Count) > expectation.Tolerance)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    featureName,
                    expectation.Count.ToString(),
                    feature.Value.ToString("R")));
            }
        }
    }

    private static void VerifyFeatures(
        IReadOnlyList<HeadlessFeatureExpectation> expectations,
        EncodedObservation observation,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        foreach (HeadlessFeatureExpectation expectation in expectations)
        {
            ObservationFeature? feature = observation.Features.FirstOrDefault(item =>
                string.Equals(item.Name, expectation.Name, StringComparison.Ordinal));

            if (feature is null)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    expectation.Name,
                    expectation.Value.ToString("R"),
                    "<missing>"));
                continue;
            }

            if (Math.Abs(feature.Value - expectation.Value) > expectation.Tolerance)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    expectation.Name,
                    expectation.Value.ToString("R"),
                    feature.Value.ToString("R")));
            }
        }
    }

    private static void VerifyEventTypes(
        IReadOnlyList<HeadlessEventTypeExpectation> expectations,
        HeadlessEpisodeResult result,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        if (expectations.Count == 0)
        {
            return;
        }

        IReadOnlyList<GameEvent> events = result.InitialState.Events
            .Concat(result.Steps.SelectMany(step => step.Result.Events))
            .ToArray();

        foreach (HeadlessEventTypeExpectation expectation in expectations)
        {
            int actualCount = events.Count(item => item.Type == expectation.Type);

            if (actualCount < expectation.MinCount)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    $"EventType.{expectation.Type}.MinCount",
                    expectation.MinCount.ToString(),
                    actualCount.ToString()));
            }
        }
    }

    private static void VerifyActionOutcome(
        HeadlessScenarioExpectation expectation,
        RlActionOutcome outcome,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        VerifyNullable("LastActionProcessed", expectation.LastActionProcessed, outcome.WasProcessed, failures);
        VerifyNullable("LastActionRejected", expectation.LastActionRejected, outcome.WasRejected, failures);
        VerifyString("LastActionType", expectation.LastActionType, outcome.ActionType, failures);
    }

    private static void VerifyTransitionIntegrity(
        HeadlessEpisodeResult result,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        IReadOnlyList<RlTransition> transitions = result.Transitions();
        if (transitions.Count != result.StepCount)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "TransitionCount",
                result.StepCount.ToString(),
                transitions.Count.ToString()));
            return;
        }

        for (int index = 0; index < transitions.Count; index++)
        {
            RlTransition transition = transitions[index];
            int expectedStepIndex = index + 1;
            if (transition.StepIndex != expectedStepIndex)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    $"Transition[{index}].StepIndex",
                    expectedStepIndex.ToString(),
                    transition.StepIndex.ToString()));
            }

            RlStepResult expectedPreviousState = index == 0
                ? result.InitialState
                : transitions[index - 1].NextState;

            if (!ReferenceEquals(transition.PreviousState, expectedPreviousState))
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    $"Transition[{index}].PreviousState",
                    "previous episode state reference",
                    "different state reference"));
            }
        }

        RlStepResult expectedFinalState = transitions.Count == 0
            ? result.InitialState
            : transitions[^1].NextState;

        if (!ReferenceEquals(result.FinalState, expectedFinalState))
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "FinalState",
                "last transition next state",
                "different state reference"));
        }
    }

    private static void VerifySampleBatch(
        HeadlessEpisodeResult result,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        RlEpisodeSampleBatch batch = result.ToSampleBatch();
        if (batch.Count != result.StepCount)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "SampleCount",
                result.StepCount.ToString(),
                batch.Count.ToString()));
        }

        if (!string.Equals(batch.ScenarioName, result.ScenarioName, StringComparison.Ordinal))
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "SampleBatch.ScenarioName",
                result.ScenarioName,
                batch.ScenarioName));
        }

        if (batch.Samples.Count == 0)
        {
            return;
        }

        foreach (RlTransitionSample sample in batch.Samples)
        {
            RlVectorSchemaValidationResult schemaValidation = batch.Schema.ValidateSample(sample);
            if (!schemaValidation.Passed)
            {
                failures.Add(new HeadlessScenarioVerificationFailure(
                    $"Sample[{sample.StepIndex}].Schema",
                    "valid vector lengths",
                    string.Join("; ", schemaValidation.Failures)));
            }
        }

        RlTransitionSample firstSample = batch.Samples[0];
        int expectedObservationLength = result.InitialState.Observation.Length;
        if (firstSample.Observation.Length != expectedObservationLength)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "Sample[0].Observation.Length",
                expectedObservationLength.ToString(),
                firstSample.Observation.Length.ToString()));
        }

        int expectedActionMaskLength = result.InitialState.ActionMask.Length;
        if (firstSample.ActionMask.Length != expectedActionMaskLength)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "Sample[0].ActionMask.Length",
                expectedActionMaskLength.ToString(),
                firstSample.ActionMask.Length.ToString()));
        }
    }

    private static void VerifyFingerprint(
        HeadlessEpisodeResult result,
        List<HeadlessScenarioVerificationFailure> failures)
    {
        HeadlessEpisodeFingerprint fingerprint = result.ToFingerprint();
        if (!string.Equals(fingerprint.ScenarioName, result.ScenarioName, StringComparison.Ordinal))
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "Fingerprint.ScenarioName",
                result.ScenarioName,
                fingerprint.ScenarioName));
        }

        if (fingerprint.StepCount != result.StepCount)
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "Fingerprint.StepCount",
                result.StepCount.ToString(),
                fingerprint.StepCount.ToString()));
        }

        if (string.IsNullOrWhiteSpace(fingerprint.Value))
        {
            failures.Add(new HeadlessScenarioVerificationFailure(
                "Fingerprint.Value",
                "non-empty",
                "<empty>"));
        }
    }
}

public sealed record HeadlessScenarioExpectation
{
    public bool? IsTerminal { get; init; }

    public int? StepCount { get; init; }

    public HeadlessEpisodeStopReason? StopReason { get; init; }

    public HeadlessPlayerId? WinnerId { get; init; }

    public bool? IsDraw { get; init; }

    public bool? IsSurrender { get; init; }

    public string? ResultReason { get; init; }

    public double? FinalReward { get; init; }

    public double? TotalReward { get; init; }

    public double? FinalDiscount { get; init; }

    public int? LegalActionCount { get; init; }

    public bool? LastActionProcessed { get; init; }

    public bool? LastActionRejected { get; init; }

    public string? LastActionType { get; init; }

    public double Tolerance { get; init; } = 0.000001d;

    public IReadOnlyList<HeadlessZoneCountExpectation> ZoneCounts { get; init; } =
        Array.Empty<HeadlessZoneCountExpectation>();

    public IReadOnlyList<HeadlessFeatureExpectation> Features { get; init; } =
        Array.Empty<HeadlessFeatureExpectation>();

    public IReadOnlyList<HeadlessEventTypeExpectation> EventTypes { get; init; } =
        Array.Empty<HeadlessEventTypeExpectation>();
}

public sealed record HeadlessFeatureExpectation(
    string Name,
    double Value,
    double Tolerance = 0.000001d);

public sealed record HeadlessZoneCountExpectation(
    HeadlessPlayerId PlayerId,
    ChoiceZone Zone,
    int Count,
    double Tolerance = 0.000001d);

public sealed record HeadlessEventTypeExpectation(
    GameEventType Type,
    int MinCount = 1);

public sealed record HeadlessScenarioVerificationResult(
    string ScenarioName,
    IReadOnlyList<HeadlessScenarioVerificationFailure> Failures)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record HeadlessScenarioVerificationFailure(
    string Field,
    string Expected,
    string Actual);
