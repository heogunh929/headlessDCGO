namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace with full DCGO memory gauge semantics once cost/payment rules are ported.
public sealed record HeadlessMemoryState(
    int Current,
    int Minimum,
    int Maximum)
{
    public static HeadlessMemoryState Default { get; } = new(
        Current: 0,
        Minimum: -10,
        Maximum: 10);
}
