namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace these scheduler counters with full effect stack state once card effects are ported.
public sealed record HeadlessEffectState(
    int PendingCount,
    int TotalEnqueuedCount,
    int TotalResolvedCount,
    int LastResolvedCount)
{
    public bool HasPendingEffects => PendingCount > 0;

    public static HeadlessEffectState Empty { get; } = new(
        PendingCount: 0,
        TotalEnqueuedCount: 0,
        TotalResolvedCount: 0,
        LastResolvedCount: 0);
}
