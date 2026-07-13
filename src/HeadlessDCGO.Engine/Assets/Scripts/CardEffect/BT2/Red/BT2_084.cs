// Source: Assets/Scripts/CardEffect/BT2/Red/BT2_084.cs
// [Your Turn][When Attacking player] Suspend this Tamer → Red Digimon +2000 DP for the turn.
// [Security] Play this Tamer.
//
// STOP: OnAllyAttack — suspend-self cost body와 attackingPermanent 대상 ChangeDigimonDP(+2000)
//       조합을 커버하는 헤드리스 body 프리미티브 없음
//       (SuspendPermanentsClass on card.PermanentOfThisCard() + GManager.attackProcess.AttackingPermanent
//       접근 경로 모두 헤드리스에 없음; 복합 body 프리미티브도 카탈로그에 없음)
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_084 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: OnAllyAttack — self-suspend cost body + ChangeDigimonDP(attackingPermanent, +2000,
        //       UntilEachTurnEnd) 조합 프리미티브 없음

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}