// TEST FIXTURE (not a real card). Mirrors the BT3_031 / BT3_111 continuous digivolution-cost gate (G5):
// "while this card is in your hand, digivolving it (from hand) onto a permanent whose top card is named
// Paildramon or Dinobeemon costs 2 less". Exercises the AS-IS ChangeCostClass digivolution-cost fold in
// CardSource.GetPayingCostWithBaseCost / GetChangedCostItselef (the moving card's own [None] effect, scanned
// by the "effects of itself" branch — a hand card the continuous registrar never scans). Inert in actual play.
//
// (W3c-final) Retargeted off the latent-workaround DigivolutionCostGateEffect (+ the retired invented
// FoldLegacyDigivolutionCostModifiers dispatch-first fold) onto the AS-IS 1:1 canonical factory
// CardEffectFactory.ChangeDigivolutionCostStaticEffect (returns a real ChangeCostClass). The moving card's own
// [None] gate is now read directly by GetChangedCostItselef's "effects of itself" scan — no def-dispatch gap
// (the former "no cost-engine consumer yet / RD-P6C1-2" note is stale: R2-C landed the GetChangedCostItselef /
// GetChangedPayingCost IChangeCostEffect fold). ContinuousModifierGate.ResolveDigivolutionCost passes Root.None,
// so the root gate is satisfied by the fixture's `rootCondition: _ => true` (its hand/owner gate is the
// cardCondition + condition); the target-top gate is the permanentCondition.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDigivolveCostGate : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            bool Condition() => CardEffectCommons.IsExistOnHand(card);

            bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent is not null
                && CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(targetPermanent, card)
                && (targetPermanent.TopCard.EqualsCardName("Paildramon") || targetPermanent.TopCard.EqualsCardName("Dinobeemon"));

            bool CardSourceCondition(CardSource cardSource) =>
                cardSource.InstanceId == card.InstanceId && CardEffectCommons.IsExistOnHand(cardSource);

            bool RootCondition(SelectCardEffect.Root root) => true;

            ChangeCostClass gate = CardEffectFactory.ChangeDigivolutionCostStaticEffect<int>(
                changeValue: -2,
                permanentCondition: PermanentCondition,
                cardCondition: CardSourceCondition,
                rootCondition: RootCondition,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                setFixedCost: false);

            if (gate is not null)
            {
                effects.Add(gate);
            }
        }

        return effects;
    }
}
