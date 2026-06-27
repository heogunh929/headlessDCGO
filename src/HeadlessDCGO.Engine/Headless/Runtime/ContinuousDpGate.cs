namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (N-2 / D-A1·D-A2·D-A3) Bridges <see cref="ContinuousEffectEvaluator"/> DP output into battle DP
/// computation, mirroring the original <c>Permanent.GetDP</c> which rescans every field / public-security
/// / player continuous effect on each access. The static per-instance DP (printed DP combined with the
/// instance's own <c>dpModifiers</c>) is taken as the base, then continuous DP modifiers sourced from
/// OTHER cards are applied on top.
///
/// D-A3 — <c>ImmuneFromDPMinus</c>: this is modelled as a continuous DpReduction/Immune REPLACEMENT
/// (<see cref="ReplacementHelpers.ImmuneFromDpReduction"/>), not as a numeric modifier. When such a
/// replacement targets the card, DP-reducing continuous modifiers (negative <c>Dp</c> deltas) are
/// dropped before resolution, so reductions are prevented while positive buffs still apply.
///
/// Like <see cref="ContinuousRestrictionGate"/> and <see cref="BattleDeletionGate"/>, this queries the
/// continuous-role registry only, so it is a pure no-op (returns the base DP unchanged) until continuous
/// DP effects are registered by the Phase 4 card pool.
/// </summary>
public static class ContinuousDpGate
{
    /// <summary>Query scope used for continuous re-evaluation (shared with the other gates).</summary>
    public const string Scope = ContinuousRestrictionGate.Scope;

    /// <summary>
    /// Returns <paramref name="baseDp"/> adjusted by the continuous DP modifiers targeting
    /// <paramref name="cardId"/>, honouring DP-reduction immunity. With no registered continuous effects
    /// this returns the base unchanged.
    /// </summary>
    public static int ResolveDp(EngineContext context, HeadlessEntityId cardId, int baseDp)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseDp;
        }

        ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(
            context.EffectRegistry,
            new EffectQueryContext(Scope, targetEntityId: cardId));

        IReadOnlyList<NumericModifier> modifiers = result.Modifiers;

        // D-A3: when a continuous DpReduction/Immune replacement targets this card, prevent DP-reducing
        // modifiers (negative Dp deltas) from applying; positive buffs are unaffected.
        if (ReplacementHelpers.ImmuneFromDpReduction(cardId, result.Replacements).IsReplaced)
        {
            modifiers = modifiers
                .Where(modifier => !IsDpReduction(modifier))
                .ToArray();
        }

        return ModifierHelpers.ResolveDp(baseDp, modifiers, cardId).FinalValue;
    }

    private static bool IsDpReduction(NumericModifier modifier) =>
        modifier.Metric == NumericModifierMetric.Dp &&
        modifier.Mode == NumericModifierMode.Add &&
        modifier.Value < 0;
}
