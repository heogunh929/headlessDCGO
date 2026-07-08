// 1:1 mirror of the original BT3_042 (BT3/Yellow).
//   [When Attacking] If you have 3 or fewer security cards, 1 of your opponent's Digimon gets -6000 DP for
//              the turn.  -> SelectAndBuffDpEffect (opponent Digimon, -6000, UntilEachTurnEnd); the
//              security<=3 gate is folded into canTarget (zero candidates when the gate fails, mirroring
//              the AS-IS CanActivateCondition gate).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_042 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.SecurityCount(card) <= 3
                    && CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -6000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[When Attacking] If you have 3 or fewer security cards, 1 of your opponent's Digimon gets -6000 DP for the turn."));
        }

        return cardEffects;
    }
}
