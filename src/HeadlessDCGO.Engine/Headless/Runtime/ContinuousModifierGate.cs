namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (B-2) Sibling of <c>ContinuousDpGate</c> for the other numeric metrics that continuous
/// effects modify: Security Attack and play / digivolution cost. Each folds in card-targeted AND
/// player-scope continuous modifiers (via <see cref="ContinuousScopeEvaluation"/>) and resolves them
/// over a base value, mirroring the original per-access rescans. Because the modifiers are sourced from
/// continuous registry bindings, an <see cref="HeadlessDCGO.Engine.Headless.Effects.EffectDuration"/>
/// tag (F-1) makes a "+1 Security Attack until end of turn" effect expire automatically — no special
/// handling needed here. With no registered continuous effects each method returns the base unchanged.
/// </summary>
public static class ContinuousModifierGate
{
    /// <summary>Query scope used for continuous re-evaluation (shared with the other gates).</summary>
    public const string Scope = ContinuousRestrictionGate.Scope;

    // (R2-C) ResolvePlayCost / ResolveDigivolutionCost are now THIN DELEGATES to the single AS-IS pay-cost
    // orchestrator CardSource.GetPayingCostWithBaseCost — the play/digivolution-cost logic (DigiXros/Assembly,
    // the GetChangedCostItselef/GetChangedPayingCost IChangeCostEffect fold, the legacy substrate NumericModifier
    // union, the 0 floor) lives there 1:1. These wrappers are retained (not deleted) because direct test callers
    // still use them; the mirror's live play/digivolve/option chokes call GetPayingCostWithBaseCost directly with
    // the real source root. The `Root.None` here matches the previous FoldPlayCost hard-coding for those direct
    // callers (root-dependent cost effects are exercised only via the live chokes, which thread the real root).
    public static int ResolvePlayCost(
        EngineContext context, HeadlessEntityId cardId, int basePlayCost, bool canReduceCost = true,
        IReadOnlyList<HeadlessEntityId>? targetPermanentIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return basePlayCost;
        }

        _ = canReduceCost; // (R2-C) the invented external knob is subsumed by the live Player.CanReduceCost veto.
        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        var cardSource = new CardSource(context, cardId, owner);
        List<Permanent>? targetPermanents = targetPermanentIds is null
            ? null
            : targetPermanentIds.Select(id => new Permanent(context, id, owner)).ToList();
        return cardSource.GetPayingCostWithBaseCost(basePlayCost, Assets.Scripts.Script.SelectCardEffect.Root.None, targetPermanents);
    }

    public static int ResolveDigivolutionCost(
        EngineContext context, HeadlessEntityId cardId, int baseDigivolutionCost,
        HeadlessEntityId digivolveTargetPermanentId = default, bool canReduceCost = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseDigivolutionCost;
        }

        _ = canReduceCost;
        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(cardId, out var inst) && inst is not null
            ? inst.OwnerId
            : default;
        var cardSource = new CardSource(context, cardId, owner);
        // A non-null target permanent forces the digivolution branch (digivolution-metric legacy fold + the
        // moving card's own dispatch-first DigivolutionCostGateEffect); its InstanceId (possibly empty) is the
        // AS-IS "digivolving FROM" permanent id threaded to the own-gated collection.
        var targetPermanents = new List<Permanent> { new Permanent(context, digivolveTargetPermanentId, owner) };
        return cardSource.GetPayingCostWithBaseCost(baseDigivolutionCost, Assets.Scripts.Script.SelectCardEffect.Root.None, targetPermanents);
    }

    // ===== (R2-C) LEGACY substrate cost fold — the mirror mid-migration NumericModifier bindings only =========
    // The AS-IS cost pipeline (CardSource.GetPayingCostWithBaseCost) has NO registry. These helpers are the
    // substrate half the mirror still needs while some continuous cost effects (BeforePayCostReduction /
    // ChangePlayCostPlayer producers, the dispatch-first DigivolutionCostGateEffect) are expressed as legacy
    // NumericModifier bindings rather than new-model IChangeCostEffect kind-classes. CardSource.
    // GetPayingCostWithBaseCost UNIONs the result of these over the base cost, THEN runs the AS-IS-1:1 new-model
    // scan (GetChangedCostItselef / GetChangedPayingCost). `playerCanReduce` is the live CannotReduceCost veto
    // (Player.CanReduceCost) the caller already computed; it is ANDed with the (R2-C ③ transitional) registry
    // CostReductionImmune so a mid-migration registry immunity keeps applying until ③ retires it.

    /// <summary>(R2-C) Fold the mirror's legacy continuous PLAY-cost NumericModifier bindings over
    /// <paramref name="cost"/>. Used by <see cref="CardSource.GetPayingCostWithBaseCost"/> as its substrate union
    /// step.</summary>
    public static int FoldLegacyPlayCostModifiers(
        EngineContext context, HeadlessEntityId cardId, int cost, bool playerCanReduce)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return cost;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        return ModifierHelpers.ResolvePlayCost(cost, result.Modifiers, canReduceCost: playerCanReduce).FinalValue;
    }

    /// <summary>(R2-C) Fold the mirror's legacy continuous DIGIVOLUTION-cost NumericModifier bindings (registry +
    /// the moving card's own dispatch-first <see cref="DigivolutionCostGateEffect"/>) over
    /// <paramref name="cost"/>.</summary>
    public static int FoldLegacyDigivolutionCostModifiers(
        EngineContext context, HeadlessEntityId cardId, int cost,
        HeadlessEntityId digivolveTargetPermanentId, bool playerCanReduce)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return cost;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId, digivolveTargetPermanentId);
        IReadOnlyList<NumericModifier> ownGated =
            DigivolutionCostGateEffect.CollectOwnGatedModifiers(context, cardId, digivolveTargetPermanentId);
        IReadOnlyList<NumericModifier> modifiers = ownGated.Count == 0
            ? result.Modifiers
            : result.Modifiers.Concat(ownGated).ToArray();
        return ModifierHelpers.ResolveDigivolutionCost(cost, modifiers, canReduceCost: playerCanReduce).FinalValue;
    }

    // (R2-C ③) The registry-key cost-immunity consumer CostReductionImmune (D-8/#5, ImmuneFrom*CostReductionKey)
    // was RETIRED: the sole immunity representation is now the live AS-IS Player.CanReduceCost scan
    // (ICannotReduceCostEffect / CannotReduceCostClass), which the cost pipeline already re-derives as
    // `playerCanReduce`. The ReplacementHelpers.ImmuneFrom*CostReductionKey constants + the factory that produced
    // them (CardEffectFactory.CanNotReduceCostStaticEffect) are now dead / flipped to the kind-class.
}
