// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — Chartreuse Memory Boost! (LM_047, Option / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/LM/Yellow/LM_047.cs (126 lines, 4 regions)
//    * Color Requirements Condition :13-42  (timing == None — IgnoreColorConditionClass, self 한정, 필드에
//                                            녹색 Digimon/Tamer 존재 게이트)
//    * [Main]                       :44-103 (OptionSkill — 덱탑 3장 공개→황/녹 Digimon 1장 손패→나머지 덱밑→
//                                            delay 배치)
//    * [Main] <Delay>               :105-112(OnDeclaration — Gain2MemoryOptionDelayEffect)
//    * [Security]                   :114-121(SecuritySkill — PlaceSelfDelayOptionSecurityEffect)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #13, 3축):
//    * P:SelectCardConditionClass    — [Main] 몸통 고급 SelectCardConditionClass 술어(AS-IS :81-94). ★수확
//      예상 정정: 감사 §6은 이 축을 "SelectCardConditionClass 미평가(저장만)"로 STOP-예상했으나, 미러
//      브릿지 RevealDeckTopCardsAndSelect(RevealLibrary.cs:333)는 CanTargetConditionCard 술어를 실평가
//      (allRevealed.Where(canTarget)) — fidelity-over-coverage 충족. 예상 빗나감(포팅 성공) → 원장 정정.
//    * P:Gain2MemoryOptionDelayEffect — <Delay> 팔(AS-IS :109). 미러 CardEffectFactory.cs:197 실존.
//    * P:PlaceSelfDelayOptionSecurityEffect — [Security] 팔(AS-IS :118). 미러 CardEffectFactory.cs:940 실존.
//    * (+P:IgnoreColorConditionClass, P:RevealDeckTopCardsAndSelect(고급), P:PlaceDelayOptionCards,
//       T:OptionSkill/OnDeclaration/SecuritySkill, X:DelayOption)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [Main] 옵션 주효과 → OptionSkill + CanTriggerOptionMainEffect(hashtable, card) 게이트(AS-IS :71-74).
//    * <Delay>/[Security] → 팩토리(Gain2MemoryOptionDelayEffect/PlaceSelfDelayOptionSecurityEffect)가 각각
//      OnDeclaration/SecuritySkill 타이밍의 팔을 담당 — LM_054 판례(델레이/시큐리티 배치 계열) 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * CardColor.Yellow/Green (AS-IS enum) → "Yellow"/"Green" (미러 string-색 idiom — BT2_044·LM_054 헤더).
//      [Main] 술어는 HasCardColor(string), 색-요구 게이트 술어는 CardColors.Contains(string) — AS-IS 각각의
//      호출 형태(HasCardColor vs CardColors.Contains)를 1:1 보존.
//    * PlaceDelayOptionCards 브릿지(PlayCardsBridge)는 root 명시 필요 — AS-IS 기본값 Root.Execution 그대로
//      전달(LM_054 헤더 판례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.LM.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class LM_047 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Color Requirements Condition

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Green also meets this card's color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                // AS-IS :25-28 — 필드에 owner의 녹색 Digimon/Tamer 퍼머넌트가 있을 때만 색 요구를 무시.
                return CardEffectCommons.HasMatchConditionPermanent(card, permanent =>
                    permanent.TopCard.Owner == card.Owner
                    && permanent.TopCard.CardColors.Contains("Green")
                    && (permanent.IsDigimon || permanent.IsTamer), true);
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        #endregion

        #region Main

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Reveal the top 3 cards of your deck. Add 1 yellow or green Digimon card among them to the hand. Return the rest to the bottom of deck. Then, place this card in the battle area.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasCardColor("Yellow") || cardSource.HasCardColor("Green"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.RevealDeckTopCardsAndSelect(
                    revealCount: 3,
                    selectCardConditions:
                    new SelectCardConditionClass[]
                    {
                        new SelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: false,
                            selectCardCoroutine: null,
                            message: "Select 1 yellow or green Digimon card.",
                            maxCount: 1,
                            canEndNotMax: false,
                            mode: SelectCardEffect.Mode.AddHand),
                    },
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass,
                    canNoAction: false);

                // AS-IS :99 PlaceDelayOptionCards(card, activateClass) — 브릿지는 root 명시 필요,
                // AS-IS 기본값 Root.Execution 그대로(LM_054 판례).
                await CardEffectCommons.PlaceDelayOptionCards(
                    card: card, cardEffect: activateClass, root: SelectCardEffect.Root.Execution);
            }
        }

        #endregion

        #region Main Delay

        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.Gain2MemoryOptionDelayEffect(card));
        }

        #endregion

        #region Security

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaceSelfDelayOptionSecurityEffect(card));
        }

        #endregion

        return cardEffects;
    }
}
