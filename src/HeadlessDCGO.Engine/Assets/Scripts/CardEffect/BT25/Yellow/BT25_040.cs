// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 카드 (수확 트랜치) — MagnaAngemon (BT25_040, Digimon / Yellow / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Yellow/BT25_040.cs (268 lines, 5 regions)
//    * [None] Digivolution Condition :17-27  (AddSelfDigivolutionRequirementStaticEffect: TS-트레이트 위 Lv.4 코스트3)
//    * [When Trashed] OnDiscardSecurity :29-84 (시큐리티 삭제 시 손패 Lv.4↓ [Angel]/[Iliad] 무료 플레이)
//    * [Ascension] OnDestroyedAnyone :86-91 (AscensionSelfEffect)
//    * Shared OP/WD                 :95-192 (ActivateClassesForSharedEffects: 시큐리티 톱/밑 1장 trash→상대 1체 -8000)
//    * [All Turns] OnLoseSecurity    :194-262 (inherited: 시큐리티 소실 시 상대 1체 -4000)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #9, 4축):
//    * K:Ascension                          — [Ascension] AscensionSelfEffect (AS-IS :89; KeyWordEffects/Ascension)
//    * P:TrashSecurityAndProcessAccordingToResult — Shared 몸통 (AS-IS :131)
//    * T:OnDiscardSecurity                  — [When Trashed] 트리거 (AS-IS :30; CanTriggerOnTrashSelfSecurity)
//    * T:OnLoseSecurity                     — [All Turns] 트리거 (AS-IS :196; CanTriggerWhenLoseSecurity)
//    * (+P:ActivateClassesForSharedEffects 공유 once-per-turn, ChangeDigimonDP, SelectHandEffect PlayForFree,
//       AddSelfDigivolutionRequirementStaticEffect, userSelectionManager Int-selection)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Trashed] → OnDiscardSecurity + CanTriggerOnTrashSelfSecurity(cardEffect != null) 게이트(AS-IS :43-46).
//      (MatchStateMutationSink이 effect-caused Security→Trash CardMoved에서 OnDiscardSecurity 파생 — cause id 스레드.)
//    * [Ascension] → OnDestroyedAnyone(삭제-전 수집 창; DeletionReplacementGate가 AscensionSelfEffect 해소).
//    * Shared OP/WD → CardEffectFactory.ActivateClassesForSharedEffects(onPlay+whenDigivolving) 그대로(AS-IS :183-191).
//    * [All Turns] → OnLoseSecurity + CanTriggerWhenLoseSecurity + SetIsInheritedEffect(true)(AS-IS :202-210).
//
// 수확(적중/빗나감): 감사 §6은 이 카드를 Ascension STOP(RD-3A-01/RD-P6C3-A3) 및 공유 once-per-turn STOP(P2-9)
//    후보로 예측했으나 — 두 예측 모두 BUSTED: (a) AscensionSelfEffect→AscensionProcess는 RD-P6C2-1 상환 완료
//    (CanAddSecurity→bool선택→CardObjectController.AddSecurityCard, 미러 클린)이며 DeletionReplacementGate가
//    OnDestroyedAnyone 창에서 해소; (b) ActivateClassesForSharedEffects는 순수 디스패처(STOP 없음). 5팔 전부 포팅.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask (BT8_092 idiom).
//    * `permanent.TopCard.HasTSTraits`(AS-IS CardSource.cs:3739 = EqualsTraits("TS")) → `EqualsTraits("TS")`.
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId; Player 핸들은 `new Player(card.Context, card.Owner)`(BT2_023 idiom).
//    * SelectHandEffect/SelectPermanentEffect = `GManager.instance.GetComponent<...>()`; SelectPermanentEffect의
//      canTargetCondition는 정본 Func<Permanent,bool> — Permanent 술어 직결(id 어댑터 없음).
//    * card-less `HasMatchConditionPermanent(cond)` → `(card, cond)` 오버로드(BT9_109 관례).
//    * `TrashSecurityAndProcessAccordingToResult(player: card.Owner, ...)` → player: `new Player(...)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_040 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static Effects

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool Condition(Permanent permanent)
            {
                // AS-IS :22 `permanent.TopCard.HasTSTraits` = EqualsTraits("TS") (CardSource.cs:3739).
                return permanent.TopCard.EqualsTraits("TS");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 4, permanentCondition: Condition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region When Trashed
        if (timing == EffectTiming.OnDiscardSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or lower [Angel] or [Iliad] card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "When effects trashing this card from the security stack, you may play 1 level 4 or lower [Angel] or [Iliad] trait card from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnTrashSelfSecurity(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanPlayCardCondition(CardSource cardSource)
            {
                return cardSource.HasPlayCost
                    && cardSource.HasLevel
                    && cardSource.Level <= 4
                    && (cardSource.EqualsTraits("Angel") || cardSource.EqualsTraits("Iliad"))
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, CanPlayCardCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanPlayCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.PlayForFree,
                    cardEffect: activateClass);

                await selectHandEffect.Activate();
            }
        }
        #endregion

        #region Ascension
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.AscensionSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #endregion

        #region Shared OP/WD
        string SharedEffectName = "By trashing top or bottom security card, 1 opponent digimon gains -8k DP until their turn ends";

        string SharedEffectDescription(string tag) => $"[{tag}] By trashing your top or bottom security card, 1 of your opponent's Digimon gets -8000 DP until their turn ends.";

        bool CanSelectPermanentCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            Player owner = new Player(card.Context, card.Owner);
            bool requiresSelection = owner.SecurityCards.Count >= 2;

            if (owner.SecurityCards.Count > 0)
            {
                #region Setup Location Selection
                List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();
                if (requiresSelection)
                {
                    selectionElements.Add(new SelectionElement<int>("Top", 1, 0));
                    selectionElements.Add(new SelectionElement<int>("Bottom", 2, 0));
                }
                else selectionElements.Add(new SelectionElement<int>("Trash only security card", 1, 0));
                selectionElements.Add(new SelectionElement<int>("Don't trash", 3, 1));

                string selectPlayerMessage = "Will you trash your security?";
                string notSelectPlayerMessage = "The opponent is choosing to trash their security";

                GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                await GManager.instance.userSelectionManager.WaitForEndSelect();
                #endregion

                bool doTrash = GManager.instance.userSelectionManager.SelectedIntValue != 3;
                bool fromTop = GManager.instance.userSelectionManager.SelectedIntValue == 1;

                if (doTrash)
                {
                    await CardEffectCommons.TrashSecurityAndProcessAccordingToResult(
                        player: owner,
                        trashAmount: 1,
                        activateClass: activateClass,
                        fromTop: fromTop,
                        successProcess: SuccessProcess,
                        failureProcess: null);

                    async Task SuccessProcess(List<CardSource> cardSources)
                    {
                        Permanent? selectedPermanent = null;

                        #region Select Opponent's Digimon
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOpponentsPermanentCount(card, CanSelectPermanentCondition));

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

                            Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                selectedPermanent = permanent;
                                return Task.CompletedTask;
                            }

                            selectPermanentEffect.SetUpCustomMessage("Select 1 digimon to give -8K DP", "The opponent is selecting a digimon to give -8K DP");
                            await selectPermanentEffect.Activate();
                        }
                        #endregion

                        if (selectedPermanent != null) await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: selectedPermanent,
                            changeValue: -8000,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        CardEffectFactory.ActivateClassesForSharedEffects
            (ref cardEffects, timing, card,
                SharedEffectName,
                SharedActivateCoroutine,
                SharedEffectDescription,
                optional: false,
                isSkippable: true,
                onPlay: true,
                whenDigivolving: true);
        #endregion

        #region All Turns

        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("1 of your opponent's Digimon gets -4000 DP", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_040_WST_AT");
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription() => "[All Turns] [Once Per Turn] When your security stack is removed from, 1 of your opponent's Digimon gets -4000 DP for the turn.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, player => player.PlayerId == card.Owner);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    Permanent? selectedPermanent = null;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -4000.",
                        "The opponent is selecting 1 Digimon that will get DP -4000.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: selectedPermanent,
                            changeValue: -4000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
