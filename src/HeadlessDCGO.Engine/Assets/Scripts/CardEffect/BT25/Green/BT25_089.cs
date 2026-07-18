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
//    * (+K:Link [Main] link 실행: STOP; PlaySelfTamerSecurityEffect: 클린)
//
// ③ 배선 관례 근거: [Main] → OnDeclaration + ActivateClass; [Security] → SecuritySkill factory;
//    [Start of Main] / [End of Turn] → 해당 timing 직접.
//
// 수확 명세 (STOP 팔 2 — 예측 적중, coverage_exemplar_audit §6 "Burst/AppFusion select"·"DigiXros/Link"):
//   ▸ [Main] / Link (RD-EXT3-01): suspend-cost(SuspendPeremanentAndProcessAccordingToResult) 절반은 클린 포팅.
//     link 실행 절반은 STOP — 두 독립 갭: (1) AS-IS `new ILinkCard(true, cardSource, permanent, activateClass)
//     .LinkCard()` — 미러에 `ILinkCard` 타입 부재(컴파일 블로커); 매핑 팩토리 경로(Link.cs:79-86 LinkEffect)는
//     RD-P6C2-7 STOP(WhenWouldLink 창 + link-cost 지불 + IPlacePermanentToLinkCards 전부 미이관). (2) 후보
//     술어 `CanLink(payCost:true)`(CardSource.cs:1424)는 C2-02/MIG5-CANLINK-PAYCOST throw(GetChangedLinkCost
//     프리미티브 부재). SuccessProcess 몸통을 STOP 마커로 봉인, AS-IS 본문은 주석 보존.
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

            bool CanLinkCardCondition(CardSource cardSource, bool payCost)
            {
                return cardSource.IsDigimon
                    && cardSource.EqualsTraits("Appmon")
                    && cardSource.CanLink(payCost);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass,
                    SuccessProcess,
                    null);

                Task SuccessProcess(List<Permanent> suspendedPermaments)
                {
                    // STOP (design item RD-EXT3-01): the suspend-cost above is clean; the link RESOLUTION is not.
                    // The AS-IS SuccessProcess (preserved verbatim below as comment) grants GrantedReduceLinkCostClass
                    // (clean), opens an Int-area selection + SelectHand/SelectPermanent/SelectCard (clean), then
                    // executes `new ILinkCard(true, cardSource, permanent, activateClass).LinkCard()`. The mirror has
                    // NO `ILinkCard` type (compile blocker) and the mapped factory path throws RD-P6C2-7 (WhenWouldLink
                    // window + link-cost payment + IPlacePermanentToLinkCards all unported); the effect-side candidate
                    // predicate `CanLink(payCost: true)` throws C2-02/MIG5-CANLINK-PAYCOST (no GetChangedLinkCost
                    // primitive). Kept as an AS-IS-named comment for the eventual link-subsystem port:
                    //
                    //   card.Owner.UntilCalculateFixedCostEffect.Add(GetCardEffect);  // GrantedReduceLinkCostClass(reducedCost:2)
                    //   ... SetIntSelection("From hand"/"From digivolution cards"/"Do not Link") ...
                    //   if (doLink) { SelectHandEffect / SelectPermanentEffect+SelectCardEffect(Root.DigivolutionCards) }
                    //   -> SelectCardCoroutine -> SelectPermanentEffect(CanLinkToTargetPermanent)
                    //   -> new ILinkCard(true, cardSource, permanent, activateClass).LinkCard();
                    //   card.Owner.UntilCalculateFixedCostEffect.Remove(GetCardEffect);
                    throw new NotSupportedException(
                        "STOP: BT25_089 [Main] link resolution needs ILinkCard (no mirror type) + CanLink(payCost:true) " +
                        "(no GetChangedLinkCost primitive) — RD-P6C2-7 / C2-02, design item RD-EXT3-01.");
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
