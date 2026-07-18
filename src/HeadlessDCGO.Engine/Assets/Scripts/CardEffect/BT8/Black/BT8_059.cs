// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT8_059 "Kokuwamon" (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT8/Black/BT8_059.cs (42 lines, no region markers)
//    * All Turns (timing == EffectTiming.None) :14-38 — CannotIgnoreDigivolutionConditionClass, 상시 등재.
//
// ② 프리미티브 매핑:
//    * P:CannotIgnoreDigivolutionConditionClass — 플레이어 모두 진화조건 무시 불가(무조건 true 반환)
//      (AS-IS :16-37).
//
// ③ 배선 관례 근거: 없음(단일 timing==None, 트리거 배선 불필요).
//
// 치환(substrate translations only):
//    * `Hashtable`/`Player`/`Permanent`/`CardSource` — mirror 동일 명칭 그대로(BT9_111 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Black;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_059 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            CannotIgnoreDigivolutionConditionClass cannotIgnoreDigivolutionConditionClass = new CannotIgnoreDigivolutionConditionClass();
            cannotIgnoreDigivolutionConditionClass.SetUpICardEffect("Players can't ignore digivolution requirements", CanUseCondition, card);
            cannotIgnoreDigivolutionConditionClass.SetUpCannotIgnoreDigivolutionConditionClass(IgnoreDigivolutionCondition: IgnoreDigivolutionCondition);

            cardEffects.Add(cannotIgnoreDigivolutionConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            bool IgnoreDigivolutionCondition(Player player, Permanent targetPermanent, CardSource cardSource)
            {
                return true;
            }
        }

        return cardEffects;
    }
}
