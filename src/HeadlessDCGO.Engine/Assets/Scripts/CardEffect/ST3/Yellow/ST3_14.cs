// 1:1 mirror of the original ST3_14 (ST3/Yellow) — an Option.
//   [Main]     1 of your opponent's Digimon gets -2000 DP for the turn.  -> SelectAndBuffDpEffect (opponent, -2000)
//   [Security] Add this card to its owner's hand.                        -> AddThisCardToHandEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_14 : CEntity_Effect
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

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -2000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your opponent's Digimon gets -2000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
