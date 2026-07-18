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
//    * X:AppFusion                    — [End of Turn] 몸통 (AS-IS :354; STOP)
//    * (+K:Link [Main] link 실행: G-Link 배치 2로 복원(RD-EXT3-01 해소); PlaySelfTamerSecurityEffect: 클린)
//
// ③ 배선 관례 근거: [Main] → OnDeclaration + ActivateClass; [Security] → SecuritySkill factory;
//    [Start of Main] / [End of Turn] → 해당 timing 직접.
//
// 수확 명세 (원 STOP 팔 2 — coverage_exemplar_audit §6 "Burst/AppFusion select"·"DigiXros/Link"):
//   ▸ [Main] / Link (RD-EXT3-01 **해소** — G-Link 배치 2): suspend-cost 절반은 클린 포팅(기존);
//     link 실행 절반은 AS-IS SuccessProcess(BT25_089.cs:72-229) 원문 복원 — `ILinkCard`/`GetChangedLinkCost`
//     미러 착지(RD-P6C2-7/C2-02 해소)로 STOP 봉인 제거. 치환: UntilCalculateFixedCostEffect →
//     미러 Player store-backed 리스트; SelectPermanentEffect canTargetCondition → id-adapter(PermanentOf).
//   ▸ [End of Turn] / AppFusion (RD-EXT3-02): 세 갭 — (1) `CardSource.CanAppFusionFromTargetPermanent`
//     (CardController.cs:4059) 및 PlayCard() AppFusion 분기(CardController.cs:2605)는 RD-P6C1-2 throw
//     (app-fusion 요구/코스트 체크 미이관); (2) `Permanent.PermanentFrame.FrameID` 미이관(no frame/slot model,
//     RD-P6C3-D1); (3) `hand.appFusionCondition` 필드는 미러에서 `AppFusionConditionOf()` 메서드. ActivateCoroutine
//     몸통을 STOP 마커로 봉인, AS-IS 본문 주석 보존. (LinkedCards/AppFusionCondition.linkedCondition/
//     PlayCardClass ctor+SetAppFusion 자체는 클린.)
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

            // (G-Link batch 2) SelectPermanentEffect's mirror canTargetCondition is the established id-based
            // Func<HeadlessEntityId, bool> — PermanentOf(id) adapter (ArtsDigivolve/BT25_104 idiom).
            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

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
                                canTargetCondition: id => PermanentOf(id) is { } p && CanTakeFromDigivolutionCardsEffectCondition(p),
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
                                canTargetCondition: id => PermanentOf(id) is { } p && CanLinkPermanentCondition(p),
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

            Task ActivateCoroutine(Hashtable hashtable)
            {
                // STOP (design item RD-EXT3-02): the AppFusion RESOLUTION is unported. The AS-IS ActivateCoroutine
                // (preserved verbatim below as comment) selects 1 of your Digimon then 1 app-fusable hand Digimon and
                // plays it as an app-fusion. Three gaps: (1) `CardSource.CanAppFusionFromTargetPermanent`
                // (CardController.cs:4059) — and PlayCard()'s AppFusion branch (CardController.cs:2605) — throw
                // RD-P6C1-2 (app-fusion requirement/cost check has no mirror); (2) `Permanent.PermanentFrame.FrameID`
                // has no mirror (no frame/slot model, RD-P6C3-D1); (3) `hand.appFusionCondition` is a method
                // `AppFusionConditionOf()` on the mirror CardSource, not a field. LinkedCards / AppFusionCondition
                // .linkedCondition / PlayCardClass ctor+SetAppFusion themselves are clean. Kept as AS-IS-named comment:
                //
                //   bool executed = false;
                //   if (HasMatchConditionOwnersPermanent(card, CanSelectPermanent)) {
                //     SelectPermanentEffect(mode:Custom, "Select 1 digimon to app fuse") -> selectedPermanent;
                //     if (selectedPermanent != null) {
                //       SelectHandEffect(CanSelectCard(handCard, selectedPermanent)) -> selectedCard;
                //       if (selectedCard != null && selectedCard.CanAppFusionFromTargetPermanent(selectedPermanent, true)) {
                //         linkCard = selectedPermanent.LinkedCards.Where(x => selectedCard.AppFusionConditionOf().linkedCondition(selectedPermanent, x)).First();
                //         var pcc = new PlayCardClass([selectedCard], hashtable, true, selectedPermanent, false, Root.Hand, true);
                //         pcc.SetAppFusion(new int[] { selectedPermanent.PermanentFrame.FrameID, selectedPermanent.LinkedCards.IndexOf(linkCard) });
                //         executed = true; await pcc.PlayCard();
                //       }
                //     }
                //   }
                //   if (!executed) activateClass.RemoveUse();
                throw new NotSupportedException(
                    "STOP: BT25_089 [End of Your Turn] app-fusion needs CanAppFusionFromTargetPermanent (RD-P6C1-2) + " +
                    "Permanent.PermanentFrame.FrameID (no frame/slot model, RD-P6C3-D1) — design item RD-EXT3-02.");
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
