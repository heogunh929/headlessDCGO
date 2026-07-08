// STOP: OnAllyAttack — CanActivateCondition의 "자신 배틀 필드에 황색 테이머 ≥3장" 조건을
// id 기반 commons 술어(IsOwnerBattleAreaTamer + 황색 색상 쿼리 조합)로 표현할 수 없고,
// SelectAndBuffDpEffect에 activationCondition 슬롯이 없어 게이트 없이 등록하면 가드 완화(부정확)가 됨.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_035 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: AS-IS CanActivateCondition = IsExistOnBattleArea && HasMatchConditionPermanent(opponentDigimon) &&
        // card.Owner.GetBattleAreaPermanents().Count(p => p.TopCard.CardColors.Contains(Yellow) && p.IsTamer) >= 3.
        // 세 번째 조건(황색 테이머 수 쿼리)을 Func<HeadlessEntityId,bool> 술어로 표현할
        // commons 프리미티브(IsOwnerBattleAreaTamer + id→색상 쿼리)가 없고,
        // SelectAndBuffDpEffect 시그니처에 activationCondition 파라미터가 없음.
        if (timing == EffectTiming.OnAllyAttack)
        {
        }

        return cardEffects;
    }
}