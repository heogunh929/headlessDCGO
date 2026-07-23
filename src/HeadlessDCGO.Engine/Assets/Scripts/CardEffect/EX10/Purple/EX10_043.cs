// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — EX10_043 (Sakusimon, Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX10/Purple/EX10_043.cs (327 lines, "Static Effects" +
//    "On Play/When Digivolving shared" + 4 timing 블록)
//    * Stnd Appmon Alt Digivolution Requirement :19-27 (None — AddSelfDigivolutionRequirementStaticEffect)
//    * Link Condition                           :33-41 (None — AddSelfLinkConditionStaticEffect)
//    * Link (선언)                               :47-50 (OnDeclaration — CardEffectFactory.LinkEffect)
//    * [On Play]/[When Digivolving] 공유 조건     :58-86 (레벨3 상대 디지몬 1체 삭제 후보 술어)
//    * [On Play]                                 :91-132 (OnEnterFieldAnyone, CanTriggerOnPlay)
//    * [When Digivolving]                        :138-179 (OnEnterFieldAnyone, CanTriggerWhenDigivolving)
//    * [All Turns][Once Per Turn]                :185-213 (OnLinkCardDiscarded — 링크카드 트래시 시 +1메모리)
//    * [When Attacking](Link)                    :219-320 (OnAllyAttack, SetIsLinkedEffect — 링크카드
//      트래시로 레벨4↓ 상대 디지몬 삭제)
//
// ② 프리미티브 매핑:
//    * K:Link                     — Link 선언 + AddSelfLinkConditionStaticEffect + [When Attacking] 링크소비
//      몸통(EX10_029 established idiom과 동형; 트래시-링크-카드→삭제 shape).
//    * P:AddSelfDigivolutionRequirementStaticEffect / AddSelfLinkConditionStaticEffect — 정적효과 2건.
//    * P:CanTriggerOnTrashLinkedCard / TrashLinkCardsAndProcessAccordingToResult — [When Attacking] 몸통.
//    * T:OnLinkCardDiscarded — 신규 창 타이밍 소비자. 표면 실존 확인: EffectTiming.OnLinkCardDiscarded 키
//      실존(CardController.cs:1398 StackSkillInfos 발화), CanTriggerOnTrashLinkedCard(Hashtable, ...) 실장
//      (CanUseEffects/OnTrashLinkedCard.cs:37, 몸통 실재). 그대로 포팅.
//
// ③ 배선 관례 근거:
//    * AS-IS 자체가 [On Play]와 [When Digivolving] 둘 다 timing == EffectTiming.OnEnterFieldAnyone 한 키에
//      SEPARATE ActivateClass로 등록(CanUseCondition만 CanTriggerOnPlay vs CanTriggerWhenDigivolving로 분기)
//      — EX5_058과 동형. trigger-wiring rule 3(이중-키 등록 금지) 적용: [When Digivolving] arm만
//      WhenDigivolving 전용 키로 재배선(BT17_026/EX7_072/EX5_058 확립 관례), [On Play] arm은 그대로 둔다.
//    * [When Attacking](Link) → OnAllyAttack + CanTriggerOnAttack + SetIsLinkedEffect(true)(AS-IS :224,232
//      그대로; EX10_029 [When Linked]와 달리 이 카드는 AS-IS 자체가 OnAllyAttack 키를 씀 — 방언 변환 불필요).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom); `StartCoroutine(X)`(문자열 아닌 코루틴
//      직접 호출, AS-IS :281) → `await X`.
//    * `targetPermanent.TopCard.HasStandardAppTraits` → `.EqualsTraits("Stnd.")`,
//      `targetPermanent.TopCard.HasAppmonTraits` → `.EqualsTraits("Appmon")` (EX10_029 established idiom).
//    * `card.PermanentOfThisCard()`(값-동등 비교, AS-IS :201 `perm == card.PermanentOfThisCard()`) →
//      `ICardEffect.ResolvePermanentOfThisCard(card)`(§2.3 값-동등 규칙); `.HasNoLinkCards`도 동일 브릿지.
//    * `Func<Permanent,bool>` 술어(CanSelectPermanentOPWDCondition/CanSelectPermanentCondition) —
//      HasMatchConditionPermanent/MatchConditionPermanentCount는 Permanent-오버로드, SelectPermanentEffect.
//      canTargetCondition은 정본 Func<Permanent,bool> — 전부 술어 직결(id 어댑터 없음).
//    * `thisCardPermament.LinkedCards` — 미러 Permanent.LinkedCards(List<CardSource>) 그대로(EX10_029:162,178).
//    * `card.Owner.AddMemory(1, activateClass)` — HeadlessPlayerId 확장 그대로(§2.2 예외절).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Purple;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX10_043 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static Effects

        #region Stnd Appmon Alternative Digivolution Requirement

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Stnd.");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Link Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Appmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 2, card: card));
        }

        #endregion

        #region Link

        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }

        #endregion

        #endregion

        #region On Play/When Digivolving shared

        bool CanSelectPermanentOPWDCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
            {
                if (permanent.Level == 3)
                {
                    if (permanent.TopCard.HasLevel)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentOPWDCondition))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 level 3 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] Delete 1 of your opponent's level 3 Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentOPWDCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentOPWDCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentOPWDCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS 두 번째 arm도 OnEnterFieldAnyone(:138)에 등록하지만, 미러 방언은 WhenDigivolving
        //   전용 키(trigger-wiring rule 3 — 이중-키 등록 금지).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 level 3 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] Delete 1 of your opponent's level 3 Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentOPWDCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentOPWDCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentOPWDCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.OnLinkCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain +1 Memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("GainMemory_EX10-043");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All turns] [Once Per Turn] When effects trash any of this Digimon's link cards, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerOnTrashLinkedCard(hashtable, perm => perm == ICardEffect.ResolvePermanentOfThisCard(card), cardEffect => cardEffect != null, source => source != null);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        #endregion

        #region Link - When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash to delete", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription() => "[When Attacking] By trashing 1 of this Digimon's link cards, delete 1 of your opponent's level 4 or lower Digimon.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistLinked(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistLinked(card) &&
                       !ICardEffect.ResolvePermanentOfThisCard(card).HasNoLinkCards;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) && permanent.Level <= 4;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent thisCardPermament = ICardEffect.ResolvePermanentOfThisCard(card);

                CardSource? selectedCard = null;

                #region Select Link Card

                int maxCount = Math.Min(1, thisCardPermament.LinkedCards.Count);
                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                            canTargetCondition: _ => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 linked card to trash.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: thisCardPermament.LinkedCards,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCard = cardSource;
                    return Task.CompletedTask;
                }
                selectCardEffect.SetUpCustomMessage("Select 1 linked card to trash.", "The opponent is selecting 1 linked card to trash.");
                await selectCardEffect.Activate();

                #endregion

                if (selectedCard != null)
                {
                    await CardEffectCommons.TrashLinkCardsAndProcessAccordingToResult(
                        targetPermanent: thisCardPermament,
                        targetLinkCards: new List<CardSource>() { selectedCard },
                        activateClass: activateClass,
                        successProcess: SuccessProcess,
                        failureProcess: null);
                }

                async Task SuccessProcess(List<CardSource> trashedLinkCards)
                {
                    if (trashedLinkCards.Any())
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
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
                                selectPermanentCoroutine: null,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Destroy,
                                cardEffect: activateClass);

                            await selectPermanentEffect.Activate();
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
