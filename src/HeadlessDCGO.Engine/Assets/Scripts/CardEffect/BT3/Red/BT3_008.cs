namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_008 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectRagnaLoardmon(HeadlessEntityId id)
            {
                CardSource candidate = new(card.Context, id, card.Owner, card.Owner);
                return candidate.ContainsCardName("RagnaLoardmon");
            }

            bool CanSelectLegendArmsDigimon(HeadlessEntityId id)
            {
                CardSource candidate = new(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasTrait("Legend-Arms");
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 5,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectRagnaLoardmon,
                        message: "Select 1 [RagnaLoardmon].",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),

                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectLegendArmsDigimon,
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
