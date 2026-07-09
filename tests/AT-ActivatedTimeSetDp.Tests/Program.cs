using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (ActivatedTime) AS-IS applies the DP NotIsUpDown/"set" group in OrderBy(ActivatedTime) (Permanent.cs:301/472):
// among several "DP becomes X" effects the LATEST-activated one is applied last and therefore wins. The headless
// NumericModifier previously had no activation-order concept and tie-broke the set group by Id, so which set won
// depended on the arbitrary effect-id string, not activation time. This verifies the new ActivationOrder governs
// the set-DP fold order, with Id remaining the deterministic tie-break for equal orders (AS-IS stable sort over
// equal MinValue timestamps). The static-DP path (DpModifier.ActivatedOrder / DpCalculator) already mirrors this;
// this covers the continuous ModifierHelpers path.

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

NumericModifier SetDp(string id, int value, long activationOrder) =>
    NumericModifier.Set(id, NumericModifierMetric.Dp, value) with { ActivationOrder = activationOrder };

int ResolveDp(params NumericModifier[] modifiers) =>
    ModifierHelpers.ResolveDp(baseDp: 3000, modifiers).FinalValue;

// --- 1. Latest-activated set wins even when its Id sorts FIRST (proves activation order beats Id). ---
// "a-late" sorts first alphabetically but has the higher ActivationOrder, so it applies last and wins.
Check(
    ResolveDp(SetDp("z-early", 5000, activationOrder: 1), SetDp("a-late", 8000, activationOrder: 2)) == 8000,
    "later-activated set wins over an alphabetically-earlier-id set (8000)");

// --- 2. Symmetric: swap which id carries the later order — the later one still wins. ---
Check(
    ResolveDp(SetDp("z-late", 8000, activationOrder: 2), SetDp("a-early", 5000, activationOrder: 1)) == 8000,
    "later-activated set wins regardless of id ordering (8000)");

// --- 3. Equal activation order (both default 0) falls back to Id tie-break: largest id applied last. ---
Check(
    ResolveDp(SetDp("a", 5000, activationOrder: 0), SetDp("b", 7000, activationOrder: 0)) == 7000,
    "equal order -> Id tie-break, larger id applied last wins (7000)");

// --- 4. A relative (isUpDown) delta is applied BEFORE the set group and is unaffected by activation order. ---
NumericModifier delta = NumericModifier.Add("delta", NumericModifierMetric.Dp, 2000);
Check(
    ResolveDp(delta, SetDp("set", 6000, activationOrder: 5)) == 6000,
    "a set overwrites a prior relative delta (set wins = 6000)");

// --- 5. No set modifiers: relative deltas sum as before (regression-neutral for the common path). ---
Check(
    ResolveDp(
        NumericModifier.Add("d1", NumericModifierMetric.Dp, 1000),
        NumericModifier.Add("d2", NumericModifierMetric.Dp, 2000)) == 3000 + 1000 + 2000,
    "relative deltas still sum with no set present (6000)");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall ActivatedTime set-DP checks passed.");
