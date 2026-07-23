// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Treadmill Training (LM_054, Option / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/LM/Yellow/LM_054.cs (204 lines, 4 regions)
//    * Ignore Color Requirements :16-33  (timing == None — IgnoreColorConditionClass, self 한정)
//    * [Main]                    :42-84  (OptionSkill — 덱탑 2장 공개→황/흑 1장 손패→나머지 덱밑→delay 배치)
//    * <Delay>                   :90-185 (OnDeclaration — 자기 삭제→디지몬 1장 선택→코스트-2 손패 진화)
//    * [Security]                :191-197(SecuritySkill — AddActivateMainOptionSecurityEffect)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #2, 7축):
//    * K:Training 축 표기 주의 — 감사 §4는 LM_054를 K:Training으로 계상하나 이는 룰텍스트 매치
//      ("Treadmill Training" 카드명이 <Training> 토큰과 겹침)로, 실제 <Training> 키워드 효과는 없음.
//      본 카드의 실효 축은 아래 6개 + X:DelayOption. (감사 검출=토큰∪룰텍스트 방식의 한계 — 보고서에 명기)
//    * P:DeletePeremanentAndProcessAccordingToResult — <Delay> 몸통 자기-삭제 (AS-IS :133)
//    * P:DigivolveIntoHandOrTrashCard                — <Delay> 몸통 코스트-2 진화 (AS-IS :171-180)
//    * P:IgnoreColorConditionClass                   — 색 요구 무시 상시 (AS-IS :16-33)
//    * P:PlaceDelayOptionCards                       — [Main] 몸통 말미 delay 배치 (AS-IS :82)
//    * X:DelayOption                                 — <Delay> 선언·발화 전체
//    * X:IgnoreRequirement                           — IgnoreColorConditionClass 경유(옵션 색 요구 우회)
//    * (+P:SimplifiedRevealDeckTopCardsAndSelect/SimplifiedSelectCardConditionClass, E:SelectPermanentEffect,
//       P:AddActivateMainOptionSecurityEffect, T:OptionSkill/OnDeclaration/SecuritySkill)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [Main] 옵션 주효과 → OptionSkill + CanTriggerOptionMainEffect(hashtable, card) 게이트(AS-IS :60-63
//      그대로; OptionActivateAction이 ActivateOption 액션에서 OptionSkill을 해소).
//    * <Delay> 선언 → OnDeclaration + CanDeclareOptionDelayEffect(card) 게이트(AS-IS :126-129 그대로).
//    * [Security] → SecuritySkill + AddActivateMainOptionSecurityEffect(SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * CardColor.Yellow/Black (AS-IS enum) → "Yellow"/"Black" (미러 HasCardColor(string) idiom — BT2_044 헤더).
//    * `card.PermanentOfThisCard()`(가변 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
//    * `cardSource.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass)` (AS-IS :109) —
//      점유-프레임 환원(RD-P6C1-2, CardSource.CanPlayFromHandDuringMainPhase 헤더): owner 검사 +
//      cardSource.CanEvolve(permanent, true). PayCost:false 분기는 AS-IS도 빈-프레임 전용이라 소거 동일.
//    * PlaceDelayOptionCards 브릿지(PlayCardsBridge.cs:189)는 root 명시 필요 — AS-IS 기본값
//      SelectCardEffect.Root.Execution 그대로 전달.
//    * `card.Owner.GetBattleAreaPermanents()/HandCards` → `new Player(card.Context, card.Owner).*`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.LM.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class LM_054 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ignore Color Requirements

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return !new Player(card.Context, card.Owner).GetBattleAreaPermanents().Some(permanet =>
                    permanet.TopCard.EqualsCardName("Treadmill Training"));
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        #endregion

        // AS-IS :37-38 CardColor targetColor/targetColor1 — 미러 string-색 idiom(BT2_044 헤더).
        string targetColor = "Yellow";
        string targetColor1 = "Black";

        #region Main Effect

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Reveal the top 2 cards of your deck. Add 1 Yellow or Black card among them to the hand. Return the rest at the bottom of your deck. Then, place this card in the battle area.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.HasCardColor(targetColor)
                    || cardSource.HasCardColor(targetColor1);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                    revealCount: 2,
                    simplifiedSelectCardConditions:
                    new SimplifiedSelectCardConditionClass[]
                    {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition:CanSelectCardCondition,
                        message: "Select 1 Yellow or Black card.",
                        mode: SelectCardEffect.Mode.AddHand,
                        maxCount: 1,
                        selectCardCoroutine: null),
                    },
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass
                );
                // AS-IS :82 PlaceDelayOptionCards(card, activateClass) — 브릿지는 root 명시 필요,
                // AS-IS 기본값 Root.Execution 그대로.
                await CardEffectCommons.PlaceDelayOptionCards(
                    card: card, cardEffect: activateClass, root: SelectCardEffect.Root.Execution);
            }
        }

        #endregion

        #region Delay

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon digivolves", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] <Delay> - 1 of your Digimon may digivolve into a Yellow or Black Digimon card in the hand with the digivolution cost reduced by 2.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    foreach (CardSource cardSource in new Player(card.Context, card.Owner).HandCards)
                    {
                        // AS-IS :109 cardSource.CanPlayCardTargetFrame(permanent.PermanentFrame, false,
                        // activateClass) — 점유-프레임 환원(RD-P6C1-2): owner 검사 + CanEvolve.
                        if (CanSelectCardCondition(cardSource)
                            && permanent.TopCard.Owner == cardSource.Owner
                            && cardSource.CanEvolve(permanent, true))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && (cardSource.HasCardColor(targetColor)
                        || cardSource.HasCardColor(targetColor1));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanDeclareOptionDelayEffect(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
                    targetPermanents: new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass: activateClass,
                    successProcess: permanents => SuccessProcess(),
                    failureProcess: null);

                async Task SuccessProcess()
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        Permanent? selectedPermanent = null;

                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will digivolve.", "The opponent is selecting 1 Digimon that will digivolve.");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                targetPermanent: selectedPermanent,
                                cardCondition: CanSelectCardCondition,
                                payCost: true,
                                reduceCostTuple: (reduceCost: 2, reduceCostCardCondition: null),
                                fixedCostTuple: null,
                                ignoreDigivolutionRequirementFixedCost: -1,
                                isHand: true,
                                activateClass: activateClass,
                                successProcess: null);
                        }
                    }
                }
            }
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "[Main] Reveal the top 2 cards of your deck. Add 1 Yellow or Black card among them to the hand.Return the rest at the bottom of your deck.Then, place this card in the battle area.");
        }

        #endregion

        return cardEffects;
    }
}
