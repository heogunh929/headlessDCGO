namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.State;

public interface IStateFingerprintService
{
    string BuildCanonicalSnapshot(MatchState state);

    string BuildCanonicalSnapshot(MatchStateSnapshot snapshot);

    string ComputeFingerprint(MatchState state);

    string ComputeFingerprint(MatchStateSnapshot snapshot);
}
