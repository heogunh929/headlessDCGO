namespace HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Computes a permanent's effective DP from its base (top-card) DP and a set of typed
/// <see cref="DpModifier"/>s, faithfully mirroring the original <c>Permanent.BaseDP</c> accumulation
/// (G3.5-RL-B1):
/// <list type="number">
/// <item>start from the base (printed) DP;</item>
/// <item>apply all <see cref="DpModifierKind.Relative"/> up/down deltas (summed);</item>
/// <item>apply <see cref="DpModifierKind.Absolute"/> "set" modifiers in <see cref="DpModifier.ActivatedOrder"/>
/// order — each replaces the value, so the last activated set wins;</item>
/// <item>clamp the result at zero.</item>
/// </list>
/// </summary>
public static class DpCalculator
{
    public static int ComputeDp(int baseDp, IEnumerable<DpModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        DpModifier[] all = modifiers as DpModifier[] ?? modifiers.ToArray();

        int dp = baseDp;

        foreach (DpModifier modifier in all)
        {
            if (modifier.IsRelative)
            {
                dp += modifier.Value;
            }
        }

        foreach (DpModifier modifier in all
                     .Where(modifier => modifier.IsAbsolute)
                     .OrderBy(modifier => modifier.ActivatedOrder))
        {
            dp = modifier.Value;
        }

        return dp < 0 ? 0 : dp;
    }
}
