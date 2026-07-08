// 1:1 mirror of the original BT3_043 (BT3/Yellow).
//   [When Digivolving] Up to 5 of your opponent's Digimon gain <Security Attack -2> until the end of your
//              opponent's next turn.  -> SelectAndBuffSAttackEffect (opponent Digimon, maxCount 5, -2,
//              UntilOpponentTurnEnd); maxCount>1 makes the underlying primitive set canEndNotMax:true,
//              matching AS-IS's canEndNotMax:true (stop after selecting at least 1, up to 5).
//   [On Deletion]      1 of your opponent's Digimon gets -11000 DP for the turn.
//              -> SelectAndBuffDpEffect (opponent Digimon, -11000, UntilEachTurnEnd)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_043 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 5,
                changeValue: -2,
                duration: EffectDuration.UntilOpponentTurnEnd,
                description: "[When Digivolving] Up to 5 of your opponent's Digimon gain <Security Attack -2> (This Digimon checks 2 fewer security cards) until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -11000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[On Deletion] 1 of your opponent's Digimon gets -11000 DP for the turn."));
        }

        return cardEffects;
    }
}
