// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT19_061 (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT19/Black/BT19_061.cs (6 regions)
//    * None            : AddSelfDigivolutionRequirementStaticEffect([Xros Heart] level3 위 cost2).
//    * None            : ChangeCardNamesForDigiXrosClass — DigiXros 시 [Sparrowmon]으로도 취급
//      (PRIMARY covered element: ChangeCardNamesForDigiXros).
//    * On Play / When Digivolving: 덱 top3 공개, [Xros Heart]/[Blue Flare] 1장 손패로.
//    * On Deletion     : [Xros Heart]/[Blue Flare] 1장 손패/트래시에서 테이머 밑에 배치 (<Save> firing geometry).
//    * Your Turn - ESS : CollisionSelfStaticEffect(inherited).
// ③ 배선: [When Digivolving]은 미러 방언 EffectTiming.WhenDigivolving 전용 키(AD1_006/BT17_026 idiom).
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.Owner.LibraryCards.Count` → `new Player(card.Context, card.Owner).LibraryCards.Count`.
//    * SelectPermanentEffect.canTargetCondition id-형 → id 어댑터(BT17_026 idiom).
//    * `selectedPermanent.AddDigivolutionCardsBottom(list, activateClass)` → `(list, activateClass
//      .EffectSourceCard?.InstanceId)`.
//    * UserSelectionManager.SetBoolSelection/SetBool/WaitForEndSelect/SelectedBoolValue 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_061 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Alternate Digivolution

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 3 &&
                       targetPermanent.TopCard.EqualsTraits("Xros Heart");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition,
                digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region DigiXros name

        if (timing == EffectTiming.None)
        {
            ChangeCardNamesForDigiXrosClass changeCardNamesForDigiXrosClass = new ChangeCardNamesForDigiXrosClass();
            changeCardNamesForDigiXrosClass.SetUpICardEffect("Also treated as [Sparrowmon] for a DigiXros", CanUseCondition, card);
            changeCardNamesForDigiXrosClass.SetUpChangeCardNamesForDigiXrosClass(changeCardNames: ChangeCardNames);

            cardEffects.Add(changeCardNamesForDigiXrosClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            List<string> ChangeCardNames(CardSource cardSource, List<string> cardNames)
            {
                if (cardSource == card)
                {
                    cardNames.Add("Sparrowmon");
                }

                return cardNames;
            }
        }

        #endregion

        #region On Play/ When Digivolving Shared

        bool CanSelectCardConditionShared(CardSource cardSource)
        {
            return cardSource.EqualsTraits("Xros Heart") || cardSource.EqualsTraits("Blue Flare");
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   new Player(card.Context, card.Owner).LibraryCards.Count >= 1;
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 3 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Play] Reveal the top 3 cards of your deck. Add 1 card with the [Xros Heart]/[Blue Flare] trait among them to the hand. Trash the rest.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                        revealCount: 3,
                        simplifiedSelectCardConditions:
                        new SimplifiedSelectCardConditionClass[]
                        {
                            new(
                                canTargetCondition: CanSelectCardConditionShared,
                                message: "Select 1 Digimon card with the [Xros Heart]/[Blue Flare] in one of its traits.",
                                mode: SelectCardEffect.Mode.AddHand,
                                maxCount: 1,
                                selectCardCoroutine: null)
                        },
                        remainingCardsPlace: RemainingCardsPlace.Trash,
                        activateClass: activateClass
                    );
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 3 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] Reveal the top 3 cards of your deck. Add 1 card with the [Xros Heart]/[Blue Flare] trait among them to the hand. Trash the rest.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                        revealCount: 3,
                        simplifiedSelectCardConditions:
                        new SimplifiedSelectCardConditionClass[]
                        {
                            new(
                                canTargetCondition: CanSelectCardConditionShared,
                                message: "Select 1 Digimon card with the [Xros Heart]/[Blue Flare] in one of its traits.",
                                mode: SelectCardEffect.Mode.AddHand,
                                maxCount: 1,
                                selectCardCoroutine: null)
                        },
                        remainingCardsPlace: RemainingCardsPlace.Trash,
                        activateClass: activateClass
                    );
            }
        }

        #endregion

        #region On Deletion

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place 1 [Xros Heart]/[Blue Flare] card from trash under 1 of your Tamers, then <Save>",
                CanUseCondition,
                card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Deletion] Place 1 Digimon card with the [Xros Heart]/[Blue Flare] trait from your hand or trash under your Tamers.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool IsOwnTamerCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                       && permanent.IsTamer;
            }

            bool IsOwnTamerConditionById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && IsOwnTamerCondition(permanent);
            }

            bool HasTraitCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon && (cardSource.EqualsTraits("Xros Heart") || cardSource.EqualsTraits("Blue Flare"));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       CardEffectCommons.HasMatchConditionOwnersPermanent(card, IsOwnTamerCondition) &&
                       (CardEffectCommons.HasMatchConditionOwnersHand(card, HasTraitCondition) ||
                        CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasTraitCondition));
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, HasTraitCondition);
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasTraitCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>
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
                            canTargetCondition: HasTraitCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: false,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select 1 card to place under a tamer.",
                            "The opponent is selecting 1 card to play.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Placed card");

                        await selectHandEffect.Activate();
                    }
                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: HasTraitCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to place under a tamer.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to place under a tamer.",
                            "The opponent is selecting 1 card to place under a tamer.");

                        await selectCardEffect.Activate();
                    }

                    if (selectedCards.Count >= 1)
                    {
                        Permanent? selectedPermanent = null;

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: IsOwnTamerConditionById,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Tamer to place the chosen card under.",
                            "The opponent is selecting 1 Tamer to place the chosen card under.");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);
                        }
                    }
                }
            }
        }

        #endregion

        #region Your Turn - ESS

        if (timing == EffectTiming.OnCounterTiming)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) && CardEffectCommons.IsOwnerTurn(card) &&
                       ICardEffect.ResolvePermanentOfThisCard(card).TopCard.EqualsTraits("Xros Heart");
            }

            cardEffects.Add(CardEffectFactory.CollisionSelfStaticEffect(isInheritedEffect: true, card: card, condition: Condition));
        }

        #endregion

        return cardEffects;
    }
}
