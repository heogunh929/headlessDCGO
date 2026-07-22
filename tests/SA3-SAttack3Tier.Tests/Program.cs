using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

// (SAttack 3-tier) AS-IS Permanent.Strike_AllowMinus buckets every applicable IChangeSAttackEffect by
// isUpDown()->CalculateOrder into three tiers applied strictly in sequence — UpToConstant, then UpDownValue,
// then DownToConstant (Permanent.cs:1872-1930). The switch has NO case for UpValue/DownValue, so an effect
// reporting either is collected but never folded. The headless NumericModifier previously had only a boolean
// isUpDown (a DP/Cost 2-group axis) and could not represent this 3-tier ordering; this verifies the new
// CalculateOrder tier governs the SecurityAttack fold order and drop.

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// (RD-A6-02 re-aim) ModifierHelpers.ResolveSecurityAttack / ModifierHelperFactory (zero src consumers) are
// deleted; fold directly via the live Evaluate(NumericModifierRequest) surface and the record's own
// NumericModifier.Add/InvertSecurityAttack factories (both live-consumed inside ModifierHelpers.ReadSimpleModifiers).
NumericModifierResult Resolve(params NumericModifier[] modifiers)
{
    return ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.SecurityAttack, 1, modifiers, minimumValue: 0));
}

NumericModifier ChangeSecurityAttack(string id, int value, CalculateOrder calcOrder = CalculateOrder.UpDownValue) =>
    NumericModifier.Add(id, NumericModifierMetric.SecurityAttack, value, calcOrder: calcOrder);

NumericModifier InvertSecurityAttack(string id, int value) =>
    NumericModifier.InvertSecurityAttack(id, value);

// --- 1. Tier ordering is the PRIMARY fold key (beats the Id tie-break). ---
// Ids are chosen so alphabetical Id order (a-down, m-mid, z-up) is the REVERSE of the tier order
// (z-up = UpToConstant first, m-mid = UpDownValue, a-down = DownToConstant last). If tier is the primary
// key the applied order is [z-up, m-mid, a-down]; if Id were primary it would be [a-down, m-mid, z-up].
NumericModifierResult ordered = Resolve(
    ChangeSecurityAttack("a-down", 3, calcOrder: CalculateOrder.DownToConstant),
    ChangeSecurityAttack("z-up", 5, calcOrder: CalculateOrder.UpToConstant),
    ChangeSecurityAttack("m-mid", 2, calcOrder: CalculateOrder.UpDownValue));

Check(
    ordered.AppliedModifierIds.SequenceEqual(new[] { "z-up", "m-mid", "a-down" }),
    $"UpToConstant -> UpDownValue -> DownToConstant fold order (got [{string.Join(",", ordered.AppliedModifierIds)}])");
Check(ordered.FinalValue == 1 + 5 + 2 + 3, "additive tiers sum regardless of tier (1+5+2+3=11)");

// --- 2. UpValue / DownValue have no AS-IS switch case -> collected but never applied (dropped). ---
NumericModifierResult dropped = Resolve(
    ChangeSecurityAttack("keep", 4, calcOrder: CalculateOrder.UpDownValue),
    ChangeSecurityAttack("drop-up", 100, calcOrder: CalculateOrder.UpValue),
    ChangeSecurityAttack("drop-down", 100, calcOrder: CalculateOrder.DownValue));

Check(dropped.FinalValue == 1 + 4, "UpValue/DownValue SAttack modifiers do NOT fold into the total (1+4=5)");
Check(dropped.AppliedModifierIds.SequenceEqual(new[] { "keep" }), "only the UpDownValue modifier is applied");
Check(
    dropped.SkippedModifierIds.OrderBy(x => x).SequenceEqual(new[] { "drop-down", "drop-up" }),
    "UpValue/DownValue modifiers are reported as skipped");

// --- 3. Default tier (no calcOrder given) is UpDownValue -> plain deltas still fold (regression-neutral). ---
NumericModifierResult defaulted = Resolve(
    ChangeSecurityAttack("d1", 2),
    ChangeSecurityAttack("d2", 3));
Check(defaulted.FinalValue == 1 + 2 + 3, "default-tier deltas fold as UpDownValue (1+2+3=6)");

// --- 4. Invert is consumed globally (not positional): +2 delta flipped to -2 by an active inversion. ---
NumericModifierResult inverted = Resolve(
    ChangeSecurityAttack("up2", 2, calcOrder: CalculateOrder.UpDownValue),
    InvertSecurityAttack("inv", 1));
Check(inverted.FinalValue == Math.Max(0, 1 - 2), "an active inversion flips a +2 SAttack delta to -2 (clamped to 0)");

// --- 5. Structured-metadata path can carry the tier via the calcOrder key. ---
var structured = new NumericModifier(
    id: "structured-down",
    metric: NumericModifierMetric.SecurityAttack,
    value: 7,
    calcOrder: CalculateOrder.DownToConstant);
NumericModifierResult mixed = Resolve(
    structured,
    ChangeSecurityAttack("up-first", 1, calcOrder: CalculateOrder.UpToConstant));
Check(
    mixed.AppliedModifierIds.SequenceEqual(new[] { "up-first", "structured-down" }),
    "an explicit DownToConstant modifier folds after an UpToConstant one");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall SAttack 3-tier checks passed.");
