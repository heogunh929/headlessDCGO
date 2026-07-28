// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT17_068 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT17/Purple/BT17_068.cs (420 lines, 4 regions)
//    * When Revealed from Deck :15-37  (timing None — ChangeCardLevelClass, IsBeingRevealed gate, level 6)
//    * When Would be Played    :41-184 (BeforePayCost — CanTriggerWhenPermanentWouldPlay gate: return 1
//                                        [Apocalymon] trash card to deck bottom -> play cost -3)
//    * On Deletion              :188-323(OnDestroyedAnyone — CanTriggerOnDeletion + IsByEffect gate: play
//                                        [Gulfmon]/lvl6[Dark Masters] from hand or trash free)
//    * When Attacking - ESS     :327-414(OnAllyAttack, inherited — place 1 lvl5-[Dark Masters]-text trash card
//                                        under digivolution cards, +2000 DP for the turn)
//
// ② 프리미티브 매핑:
//    * P:ChangeCardLevelClass, P:CanTriggerWhenPermanentWouldPlay, P:GetCardFromHashtable, P:ChangeCostClass,
//      P:ShowReducedCost, P:ReturnRevealedCardsToLibraryBottom(범용 단일/복수 카드 -> 덱바닥 브릿지 —
//      RevealLibrary.cs 유래 이름이지만 실제로는 인자로 받은 CardSource 목록을 그대로 덱바닥으로 옮기는
//      범용 헬퍼; "reveal 상태" 검사 없음. AS-IS `CardObjectController.AddLibraryBottomCards`의 카드-호출
//      가능 대응물), P:IsByEffect(Hashtable 오버로드, CanUseEffects/OnDeletion.cs:111),
//      P:CanActivateOnDeletion, P:ChangeDigimonDP
//
// ③ 배선 관례 근거:
//    * [When Would be Played] → EffectTiming.BeforePayCost(AS-IS 그대로) + CanTriggerWhenPermanentWouldPlay.
//    * [On Deletion] → OnDestroyedAnyone(AS-IS 그대로) + CanTriggerOnDeletion + IsByEffect(hashtable, null).
//    * [When Attacking] ESS → OnAllyAttack + CanTriggerOnAttack, SetIsInheritedEffect(true).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`.
//    * AS-IS `cardFromHashtable && cardFromHashtable.PayingCost(...) > cardFromHashtable.Owner.MaxMemoryCost`
//      (Unity CardSource 진위성 오버로드 — 미러 CardSource는 `operator true/false` 없음) →
//      `cardFromHashtable is not null && cardFromHashtable.PayingCost(...) > new
//      Player(card.Context, cardFromHashtable.Owner).MaxMemoryCost`.
//    * `card.Owner.CanReduceCost(null, card)` — 가드 자체는 UI(BuffSE 재생)만 감쌈 → §2.6 스트립(PlaySE 호출
//      제거, 가드 술어는 순수 UI라 보존 불필요).
//    * `CardObjectController.AddLibraryBottomCards(cardSources)` + `Effects.ShowCardEffect(...)`(UI) →
//      `CardEffectCommons.ReturnRevealedCardsToLibraryBottom(cardSources, card)`(물리 이동만 보존, UI 스트립).
//    * `card.Owner.HandCards`/`TrashCards` → `new Player(card.Context, card.Owner).*`.
//    * `card.PermanentOfThisCard()`(값-필요 지점: AddDigivolutionCardsBottom) → `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT17.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT17_068 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Revealed from Deck

        if (timing == EffectTiming.None)
        {
            ChangeCardLevelClass changeCardLevelClass = new ChangeCardLevelClass();
            changeCardLevelClass.SetUpICardEffect($"Also treated as level 6 when revealed from the top of the deck.", CanUseCondition, card);
            changeCardLevelClass.SetUpChangeCardLevelClass(GetLevel: GetLevel);
            changeCardLevelClass.SetNotShowUI(true);
            cardEffects.Add(changeCardLevelClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return card.IsBeingRevealed;
            }

            int GetLevel(CardSource cardSource, int level)
            {
                if (cardSource == card)
                    level = 6;

                return level;
            }
        }

        #endregion

        #region When Would be Played

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return [Apocalymon] from your trash to deck, to get Play Cost -3", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetHashString("PlayCost-3_BT17_068");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "When this card would be played from the hand, by returning 1 [Apocalymon] from your trash to the bottom of the deck, reduce the play cost by 3.";
            }

            bool IsApocalymonCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon &&
                       cardSource.EqualsCardName("Apocalymon");
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card &&
                       CardEffectCommons.IsExistOnHand(cardSource);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsApocalymonCardCondition);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsApocalymonCardCondition))
                {
                    CardSource cardFromHashtable = CardEffectCommons.GetCardFromHashtable(hashtable);

                    bool returned = false;
                    bool canNoSelect = !(cardFromHashtable is not null &&
                                         cardFromHashtable.PayingCost(SelectCardEffect.Root.Hand, null, checkAvailability: false) >
                                         new Player(card.Context, cardFromHashtable.Owner).MaxMemoryCost);

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: IsApocalymonCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => canNoSelect,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        message: "Select 1 card to place at the bottom of the deck.",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: false,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetNotShowCard();
                    selectCardEffect.SetNotAddLog();

                    await selectCardEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count == 1)
                        {
                            await CardEffectCommons.ReturnRevealedCardsToLibraryBottom(cardSources, card);

                            returned = true;
                        }
                    }

                    if (returned)
                    {
                        // AS-IS :129-132 `if (card.Owner.CanReduceCost(null, card)) PlaySE(BuffSE)` — UI 연출만
                        // (§2.6 스트립). 가드 술어는 순수 UI라 보존 불필요.

                        ChangeCostClass changeCostClass = new ChangeCostClass();
                        changeCostClass.SetUpICardEffect("Play Cost -3", CanUseConditionChangeCost, card);
                        changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition,
                            rootCondition: RootCondition, isUpDown: IsUpDown, isCheckAvailability: () => false,
                            isChangePayingCost: () => true);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);

                        await CardEffectCommons.ShowReducedCost(hashtable);

                        bool CanUseConditionChangeCost(Hashtable hashtableChangeCost)
                        {
                            return true;
                        }

                        int ChangeCost(CardSource cardSource, int cost, SelectCardEffect.Root root,
                            List<Permanent> targetPermanents)
                        {
                            if (CardSourceCondition(cardSource) && RootCondition(root) && PermanentsCondition(targetPermanents))
                            {
                                cost -= 3;
                            }

                            return cost;
                        }

                        bool CardSourceCondition(CardSource cardSource)
                        {
                            return cardSource == card;
                        }

                        bool RootCondition(SelectCardEffect.Root root)
                        {
                            return true;
                        }

                        bool PermanentsCondition(List<Permanent> targetPermanents)
                        {
                            return targetPermanents == null ||
                                   targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0;
                        }

                        bool IsUpDown()
                        {
                            return true;
                        }
                    }
                }
            }
        }

        #endregion

        #region On Deletion

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Digimon from hand or trash.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Deletion] If deleted by an effect, you may play 1 [Gulfmon] or 1 level 6 Digimon with the [Dark Masters] trait from your hand or trash without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card) &&
                       CardEffectCommons.IsByEffect(hashtable, null);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.EqualsCardName("Gulfmon") ||
                       (cardSource.IsDigimon &&
                        cardSource.HasLevel &&
                        cardSource.Level == 6 &&
                        cardSource.ContainsTraits("Dark Masters"));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       (new Player(card.Context, card.Owner).HandCards.Some(CanSelectCardCondition) ||
                        new Player(card.Context, card.Owner).TrashCards.Some(CanSelectCardCondition));
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = new Player(card.Context, card.Owner).HandCards.Some(CanSelectCardCondition);
                bool canSelectTrash = new Player(card.Context, card.Owner).TrashCards.Some(CanSelectCardCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                        {
                            new(message: "From hand", value: true, spriteIndex: 0),
                            new(message: "From trash", value: false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "From which area do you play a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to play a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements,
                            selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage,
                            notSelectPlayerMessage: notSelectPlayerMessage);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager
                        .WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    List<CardSource> selectedCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
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

                        selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectHandEffect.Activate();
                    }

                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to play.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectCardEffect.Activate();
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false,
                        root: (fromHand) ? SelectCardEffect.Root.Hand : SelectCardEffect.Root.Trash,
                        activateETB: true);
                }
            }
        }

        #endregion

        #region When Attacking - ESS

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place 1 digimon from trash under this Digimon's digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Attacking] (Once Per Turn) By placing 1 level 5 or lower card with [Dark Masters] in its text from your trash as this Digimon's bottom digivolution card, this Digimon gets +2000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool IsDarkMastersCardCondition(CardSource cardSource)
            {
                return cardSource.HasLevel &&
                       cardSource.Level <= 5 &&
                       cardSource.HasText("Dark Masters");
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsDarkMastersCardCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, IsDarkMastersCardCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: IsDarkMastersCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to place on bottom of digivolution cards.",
                        maxCount: 1,
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

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    await selectCardEffect.Activate();

                    if (selectedCards.Count >= 1)
                    {
                        if (CardEffectCommons.IsExistOnBattleArea(card))
                        {
                            await ICardEffect.ResolvePermanentOfThisCard(card)
                                .AddDigivolutionCardsBottom(
                                    selectedCards,
                                    activateClass.EffectSourceCard?.InstanceId);

                            await CardEffectCommons.ChangeDigimonDP(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), changeValue: 2000,
                                    effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
