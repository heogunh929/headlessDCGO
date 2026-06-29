// 1:1 mirror of the original ST3_11 (ST3/Yellow).
//   [When Attacking] 1 of your opponent's Digimon gets -4000 DP for the turn.
//   -> SelectAndBuffDpEffect (OnAllyAttack, select 1 opponent Digimon, -4000, UntilEachTurnEnd) — same as
//   ST3_08 with a larger debuff.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_11 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -4000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[When Attacking] 1 of your opponent's Digimon gets -4000 DP for the turn."));
        }

        return cardEffects;
    }
}
