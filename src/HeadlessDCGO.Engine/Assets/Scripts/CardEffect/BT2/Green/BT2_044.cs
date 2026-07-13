// 1:1 mirror of the original BT2_044 (BT2/Green).
//   [When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 Digimon card and
//   1 green Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order.
//   -> SimplifiedRevealDeckTopCardsAndSelect (reveal 3, select Lv5 Digimon -> Hand + green Tamer -> Hand, rest -> DeckBottom)
// Structure follows BT1_074 exactly: WhenDigivolving timing, same factory, same fold of CanUseCondition/
// CanActivateCondition. Only the conditions and description differ.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var revealed = new CardSource(card.Context, id, card.Controller, card.Owner);
                return revealed.IsDigimon && revealed.Level == 5 && revealed.HasLevel;
            }

            bool CanSelectCardCondition1(HeadlessEntityId id)
            {
                var revealed = new CardSource(card.Context, id, card.Controller, card.Owner);
                return revealed.IsTamer && revealed.HasCardColor("Green");
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition,
                        message: "Select 1 level 5 Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition1,
                        message: "Select 1 green Tamer card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "[When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 Digimon card and 1 green Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}