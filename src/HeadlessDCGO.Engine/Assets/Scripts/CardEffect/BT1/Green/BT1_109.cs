// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_109.cs
// 1:1 mirror of the original BT1_109 (BT1/Green) — an Option. No [Security] region in the original (single
// OptionSkill timing block only).
//   [Main] "For the turn, the next time you would digivolve one of your green Digimon from level 5 to level
//   6, decrease the digivolution cost by 4." -> STOP (see below).
// AS-IS (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
// ActivateCoroutine builds a ChangeCostClass ("Digivolution Cost -4") whose ChangeCost(cardSource, cost, root,
// targetPermanents) reduces cost by 4 ONLY when BOTH:
//   - CardSourceCondition(cardSource): the digivolving-TO card is level 6 && HasLevel (the new top card), AND
//   - PermanentsCondition(targetPermanents): at least one target permanent is the owner's own battle-area
//     Digimon, CardColors contains Green, Level == 5, HasLevel (the digivolving-FROM permanent).
// It is registered via CardEffectCommons.AddEffectToPlayer(UntilEachTurnEnd, card, getCardEffect) alongside a
// paired cleanup ActivateClass ("Remove Effect", background process, AfterPayCost) that fires on
// CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(permanentCondition, cardCondition) and removes BOTH
// UntilEachTurnEndEffects entries — giving the AS-IS "next time only, this turn only" one-shot semantics.
//
// Pieces confirmed to exist individually:
//   - CardEffectCommons.AddEffectToPlayer(effectDuration, card, cardEffect, timing) IS ported
//     (CardPortingFramework.cs:8498) as a fire-once player-scope delayed-effect registrar.
//   - CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve IS ported (CardPortingFramework.cs:6474) as a
//     bridge predicate, but is not yet wired as a live CanUse gate by any ported card (grepped
//     Assets/Scripts/CardEffect for the symbol — zero hits outside the framework itself).
//   - CardEffectFactory.ChangeDigivolutionCostStaticEffect (CardPortingFramework.cs ~4648) is the ONE ported
//     digivolution-cost-delta primitive, but it is explicitly SELF-scoped: it registers a
//     ContinuousSelfModifierEffect keyed to `card.InstanceId` (this Option card's own instance, which never
//     digivolves) — its own doc comment states "per-target permanent/root conditions are out of this delta
//     primitive's scope (per-card follow-up)". Not applicable: BT1_109 must reduce the cost of a DIFFERENT
//     Digimon's digivolution, not its own.
//   - The engine's digivolution-cost resolution pipeline itself only supports a single-sided predicate: the
//     one existing player-scope path capable of reaching digivolution cost (PlayerScopeContinuousHelpers ->
//     ContinuousScopeEvaluation.ApplicableEffects -> ContinuousModifierGate.ResolveDigivolutionCost) is queried
//     with `cardId = payload.CardId` (Headless/Runtime/DigivolveAction.cs:38/189/424, TryGetEvolutionCost),
//     which is the NEW card being digivolved TO (the level-6 side) — the player-scope predicate
//     (Func<CardSource,bool>, ContinuousScopeEvaluation.cs:107-116) only ever sees THAT card's CardSource. The
//     underlying permanent being digivolved FROM (payload.TargetCardId, the level-5 host) is never passed to
//     the cost-resolution predicate — confirmed by reading ContinuousModifierGate.ResolveDigivolutionCost and
//     ModifierHelpers.ResolveDigivolutionCost (Assets/Scripts/Script/CardEffectCommons/ModifierHelpers.cs),
//     neither of which accepts a target-permanent id. (Contrast: continuous DIGIVOLVE RESTRICTIONS ARE gated on
//     the underlying permanent — ContinuousRestrictionGate.EvaluateDigivolve(context, payload.TargetCardId),
//     DigivolveAction.cs:468 — but that is a different gate scope from the cost modifier, and restrictions
//     don't carry a numeric delta.) So there is no hook through which a registered cost-reduction effect can
//     evaluate the AS-IS PermanentsCondition (owner's own green, level-5, battle-area Digimon) at all.
// Registering only the CardSourceCondition half (any level-6 digivolve, dropping the green/level-5/owner
// permanent gate) would broaden the effect far beyond the printed text — not a faithful partial mirror
// (fidelity-over-coverage) — so the whole timing is left unregistered.
// Grepped for ChangeCostClass / DigivolutionCostHelpers / ContinuousModifierGate.ResolveDigivolutionCost
// and PlayerScopeContinuousHelpers usages twice — confirmed structural primitive gap (the cost-resolution
// query signature itself lacks a target-permanent parameter), not a naming miss. Engine-side primitive
// development (extending ResolveDigivolutionCost / the player-scope query to also carry the target permanent)
// is deferred to the strong-model primitive pass. — 강모델
// if (timing == EffectTiming.OptionSkill) { ... }

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_109 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
