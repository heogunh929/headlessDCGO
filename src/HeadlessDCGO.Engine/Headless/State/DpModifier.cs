namespace HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// How a <see cref="DpModifier"/> changes a permanent's DP, mirroring the original
/// <c>IChangeBaseDPEffect</c> split (G3.5-RL-B1):
/// <list type="bullet">
/// <item><see cref="Relative"/> — an up/down delta (e.g. "+3000 DP"); the original's IsUpDown effects.</item>
/// <item><see cref="Absolute"/> — a "set DP to X" effect; the original's NotIsUpDown effects.</item>
/// </list>
/// </summary>
public enum DpModifierKind
{
    Relative,
    Absolute
}

/// <summary>
/// A single typed DP modification with provenance and an activation order used to resolve the
/// application sequence of absolute "set" modifiers (the original ordered them by ActivatedTime).
/// </summary>
public sealed record DpModifier(
    int Value,
    DpModifierKind Kind,
    long ActivatedOrder = 0,
    string Source = "")
{
    public bool IsRelative => Kind == DpModifierKind.Relative;

    public bool IsAbsolute => Kind == DpModifierKind.Absolute;

    public static DpModifier Relative(int delta, long activatedOrder = 0, string source = "")
    {
        return new DpModifier(delta, DpModifierKind.Relative, activatedOrder, source);
    }

    public static DpModifier Absolute(int value, long activatedOrder = 0, string source = "")
    {
        return new DpModifier(value, DpModifierKind.Absolute, activatedOrder, source);
    }
}
