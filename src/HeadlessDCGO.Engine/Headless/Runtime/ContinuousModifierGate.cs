namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (B-2) Sibling of <c>ContinuousDpGate</c>. Security Attack continuous modifiers still fold through the
/// registry-sourced <see cref="ContinuousScopeEvaluation"/> path (an <see cref="EffectDuration"/> tag makes a
/// "+1 Security Attack until end of turn" effect expire automatically). The play / digivolution COST wrappers
/// (<see cref="ResolvePlayCost"/> / <see cref="ResolveDigivolutionCost"/>) are now thin delegates to the single
/// AS-IS pay-cost orchestrator <c>CardSource.GetPayingCostWithBaseCost</c> — no substrate cost fold remains here
/// (see the W3c-final retirement note below).
/// </summary>
public static class ContinuousModifierGate
{
    /// <summary>Query scope used for continuous re-evaluation (shared with the other gates).</summary>
    public const string Scope = ContinuousRestrictionGate.Scope;

    // (R2-C) ResolvePlayCost / ResolveDigivolutionCost are now THIN DELEGATES to the single AS-IS pay-cost
    // orchestrator CardSource.GetPayingCostWithBaseCost — the play/digivolution-cost logic (DigiXros/Assembly,
    // the GetChangedCostItselef/GetChangedPayingCost IChangeCostEffect fold, the 0 floor) lives there 1:1.
    // These wrappers are retained (not deleted) because direct test callers
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
        // A non-null target permanent forces the digivolution branch (isEvolution=true in
        // GetPayingCostWithBaseCost); its InstanceId (possibly empty) is the AS-IS "digivolving FROM" permanent id
        // threaded into the ChangeCostClass targetPermanents so a digivolution-gated gate can match on the top card.
        var targetPermanents = new List<Permanent> { new Permanent(context, digivolveTargetPermanentId, owner) };
        return cardSource.GetPayingCostWithBaseCost(baseDigivolutionCost, Assets.Scripts.Script.SelectCardEffect.Root.None, targetPermanents);
    }

    // ===== (W3c-final) LEGACY substrate cost fold — RETIRED =====================================================
    // The former FoldLegacyPlayCostModifiers / FoldLegacyDigivolutionCostModifiers (the mirror mid-migration UNION
    // of invented EffectRegistry NumericModifier cost bindings + the dispatch-first DigivolutionCostGateEffect over
    // the base cost) are DELETED. Producer census reached 0: no card registers a PlayCost/DigivolutionCost
    // continuous NumericModifier — BeforePayCost reductions, "your cards cost less" owner-scope reductions, and the
    // hand-card digivolution-cost gate (ChangeDigivolutionCostStaticEffect) are all expressed as the AS-IS
    // ChangeCostClass, folded 1:1 by CardSource.GetChangedCostItselef / GetChangedPayingCost. The AS-IS pipeline has
    // NO such union; CanReduceCost immunity is the single live scan (ICannotReduceCostEffect / CannotReduceCostClass)
    // that ChangeCostClass.GetCost's own IsUpDown veto consults. DigivolutionCostGateEffect (its only consumer) is
    // likewise deleted. The registry-key CostReductionImmune (D-8/#5) was already retired to the same kind-class.
}
