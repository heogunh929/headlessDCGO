// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — Tuwarmon (EX10_045, Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX10/Purple/EX10_045.cs (386 lines, 9 regions)
//    * Alternate Digivolution Req :16-28  (None — AddSelfDigivolutionRequirementStaticEffect, [Damemon] 위 코스트 1)
//    * DigiXros                   :30-102 (None — AddDigiXrosConditionClass, [Damemon]+[ChuuChuumon] 2재료)
//    * Collision                  :104-111(OnCounterTiming — CollisionSelfStaticEffect)
//    * Rush                       :113-121(None — RushSelfStaticEffect)
//    * OP/WD/WA Shared            :124-279(진화원 1장 트래시→[Bagra Army] 1체 <Blocker>&<Retaliation> 부여)
//    * [On Play]                  :281-298(OnEnterFieldAnyone + CanTriggerOnPlay)
//    * [When Digivolving]         :300-317(AS-IS OnEnterFieldAnyone + CanTriggerWhenDigivolving)
//    * [When Attacking]           :319-336(OnAllyAttack + CanTriggerOnAttack, Once Per Turn)
//    * <Save>                     :338-345(OnDestroyedAnyone — SaveEffect)
//    * ESS <Draw 1>               :347-380(OnDigivolutionCardDiscarded — inherited)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #30, 2축 + 인접):
//    * K:Save — <Save> 팔(AS-IS :342). ★수확 예상 정정: 감사 §1은 K:Save를 ❌미커버로 표기했으나 미러
//      팩토리 SaveEffect(Save.cs:18)+SaveProcess 실존 — 포팅 성공. 예상 빗나감 → 원장 정정.
//    * P:TrashDigivolutionCardsAndProcessAccordingToResult — 공유 몸통(AS-IS :219). 미러
//      DigivolveAndTrashBridge.cs:108 실존 — 포팅 성공(감사 ❌미커버 정정).
//    * X:DigiXros — DigiXros 재료 조건 등록(AS-IS :34). None-타이밍 등록(AddDigiXrosConditionClass)은
//      데이터-홀더로 착지(AD1_025 Assembly 판례와 동형)하며, 이 조건을 소비하는 인터랙티브 DigiXros 플레이
//      (SelectDigiXrosClass.Select)도 착지(RD-R5-04/RD-EXT3-01 상환 — 배치 7b): Permanent.
//      CanSubstituteForDigiXrosCondition(Permanent.cs:3644) + SelectHandEffect 미러 실착지, Select에 STOP 없음.
//      펌프 플레이 경로(PlayCardClass.PlayCard → SelectDigiXrosClass.Select, CardController.cs:3387-3390)로 소비
//      실증(RD-BATCH7B.Witness). 즉 등록·소비 두 팔 모두 실착지.
//    * (+K:Rush(RushSelfStaticEffect)·K:Collision(CollisionSelfStaticEffect)·K:Blocker/Retaliation(GainBlocker/
//       GainRetaliation)·P:AddSelfDigivolutionRequirementStaticEffect·P:DrawClass, S:SelectPermanentEffect/
//       SelectCardEffect(Root.Custom 진화원), T:OnDestroyedAnyone/OnDigivolutionCardDiscarded/OnCounterTiming)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :293).
//    * [When Digivolving] → 미러 방언 WhenDigivolving 전용 키(DigivolveAction이 WhenDigivolving만 해소 —
//      이중-키 등록 금지, BT17_026 판례 rule 3). AS-IS는 OnEnterFieldAnyone+CanTriggerWhenDigivolving(:302,313).
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card) triggerGate(AS-IS :331). SetHashString
//      "OPWDWA_EX10_045" 3-팔 공유(Once Per Turn 공유 계정).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask`.
//    * SelectPermanentEffect canTargetCondition는 정본 Func<Permanent,bool> — AS-IS Permanent-술어를 그대로
//      전달(id 어댑터 없음). Has/Count 스캔도 동일 Permanent-술어 오버로드 직접 사용.
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId; Player 조작은 `new Player(card.Context, card.Owner)`.
//    * `new DrawClass(card.Owner, 1, activateClass)` → 미러 `new DrawClass(card.Context, card.Owner, 1,
//      cause, activateClass)`(CardController.cs:186 시그니처; cause=효과원 InstanceId).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(LM_054 판례).
//    * SetUpCustomMessage 등 UI 연출 문자열은 유지(미러 SelectPermanentEffect가 수용).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Purple;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX10_045 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Damemon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition, digivolutionCost: 1, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Digixros

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "Damemon");

                    bool CanSelectCardCondition(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.CardNames_DigiXros.Contains("Damemon"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    DigiXrosConditionElement element1 = new DigiXrosConditionElement(CanSelectCardCondition1, "ChuuChuumon");

                    bool CanSelectCardCondition1(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.CardNames_DigiXros.Contains("ChuuChuumon"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>() { element, element1 };

                    DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, null, 2);

                    return digiXrosCondition;
                }

                return null;
            }
        }

        #endregion

        #region Collision

        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.CollisionSelfStaticEffect(false, card, null));
        }

        #endregion

        #region Rush

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RushSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region OP/WD/WA Shared

        string SharedEffectDiscription(string tag)
        {
            return $"[{tag}] [Once Per Turn] By trashing any 1 digivolution card of your [Bagra Army] trait Digimon, 1 of your [Bagra Army] trait Digimon gains <Blocker> and <Retaliation> until your opponent's turn ends.";
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        bool CanSelectPermanent(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                   permanent.TopCard.CardTraits.Contains("Bagra Army");
        }

        bool CanSelectPermamentTrashDigivolution(Permanent permanent)
        {
            return CanSelectPermanent(permanent)
                && permanent.DigivolutionCards.Count > 0;
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermamentTrashDigivolution))
            {
                #region Select Permanent to trash Digivolution Cards

                Permanent selectedPermanment = null;

                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermamentTrashDigivolution));
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermamentTrashDigivolution,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanment = permanent;
                    return Task.CompletedTask;
                }

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon trash digivolution card", "The opponent is selecting 1 Digimon trash digivolution card");
                await selectPermanentEffect.Activate();

                #endregion

                if (selectedPermanment != null)
                {
                    #region Select Digivolution Card to trash

                    CardSource selectedCard = null;
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: x => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 digivolution card to discard.",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Custom,
                        customRootCardList: selectedPermanment.DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: null);

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }

                    selectCardEffect.SetUpCustomMessage("Select 1 digivolution card to discard.", "The opponent is selecting 1 digivolution card to discard.");
                    await selectCardEffect.Activate();

                    #endregion

                    if (selectedCard != null)
                    {
                        await CardEffectCommons.TrashDigivolutionCardsAndProcessAccordingToResult(
                            targetPermanent: selectedPermanment,
                            targetDigivolutionCards: new List<CardSource>() { selectedCard },
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null);
                    }

                    async Task SuccessProcess(List<CardSource> cardSources)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanent))
                        {
                            #region Select Permanent to gain effects

                            Permanent selectedPermanment1 = null;

                            int maxCount1 = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanent));
                            SelectPermanentEffect selectPermanentEffect1 = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect1.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanSelectPermanent,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: maxCount1,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                selectedPermanment1 = permanent;
                                return Task.CompletedTask;
                            }

                            selectPermanentEffect1.SetUpCustomMessage("Select 1 Digimon to gain <Blocker> & <Retaliation>", "The opponent is selecting 1 Digimon to gain <Blocker> & <Retaliation>");
                            await selectPermanentEffect1.Activate();

                            #endregion

                            if (selectedPermanment1 != null)
                            {
                                await CardEffectCommons.GainBlocker(
                                    targetPermanent: selectedPermanment1,
                                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                                    activateClass: activateClass);

                                await CardEffectCommons.GainRetaliation(
                                    targetPermanent: selectedPermanment1,
                                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                                    activateClass: activateClass);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By trashing 1 source, 1 digimon gains Blocker, Retaliation", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, hashtable => SharedActivateCoroutine(hashtable, activateClass), 1, true, SharedEffectDiscription("On Play"));
            activateClass.SetHashString("OPWDWA_EX10_045");
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:302)이나 미러 방언은 WhenDigivolving 전용 키(BT17_026 rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By trashing 1 source, 1 digimon gains Blocker, Retaliation", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, hashtable => SharedActivateCoroutine(hashtable, activateClass), 1, true, SharedEffectDiscription("When Digivolving"));
            activateClass.SetHashString("OPWDWA_EX10_045");
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }

        #endregion

        #region When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By trashing 1 source, 1 digimon gains Blocker, Retaliation", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, hashtable => SharedActivateCoroutine(hashtable, activateClass), 1, true, SharedEffectDiscription("When Attacking"));
            activateClass.SetHashString("OPWDWA_EX10_045");
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }
        }

        #endregion

        #region Save

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.SaveEffect(card: card));
        }

        #endregion

        #region ESS

        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<Draw 1>", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When effects trash this card from a [Bagra Army] trait Digimon's digivolution cards, <Draw 1>";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                Permanent trashedPermanent = CardEffectCommons.GetPermanentFromHashtable(hashtable);
                return trashedPermanent.TopCard.EqualsTraits("Bagra Army") &&
                       CardEffectCommons.CanTriggerOnTrashSelfDigivolutionCard(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId, activateClass).Draw();
            }
        }

        #endregion

        return cardEffects;
    }
}
