namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (B-2) Sibling of <see cref="ContinuousDpGate"/> for the other numeric metrics that continuous
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
    public const string Scope = ContinuousDpGate.Scope;

    public static int ResolveSecurityAttack(EngineContext context, HeadlessEntityId cardId, int baseSecurityAttack)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseSecurityAttack;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        int legacyResolved = ModifierHelpers.ResolveSecurityAttack(baseSecurityAttack, result.Modifiers, cardId).FinalValue;

        // (P6 STAGE B) UNION the new-model IChangeSAttackEffect scan (AS-IS Permanent.Strike_AllowMinus) over
        // the legacy binding result. Legacy SAttack effects flow through result.Modifiers (bindings); ported
        // kind-classes (ChangeSAttackClass) implement IChangeSAttackEffect and register no binding, so the
        // interface scan is their only path. The two representations are interface-disjoint (legacy
        // ContinuousSelfModifierEffect is `: ICardEffect` only) — no double count.
        return Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldSAttack(context, cardId, legacyResolved);
    }

    public static int ResolvePlayCost(
        EngineContext context, HeadlessEntityId cardId, int basePlayCost, bool canReduceCost = true,
        IReadOnlyList<HeadlessEntityId>? targetPermanentIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return basePlayCost;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        // D-8: a continuous "cost cannot be reduced" replacement (AS-IS ICannotReduceCostEffect) forces
        // reductions off, mirroring ContinuousDpGate's DP-reduction immunity. (#5) This is the PLAY-cost path,
        // so a DIGIVOLUTION-only immunity must NOT apply here.
        bool effectiveCanReduce = canReduceCost && !CostReductionImmune(context, cardId, result, isDigivolution: false);
        int legacyResolved = ModifierHelpers.ResolvePlayCost(basePlayCost, result.Modifiers, canReduceCost: effectiveCanReduce).FinalValue;

        // (P6 STAGE B) UNION the new-model IChangeCostEffect fold (AS-IS CardSource.GetChangedCostItselef +
        // GetChangedPayingCost) over the legacy binding result — same disjoint-interface union as SAttack/DP.
        // `targetPermanentIds` threads real battle-area permanents through for the target-permanent-conditioned
        // shape (ChangePlayCostStaticEffect's PermanentsCondition) — absent at the real call sites today (no
        // caller builds an evolution-target list for a plain play), so it defaults to none.
        return Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldPlayCost(
            context, cardId, legacyResolved, canReduceCost: effectiveCanReduce, targetPermanentIds: targetPermanentIds);
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

        // (BT1_109) pass the digivolving-FROM target permanent so a two-sided player-scope cost predicate
        // ("...digivolve one of your green Digimon from level 5 to level 6...") can gate on it. Default id
        // leaves single-sided effects unchanged.
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId, digivolveTargetPermanentId);
        // (#5) DIGIVOLUTION-cost path — a PLAY-only immunity must NOT apply here.
        bool effectiveCanReduce = canReduceCost && !CostReductionImmune(context, cardId, result, isDigivolution: true);
        // (G5 / BT3_031 / BT3_111) additionally fold the MOVING card's OWN gated cost statics, read dispatch-first
        // (they live on a card in hand, which the continuous registrar does not scan). Folded together with the
        // registry modifiers so the "cannot be reduced" replacement + cost floor apply once, uniformly.
        IReadOnlyList<NumericModifier> ownGated =
            DigivolutionCostGateEffect.CollectOwnGatedModifiers(context, cardId, digivolveTargetPermanentId);
        IReadOnlyList<NumericModifier> modifiers = ownGated.Count == 0
            ? result.Modifiers
            : result.Modifiers.Concat(ownGated).ToArray();
        int legacyResolved = ModifierHelpers.ResolveDigivolutionCost(baseDigivolutionCost, modifiers, canReduceCost: effectiveCanReduce).FinalValue;

        // (P6 STAGE B) UNION the new-model IChangeCostEffect fold — AS-IS's digivolution-cost path folds the
        // SAME CardSource.GetChangedCostItselef/GetChangedPayingCost IChangeCostEffect scan as the play-cost
        // path (CardSource.cs:741/743, called uniformly for both play and evolve; `isEvolution` only branches
        // the DigiXros/Assembly special-cost regions, not this scan). ChangeDigivolutionCostStaticEffect always
        // builds isChangePayingCost:true (ChangeDigivolutionCost.cs), so it folds in FoldPlayCost's second
        // (paying) group. `digivolveTargetPermanentId` is the AS-IS "digivolving FROM" target permanent
        // (PermanentsCondition needs a real matching battle/breeding-area permanent — CardEffectCommons.
        // IsPermanentExistsOnField, a superset of IsPermanentExistsOnBattleArea covering breeding too; passing
        // it here as the sole target is correct for the common single-target case).
        return Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldPlayCost(
            context, cardId, legacyResolved, canReduceCost: effectiveCanReduce,
            targetPermanentIds: digivolveTargetPermanentId.IsEmpty ? null : new[] { digivolveTargetPermanentId });
    }

    /// <summary>(D-8 / #5) Whether a continuous "cost cannot be reduced" restriction targets the card for the
    /// cost being resolved. A <c>Both</c>-scope immunity (parsed replacement) applies to either cost; a
    /// <c>Play</c>- or <c>Digivolve</c>-scoped immunity applies only to its matching path (read as a raw scoped
    /// binding, like the DP-minus source predicate), so a "can't reduce DIGIVOLUTION cost" effect does not block
    /// the PLAY cost and vice versa.</summary>
    private static bool CostReductionImmune(EngineContext context, HeadlessEntityId cardId, ContinuousEvaluationResult result, bool isDigivolution)
    {
        if (ReplacementHelpers.ImmuneFromCostReduction(cardId, result.Replacements).IsReplaced)
        {
            return true;   // Both-scope (protects either cost).
        }

        string kindKey = isDigivolution
            ? ReplacementHelpers.ImmuneFromDigivolutionCostReductionKey
            : ReplacementHelpers.ImmuneFromPlayCostReductionKey;
        foreach (var effect in ContinuousScopeEvaluation.ApplicableEffects(context, Scope, cardId))
        {
            if (effect.Context.Values.TryGetValue(kindKey, out object? raw) && raw is bool flag && flag)
            {
                return true;
            }
        }

        return false;
    }
}
