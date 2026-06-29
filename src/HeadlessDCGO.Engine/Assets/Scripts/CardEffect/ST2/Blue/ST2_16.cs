// 1:1 mirror of the original ST2_16 (ST2/Blue) — an Option.
//   [Main] Return 1 of your opponent's Digimon to its owner's hand. (Trash all of the digivolution cards
//          of that Digimon.)  -> SelectAndBounceEffect (OptionSkill, ReturnToHand)
//   [Security] (use the Main effect)  -> AddActivateMainOptionSecurityEffect
// ReturnToHand returns the permanent's top card to hand; the engine's bounce handles the under-cards.
// Resolved imperatively until the interactive activation flow is wired.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_16 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBounceEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                description: "[Main] Return 1 of your opponent's Digimon to its owner's hand."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Return 1 opponent's Digimon to hand");
        }

        return cardEffects;
    }
}
