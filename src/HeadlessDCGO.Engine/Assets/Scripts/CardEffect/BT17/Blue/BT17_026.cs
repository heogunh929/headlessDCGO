// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Beowolfmon (BT17_026, Digimon / Blue / Lv.5)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT17/Blue/BT17_026.cs (646 lines, 3 regions)
//    * [Hand][Main]           :16-415  (timing == EffectTiming.OnDeclaration)
//    * [When Digivolving]     :419-564 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving gate)
//    * [When Attacking] ESS   :568-641 (timing == OnAllyAttack, Once Per Turn, inherited)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #6):
//    * P:CanNotSuspendClass        — [When Digivolving] 몸통이 selectedPermanent.UntilOwnerTurnEndEffects로
//                                    "can't suspend" 부여 (AS-IS :525-528)
//    * P:ChangeCardColorClass      — [Hand][Main] 임시 "treated as blue" player-effect (AS-IS :273-306)
//    * P:ChangePermanentLevelClass — [Hand][Main] 임시 "treated as level 4" (AS-IS :309-327)
//    * P:DontHaveDPClass           — [Hand][Main] 임시 "don't have DP" (AS-IS :352-356)
//    * (+P:TreatAsDigimonClass     — [Hand][Main] 임시 "treated as Digimon" (AS-IS :330-349))
//    * (+E:SelectPermanentEffect Mode.Custom/Mode.Bounce, E:SelectCardEffect Root.Trash/byPreSelectedList
//       + Root.Custom(진화원), P:DigivolveIntoHandOrTrashCard fixedCost 변형,
//       T:OnDeclaration, T:WhenDigivolving, T:OnAllyAttack)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone에
//      두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지 — STOP 가드 있음).
//      CanUseCondition의 CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :433-436 그대로 유지.
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card) triggerGate 필수(AS-IS :582-585).
//    * [Hand][Main] 선언형은 AS-IS 그대로 OnDeclaration.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `card.Owner.HandCards/TrashCards/GetBattleAreaPermanents()` → `new Player(card.Context, card.Owner).*`
//      (미러 Player 접근 idiom; CardSource/Permanent Equals는 InstanceId 기반이라 Contains 유지 가능).
//    * `card.CanPlayCardTargetFrame(selectedPermanent.PermanentFrame, true, activateClass)` (AS-IS :374) —
//      미러는 frame/slot 모델이 없음(RD-P6C1-2 확립 어댑테이션, CardSource.CanPlayFromHandDuringMainPhase
//      헤더 참조): OCCUPIED frame 경로는 owner 검사 + CanEvolve(framePermanent, true)로 환원
//      (AS-IS CanPlayCardTargetFrame :1116-1196의 점유-프레임 분기와 결과 동일).
//    * `GManager.instance.GetComponent<Effects>().CreateDebuffEffect(...)` (AS-IS :532-533) = UI 연출 —
//      미러 확립 관례상 스트립(CardController.cs:899 "CreateDebuffEffect = UI (stripped)" 동일).
//    * AS-IS :392 `StartCoroutine("DigivolvedFailed")` (문자열 디스패치) → DigivolvedFailed() 직접 await.
//    * AS-IS :397 `new IDiscardHand(card, hashtable).Discard()` → 미러 IDiscardHand(card) +
//      Discard(discardBatchId:null, causeEffectSourceId) (CardController.cs:121 미러 시그니처).
//    * AS-IS :302 ChangeCardColors가 라벨 "Treated as blue"인데 CardColor.Red를 추가 — **AS-IS 원본 버그를
//      1:1 유지**(단순화·수정 금지 원칙). 미러 ChangeCardColorClass는 List<CardColor> enum 사용.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT17.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT17_026 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Hand - Main

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your [Koji Minamoto] digivolves into this card", CanUseCondition,
                card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[Hand] [Main] By placing 1 [Lobomon] and [KendoGarurumon] from your trash under 1 of your [Koji Minamoto]s, digivolve it into this card as if that card is a level 4 blue Digimon for a digivolution cost of 3.";
            }

            bool CanSelectLobomonCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.Owner == card.Owner)
                    {
                        if (cardSource.EqualsCardName("Lobomon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectKendoGarurumonCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.Owner == card.Owner)
                    {
                        if (cardSource.EqualsCardName("KendoGarurumon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (permanent != null)
                {
                    if (permanent.TopCard != null)
                    {
                        if (permanent.TopCard.Owner == card.Owner)
                        {
                            if (new Player(card.Context, permanent.TopCard.Owner).GetBattleAreaPermanents().Contains(permanent))
                            {
                                if (!permanent.IsToken)
                                {
                                    if (permanent.TopCard.EqualsCardName("Koji Minamoto") ||
                                        permanent.TopCard.EqualsCardName("KojiMinamoto"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
                    {
                        if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card,
                                CanSelectLobomonCardCondition))
                        {
                            if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card,
                                    CanSelectKendoGarurumonCardCondition))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
                    {
                        Permanent? selectedPermanent = null;

                        int maxCount = Math.Min(1,
                            new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count(CanSelectPermanentCondition));

                        SelectPermanentEffect selectPermanentEffect =
                            GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 [Koji Minamoto].",
                            "The opponent is selecting 1 [Koji Minamoto].");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;
                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card,
                                    (cardSource) =>
                                        CanSelectLobomonCardCondition(cardSource) ||
                                        CanSelectKendoGarurumonCardCondition(cardSource)))
                            {
                                bool digivolve = false;

                                maxCount = 2;

                                if (new Player(card.Context, card.Owner).TrashCards.Count((cardSource) =>
                                        CanSelectLobomonCardCondition(cardSource) ||
                                        CanSelectKendoGarurumonCardCondition(cardSource)) <
                                    maxCount)
                                {
                                    maxCount = new Player(card.Context, card.Owner).TrashCards.Count((cardSource) =>
                                        CanSelectLobomonCardCondition(cardSource) ||
                                        CanSelectKendoGarurumonCardCondition(cardSource));
                                }

                                List<CardSource> selectedCards = new List<CardSource>();

                                SelectCardEffect selectCardEffect =
                                    GManager.instance.GetComponent<SelectCardEffect>();

                                selectCardEffect.SetUp(
                                    canTargetCondition: (cardSource) =>
                                        CanSelectLobomonCardCondition(cardSource) ||
                                        CanSelectKendoGarurumonCardCondition(cardSource),
                                    canTargetCondition_ByPreSelecetedList: CanTargetConditionByPreSelectedList,
                                    canEndSelectCondition: CanEndSelectCondition,
                                    canNoSelect: () => false,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message:
                                    "Select cards to place on the bottom of Digivolution cards\n(cards will be placed in the digivolution cards so that cards with lower numbers are on top).",
                                    maxCount: maxCount,
                                    canEndNotMax: false,
                                    isShowOpponent: true,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Trash,
                                    customRootCardList: null,
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                                await selectCardEffect.Activate();

                                bool CanTargetConditionByPreSelectedList(List<CardSource> cardSources,
                                    CardSource cardSource)
                                {
                                    if (cardSources.Count(CanSelectLobomonCardCondition) >= 1)
                                    {
                                        if (CanSelectLobomonCardCondition(cardSource))
                                        {
                                            return false;
                                        }
                                    }

                                    if (cardSources.Count(CanSelectKendoGarurumonCardCondition) >= 1)
                                    {
                                        if (CanSelectKendoGarurumonCardCondition(cardSource))
                                        {
                                            return false;
                                        }
                                    }

                                    return true;
                                }

                                bool CanEndSelectCondition(List<CardSource> cardSources)
                                {
                                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card,
                                            CanSelectLobomonCardCondition))
                                    {
                                        if (cardSources.Count(CanSelectLobomonCardCondition) == 0)
                                        {
                                            return false;
                                        }
                                    }

                                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card,
                                            CanSelectKendoGarurumonCardCondition))
                                    {
                                        if (cardSources.Count(CanSelectKendoGarurumonCardCondition) == 0)
                                        {
                                            return false;
                                        }
                                    }

                                    if (cardSources.Count(CanSelectLobomonCardCondition) >= 2)
                                    {
                                        return false;
                                    }

                                    if (cardSources.Count(CanSelectKendoGarurumonCardCondition) >= 2)
                                    {
                                        return false;
                                    }

                                    return true;
                                }

                                Task SelectCardCoroutine(CardSource cardSource)
                                {
                                    selectedCards.Add(cardSource);
                                    return Task.CompletedTask;
                                }

                                if (selectedCards.Count >= 1)
                                {
                                    await selectedPermanent.AddDigivolutionCardsBottom(
                                        selectedCards, activateClass.EffectSourceCard?.InstanceId);

                                    if (selectedCards.Count == 2)
                                    {
                                        digivolve = true;
                                    }
                                }

                                if (digivolve)
                                {
                                    CardSource topCard = selectedPermanent.TopCard;

                                    ChangeCardColorClass changeCardColorClass = new ChangeCardColorClass();
                                    changeCardColorClass.SetUpICardEffect($"Treated as blue",
                                        CanUseChangeColorCondition,
                                        card);
                                    changeCardColorClass.SetUpChangeCardColorClass(
                                        ChangeCardColors: ChangeCardColors);
                                    changeCardColorClass.SetNotShowUI(true);

                                    bool CanUseChangeColorCondition(Hashtable ccHashtable)
                                    {
                                        if (selectedPermanent.TopCard != null)
                                        {
                                            if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Contains(selectedPermanent))
                                            {
                                                if (topCard == selectedPermanent.TopCard)
                                                {
                                                    return true;
                                                }
                                            }
                                        }

                                        return false;
                                    }

                                    // AS-IS :297-306 — 라벨은 "blue"지만 원본이 CardColor.Red를 추가(원본 버그).
                                    // 1:1 원칙: 그대로 미러(수정 금지).
                                    List<CardColor> ChangeCardColors(CardSource cardSource,
                                        List<CardColor> cardColors)
                                    {
                                        if (cardSource == selectedPermanent.TopCard)
                                        {
                                            cardColors.Add(CardColor.Red);
                                        }

                                        return cardColors;
                                    }


                                    ChangePermanentLevelClass changePermanentLevelClass =
                                        new ChangePermanentLevelClass();
                                    changePermanentLevelClass.SetUpICardEffect($"Treated as level 4",
                                        CanUseChangeColorCondition, card);
                                    changePermanentLevelClass.SetUpChangePermanentLevelClass(GetLevel: GetLevel);
                                    changePermanentLevelClass.SetNotShowUI(true);

                                    int GetLevel(Permanent permanent, int level)
                                    {
                                        if (selectedPermanent.TopCard != null)
                                        {
                                            if (permanent == selectedPermanent)
                                            {
                                                level = 4;
                                            }
                                        }

                                        return level;
                                    }


                                    TreatAsDigimonClass treatAsDigimonClass = new TreatAsDigimonClass();
                                    treatAsDigimonClass.SetUpICardEffect($"Treated as Digimon",
                                        CanUseChangeColorCondition,
                                        card);
                                    treatAsDigimonClass.SetUpTreatAsDigimonClass(
                                        permanentCondition: PermanentCondition);
                                    treatAsDigimonClass.SetNotShowUI(true);

                                    bool PermanentCondition(Permanent permanent)
                                    {
                                        if (selectedPermanent.TopCard != null)
                                        {
                                            if (permanent == selectedPermanent)
                                            {
                                                return true;
                                            }
                                        }

                                        return false;
                                    }


                                    DontHaveDPClass dontHaveDPClass = new DontHaveDPClass();
                                    dontHaveDPClass.SetUpICardEffect("Don't have DP", CanUseChangeColorCondition,
                                        card);
                                    dontHaveDPClass.SetUpDontHaveDPClass(PermanentCondition: PermanentCondition);
                                    dontHaveDPClass.SetNotShowUI(true);


                                    List<Func<EffectTiming, ICardEffect>> getCardEffects =
                                        new List<Func<EffectTiming, ICardEffect>>()
                                        {
                                            _ => changeCardColorClass,
                                            _ => changePermanentLevelClass,
                                            _ => treatAsDigimonClass,
                                            _ => dontHaveDPClass,
                                        };

                                    foreach (Func<EffectTiming, ICardEffect> getCardEffect in getCardEffects)
                                    {
                                        new Player(card.Context, card.Owner).PermanentEffects.Add(getCardEffect);
                                    }


                                    // AS-IS :374 card.CanPlayCardTargetFrame(selectedPermanent.PermanentFrame,
                                    // true, activateClass) — 점유-프레임 환원(RD-P6C1-2): owner 검사 + CanEvolve.
                                    if (selectedPermanent.TopCard.Owner == card.Owner
                                        && card.CanEvolve(selectedPermanent, true))
                                    {
                                        await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                            targetPermanent: selectedPermanent,
                                            cardCondition: source => source == card,
                                            payCost: true,
                                            reduceCostTuple: null,
                                            fixedCostTuple: (fixedCost: 3, fixedCostCardCondition: null),
                                            ignoreDigivolutionRequirementFixedCost: -1,
                                            isHand: true,
                                            activateClass: activateClass,
                                            successProcess: null,
                                            failedProcess: DigivolvedFailed,
                                            isOptional: false);
                                    }
                                    else
                                    {
                                        // AS-IS :392 StartCoroutine("DigivolvedFailed") 문자열 디스패치 → 직접 await.
                                        await DigivolvedFailed();
                                    }

                                    async Task DigivolvedFailed()
                                    {
                                        // AS-IS :397-400 IDiscardHand(card, hashtable).Discard() —
                                        // 미러 시그니처(CardController.cs:121): batch-id 없음(null), cause=효과원.
                                        await new IDiscardHand(card).Discard(
                                            null, activateClass.EffectSourceCard?.InstanceId);
                                    }

                                    foreach (Func<EffectTiming, ICardEffect> getCardEffect in getCardEffects)
                                    {
                                        new Player(card.Context, card.Owner).PermanentEffects.Remove(getCardEffect);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:419)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 digivolution card and 1 of your opponent's Digimon can't suspend", CanUseCondition,
                card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] By returning 1 card with the [Hybrid] trait from this Digimon's digivolution cards to the hand, 1 of your opponent's Digimon or Tamers can't suspend until the end of their turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanSelectSourceCardCondition(CardSource cardSource)
            {
                return cardSource.CardTraits.Contains("Hybrid");
            }

            bool CanSelectOpponentPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card))
                {
                    return permanent.IsDigimon || permanent.IsTamer;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectSourceCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                if (selectedPermanent.DigivolutionCards.Count(CanSelectSourceCardCondition) >= 1)
                {
                    int maxCount = Math.Min(1, selectedPermanent.DigivolutionCards.Count(CanSelectSourceCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectSourceCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to add to your hand.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Custom,
                        customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();

                    // HasMatchConditionOpponentsPermanent/MatchConditionPermanentCount/SelectPermanentEffect.SetUp의
                    // canTargetCondition 모두 Permanent-술어(Func<Permanent,bool>) — 동일 술어를 세 호출부 전부에 직결
                    // (id-flip 3b).
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectOpponentPermanentCondition))
                    {
                        maxCount = Math.Min(1,
                            CardEffectCommons.MatchConditionPermanentCount(card, CanSelectOpponentPermanentCondition));

                        SelectPermanentEffect selectPermanentEffect =
                            GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition_ByPreSelecetedList: null,
                            canTargetCondition: CanSelectOpponentPermanentCondition,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon/Tamer that will be unable to suspend.",
                            "The opponent is selecting 1 Digimon/Tamer that will be unable to suspend.");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;
                            CanNotSuspendClass canNotSuspendClass = new CanNotSuspendClass();
                            canNotSuspendClass.SetUpICardEffect("Can't Suspend", CanUseCanNotSuspendCondition, card);
                            canNotSuspendClass.SetUpCanNotSuspendClass(PermanentCondition: PermanentCanNotSuspendCondition);
                            selectedPermanent.UntilOwnerTurnEndEffects.Add(_ => canNotSuspendClass);

                            // AS-IS :530-534 `if (!CanNotBeAffected) CreateDebuffEffect(...)` = UI 연출 —
                            // 미러 확립 관례상 스트립(CardController.cs:899 동일). 가드 술어 자체는
                            // CanUseCanNotSuspendCondition에 1:1 유지되어 효과 유효성은 보존.
                            return Task.CompletedTask;

                            bool CanUseCanNotSuspendCondition(Hashtable hashtableCanNotSuspend)
                            {
                                if (selectedPermanent.TopCard != null)
                                {
                                    if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool PermanentCanNotSuspendCondition(Permanent permanentCanNotSuspend)
                            {
                                if (permanentCanNotSuspend == selectedPermanent)
                                {
                                    return true;
                                }

                                return false;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region When Attacking - ESS

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 of your opponent's Digimon to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Attacking] [Once Per Turn] If this Digimon has the [Hybrid]/[Ten Warriors] trait, return 1 of your opponent's level 4 or lower Digimon to the hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                // ③ 배선: self-스코프 [When Attacking] triggerGate 필수(trigger-wiring rule 1) —
                //   AS-IS :582-585 CanTriggerOnAttack(hashtable, card) 그대로.
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.Level <= 4)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsTraits("Hybrid") ||
                        ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsTraits("Ten Warriors") ||
                        ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsTraits("TenWarriors"))
                    {
                            return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
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
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
