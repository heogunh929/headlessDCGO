namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_099 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: AS-IS None 타이밍의 플레이 비용 감소(자신 배틀 영역 황색 테이머 수만큼 동적 감소)는
        // BeforePayCostReductionEffect 가 사용 가능한 심볼에 없고,
        // 동적 amount(Func<int>) 시그니처가 제공되지 않아 충실한 등록 불가.

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
                changeValue: -12000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your opponent's Digimon gets -12000 DP for the turn."));
        }

        return cardEffects;
    }
}