// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT8_071 "Psychemon" (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT8/Purple/BT8_071.cs (61 lines, no region markers)
//    * All Turns (timing == EffectTiming.None) :15-57 — CannotReduceCostClass, 상시 등재.
//
// ② 프리미티브 매핑:
//    * P:CannotReduceCostClass — 플레이어 모두 코스트 감소 불가(대상-퍼머넌트 조건은 항상 "없음"만 통과,
//      카드 조건은 HasPlayCost) (AS-IS :16-56).
//
// ③ 배선 관례 근거: 없음(단일 timing==None, 트리거 배선 불필요).
//
// 치환(substrate translations only):
//    * `Hashtable`/`Player`/`Permanent`/`CardSource` — mirror 동일 명칭 그대로(BT9_111 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Purple;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_071 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            CannotReduceCostClass cannotReduceCostClass = new CannotReduceCostClass();
            cannotReduceCostClass.SetUpICardEffect("Players can't reduce play costs", CanUseCondition, card);
            cannotReduceCostClass.SetUpCannotReduceCostClass(
                playerCondition: PlayerCondition,
                targetPermanentsCondition: TargetPermanentsCondition,
                cardCondition: CardCondition);
            cardEffects.Add(cannotReduceCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool PlayerCondition(Player player)
            {
                return true;
            }

            bool TargetPermanentsCondition(List<Permanent> targetPermanents)
            {
                if (targetPermanents == null)
                {
                    return true;
                }

                else
                {
                    if (targetPermanents.Count((permanent) => permanent != null) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource.HasPlayCost;
            }
        }

        return cardEffects;
    }
}
