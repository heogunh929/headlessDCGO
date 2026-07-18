// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT16_052 (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT16/Black/BT16_052.cs (75 lines, 3 regions)
//    * Alternate Digivolution Requirement :15-23 (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [When Digivolving]                 :29-63 (timing == OnEnterFieldAnyone — PlayKoHagurumonToken)
//    * Inherited Effect                   :69    (BlockerSelfStaticEffect, isInheritedEffect: true, 상시등록)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [Hagurumon] 위 코스트 0
//      (AS-IS :17-22; BT9_009/BT9_013 established idiom).
//    * P:PlayKoHagurumonToken — [When Digivolving] 토큰 소환 (AS-IS :61; symbol_map.csv row 409 OK,
//      Script/CardEffectCommons/PlayCardsBridge.cs:317 실장).
//    * P:BlockerSelfStaticEffect — Inherited Effect, 상시 [Blocker] (AS-IS :69; symbol_map.csv row 35 OK).
//
// ③ 배선 관례 근거: [When Digivolving] → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 그대로
//    (AS-IS :43 그대로 유지).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom).
//    * AS-IS :50 `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() && frame.IsBattleAreaFrame())
//      >= 1` — 빈-프레임 용량 체크 소거(symbol_map_guide §2.8, RD-P6C1-1/-2 확립 어댑테이션; 미러는 frame/slot
//      모델이 없고 토큰-플레이 공용층이 하류에서 용량을 재검사); co-conjunct(IsExistOnBattleArea)만 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT16.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT16_052 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("Hagurumon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [KoHagurumon] token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may play 1 [KoHagurumon] (Digimon/Black/1000 DP/<Blocker><Decoy(Black)>\"[Your Turn] This Digimon can't attack\") token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :50 `&& card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() &&
                // frame.IsBattleAreaFrame()) >= 1` — 빈-프레임 용량 체크 소거(§2.8, RD-P6C1-1/-2).
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlayKoHagurumonToken(activateClass);
            }
        }

        #endregion

        #region Inherited Effect

        cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(true, card, null));

        #endregion

        return cardEffects;
    }
}
