// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 1:1 포팅 카드 — Navy Memory Boost! (LM_046, Option / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/LM/Blue/LM_046.cs (126 lines, 4 regions)
//    * Color Requirements Condition :13-42  (timing == None — IgnoreColorConditionClass; 자기 색 요구를 무시하되,
//      owner의 Purple 디지몬/테이머 퍼머넌트가 필드에 있을 때만 발화)
//    * Main                          :44-103 (OptionSkill — 덱탑 3장 공개→청/자 디지몬 1장 손패→나머지 덱밑→delay 배치)
//    * Main Delay                    :105-112(OnDeclaration — CardEffectFactory.Gain2MemoryOptionDelayEffect)
//    * Security                      :114-121(SecuritySkill — CardEffectFactory.PlaceSelfDelayOptionSecurityEffect)
//
// ② 배선 관례 근거 (trigger-wiring-porting-rules, LM_054 exemplar 헤더):
//    * [Main] 옵션 주효과 → OptionSkill + CanTriggerOptionMainEffect(hashtable, card) 게이트(AS-IS :71-74 그대로).
//    * <Delay> → OnDeclaration + Gain2MemoryOptionDelayEffect(card) 팩토리(자체 배선).
//    * [Security] → SecuritySkill + PlaceSelfDelayOptionSecurityEffect(card) 팩토리(자체 배선).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X` (LM_054 idiom).
//    * CardColor.Blue/Purple (AS-IS enum) → "Blue"/"Purple" (미러 HasCardColor(string)/CardColors:IReadOnlyList<string> idiom).
//    * `CardEffectCommons.HasMatchConditionPermanent(Func<Permanent,bool>, isContainBreedingArea)` (AS-IS 무-card 오버로드,
//      GameContextDeterminarion.cs:641) → 미러 VIEW-predicate 오버로드 `HasMatchConditionPermanent(card, pred, true)`
//      (CardEffectCommons.cs:3731; 양측 배틀에어리어+육성 스캔).
//    * `permanent.TopCard.CardColors.Contains(CardColor.Purple)` → `permanent.TopCard.CardColors.Contains("Purple")`.
//    * PlaceDelayOptionCards 브릿지(PlayCardsBridge.cs:210)는 root 명시 필요 — AS-IS 기본값 SelectCardEffect.Root.Execution 그대로.
//    * AS-IS는 non-Simplified `RevealDeckTopCardsAndSelect` + `SelectCardConditionClass`(canNoAction:false)를 사용;
//      미러 동명 메서드/타입(RevealLibrary.cs:333 / :115)이 존재하므로 그대로 사용(Simplified* 치환 아님).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.LM.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class LM_046 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Color Requirements Condition

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Purple also meets this card's color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.HasMatchConditionPermanent(card, (permanent) =>
                    permanent.TopCard.Owner == card.Owner &&
                    permanent.TopCard.CardColors.Contains("Purple") &&
                    (permanent.IsDigimon || permanent.IsTamer), true);
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    return true;
                }

                return false;
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
                return "[Main] Reveal the top 3 cards of your deck. Add 1 blue or Purple Digimon card among them to the hand. Return the rest to the bottom of deck. Then, place this card in the battle area.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasCardColor("Blue") || cardSource.HasCardColor("Purple"))
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
                            canTargetCondition:CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList:null,
                            canEndSelectCondition:null,
                            canNoSelect:false,
                            selectCardCoroutine: null,
                            message: "Select 1 blue or purple Digimon card.",
                            maxCount: 1,
                            canEndNotMax:false,
                            mode: SelectCardEffect.Mode.AddHand
                            )
                    },
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass,
                    canNoAction: false);

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
