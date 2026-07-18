// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT25_101 (Digimon / Black, Divine Arms Version (Omega))
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Black/BT25_101.cs (370 lines, 7 regions)
//    * Ignore Color Requirement :15-24  (timing None — CardEffectFactory.UseRequirements, HasTSTraits gate)
//    * Security                 :27-34  (SecuritySkill — AddActivateMainOptionSecurityEffect)
//    * Main                     :37-234 (OptionSkill — trash 1 [TS] hand card -> Draw2 -> optionally link
//                                          this card or 1 [TS] trash card to 1 of your Digimon, free)
//    * Link Condition           :239-247(timing None — AddSelfLinkConditionStaticEffect, Vulcanusmon, cost3)
//    * Link                     :251-256(OnDeclaration — CardEffectFactory.LinkEffect)
//    * Sec Atk+1/Reboot         :259-265(timing None — ChangeSelfSAttackStaticEffect +1 / RebootSelfStaticEffect,
//                                          both isLinkedEffect:true)
//    * All Turns                :268-364(WhenRemoveField — trash 1 link card to not leave the battle area,
//                                          isSkippable, isLinkedEffect)
//
// ② 프리미티브 매핑:
//    * P:UseRequirements, P:AddActivateMainOptionSecurityEffect, P:AddSelfLinkConditionStaticEffect,
//      P:LinkEffect, P:ChangeSelfSAttackStaticEffect, P:RebootSelfStaticEffect,
//      P:TrashHandAndProcessAccordingToResult, P:TrashLinkCardsAndProcessAccordingToResult
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect. [All Turns] → WhenRemoveField(AS-IS 그대로) +
//      CanTriggerWhenRemoveField.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`;
//      nested `IEnumerator ... { ...; yield return null; }` (실 await 없음) → `Task ... { ...; return
//      Task.CompletedTask; }` (BT8_092 idiom).
//    * `card.PermanentOfThisCard()`(값-동등/가변 지점: LinkedCards, willBeRemoveField, Hide*Effect 가드) →
//      `ICardEffect.ResolvePermanentOfThisCard(card)` (§2.3).
//    * `card.Owner.HandCards`/`TrashCards` → `new Player(card.Context, card.Owner).*`.
//    * `CardEffectCommons.TrashHandAndProcessAccordingToResult(player: card.Owner, ...)` — player 인자는
//      `Player` 인스턴스형(AS-IS bridge 시그니처, ProcessAccordingToResultBridge.cs:99) → `new
//      Player(card.Context, card.Owner)`.
//    * AS-IS :353-356 `thisPermanent.HideDeleteEffect()/HideHandBounceEffect()/HideDeckBounceEffect()/
//      HideWillRemoveFieldEffect()` = UI 연출(미러 부재, §2.6 스트립) — `willBeRemoveField = false`만
//      로드베어링 상태 플래그로 보존(§2.6, BT2_082:187 판례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_101 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ignore Color Requirement
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.UseRequirements(card, CardCondition));

            bool CardCondition(CardSource cardSource)
            {
                return cardSource.HasTSTraits;
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "[Security] By trashing 1 [TS] trait card from your hand, <Draw 2>, After you may link this card or 1 [TS] trait card from your trash to 1 of your Digimon on the field without paying the cost.");
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By trashing 1 [TS] trait card from hand, Draw 2, then you may link this card or 1 [TS] trait card in trash to 1 of your digimon without paying cost.", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Main] By trashing 1 [TS] trait card from your hand, <Draw 2> After, you may link this card or 1 [TS] trait card from your trash to 1 of your Digimon on the field without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool IsTsTraitCard(CardSource cardSource)
            {
                return cardSource.HasTSTraits;
            }

            bool PermanentThisCardCanLinkToCondition(Permanent permanent)
            {
                return permanent.IsDigimon
                    && CardEffectCommons.IsOwnerPermanent(permanent, card)
                    && card.CanLinkToTargetPermanent(permanent, false, true);
            }

            bool CanLinkTrashCardCondition(CardSource cardSource)
            {
                return cardSource.HasTSTraits
                    && cardSource.CanLink(false, true);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersHand(card, IsTsTraitCard))
                {
                    CardSource selectedCardToTrash = null;

                    #region Select Hand Card to Trash

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, IsTsTraitCard));

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsTsTraitCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCardToTrash = cardSource;
                        return Task.CompletedTask;
                    }

                    await selectHandEffect.Activate();
                    #endregion

                    if (selectedCardToTrash != null)
                    {
                        await CardEffectCommons.TrashHandAndProcessAccordingToResult(
                            player: new Player(card.Context, card.Owner),
                            hashtable: hashtable,
                            cardToTrash: selectedCardToTrash,
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null
                        );

                        async Task SuccessProcess(CardSource cardSource)
                        {
                            await new DrawClass(card.Context, card.Owner, 2, activateClass).Draw();

                            bool canSelectThisCard = CardEffectCommons.HasMatchConditionPermanent(card, PermanentThisCardCanLinkToCondition);
                            bool canSelectTrashCard = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanLinkTrashCardCondition);

                            if (canSelectThisCard || canSelectTrashCard)
                            {
                                #region Setup Location Selection
                                List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();

                                if (canSelectThisCard) selectionElements.Add(new(message: $"This Card", value: 1, spriteIndex: 0));
                                if (canSelectTrashCard) selectionElements.Add(new(message: $"[TS] Trait card from trash", value: 2, spriteIndex: 0));
                                selectionElements.Add(new(message: $"None", value: 3, spriteIndex: 1));

                                string selectPlayerMessage = "Will you link a card to 1 digimon on your field?";
                                string notSelectPlayerMessage = "The opponent is choosing to link a card to 1 digimon on their field.";

                                GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                                await GManager.instance.userSelectionManager.WaitForEndSelect();
                                #endregion

                                bool doLink = GManager.instance.userSelectionManager.SelectedIntValue != 3;
                                bool selectThisCard = GManager.instance.userSelectionManager.SelectedIntValue == 1;

                                if (doLink)
                                {
                                    Permanent selectedPermament = null;
                                    CardSource selectedCardToLink = null;

                                    Task SelectPermamentToLinkToCoroutine(Permanent permanent)
                                    {
                                        selectedPermament = permanent;
                                        return Task.CompletedTask;
                                    }

                                    if (selectThisCard)
                                    {
                                        selectedCardToLink = card;
                                    }
                                    else
                                    {
                                        Task SelectCardToLinkCoroutine(CardSource cardSource1)
                                        {
                                            selectedCardToLink = cardSource1;
                                            return Task.CompletedTask;
                                        }

                                        #region Select Trash Card to Link
                                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                        selectCardEffect.SetUp(
                                            canTargetCondition: CanLinkTrashCardCondition,
                                            canTargetCondition_ByPreSelecetedList: null,
                                            canEndSelectCondition: null,
                                            canNoSelect: () => true,
                                            selectCardCoroutine: SelectCardToLinkCoroutine,
                                            afterSelectCardCoroutine: null,
                                            message: "Select 1 [TS] trait card to link",
                                            maxCount: 1,
                                            canEndNotMax: false,
                                            isShowOpponent: true,
                                            mode: SelectCardEffect.Mode.Custom,
                                            root: SelectCardEffect.Root.Trash,
                                            customRootCardList: null,
                                            canLookReverseCard: true,
                                            selectPlayer: card.Owner,
                                            cardEffect: activateClass);

                                        selectCardEffect.SetUpCustomMessage("Select 1 [TS] trait card from your trash to link.", "The opponent is selecting 1 [TS] trait card from their trash to link.");
                                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected card");
                                        await selectCardEffect.Activate();
                                        #endregion
                                    }

                                    bool CanLinkChosenCardCondition(Permanent permanent)
                                    {
                                        return permanent.IsDigimon
                                        && CardEffectCommons.IsOwnerPermanent(permanent, card)
                                        && selectedCardToLink.CanLinkToTargetPermanent(permanent, false, true);
                                    }

                                    // (미러 idiom — BT17_026) SelectPermanentEffect.SetUp의 canTargetCondition은
                                    // id-형이므로 AS-IS Permanent-술어는 그대로 두고 id 어댑터를 덧댄다.
                                    Permanent? PermanentOfChosen(HeadlessEntityId id) =>
                                        card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                                            ? new Permanent(card.Context, id, rec.OwnerId)
                                            : null;

                                    bool CanLinkChosenCardConditionById(HeadlessEntityId id)
                                    {
                                        Permanent? permanent = PermanentOfChosen(id);
                                        return permanent is not null && CanLinkChosenCardCondition(permanent);
                                    }

                                    #region Select Digimon that will gain selected card as link
                                    if (selectedCardToLink != null)
                                    {
                                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanLinkChosenCardCondition))
                                        {
                                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                            selectPermanentEffect.SetUp(
                                                selectPlayer: card.Owner,
                                                canTargetCondition: CanLinkChosenCardConditionById,
                                                canTargetCondition_ByPreSelecetedList: null,
                                                canEndSelectCondition: null,
                                                maxCount: 1,
                                                canNoSelect: true,
                                                canEndNotMax: false,
                                                selectPermanentCoroutine: SelectPermamentToLinkToCoroutine,
                                                afterSelectPermanentCoroutine: null,
                                                mode: SelectPermanentEffect.Mode.Custom,
                                                cardEffect: activateClass);

                                            selectPermanentEffect.SetUpCustomMessage("Select 1 digimon to gain link.", "The opponent is selecting 1 Digimon to gain link.");
                                            await selectPermanentEffect.Activate();
                                        }
                                        #endregion
                                    }

                                    if (selectedPermament != null && selectedCardToLink != null) await selectedPermament.AddLinkCard(selectedCardToLink, activateClass.EffectSourceCard?.InstanceId);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Link Effects
        #region Link Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Vulcanusmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 3, card: card));
        }

        #endregion

        #region Link
        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #region Sec Atk +1/Reboot
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null, isLinkedEffect: true));

            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null, isLinkedEffect: true));
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 link card to not leave battle field", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] When this [Vulcanusmon] would leave the battle area, by trashing 1 of its link cards, it doesn't leave.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistLinked(card) &&
                       CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card) &&
                       ICardEffect.ResolvePermanentOfThisCard(card).TopCard.EqualsCardName("Vulcanusmon");
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistLinked(card) &&
                       !ICardEffect.ResolvePermanentOfThisCard(card).HasNoLinkCards;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return true;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                CardSource selectedLinkCard = null;
                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                #region Select Link Card to Trash
                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanSelectCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: SelectCardCoroutine,
                    message: "Select 1 link card to trash.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.LinkedCards,
                    customRootCardList: thisPermanent.LinkedCards,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                Task SelectCardCoroutine(List<CardSource> cardSources)
                {
                    if (cardSources.Count > 0)
                    {
                        selectedLinkCard = cardSources[0];
                    }

                    return Task.CompletedTask;
                }

                selectCardEffect.SetUpCustomMessage("Select 1 link card to trash.", "The opponent is selecting 1 link card to trash.");
                await selectCardEffect.Activate();
                #endregion

                if (selectedLinkCard != null)
                {
                    await CardEffectCommons.TrashLinkCardsAndProcessAccordingToResult(
                        targetPermanent: thisPermanent,
                        targetLinkCards: new List<CardSource>() { selectedLinkCard },
                        activateClass: activateClass,
                        successProcess: SuccessProcess,
                        failureProcess: null);

                    async Task SuccessProcess(List<CardSource> cardSources)
                    {
                        thisPermanent.willBeRemoveField = false;

                        // AS-IS :353-356 HideDeleteEffect/HideHandBounceEffect/HideDeckBounceEffect/
                        // HideWillRemoveFieldEffect = UI 연출(미러 부재) — 스트립(§2.6). 로드베어링 상태
                        // 플래그(willBeRemoveField = false)만 보존.

                        await Task.CompletedTask;
                    }
                }

            }
        }
        #endregion
        #endregion

        return cardEffects;
    }
}
