// 1:1 mirror of the original BT3_008 (BT3/Red).
//   [On Play] Reveal the top 5 cards of your deck. Add 1 [RagnaLoardmon] and 1 Digimon card with
//   [Legend-Arms] in its traits among them to your hand. Place the remaining cards at the bottom of your
//   deck in any order.
//   -> SimplifiedRevealDeckTopCardsAndSelect (OnEnterFieldAnyone, revealCount 5, two sequential passes over
//   the shared revealed pool — a card already picked by pass 1 is excluded from pass 2, which is exactly the
//   AS-IS mutualConditions:true relaxation — remaining to DeckBottom).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_008 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id) =>
                new CardSource(card.Context, id, card.Owner, card.Owner).CardNames.Contains("RagnaLoardmon");

            bool CanSelectCardCondition1(HeadlessEntityId id)
            {
                CardSource revealed = new CardSource(card.Context, id, card.Owner, card.Owner);
                return revealed.IsDigimon && revealed.CardTraits.Contains("Legend-Arms");
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 5,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition,
                        message: "Select 1 [RagnaLoardmon].",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition1,
                        message: "Select 1 Digimon card with [Legend-Arms] in its traits.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "[On Play] Reveal the top 5 cards of your deck. Add 1 [RagnaLoardmon] and 1 Digimon card with [Legend-Arms] in its traits among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}
