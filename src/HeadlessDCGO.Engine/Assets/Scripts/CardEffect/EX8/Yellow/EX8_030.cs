// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — EX8_030 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Yellow/EX8_030.cs (86 lines, 2 regions, no coroutine body)
//    * Alternate Digivolution Conditions :14-31 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * All Turns                         :37-79 (timing == None — CannotAddMemoryClass, opponent scoped,
//      non-Tamer-effect scoped)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — Lv.2 [NSo] 위 코스트 0
//      (AS-IS :16-30)
//    * P:CannotAddMemoryClass — 상대는 메모리 획득 불가(비-테이머 효과 한정) (AS-IS :39-78)
//
// ③ 배선 관례 근거:
//    * 둘 다 timing == None(상시 정적 효과) — AS-IS 그대로.
//
// 치환(substrate translations only):
//    * `player == card.Owner.Enemy` → `player.PlayerId == CardEffectCommons.OpponentOf(card)` (BT24_018 idiom).
//    * `cardEffect.EffectSourceCard != null && !cardEffect.IsTamerEffect` — ICardEffect.EffectSourceCard/
//      IsTamerEffect 그대로 미러에 존재(구조 변경 없음).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Yellow;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class EX8_030 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Conditions

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 2)
                {
                    return targetPermanent.TopCard.EqualsTraits("NSo");
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 0,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.None)
        {
            CannotAddMemoryClass cannotAddMemoryClass = new CannotAddMemoryClass();
            cannotAddMemoryClass.SetUpICardEffect("Opponent can't gain Memory", CanUseCondition, card);
            cannotAddMemoryClass.SetUpCannotAddMemoryClass(PlayerCondition: PlayerCondition, CardEffectCondition: CardEffectCondition);
            cardEffects.Add(cannotAddMemoryClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool PlayerCondition(Player player)
            {
                return player.PlayerId == CardEffectCommons.OpponentOf(card);
            }

            bool CardEffectCondition(ICardEffect cardEffect)
            {
                if (cardEffect != null)
                {
                    if (cardEffect.EffectSourceCard != null)
                    {
                        if (!cardEffect.IsTamerEffect)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        #endregion

        return cardEffects;
    }
}
