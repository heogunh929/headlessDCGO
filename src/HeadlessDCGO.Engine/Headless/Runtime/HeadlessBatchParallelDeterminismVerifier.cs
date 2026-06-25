namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Move this into executable parity tests once project test infrastructure exists.
public sealed class HeadlessBatchParallelDeterminismVerifier(
    HeadlessEpisodeBatchRunner? runner = null)
{
    private readonly HeadlessEpisodeBatchRunner _runner = runner ?? new HeadlessEpisodeBatchRunner();

    public async Task<HeadlessBatchParallelDeterminismResult> VerifyAsync(
        HeadlessEpisodeBatchRequest request,
        int parallelism,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        int normalizedParallelism = Math.Max(2, parallelism);
        HeadlessEpisodeBatchResult sequential = await _runner
            .RunAsync(request with { MaxDegreeOfParallelism = 1 }, cancellationToken)
            .ConfigureAwait(false);
        HeadlessEpisodeBatchResult parallel = await _runner
            .RunAsync(request with { MaxDegreeOfParallelism = normalizedParallelism }, cancellationToken)
            .ConfigureAwait(false);

        List<HeadlessBatchParallelMismatch> mismatches = new();
        CompareBatchSummary(sequential, parallel, mismatches);
        CompareEpisodes(sequential, parallel, mismatches);

        return new HeadlessBatchParallelDeterminismResult(
            request.Name,
            normalizedParallelism,
            sequential,
            parallel,
            mismatches.ToArray());
    }

    private static void CompareBatchSummary(
        HeadlessEpisodeBatchResult sequential,
        HeadlessEpisodeBatchResult parallel,
        List<HeadlessBatchParallelMismatch> mismatches)
    {
        AddMismatchIfDifferent(
            mismatches,
            0,
            "EpisodeCount",
            sequential.EpisodeCount.ToString(),
            parallel.EpisodeCount.ToString());
        AddMismatchIfDifferent(
            mismatches,
            0,
            "TotalStepCount",
            sequential.TotalStepCount.ToString(),
            parallel.TotalStepCount.ToString());
        AddMismatchIfDifferent(
            mismatches,
            0,
            "TotalSampleCount",
            sequential.TotalSampleCount.ToString(),
            parallel.TotalSampleCount.ToString());
        AddMismatchIfDifferent(
            mismatches,
            0,
            "TerminalEpisodeCount",
            sequential.TerminalEpisodeCount.ToString(),
            parallel.TerminalEpisodeCount.ToString());
        AddMismatchIfDifferent(
            mismatches,
            0,
            "HasConsistentSchema",
            sequential.HasConsistentSchema.ToString(),
            parallel.HasConsistentSchema.ToString());
        AddMismatchIfDifferent(
            mismatches,
            0,
            "TotalReward",
            sequential.TotalReward.ToString("R"),
            parallel.TotalReward.ToString("R"));

        foreach (HeadlessEpisodeStopReason stopReason in Enum.GetValues<HeadlessEpisodeStopReason>())
        {
            AddMismatchIfDifferent(
                mismatches,
                0,
                $"StopReason.{stopReason}",
                sequential.CountByStopReason(stopReason).ToString(),
                parallel.CountByStopReason(stopReason).ToString());
        }
    }

    private static void CompareEpisodes(
        HeadlessEpisodeBatchResult sequential,
        HeadlessEpisodeBatchResult parallel,
        List<HeadlessBatchParallelMismatch> mismatches)
    {
        int maxCount = Math.Max(sequential.Episodes.Count, parallel.Episodes.Count);
        for (int index = 0; index < maxCount; index++)
        {
            HeadlessEpisodeBatchItemResult? sequentialItem =
                index < sequential.Episodes.Count ? sequential.Episodes[index] : null;
            HeadlessEpisodeBatchItemResult? parallelItem =
                index < parallel.Episodes.Count ? parallel.Episodes[index] : null;

            if (sequentialItem is null || parallelItem is null)
            {
                mismatches.Add(new HeadlessBatchParallelMismatch(
                    index + 1,
                    "Episode",
                    sequentialItem is null ? "<missing>" : "present",
                    parallelItem is null ? "<missing>" : "present"));
                continue;
            }

            CompareEpisode(sequentialItem, parallelItem, mismatches);
        }
    }

    private static void CompareEpisode(
        HeadlessEpisodeBatchItemResult sequential,
        HeadlessEpisodeBatchItemResult parallel,
        List<HeadlessBatchParallelMismatch> mismatches)
    {
        int runIndex = sequential.RunIndex;
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "RunIndex",
            sequential.RunIndex.ToString(),
            parallel.RunIndex.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "RandomSeed",
            sequential.RandomSeed.ToString(),
            parallel.RandomSeed.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "Fingerprint",
            sequential.Fingerprint.Value,
            parallel.Fingerprint.Value);
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "StepCount",
            sequential.Episode.StepCount.ToString(),
            parallel.Episode.StepCount.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "SampleCount",
            sequential.Episode.ToSampleBatch().Count.ToString(),
            parallel.Episode.ToSampleBatch().Count.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "IsTerminal",
            sequential.Episode.IsTerminal.ToString(),
            parallel.Episode.IsTerminal.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "StopReason",
            sequential.Episode.StopReason.ToString(),
            parallel.Episode.StopReason.ToString());
        AddMismatchIfDifferent(
            mismatches,
            runIndex,
            "TotalReward",
            sequential.Episode.TotalReward.ToString("R"),
            parallel.Episode.TotalReward.ToString("R"));
    }

    private static void AddMismatchIfDifferent(
        List<HeadlessBatchParallelMismatch> mismatches,
        int runIndex,
        string field,
        string sequentialValue,
        string parallelValue)
    {
        if (string.Equals(sequentialValue, parallelValue, StringComparison.Ordinal))
        {
            return;
        }

        mismatches.Add(new HeadlessBatchParallelMismatch(
            runIndex,
            field,
            sequentialValue,
            parallelValue));
    }
}

public sealed record HeadlessBatchParallelDeterminismResult(
    string Name,
    int Parallelism,
    HeadlessEpisodeBatchResult Sequential,
    HeadlessEpisodeBatchResult Parallel,
    IReadOnlyList<HeadlessBatchParallelMismatch> Mismatches)
{
    public bool Passed => Mismatches.Count == 0;
}

public sealed record HeadlessBatchParallelMismatch(
    int RunIndex,
    string Field,
    string SequentialValue,
    string ParallelValue);
