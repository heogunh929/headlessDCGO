// TEST FIXTURE (not a real card). Mirrors the BT3_031 / BT3_111 continuous digivolution-cost gate (G5):
// "while this card is in your hand, digivolving it (from hand) onto a permanent whose top card is named
// Paildramon or Dinobeemon costs 2 less". Exercises the dispatch-first cost fold in
// ContinuousModifierGate.ResolveDigivolutionCost (a hand card the continuous registrar never scans). Inert
// in actual play beyond this.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;

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

            bool RootCondition(ChoiceZone root) => root == ChoiceZone.Hand;

            effects.Add(CardEffectFactory.ChangeDigivolutionCostStaticEffect(
                changeValue: -2,
                permanentCondition: PermanentCondition,
                cardCondition: CardSourceCondition,
                rootCondition: RootCondition,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                setFixedCost: false));
        }

        return effects;
    }
}
