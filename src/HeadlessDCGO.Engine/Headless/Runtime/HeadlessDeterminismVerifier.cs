namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Move this into executable tests once deterministic parity gates are available.
public sealed class HeadlessDeterminismVerifier(
    Func<HeadlessScenarioRunner>? runnerFactory = null)
{
    private readonly Func<HeadlessScenarioRunner> _runnerFactory =
        runnerFactory ?? (() => new HeadlessScenarioRunner());

    public async Task<HeadlessDeterminismVerificationResult> VerifyAsync(
        HeadlessScenario scenario,
        int repeatCount = 2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        int normalizedRepeatCount = Math.Max(2, repeatCount);
        List<HeadlessDeterminismRun> runs = new();

        for (int index = 0; index < normalizedRepeatCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HeadlessEpisodeResult episode = await _runnerFactory()
                .RunAsync(scenario, cancellationToken)
                .ConfigureAwait(false);
            HeadlessEpisodeFingerprint fingerprint = episode.ToFingerprint();

            runs.Add(new HeadlessDeterminismRun(
                RunIndex: index + 1,
                Fingerprint: fingerprint.Value,
                StepCount: episode.StepCount,
                TotalReward: episode.TotalReward,
                IsTerminal: episode.IsTerminal,
                StopReason: episode.StopReason));
        }

        string expectedFingerprint = runs[0].Fingerprint;
        HeadlessDeterminismMismatch[] mismatches = runs
            .Where(run => !string.Equals(run.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            .Select(run => new HeadlessDeterminismMismatch(
                run.RunIndex,
                expectedFingerprint,
                run.Fingerprint))
            .ToArray();

        return new HeadlessDeterminismVerificationResult(
            scenario.Name,
            runs,
            mismatches);
    }
}

public sealed record HeadlessDeterminismRun(
    int RunIndex,
    string Fingerprint,
    int StepCount,
    double TotalReward,
    bool IsTerminal,
    HeadlessEpisodeStopReason StopReason);

public sealed record HeadlessDeterminismMismatch(
    int RunIndex,
    string ExpectedFingerprint,
    string ActualFingerprint);

public sealed record HeadlessDeterminismVerificationResult(
    string ScenarioName,
    IReadOnlyList<HeadlessDeterminismRun> Runs,
    IReadOnlyList<HeadlessDeterminismMismatch> Mismatches)
{
    public bool Passed => Mismatches.Count == 0;
}
