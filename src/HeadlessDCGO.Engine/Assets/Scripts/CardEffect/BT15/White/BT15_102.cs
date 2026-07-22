// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT15_102 (Digimon / White)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT15/White/BT15_102.cs (3 regions)
//    * BeforePayCost : 배틀에어리어/트래시의 서로 다른 이름 [Dark Masters] 최대 3장을 밑에 배치, 장당 play cost -4
//      (PRIMARY covered element: SelectDigiXros — SelectDigiXrosClass.AddDigivolutionCardInfos).
//    * None          : ChangeCostClass -4 (not shown, availability 계산용).
//    * OnEndTurn     : 트래시의 level≤6 1장을 밑 진화원으로 배치 후 그 [On Play] 효과 1개 발동, level6 진화원 수만큼
//      상대 덱 top 2장씩 트래시.
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X`.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `card.Owner.TrashCards` → `new Player(card.Context, card.Owner).TrashCards`;
//      `cardSource.Owner.MaxMemoryCost` → `new Player(card.Context, cardSource.Owner).MaxMemoryCost`.
//    * `if (card.Owner.CanReduceCost(null, card)) PlaySE(...BuffSE)` = SE 연출(스트립, ST17_13 판례).
//    * `card.Owner.UntilCalculateFixedCostEffect.Add(...)` → `new Player(...)...`(BT25_072 idiom).
//    * On Play 효과 재발화 블록: EffectList_ForCard/ActivateICardEffect/SkillInfo/Activate_Optional_Effect_Execute
//      전부 미러 1:1(BT22_040 idiom). UI 데코 SetNotShowCard/SetUpSkillInfos 유지.
//    * `new IAddTrashCardsFromLibraryTop(trashCount, card.Owner.Enemy, activateClass)` → 미러 ctor
//      `(card.Context, CardEffectCommons.OpponentOf(card), trashCount, activateClass)`.
//    * `selectedPermanent.AddDigivolutionCardsBottom(list, activateClass)` → `(list, activateClass
//      .EffectSourceCard?.InstanceId)`.
//    * AS-IS `CardSource.CardID`(=CEntity_Base.CardID, 인쇄 카드번호로 "서로 다른 이름" 중복제거) → 미러
//      `CardSource.CardNumber`(=Definition.CardNumber; identity 비교의 확립된 매핑, CardSource.cs:2465).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.White;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT15_102 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Before Pay Cost - Condition Effect

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Placing 1 [Dark Masters] to get Play Cost -4", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("PlayCost-12_BT15_102");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When this card would be played, by placing up to 3 [Dark Masters] trait cards with different names from your battle area or trash under it, reduce the play cost by 4 for each one.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.CardTraits.Contains("Dark Masters") || cardSource.CardTraits.Contains("DarkMasters"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectCardConditionPermenant(Permanent permanent)
            {
                if (permanent.IsDigimon)
                {
                    if (permanent.TopCard.ContainsTraits("Dark Masters") || permanent.TopCard.ContainsTraits("DarkMasters"))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    if (CardEffectCommons.IsExistOnHand(cardSource))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanNoSelect(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.PayingCost(SelectCardEffect.Root.Hand, null, checkAvailability: false) > new Player(card.Context, cardSource.Owner).MaxMemoryCost)
                    {
                        return false;
                    }
                }

                return true;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition))
                {
                    return true;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectCardConditionPermenant))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<CardSource> digivolutionCards = new List<CardSource>();

                bool CanSelectTrashCardCondition(CardSource cardSource)
                {
                    if (CanSelectCardCondition(cardSource))
                    {
                        if (digivolutionCards.Count((filteredCard) => filteredCard.CardNumber == cardSource.CardNumber) == 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectTrashCardCondition(cardSource)))
                {
                    bool noSelect = CanNoSelect(CardEffectCommons.GetCardFromHashtable(_hashtable));
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = Math.Min(3 - digivolutionCards.Count, new Player(card.Context, card.Owner).TrashCards.Count((cardSource) => CanSelectTrashCardCondition(cardSource)));

                    if (maxCount >= 1)
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectTrashCardCondition,
                            canTargetCondition_ByPreSelecetedList: CanTargetCondition_ByPreSelecetedList,
                            canEndSelectCondition: CanEndSelectCondition,
                            canNoSelect: () => noSelect,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select cards to place in Digivolution cards.",
                            maxCount: maxCount,
                            canEndNotMax: true,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        await selectCardEffect.Activate();

                        bool CanTargetCondition_ByPreSelecetedList(List<CardSource> cardSources, CardSource cardSource)
                        {
                            List<string> cardIDs = new List<string>();

                            foreach (CardSource cardSource1 in cardSources)
                            {
                                if (!cardIDs.Contains(cardSource1.CardNumber))
                                {
                                    cardIDs.Add(cardSource1.CardNumber);
                                }
                            }

                            foreach (CardSource cardSource1 in digivolutionCards)
                            {
                                if (!cardIDs.Contains(cardSource1.CardNumber))
                                {
                                    cardIDs.Add(cardSource1.CardNumber);
                                }
                            }

                            if (cardIDs.Contains(cardSource.CardNumber))
                            {
                                return false;
                            }

                            return true;
                        }

                        bool CanEndSelectCondition(List<CardSource> cardSources)
                        {
                            List<string> cardIDs = new List<string>();

                            foreach (CardSource cardSource1 in cardSources)
                            {
                                if (!cardIDs.Contains(cardSource1.CardNumber))
                                {
                                    cardIDs.Add(cardSource1.CardNumber);
                                }
                            }

                            foreach (CardSource cardSource1 in digivolutionCards)
                            {
                                if (!cardIDs.Contains(cardSource1.CardNumber))
                                {
                                    cardIDs.Add(cardSource1.CardNumber);
                                }
                            }

                            if (cardIDs.Count != cardSources.Count + digivolutionCards.Count)
                            {
                                return false;
                            }

                            return true;
                        }

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            digivolutionCards.Add(cardSource);
                            return Task.CompletedTask;
                        }

                        if (selectedCards.Count >= 1)
                        {
                            GManager.instance.GetComponent<SelectDigiXrosClass>().AddDigivolutionCardInfos(new AddDigivolutionCardsInfo(activateClass, selectedCards));

                            await AfterSelectCardCoroutine(selectedCards);
                        }
                    }
                }

                async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    // AS-IS `if (card.Owner.CanReduceCost(null, card)) PlaySE(...BuffSE)` = SE 연출(스트립).

                    ChangeCostClass changeCostClass = new ChangeCostClass();
                    changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition1, card);
                    changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                    new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                    bool CanUseCondition1(Hashtable hashtable)
                    {
                        return true;
                    }

                    int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                    {
                        if (CardSourceCondition(cardSource))
                        {
                            if (RootCondition(root))
                            {
                                if (PermanentsCondition(targetPermanents))
                                {
                                    Cost -= cardSources.Count * 4;
                                }
                            }
                        }

                        return Cost;
                    }

                    bool PermanentsCondition(List<Permanent> targetPermanents)
                    {
                        if (targetPermanents == null)
                        {
                            return true;
                        }
                        else
                        {
                            if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    bool CardSourceCondition(CardSource cardSource)
                    {
                        return cardSource == card;
                    }

                    bool RootCondition(SelectCardEffect.Root root)
                    {
                        return true;
                    }

                    bool isUpDown()
                    {
                        return true;
                    }

                    await CardEffectCommons.ShowReducedCost(_hashtable);
                }
            }
        }

        #endregion

        #region Reduce Play Cost - Not Shown

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => true, isChangePayingCost: () => true);
            changeCostClass.SetNotShowUI(true);
            cardEffects.Add(changeCostClass);

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.CardTraits.Contains("Dark Masters") || cardSource.CardTraits.Contains("DarkMasters"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    ICardEffect? activateClass = card.EffectList(EffectTiming.BeforePayCost).Find(cardEffect => cardEffect.EffectName == "Placing 1 [Dark Masters] to get Play Cost -4");

                    if (activateClass != null)
                    {
                        if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        if (PermanentsCondition(targetPermanents))
                        {
                            List<CardSource> trashSources = new Player(card.Context, card.Owner).TrashCards.Filter(CanSelectCardCondition);
                            int targetCount = (from trashCard in trashSources select trashCard.CardNumber).Distinct().Count();

                            Cost -= targetCount * 4;
                        }
                    }
                }

                return Cost;
            }

            bool PermanentsCondition(List<Permanent> targetPermanents)
            {
                if (targetPermanents == null)
                {
                    return true;
                }
                else
                {
                    if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource == card)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return true;
            }

            bool isUpDown()
            {
                return true;
            }
        }

        #endregion

        #region End of Your Turn

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place 1 digimon from trash under this Digimon's digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Your Turn] [Once Per Turn] By placing 1 level 6 or lower card from your trash as this Digimon's bottom digivolution card, activate 1 [On Play] effect on that card as an effect. Then, trash the top 2 cards of your opponent's deck for each of this Digimon's level 6 digivolution cards.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasLevel)
                    {
                        if (cardSource.Level <= 6)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
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
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count((cardSource) => CanSelectCardCondition(cardSource)));

                        List<CardSource> selectedCards = new List<CardSource>();

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);

                            return Task.CompletedTask;
                        }

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to place on bottom of digivolution cards.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage(
                            "Select 1 card to place on bottom of digivolution cards.",
                            "The opponent is selecting 1 card to place on bottom of digivolution cards.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Digivolution Card");

                        await selectCardEffect.Activate();

                        CardSource? selectedCard = null;

                        if (selectedCards.Count >= 1)
                        {
                            if (CardEffectCommons.IsExistOnBattleArea(card))
                            {
                                await ICardEffect.ResolvePermanentOfThisCard(card).AddDigivolutionCardsBottom(
                                    selectedCards,
                                    activateClass.EffectSourceCard?.InstanceId);

                                selectedCard = selectedCards[0];
                            }
                        }

                        if (selectedCard != null)
                        {
                            List<ICardEffect> candidateEffects = selectedCard.EffectList_ForCard(EffectTiming.OnEnterFieldAnyone, card)
                                .Clone()
                                .Filter(cardEffect => cardEffect != null && cardEffect is ActivateICardEffect && !cardEffect.IsSecurityEffect && cardEffect.IsOnPlay);

                            if (candidateEffects.Count >= 1)
                            {
                                ICardEffect? selectedEffect = null;

                                if (candidateEffects.Count == 1)
                                {
                                    selectedEffect = candidateEffects[0];
                                }
                                else
                                {
                                    List<SkillInfo> skillInfos = candidateEffects
                                        .Map(cardEffect => new SkillInfo(cardEffect, null, EffectTiming.None));

                                    List<CardSource> cardSources = candidateEffects
                                        .Map(cardEffect => cardEffect.EffectSourceCard);

                                    SelectCardEffect selectSourceCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                    selectSourceCardEffect.SetUp(
                                        canTargetCondition: (cardSource) => true,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        canNoSelect: () => false,
                                        selectCardCoroutine: null,
                                        afterSelectCardCoroutine: null,
                                        message: "Select 1 effect to activate.",
                                        maxCount: 1,
                                        canEndNotMax: false,
                                        isShowOpponent: false,
                                        mode: SelectCardEffect.Mode.Custom,
                                        root: SelectCardEffect.Root.Custom,
                                        customRootCardList: cardSources,
                                        canLookReverseCard: true,
                                        selectPlayer: card.Owner,
                                        cardEffect: activateClass);

                                    selectSourceCardEffect.SetNotShowCard();
                                    selectSourceCardEffect.SetUpSkillInfos(skillInfos);
                                    selectSourceCardEffect.SetUpAfterSelectIndexCoroutine(AfterSelectIndexCoroutine);

                                    await selectSourceCardEffect.Activate();

                                    Task AfterSelectIndexCoroutine(List<int> selectedIndexes)
                                    {
                                        if (selectedIndexes.Count == 1)
                                        {
                                            selectedEffect = candidateEffects[selectedIndexes[0]];
                                        }
                                        return Task.CompletedTask;
                                    }
                                }

                                if (selectedEffect != null)
                                {
                                    Hashtable effectHashtable = CardEffectCommons.OnPlayCheckHashtableOfCard(card);

                                    if (selectedEffect.CanUse(effectHashtable))
                                    {
                                        await ((ActivateICardEffect)selectedEffect).Activate_Optional_Effect_Execute(effectHashtable);
                                    }
                                }
                            }
                        }
                    }
                }

                int trashCount = 2 * ICardEffect.ResolvePermanentOfThisCard(card).cardSources.Filter(cardSource => cardSource != card && cardSource.HasLevel && cardSource.Level == 6).Count;

                await new IAddTrashCardsFromLibraryTop(card.Context, CardEffectCommons.OpponentOf(card), trashCount, activateClass).AddTrashCardsFromLibraryTop();
            }
        }

        #endregion

        return cardEffects;
    }
}
