// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 카드 (수확 트랜치) — Kazuki & Itsuki (BT25_089, Tamer / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Green/BT25_089.cs (378 lines, 4 regions)
//    * [Start of Your Main Phase] OnStartMainPhase :15-20 (Gain1MemoryTamerOpponentDigimonEffect)
//    * [Main] OnDeclaration          :22-231 (once/turn: 손패/진화원의 [Appmon] 1장을 코스트 -2로 link)
//    * [End of Your Turn] OnEndTurn   :234-366 (once/turn: 자기 디지몬 1체를 손패 디지몬으로 app-fuse)
//    * [Security] SecuritySkill       :368-373 (PlaySelfTamerSecurityEffect)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #14, 3축):
//    * P:Gain1MemoryTamerOpponentDigimonEffect — [Start of Main] (AS-IS :18; 클린 factory)
//    * P:SuspendPeremanentAndProcessAccordingToResult — [Main] 몸통 suspend-cost (AS-IS :66; 클린)
//    * X:AppFusion                    — [End of Turn] 몸통 (AS-IS :354; RD-EXT3-02 해소 — G-AppF)
//    * (+K:Link [Main] link 실행: G-Link 배치 2로 복원(RD-EXT3-01 해소); PlaySelfTamerSecurityEffect: 클린)
//
// ③ 배선 관례 근거: [Main] → OnDeclaration + ActivateClass; [Security] → SecuritySkill factory;
//    [Start of Main] / [End of Turn] → 해당 timing 직접.
//
// 수확 명세 (원 STOP 팔 2 — coverage_exemplar_audit §6 "Burst/AppFusion select"·"DigiXros/Link"):
//   ▸ [Main] / Link (RD-EXT3-01 **해소** — G-Link 배치 2): suspend-cost 절반은 클린 포팅(기존);
//     link 실행 절반은 AS-IS SuccessProcess(BT25_089.cs:72-229) 원문 복원 — `ILinkCard`/`GetChangedLinkCost`
//     미러 착지(RD-P6C2-7/C2-02 해소)로 STOP 봉인 제거. 치환: UntilCalculateFixedCostEffect →
//     미러 Player store-backed 리스트; SelectPermanentEffect canTargetCondition → 정본 Permanent-형 직결.
//   ▸ [End of Turn] / AppFusion (RD-EXT3-02 **해소** — G-AppF): ActivateCoroutine 본문을 AS-IS(:289-364)
//     1:1 복원. 세 갭 해소: (1) `CardSource.CanAppFusionFromTargetPermanent` = 실 1:1 인스턴스 메서드
//     (CardSource.cs, RD-P6C1-2 상환) + PlayCard() AppFusion 분기(IsAppFusion/LinkedCard 프레임-룩업·
//     link-sourcing) 실배선; (2) `Permanent.PermanentFrame.FrameID` = FieldCardFrame(GetFieldPermanents
//     인덱스, RD-P6C3-D1); (3) `hand.appFusionCondition` → `AppFusionConditionOf()`. 잔여: AS-IS AddToSources가
//     호스트 LIVE-TOP을 진화원으로 강등하는 부분은 미러 permanent-identity(id==top) 한계 = 별개 프리미티브
//     MIG4-DETACH-LIVE-TOP(RD-EXT3-02 범위 밖) — full 실행은 그 지점에서 정직 STOP.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * card-less `HasMatchConditionPermanent(cond)` → `(card, cond)` 오버로드.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_089 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Start of Your Main Phase
        if (timing == EffectTiming.OnStartMainPhase)
        {
            cardEffects.Add(CardEffectFactory.Gain1MemoryTamerOpponentDigimonEffect(card));
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Link for -2", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Main] [Once Per Turn] You may link 1 [Appmon] trait Digimon card from your hand or your Digimon's digivolution cards to 1 of your Digimon with the cost reduced by 2.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanActivateSuspendCostEffect(card)
                    && (CardEffectCommons.HasMatchConditionOwnersHand(card, CanLinkCardActivateCondition)
                        || CardEffectCommons.HasMatchConditionPermanent(card, CanTakeFromDigivolutionCardsActivateCondition));
            }

            bool CanTakeFromDigivolutionCardsActivateCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.DigivolutionCards.Any(CanLinkCardActivateCondition);
            }

            bool CanLinkCardActivateCondition(CardSource cardSource) => CanLinkCardCondition(cardSource, false);

            bool CanLinkCardEffectCondition(CardSource cardSource) => CanLinkCardCondition(cardSource, true);

            bool CanLinkCardCondition(CardSource cardSource, bool payCost)
            {
                return cardSource.IsDigimon
                    && cardSource.EqualsTraits("Appmon")
                    && cardSource.CanLink(payCost);
            }

            bool CanTakeFromDigivolutionCardsEffectCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.DigivolutionCards.Any(CanLinkCardEffectCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass,
                    SuccessProcess,
                    null);

                // (G-Link batch 2, RD-EXT3-01 RESOLVED) AS-IS SuccessProcess (BT25_089.cs:72-229) verbatim:
                // grant GrantedReduceLinkCostClass(-2) via UntilCalculateFixedCostEffect, Int-area selection
                // ("From hand" / "From digivolution cards" / "Do not Link"), SelectHand or SelectPermanent+
                // SelectCard(Root.DigivolutionCards), then SelectPermanent(CanLinkToTargetPermanent(payCost:true))
                // -> new ILinkCard(true, cardSource, permanent, activateClass).LinkCard(), finally remove the grant.
                // Substrate: card.Owner.UntilCalculateFixedCostEffect -> the mirror Player store-backed list.
                async Task SuccessProcess(List<Permanent> suspendedPermaments)
                {
                    #region Link Cost Reduction
                    ICardEffect GetCardEffect(EffectTiming _timing)
                    {
                        if (_timing == EffectTiming.None)
                        {
                            return CardEffectFactory.GrantedReduceLinkCostClass(
                                card: card,
                                reducedCost: 2,
                                cardSourceCondition: _ => true,
                                permanentCondition: _ => true,
                                rootCondition: _ => true
                            );
                        }

                        return null;
                    }

                    new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(GetCardEffect);
                    #endregion

                    bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanLinkCardEffectCondition);
                    bool canSelectSources = CardEffectCommons.HasMatchConditionPermanent(card, CanTakeFromDigivolutionCardsActivateCondition);

                    List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();
                    if (canSelectHand)
                    {
                        selectionElements.Add(new SelectionElement<int>(message: $"From hand", value: 1, spriteIndex: 0));
                    }
                    if (canSelectSources)
                    {
                        selectionElements.Add(new SelectionElement<int>(message: $"From digivolution cards", value: 2, spriteIndex: 0));
                    }
                    selectionElements.Add(new SelectionElement<int>(message: $"Do not Link", value: 3, spriteIndex: 1));

                    string selectPlayerMessage = "From which area will you link a card?";
                    string notSelectPlayerMessage = "The opponent is choosing from which area to select a card.";

                    GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool doLink = GManager.instance.userSelectionManager.SelectedIntValue != 3;
                    bool fromHand = GManager.instance.userSelectionManager.SelectedIntValue == 1;
                    if (doLink)
                    {
                        if (fromHand)
                        {
                            SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                            selectHandEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanLinkCardEffectCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: true,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                mode: SelectHandEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectHandEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                            selectHandEffect.SetUpCustomMessage_ShowCard("Selected Card");

                            await selectHandEffect.Activate();
                        }
                        else
                        {
                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanTakeFromDigivolutionCardsEffectCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: true,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to select a digivolution card from", "The opponent is selecting 1 card to link.");

                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                selectCardEffect.SetUp(
                                    canTargetCondition: CanLinkCardEffectCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => true,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: "Select 1 card to add as source.",
                                    maxCount: 1,
                                    canEndNotMax: false,
                                    isShowOpponent: true,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.DigivolutionCards,
                                    customRootCardList: permanent.DigivolutionCards.ToList(),
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                                selectCardEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                                selectCardEffect.SetUpCustomMessage_ShowCard("Selected Card");

                                await selectCardEffect.Activate();
                            }
                        }

                        async Task SelectCardCoroutine(CardSource cardSource)
                        {
                            bool CanLinkPermanentCondition(Permanent permanent)
                            {
                                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                                    && cardSource.CanLinkToTargetPermanent(permanent, true);
                            }

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanLinkPermanentCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: true,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to link to.", "The opponent is selecting 1 digimon to link.");

                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                await new ILinkCard(true, cardSource, permanent, activateClass).LinkCard();
                            }
                        }
                    }

                    #region Remove Link Cost Reduction
                    new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Remove(GetCardEffect);
                    #endregion
                }
            }
        }
        #endregion

        #region End of turn
        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("App fuse 1 digimon into digimon in hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_089_AppFusion");
            activateClass.SetIsSkippable(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[End of Your Turn] [Once Per Turn] 1 of your Digimon may app fuse into a Digimon card in the hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            // (G-AppF / RD-EXT3-02 RESOLVED) AS-IS :260-274. `card.Owner.HandCards` → the mirror
            // `new Player(card.Context, card.Owner).HandCards` (BT2_023 .Enemy route); `hand.appFusionCondition`
            // → `hand.AppFusionConditionOf()` (adaptation (6)).
            bool CanSelectPermanent(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    foreach (CardSource hand in new Player(card.Context, card.Owner).HandCards)
                    {
                        if (hand.AppFusionConditionOf() != null)
                        {
                            if (hand.CanAppFusionFromTargetPermanent(permanent, true))
                                return true;
                        }
                    }
                }
                return false;
            }

            // AS-IS :276-287. `card.appFusionCondition` → `card.AppFusionConditionOf()` (adaptation (6)).
            bool CanSelectCard(CardSource card, Permanent permanent)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (card.AppFusionConditionOf() != null)
                    {
                        if (card.CanAppFusionFromTargetPermanent(permanent, true))
                            return true;
                    }
                }
                return false;
            }

            // AS-IS :289-364. IEnumerator → async Task; StartCoroutine(X) → await X;
            // `yield return null` coroutines → Task.CompletedTask.
            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool executed = false;
                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectPermanent))
                {
                    Permanent selectedPermanent = null;
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersPermanentCount(card, CanSelectPermanent));
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanent,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 digimon to app fuse", "The opponent is selecting 1 digimon to app fuse");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        CardSource selectedCard = null;
                        int maxCount1 = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, handCard => CanSelectCard(handCard, selectedPermanent)));
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: handCard => CanSelectCard(handCard, selectedPermanent),
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select digimon to app fuse into.", "The opponent is selecting digimon to app fuse into.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Selected digimon");
                        await selectHandEffect.Activate();

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCard = cardSource;
                            return Task.CompletedTask;
                        }

                        if (selectedCard != null && selectedCard.CanAppFusionFromTargetPermanent(selectedPermanent, true))
                        {
                            CardSource linkCard = selectedPermanent.LinkedCards.Where(x => selectedCard.AppFusionConditionOf()!.linkedCondition(selectedPermanent, x)).First();

                            PlayCardClass playCardClass = new PlayCardClass(new List<CardSource> { selectedCard }, hashtable, true, selectedPermanent, false, SelectCardEffect.Root.Hand, true);
                            playCardClass.SetAppFusion(new int[] { selectedPermanent.PermanentFrame!.FrameID, selectedPermanent.LinkedCards.IndexOf(linkCard) });

                            executed = true;

                            await playCardClass.PlayCard();
                        }
                    }
                }
                if (!executed) activateClass.RemoveUse();
            }
        }
        #endregion

        #region Security Effect
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }
        #endregion

        return cardEffects;
    }
}
