// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT25_096 "Mirage Beast Knight" (Digimon+Option / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Blue/BT25_096.cs
//    * BeforePayCost : 테이머 밑 face-down 카드 트래시로 use cost -2
//      (PRIMARY covered element: TrashDigivolutionCardsAndProcessAccordingToResult).
//    * None          : ChangeCostClass -2 (not shown, availability 계산용).
//    * OptionSkill   : [Main] 트래시의 [Gaogamon]+[MachGaogamon]을 [Gaomon] 밑에 배치 후 [MirageGaogamon]으로 진화.
//    * SecuritySkill : [Security] 손패/트래시에서 [Gaomon]/[Thomas H. Norstein] 무료 플레이 후 이 카드 손패로.
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `permanent.HasFaceDownDigivolutionCards` — 미러 read-side fold(coverage-exemplar 1-surface, Permanent.cs).
//    * AS-IS `DigivolutionCards.Last(x => x.IsFaceDown)`; 미러엔 CardSource.IsFaceDown(=>IsFlipped) 미러링 없어
//      정의상 동일한 `x.IsFlipped`로 조립(IsFaceDown ≡ IsFlipped, AS-IS CardSource.cs:66-72).
//    * `if (card.Owner.CanReduceCost(null, card)) ContinuousController.instance.PlaySE(...BuffSE)` = SE 연출
//      (스트립, ST17_13/EX4_062 판례).
//    * `card.Owner.UntilCalculateFixedCostEffect.Add(...)` → `new Player(card.Context, card.Owner)...`(BT25_072).
//    * `card.Owner.GetBattleAreaDigimons()` → HeadlessPlayerId W4 확장. SelectPermanentEffect canTargetCondition
//      id-형 → id 어댑터. `selectedPermanent.AddDigivolutionCardsBottom(list, activateClass)` → `(list,
//      activateClass.EffectSourceCard?.InstanceId)`.
//    * `AddThisCardToHand(card, activateClass)` → 미러 `(card, activateClass.EffectSourceCard)`(BT9_109 idiom).
//    * `List<CardSource>.Find(...)`(암시적 bool) → `... != null`(미러 CardSource엔 implicit bool 없음).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Blue;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_096 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Reduce Play Cost

        bool SharedCanSelectTamerCondition(Permanent permanent)
            => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaTamer(permanent, card)
                && permanent.HasFaceDownDigivolutionCards;

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reduce Play Cost -2", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "When this card would be used, by trashing the bottom face-down card from under any of your Tamers, reduce the use cost by 2.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, cardSource => cardSource == card)
                    && CardEffectCommons.HasMatchConditionOwnersPermanent(card, SharedCanSelectTamerCondition);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, SharedCanSelectTamerCondition))
                {
                    Permanent? selectedPermanent = null;

                    #region Select Tamer to trash bottom face down digivolution card
                    if (CardEffectCommons.HasMatchConditionPermanent(card, SharedCanSelectTamerCondition))
                    {
                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: SharedCanSelectTamerCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
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

                        selectPermanentEffect.SetUpCustomMessage("Select 1 tamer to trash bottom face down digivolution card.", "The opponent is selecting 1 tamer to trash bottom face down digivolution card.");
                        await selectPermanentEffect.Activate();
                    }
                    #endregion

                    if (selectedPermanent != null)
                    {
                        CardSource cardToTrash = selectedPermanent.DigivolutionCards.Last(x => x.IsFlipped);
                        await CardEffectCommons.TrashDigivolutionCardsAndProcessAccordingToResult(
                            targetPermanent: selectedPermanent,
                            targetDigivolutionCards: new List<CardSource>() { cardToTrash },
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null);

                        async Task SuccessProcess(List<CardSource> trashedCards)
                        {
                            // AS-IS `if (card.Owner.CanReduceCost(null, card)) PlaySE(...BuffSE)` = SE 연출(스트립).

                            ChangeCostClass changeCostClass = new ChangeCostClass();
                            changeCostClass.SetUpICardEffect("Play Cost -2", CanUseCondition1, card);
                            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                            await CardEffectCommons.ShowReducedCost(_hashtable);

                            bool CanUseCondition1(Hashtable hashtable) => true;

                            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                            {
                                if (CardSourceCondition(cardSource)
                                && RootCondition(root)
                                && PermanentsCondition(targetPermanents))
                                {
                                    Cost -= 2;
                                }

                                return Cost;
                            }

                            bool PermanentsCondition(List<Permanent> targetPermanents)
                            {
                                return targetPermanents == null
                                        || targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0;
                            }

                            bool CardSourceCondition(CardSource cardSource)
                                => cardSource != null
                                    && cardSource == card;

                            bool RootCondition(SelectCardEffect.Root root) => true;

                            bool isUpDown() => true;
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect("Play Cost -2", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => true, isChangePayingCost: () => true);
            changeCostClass.SetNotShowUI(true);
            cardEffects.Add(changeCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnHand(card)
                    && CardEffectCommons.HasMatchConditionOwnersPermanent(card, SharedCanSelectTamerCondition);
            }

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        Cost -= 2;
                    }
                }

                return Cost;
            }

            bool CardSourceCondition(CardSource cardSource)
                => cardSource != null && cardSource == card;

            bool RootCondition(SelectCardEffect.Root root) => true;

            bool isUpDown() => true;
        }

        #endregion

        #region Main

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By placing 1 [Gaogamon] and 1 [MachGaogamon] from trash as 1 [Gaomon] bottom sources, may digivolve into [MirageGaogamon] without cost or digivole requirements", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Main] By placing 1 [Gaogamon] and 1 [MachGaogamon] from your trash as 1 of your [Gaomon]'s bottom digivolution cards, that Digimon may digivolve into [MirageGaogamon] in the hand, ignoring digivolution requirements and without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                #region Conditions

                bool IsGaomon(Permanent permanent)
                    => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                        && permanent.TopCard.EqualsCardName("Gaomon");

                bool IsGaogamon(CardSource cardSource)
                    => cardSource.EqualsCardName("Gaogamon");

                bool IsMachGaogamon(CardSource cardSource)
                    => cardSource.EqualsCardName("MachGaogamon");

                bool isMirageGaogamon(CardSource cardSource)
                    => cardSource.EqualsCardName("MirageGaogamon");

                #endregion

                if (!(CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsGaogamon) && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsMachGaogamon))) return;
                Permanent? selectedPermanent = null;

                #region Select Gaomon
                if (CardEffectCommons.MatchConditionOwnersPermanentCount(card, IsGaomon) > 1)
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersPermanentCount(card, IsGaomon));

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsGaomon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 [Gaomon] to gain digivolution sources", "The opponent is selecting 1 [Gaomon] to gain digivolution sources.");
                    await selectPermanentEffect.Activate();
                }
                if (CardEffectCommons.MatchConditionOwnersPermanentCount(card, IsGaomon) == 1)
                    selectedPermanent = card.Owner.GetBattleAreaDigimons().Find(IsGaomon);
                #endregion

                if (selectedPermanent != null)
                {
                    List<CardSource> selectedTrashCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedTrashCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    #region Select Gaogamon
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsGaogamon))
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, IsGaogamon));

                        selectCardEffect.SetUp(
                            canTargetCondition: IsGaogamon,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 [Gaogamon] card",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 [Gaogamon] card to place under [Gaomon].", "The opponent is selecting a [Gaogamon] card to place under [Gaomon].");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected [Gaogamon]");
                        await selectCardEffect.Activate();
                    }
                    #endregion

                    #region Select MachGaogamon
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsMachGaogamon) && selectedTrashCards.Find(IsGaogamon) != null)
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, IsMachGaogamon));

                        selectCardEffect.SetUp(
                            canTargetCondition: IsMachGaogamon,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 [MachGaogamon] card",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 [MachGaogamon] card to place under [Gaomon].", "The opponent is selecting a [MachGaogamon] card to place under [Gaomon].");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected [MachGaogamon]");
                        await selectCardEffect.Activate();
                    }
                    #endregion

                    if (selectedTrashCards.Find(IsGaogamon) != null && selectedTrashCards.Find(IsMachGaogamon) != null)
                    {
                        await selectedPermanent.AddDigivolutionCardsBottom(
                            addedDigivolutionCards: selectedTrashCards,
                            causeEffectSourceId: activateClass.EffectSourceCard?.InstanceId);

                        await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                            targetPermanent: selectedPermanent,
                            cardCondition: isMirageGaogamon,
                            payCost: false,
                            reduceCostTuple: null,
                            fixedCostTuple: null,
                            ignoreDigivolutionRequirementFixedCost: 1,
                            isHand: true,
                            activateClass: activateClass,
                            successProcess: null);

                    }
                }

            }
        }

        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 [Gaomon]/[Thomas H. Norstein] from hand or trash, then add this to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Security] You may play 1 [Gaomon] or [Thomas H. Norstein] from your hand or trash without paying the cost. Then, add this card to the hand.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return (cardSource.EqualsCardName("Gaomon") || cardSource.EqualsCardName("Thomas H. Norstein"))
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass);
            }

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool canPlayFromHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition);
                bool canPlayFromTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition);

                if (canPlayFromHand || canPlayFromTrash)
                {

                    List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();

                    if (canPlayFromHand) selectionElements.Add(new SelectionElement<int>("Play from hand", 1, 0));
                    if (canPlayFromTrash) selectionElements.Add(new SelectionElement<int>("Play from trash", 2, 0));
                    selectionElements.Add(new SelectionElement<int>("Dont play a card", 3, 1));


                    string selectPlayerMessage = "Will you play a [Gaomon] or [Thomas H. Norstein]?";
                    string notSelectPlayerMessage = "The opponent is choosing whether to play a [Gaomon] or [Thomas H. Norstein]";


                    GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool playFromHand = GManager.instance.userSelectionManager.SelectedIntValue == 1;
                    bool dontPlay = GManager.instance.userSelectionManager.SelectedIntValue == 3;

                    if (!dontPlay)
                    {
                        CardSource? selectedCard = null;
                        SelectCardEffect.Root selectedRoot = SelectCardEffect.Root.None;

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCard = cardSource;
                            return Task.CompletedTask;
                        }

                        if (playFromHand)
                        {
                            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, CanSelectCardCondition));
                            SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                            selectHandEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanSelectCardCondition,
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

                            selectHandEffect.SetUpCustomMessage("Select 1 [Gaomon] or [Thomas H. Norstein] to play.", "The opponent is selecting 1 [Gaomon] or [Thomas H. Norstein] to play.");
                            selectHandEffect.SetUpCustomMessage_ShowCard("Selected Card");
                            await selectHandEffect.Activate();
                            selectedRoot = SelectCardEffect.Root.Hand;
                        }
                        else
                        {
                            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, CanSelectCardCondition));
                            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                            selectCardEffect.SetUp(
                                        canTargetCondition: CanSelectCardCondition,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        canNoSelect: () => true,
                                        selectCardCoroutine: SelectCardCoroutine,
                                        afterSelectCardCoroutine: null,
                                        message: "Select 1 [Gaomon] or [Thomas H. Norstein] to play",
                                        maxCount: maxCount,
                                        canEndNotMax: false,
                                        isShowOpponent: true,
                                        mode: SelectCardEffect.Mode.Custom,
                                        root: SelectCardEffect.Root.Trash,
                                        customRootCardList: null,
                                        canLookReverseCard: true,
                                        selectPlayer: card.Owner,
                                        cardEffect: activateClass);

                            selectCardEffect.SetUpCustomMessage("Select 1 [Gaomon] or [Thomas H. Norstein] to play.", "The opponent is selecting 1 [Gaomon] or [Thomas H. Norstein] to play.");
                            selectCardEffect.SetUpCustomMessage_ShowCard("Selected Card");
                            await selectCardEffect.Activate();
                            selectedRoot = SelectCardEffect.Root.Trash;
                        }

                        if (selectedCard != null && selectedRoot != SelectCardEffect.Root.None)
                            await CardEffectCommons.PlayPermanentCards(
                                cardSources: new List<CardSource>() { selectedCard },
                                activateClass: activateClass,
                                payCost: false,
                                isTapped: false,
                                root: selectedRoot,
                                activateETB: true
                            );



                    }

                }

                await CardEffectCommons.AddThisCardToHand(card, activateClass.EffectSourceCard);
            }
        }
        #endregion

        return cardEffects;
    }
}
