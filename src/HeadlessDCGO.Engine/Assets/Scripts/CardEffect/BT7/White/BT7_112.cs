// Source: DCGO/Assets/Scripts/CardEffect/BT7/White/BT7_112.cs (1:1 mirror).
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(AddDigivolutionRequirement 계열)이었으나 표면 실존 확인.
//   * [None] — AddSelfDigivolutionRequirementStaticEffect(Tamer 위 Lv.무관 코스트7, condition=핸드10장+
//     트래시조건 충족 시).
//   * [BeforePayCost] — "Return cards to deck": [When would digivolve onto own Tamer/Digimon] 손패/트래시에서
//     [Hybrid]/테이머 카드 10장을 덱 밑으로 → 성공 시 AddDigivolutionRequirementClass를
//     UntilCalculateFixedCostEffect에 등재(진화원가 7 재부여).
//   * [None] — ChangeSelfSAttackStaticEffect(+2).
//   * [When Digivolving] — 상대 디지몬 1체 삭제.
//
// 치환(substrate translations only): IEnumerator→async Task, StartCoroutine(X)/bare StartCoroutine(X)→await X;
// `card.Owner.HandCards`/`.TrashCards` → `new Player(card.Context, card.Owner).HandCards`/`.TrashCards`(§2.2);
// `CardObjectController.RemoveFromAllArea(selectedCard)` 1:1; `CardEffectCommons.ReturnRevealedCardsToLibraryBottom
// (libraryCards, activateClass)` AS-IS-시그니처 브릿지 그대로; "#region show cost"(GManager.instance.memoryObject.
// ShowMemoryPredictionLine + PlayCardClass 해시테이블 조회) = 순수 UI 프리뷰, 가드 프레디케이트 없음 → 스트립.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.White;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT7_112 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region [None] Digivolution Requirement (Tamer, cost 7)
        if (timing == EffectTiming.None)
        {
            bool CanSelectCardConditionOuter(CardSource cardSource) =>
                cardSource.CardTraits.Contains("Hybrid") || cardSource.IsTamer;

            bool Condition() =>
                new Player(card.Context, card.Owner).HandCards.Contains(card)
                && (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardConditionOuter)
                    + new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardConditionOuter) >= 10);

            bool PermanentConditionOuter(Permanent targetPermanent) => targetPermanent.IsTamer;

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentConditionOuter,
                digivolutionCost: 7,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: Condition));
        }
        #endregion

        #region [BeforePayCost] Return cards to deck
        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return cards to deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("ReturnCardsToDeck_BT7_112");
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "You may digivolve this card from your hand onto one of your Tamers as if the Tamer is a level 6 Digimon by placing 10 Tamer cards and/or cards with [Hybrid] in their traits from your hand and/or trash at the bottom of your deck in any order.";

            // Latest ruling is can return cards even when digivolving over level 6.
            bool PermanentCondition(Permanent targetPermanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(targetPermanent, card)
                && (targetPermanent.IsDigimon || targetPermanent.IsTamer);

            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.CardTraits.Contains("Hybrid") || cardSource.IsTamer;

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(hashtable, PermanentCondition, cardSource => cardSource == card);

            bool CanActivateCondition(Hashtable hashtable) =>
                new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition)
                + new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 10;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> permanents = CardEffectCommons.GetPermanentsFromHashtable(_hashtable);
                if (permanents != null)
                {
                    // If digivolution is not over a tamer, trashing is optional but allowed.
                    if (!permanents
                        .Where(permanent => permanent != null && permanent.TopCard != null)
                        .Any(permanent => permanent.IsTamer))
                    {
                        string selectPlayerMessage = "Will you place 10 Tamer or Hybrid cards to bottom of deck?";
                        string notSelectPlayerMessage = "The opponent is choosing if they will return cards to deck.";

                        List<SelectionElement<bool>> commandSelectCommands = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: "Yes", value: true, spriteIndex: 0),
                            new SelectionElement<bool>(message: "No", value: false, spriteIndex: 1),
                        };

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: commandSelectCommands, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                        await GManager.instance.userSelectionManager.WaitForEndSelect();

                        if (!GManager.instance.userSelectionManager.SelectedBoolValue)
                        {
                            return;
                        }
                    }
                }

                if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition)
                    + new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 10)
                {
                    List<CardSource> libraryCards = new List<CardSource>();

                    if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        int maxCount = Math.Min(10, new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition));
                        int minCount = 10 - new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition);
                        bool canNoSelect = minCount <= 0;

                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        bool CanEndSelectCondition(List<CardSource> cardSources) => cardSources.Count >= minCount;

                        async Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            libraryCards.Add(cardSource);
                            await Task.CompletedTask;
                        }

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: CanEndSelectCondition,
                            maxCount: maxCount,
                            canNoSelect: canNoSelect,
                            canEndNotMax: true,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select cards to place to the bottom of the deck.", "The opponent is selecting cards.");

                        await selectHandEffect.Activate();

                        foreach (CardSource selectedCard in selectedCards)
                        {
                            await CardObjectController.RemoveFromAllArea(selectedCard);
                        }
                    }

                    if (new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 1)
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        int maxCount = Math.Min(10 - libraryCards.Count, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                        if (maxCount >= 1)
                        {
                            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                            async Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCards.Add(cardSource);
                                libraryCards.Add(cardSource);
                                await Task.CompletedTask;
                            }

                            selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => false,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select cards to place to the bottom of the deck.",
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

                            foreach (CardSource selectedCard in selectedCards)
                            {
                                await CardObjectController.RemoveFromAllArea(selectedCard);
                            }
                        }
                    }

                    await CardEffectCommons.ReturnRevealedCardsToLibraryBottom(libraryCards, activateClass);

                    if (libraryCards.Count == 10)
                    {
                        // AS-IS `ContinuousController.instance.PlaySE(...BuffSE)` = UI (stripped).

                        AddDigivolutionRequirementClass addEvolutionConditionClass = new AddDigivolutionRequirementClass();
                        addEvolutionConditionClass.SetUpICardEffect("Can digivolve to this card", CanUseCondition1, card);
                        addEvolutionConditionClass.SetUpAddDigivolutionRequirementClass(getEvoCost: GetEvoCost);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => addEvolutionConditionClass);

                        // AS-IS "#region show cost" (GManager.instance.memoryObject.ShowMemoryPredictionLine, keyed
                        // off the AS-IS Hashtable "Permanents"/"IPlayCard"/"Card" entries and the PlayCardClass
                        // isJogress/PayCost flags) is a pure UI memory-prediction preview with no game-state guard
                        // — stripped entirely (rule 2.6, no mirror surface for PlayCardClass/the memory-prediction UI).

                        bool CanUseCondition1(Hashtable hashtable) => true;

                        int GetEvoCost(Permanent permanent, CardSource cardSource, CardEffectCommons.IgnoreRequirement ignore, bool ignoreDigivolutionCondition) =>
                            (CardSourceCondition(cardSource) && PermanentConditionInner(permanent)) ? 7 : -1;

                        bool PermanentConditionInner(Permanent targetPermanent) =>
                            targetPermanent != null && targetPermanent.IsTamer;

                        bool CardSourceCondition(CardSource cardSource) => cardSource == card;
                    }
                }
            }
        }
        #endregion

        #region [None] Security Attack +2
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 2, isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region [When Digivolving] Delete 1 Digimon
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[When Digivolving] Delete 1 of your opponent's Digimon.";

            bool CanSelectPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
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

        return cardEffects;
    }
}
