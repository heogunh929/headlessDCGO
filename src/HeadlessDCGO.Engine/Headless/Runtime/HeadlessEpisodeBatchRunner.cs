namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace this simple fan-out runner with trainer-scale rollout scheduling.
public sealed class HeadlessEpisodeBatchRunner
{
    public async Task<HeadlessEpisodeBatchResult> RunAsync(
        HeadlessEpisodeBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scenario);

        int repeatCount = Math.Max(0, request.RepeatCount);
        int maxDegreeOfParallelism = Math.Max(1, request.MaxDegreeOfParallelism);
        IReadOnlyList<HeadlessEpisodeBatchItemResult> items = maxDegreeOfParallelism == 1
            ? await RunSequentialAsync(request, repeatCount, cancellationToken).ConfigureAwait(false)
            : await RunParallelAsync(
                request,
                repeatCount,
                maxDegreeOfParallelism,
                cancellationToken).ConfigureAwait(false);

        return new HeadlessEpisodeBatchResult(
            request.Name,
            request.ExecutionMode,
            maxDegreeOfParallelism,
            items);
    }

    private static async Task<IReadOnlyList<HeadlessEpisodeBatchItemResult>> RunSequentialAsync(
        HeadlessEpisodeBatchRequest request,
        int repeatCount,
        CancellationToken cancellationToken)
    {
        List<HeadlessEpisodeBatchItemResult> items = new();

        for (int index = 0; index < repeatCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            items.Add(await RunOneAsync(request, index, cancellationToken).ConfigureAwait(false));
        }

        return items.ToArray();
    }

    private static async Task<IReadOnlyList<HeadlessEpisodeBatchItemResult>> RunParallelAsync(
        HeadlessEpisodeBatchRequest request,
        int repeatCount,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken)
    {
        using SemaphoreSlim semaphore = new(maxDegreeOfParallelism);

        Task<HeadlessEpisodeBatchItemResult>[] tasks = Enumerable
            .Range(0, repeatCount)
            .Select(index => RunOneWithGateAsync(request, index, semaphore, cancellationToken))
            .ToArray();

        HeadlessEpisodeBatchItemResult[] items = await Task
            .WhenAll(tasks)
            .ConfigureAwait(false);

        return items
            .OrderBy(item => item.RunIndex)
            .ToArray();
    }

    private static async Task<HeadlessEpisodeBatchItemResult> RunOneWithGateAsync(
        HeadlessEpisodeBatchRequest request,
        int zeroBasedRunIndex,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunOneAsync(request, zeroBasedRunIndex, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<HeadlessEpisodeBatchItemResult> RunOneAsync(
        HeadlessEpisodeBatchRequest request,
        int zeroBasedRunIndex,
        CancellationToken cancellationToken)
    {
        HeadlessScenario scenario = CreateScenarioForRun(request, zeroBasedRunIndex);
        Func<HeadlessRlEnvironment> environmentFactory =
            () => new HeadlessRlEnvironment(options: request.EnvironmentOptions);

        IHeadlessActionPolicy? actionPolicy = request.CreateActionPolicy();
        HeadlessEpisodeResult episode = actionPolicy is null
            ? await new HeadlessScenarioRunner(
                    environmentFactory: environmentFactory,
                    options: request.RunnerOptions)
                .RunAsync(scenario, cancellationToken)
                .ConfigureAwait(false)
            : await new HeadlessPolicyEpisodeRunner(
                    environmentFactory: environmentFactory,
                    options: request.PolicyRunnerOptions)
                .RunAsync(scenario, actionPolicy, cancellationToken)
                .ConfigureAwait(false);

        return new HeadlessEpisodeBatchItemResult(
            RunIndex: zeroBasedRunIndex + 1,
            RandomSeed: scenario.Config.RandomSeed,
            Episode: episode,
            Fingerprint: episode.ToFingerprint());
    }

    private static HeadlessScenario CreateScenarioForRun(
        HeadlessEpisodeBatchRequest request,
        int zeroBasedRunIndex)
    {
        if (!request.IncrementRandomSeed)
        {
            return request.Scenario;
        }

        MatchConfig config = request.Scenario.Config with
        {
            RandomSeed = request.Scenario.Config.RandomSeed + zeroBasedRunIndex
        };

        return request.Scenario with
        {
            Config = config
        };
    }
}

public sealed record HeadlessEpisodeBatchRequest
{
    public string Name { get; init; } = "headless-episode-batch";

    public HeadlessScenario Scenario { get; init; } = HeadlessScenario.Empty;

    public int RepeatCount { get; init; } = 1;

    public bool IncrementRandomSeed { get; init; }

    public int MaxDegreeOfParallelism { get; init; } = 1;

    public HeadlessRlEnvironmentOptions EnvironmentOptions { get; init; } =
        HeadlessRlEnvironmentOptions.Default;

    public HeadlessScenarioRunnerOptions? RunnerOptions { get; init; }

    public IHeadlessActionPolicy? ActionPolicy { get; init; }

    public Func<IHeadlessActionPolicy>? ActionPolicyFactory { get; init; }

    public HeadlessPolicyEpisodeRunnerOptions? PolicyRunnerOptions { get; init; }

    public string ExecutionMode => ActionPolicy is null && ActionPolicyFactory is null
        ? "Scripted"
        : "Policy";

    public IHeadlessActionPolicy? CreateActionPolicy()
    {
        return ActionPolicyFactory?.Invoke() ?? ActionPolicy;
    }
}

public sealed record HeadlessEpisodeBatchItemResult(
    int RunIndex,
    int RandomSeed,
    HeadlessEpisodeResult Episode,
    HeadlessEpisodeFingerprint Fingerprint);

public sealed record HeadlessEpisodeBatchResult(
    string Name,
    string ExecutionMode,
    int MaxDegreeOfParallelism,
    IReadOnlyList<HeadlessEpisodeBatchItemResult> Episodes)
{
    public int EpisodeCount => Episodes.Count;

    public int TotalStepCount => Episodes.Sum(item => item.Episode.StepCount);

    public int TotalSampleCount => Episodes.Sum(item => item.Episode.ToSampleBatch().Count);

    public double TotalReward => Episodes.Sum(item => item.Episode.TotalReward);

    public double AverageReward => EpisodeCount == 0
        ? 0d
        : TotalReward / EpisodeCount;

    public int TerminalEpisodeCount => Episodes.Count(item => item.Episode.IsTerminal);

    public IReadOnlyDictionary<HeadlessEpisodeStopReason, int> StopReasonCounts => Episodes
        .GroupBy(item => item.Episode.StopReason)
        .ToDictionary(group => group.Key, group => group.Count());

    public bool AllFingerprintsEqual => Episodes
        .Select(item => item.Fingerprint.Value)
        .Distinct(StringComparer.Ordinal)
        .Count() <= 1;

    public bool HasConsistentSchema => SchemaMismatches().Count == 0;

    public IReadOnlyList<string> Fingerprints()
    {
        return Episodes
            .Select(item => item.Fingerprint.Value)
            .ToArray();
    }

    public int CountByStopReason(HeadlessEpisodeStopReason stopReason)
    {
        return Episodes.Count(item => item.Episode.StopReason == stopReason);
    }

    public IReadOnlyList<HeadlessEpisodeBatchSchemaMismatch> SchemaMismatches()
    {
        if (Episodes.Count <= 1)
        {
            return Array.Empty<HeadlessEpisodeBatchSchemaMismatch>();
        }

        RlVectorSchema expectedSchema = Episodes[0].Episode.ToSampleBatch().Schema;
        List<HeadlessEpisodeBatchSchemaMismatch> mismatches = new();

        foreach (HeadlessEpisodeBatchItemResult item in Episodes.Skip(1))
        {
            RlVectorSchema actualSchema = item.Episode.ToSampleBatch().Schema;
            if (expectedSchema.ObservationLength != actualSchema.ObservationLength ||
                expectedSchema.ActionMaskLength != actualSchema.ActionMaskLength ||
                !expectedSchema.ObservationFeatureNames.SequenceEqual(actualSchema.ObservationFeatureNames) ||
                !expectedSchema.ActionSlotNames.SequenceEqual(actualSchema.ActionSlotNames))
            {
                mismatches.Add(new HeadlessEpisodeBatchSchemaMismatch(
                    item.RunIndex,
                    expectedSchema.ObservationLength,
                    actualSchema.ObservationLength,
                    expectedSchema.ActionMaskLength,
                    actualSchema.ActionMaskLength));
            }
        }

        return mismatches.ToArray();
    }

    public HeadlessEpisodeBatchReport ToReport()
    {
        return HeadlessEpisodeBatchReport.FromResult(this);
    }

    public RlTrainingDataset ToTrainingDataset()
    {
        return RlTrainingDataset.FromBatch(this);
    }
}

public sealed record HeadlessEpisodeBatchReport(
    string Name,
    string ExecutionMode,
    int EpisodeCount,
    int TotalStepCount,
    int TotalSampleCount,
    double TotalReward,
    double AverageReward,
    int TerminalEpisodeCount,
    int MaxDegreeOfParallelism,
    IReadOnlyDictionary<string, int> StopReasonCounts,
    bool AllFingerprintsEqual,
    bool HasConsistentSchema,
    int DatasetSampleCount,
    bool DatasetHasConsistentSchema,
    int DatasetJsonlLineCount,
    bool DatasetJsonlValid,
    IReadOnlyList<HeadlessEpisodeBatchItemReport> Episodes)
{
    public static HeadlessEpisodeBatchReport FromResult(HeadlessEpisodeBatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RlTrainingDataset dataset = result.ToTrainingDataset();

        return new HeadlessEpisodeBatchReport(
            result.Name,
            result.ExecutionMode,
            result.EpisodeCount,
            result.TotalStepCount,
            result.TotalSampleCount,
            result.TotalReward,
            result.AverageReward,
            result.TerminalEpisodeCount,
            result.MaxDegreeOfParallelism,
            result.StopReasonCounts.ToDictionary(
                item => item.Key.ToString(),
                item => item.Value),
            result.AllFingerprintsEqual,
            result.HasConsistentSchema,
            dataset.SampleCount,
            dataset.HasConsistentSchema,
            dataset.ToJsonLines().Count,
            dataset.ValidateJsonLines().Passed,
            result.Episodes
                .Select(HeadlessEpisodeBatchItemReport.FromResult)
                .ToArray());
    }
}

public sealed record HeadlessEpisodeBatchItemReport(
    int RunIndex,
    int RandomSeed,
    string Fingerprint,
    int StepCount,
    int SampleCount,
    double TotalReward,
    bool IsTerminal,
    string StopReason)
{
    public static HeadlessEpisodeBatchItemReport FromResult(HeadlessEpisodeBatchItemResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new HeadlessEpisodeBatchItemReport(
            result.RunIndex,
            result.RandomSeed,
            result.Fingerprint.Value,
            result.Episode.StepCount,
            result.Episode.ToSampleBatch().Count,
            result.Episode.TotalReward,
            result.Episode.IsTerminal,
            result.Episode.StopReason.ToString());
    }
}

public sealed record HeadlessEpisodeBatchSchemaMismatch(
    int RunIndex,
    int ExpectedObservationLength,
    int ActualObservationLength,
    int ExpectedActionMaskLength,
    int ActualActionMaskLength);

public sealed class HeadlessEpisodeBatchVerifier
{
    public HeadlessEpisodeBatchVerificationResult Verify(
        HeadlessEpisodeBatchResult result,
        HeadlessEpisodeBatchExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(expectation);

        List<HeadlessEpisodeBatchVerificationFailure> failures = new();
        VerifyNullable("EpisodeCount", expectation.EpisodeCount, result.EpisodeCount, failures);
        VerifyNullable("TotalStepCount", expectation.TotalStepCount, result.TotalStepCount, failures);
        VerifyNullable("TotalSampleCount", expectation.TotalSampleCount, result.TotalSampleCount, failures);
        VerifyNullable("TerminalEpisodeCount", expectation.TerminalEpisodeCount, result.TerminalEpisodeCount, failures);
        VerifyNullable("MaxDegreeOfParallelism", expectation.MaxDegreeOfParallelism, result.MaxDegreeOfParallelism, failures);
        VerifyStopReasonCounts(expectation.StopReasonCounts, result, failures);
        VerifyNullable("AllFingerprintsEqual", expectation.AllFingerprintsEqual, result.AllFingerprintsEqual, failures);
        VerifyNullable("HasConsistentSchema", expectation.HasConsistentSchema, result.HasConsistentSchema, failures);
        VerifyNullable("TotalReward", expectation.TotalReward, result.TotalReward, failures, expectation.Tolerance);
        VerifyNullable("AverageReward", expectation.AverageReward, result.AverageReward, failures, expectation.Tolerance);
        VerifyDataset(result, failures);

        return new HeadlessEpisodeBatchVerificationResult(
            result.Name,
            failures.ToArray());
    }

    private static void VerifyNullable<T>(
        string field,
        T? expected,
        T actual,
        List<HeadlessEpisodeBatchVerificationFailure> failures)
        where T : struct, IEquatable<T>
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (!expected.Value.Equals(actual))
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                field,
                expected.Value.ToString() ?? string.Empty,
                actual.ToString() ?? string.Empty));
        }
    }

    private static void VerifyNullable(
        string field,
        double? expected,
        double actual,
        List<HeadlessEpisodeBatchVerificationFailure> failures,
        double tolerance)
    {
        if (!expected.HasValue)
        {
            return;
        }

        if (Math.Abs(expected.Value - actual) > tolerance)
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                field,
                expected.Value.ToString("R"),
                actual.ToString("R")));
        }
    }

    private static void VerifyDataset(
        HeadlessEpisodeBatchResult result,
        List<HeadlessEpisodeBatchVerificationFailure> failures)
    {
        RlTrainingDataset dataset = result.ToTrainingDataset();
        if (dataset.SampleCount != result.TotalSampleCount)
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                "Dataset.SampleCount",
                result.TotalSampleCount.ToString(),
                dataset.SampleCount.ToString()));
        }

        if (dataset.EpisodeCount != result.EpisodeCount)
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                "Dataset.EpisodeCount",
                result.EpisodeCount.ToString(),
                dataset.EpisodeCount.ToString()));
        }

        int expectedJsonLineCount = 1 + dataset.EpisodeCount + dataset.SampleCount;
        int actualJsonLineCount = dataset.ToJsonLines().Count;
        if (actualJsonLineCount != expectedJsonLineCount)
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                "Dataset.JsonlLineCount",
                expectedJsonLineCount.ToString(),
                actualJsonLineCount.ToString()));
        }

        RlTrainingDatasetJsonlValidationResult jsonValidation = dataset.ValidateJsonLines();
        if (!jsonValidation.Passed)
        {
            failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                "Dataset.JsonlValidation",
                "passed",
                string.Join("; ", jsonValidation.Failures.Select(failure =>
                    $"{failure.LineNumber}:{failure.Field}={failure.Actual}"))));
        }
    }

    private static void VerifyStopReasonCounts(
        IReadOnlyList<HeadlessEpisodeStopReasonCountExpectation> expectations,
        HeadlessEpisodeBatchResult result,
        List<HeadlessEpisodeBatchVerificationFailure> failures)
    {
        foreach (HeadlessEpisodeStopReasonCountExpectation expectation in expectations)
        {
            int actual = result.CountByStopReason(expectation.StopReason);
            if (actual != expectation.Count)
            {
                failures.Add(new HeadlessEpisodeBatchVerificationFailure(
                    $"StopReason.{expectation.StopReason}",
                    expectation.Count.ToString(),
                    actual.ToString()));
            }
        }
    }
}

public sealed record HeadlessEpisodeBatchExpectation
{
    public int? EpisodeCount { get; init; }

    public int? TotalStepCount { get; init; }

    public int? TotalSampleCount { get; init; }

    public int? TerminalEpisodeCount { get; init; }

    public int? MaxDegreeOfParallelism { get; init; }

    public IReadOnlyList<HeadlessEpisodeStopReasonCountExpectation> StopReasonCounts { get; init; } =
        Array.Empty<HeadlessEpisodeStopReasonCountExpectation>();

    public bool? AllFingerprintsEqual { get; init; }

    public bool? HasConsistentSchema { get; init; }

    public double? TotalReward { get; init; }

    public double? AverageReward { get; init; }

    public double Tolerance { get; init; } = 0.000001d;
}

public sealed record HeadlessEpisodeStopReasonCountExpectation(
    HeadlessEpisodeStopReason StopReason,
    int Count);

public sealed record HeadlessEpisodeBatchVerificationResult(
    string Name,
    IReadOnlyList<HeadlessEpisodeBatchVerificationFailure> Failures)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record HeadlessEpisodeBatchVerificationFailure(
    string Field,
    string Expected,
    string Actual);
