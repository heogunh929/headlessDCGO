// Source: DCGO/Assets/Scripts/CardEffect/BT10/White/BT10_086.cs — 1:1 headless mirror (W2-LevelEvoCost witness).
// Omnimon X Antibody (BT10_086, Digimon / White). Five AS-IS timing blocks:
//   [None] AddSelfDigivolutionRequirementStaticEffect — digivolve onto an [Omnimon] top card for 3.
//   [None] ChangeDigivolutionCostStaticEffect(-2) — while this card is NOT on the field, digivolving into THIS card
//     onto one of your battle-area Digimon that has [X Antibody] in its digivolution cards costs 2 less. (The
//     "double-cover": the SAME [None] timing carries both an AddSelfDigivolutionRequirement static AND a
//     ChangeDigivolutionCost static — the READ side, CardSource.GetChangedPayingCost, folds the ChangeCostClass over
//     the moving card's own None EffectList, so both statics are live on the digivolve-cost pipeline.)
//   [When Digivolving] Return all opponent Digimon with the HIGHEST level to the bottom of their decks (IsMaxLevel
//     witness — the level predicate GATES which enemy Digimon are bounced; when >1, the owner orders them).
//   [When Digivolving][Once Per Turn] / [When Attacking][Once Per Turn] By placing 1 [X Antibody]/level-6 digivolution
//     card at the bottom of the deck, reveal the opponent's security, trash 1, then shuffle.
// ③ wiring: AS-IS registers the two [When Digivolving] blocks under OnEnterFieldAnyone (:193/:326) + a
//   CanTriggerWhenDigivolving gate; the mirror keys them under the dedicated EffectTiming.WhenDigivolving
//   (DigivolveAction emits ONLY WhenDigivolving on digivolve; double-key registration forbidden — trigger-wiring
//   rule 3, BT18_042/BT20_079 idiom). The [When Attacking] block stays OnAllyAttack (CanTriggerOnAttack). Gate bodies
//   are kept verbatim.
// Substrate translations only: IEnumerator->async Task; `yield return (StartCoroutine/ContinuousController...
//   .StartCoroutine)(X)` -> `await X`; `lone yield return null` in a select coroutine -> `return Task.CompletedTask`;
//   `card.Owner.Enemy` -> `new Player(card.Context, card.Owner).Enemy!` (2-player non-null) for its SecurityCards /
//   `CardEffectCommons.OpponentOf(card)` (HeadlessPlayerId) for GetBattleAreaDigimons / IsMaxLevel / IReduceSecurity /
//   ShuffleSecurityAsync; `card.PermanentOfThisCard()` (as a Permanent arg) -> `ICardEffect.ResolvePermanentOfThisCard(
//   card)`; `permanent.CannotReturnToLibrary(activateClass)` -> `NewModelContinuousScan.HasCannotReturnToLibrary(
//   card.Context, permanent.InstanceId, activateClass.EffectSourceCard!.InstanceId)` (the 1:1 mirror of AS-IS
//   Permanent.CannotReturnToLibrary(ICardEffect), NewModelContinuousScan.cs:1736); `new DeckBottomBounceClass(list,
//   CardEffectHashtable(activateClass)).DeckBounce()` -> `CardEffectCommons.DeckBouncePeremanentAndProcessAccordingTo
//   Result(list, activateClass, null, null)`; `CardObjectController.AddLibraryBottomCards([cs])` -> per-card
//   `IZoneMover.MoveToDeckBottomAsync` (EX7_072 idiom, named helper unported); `IReduceSecurity(enemy, ref
//   nullSkillInfos, activateClass)` -> `new IReduceSecurity(card.Context, OpponentOf(card), refCollector: null,
//   activateClass)` (BT18_042 idiom); `enemy.SecurityCards = RandomUtility.ShuffledDeckCards(...)` ->
//   `IZoneMover.ShuffleSecurityAsync(OpponentOf(card))` (BT1_087 idiom); ShowCardEffect / SE / security-break visuals =
//   UI, stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT10.White;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT10_086 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardNames.Contains("Omnimon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 3,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return !CardEffectCommons.IsExistOnField(card);
            }

            bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent != null)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(targetPermanent, card))
                    {
                        if (targetPermanent.DigivolutionCards.Some((cardSource) =>
                        cardSource.CardNames.Contains("XAntibody") || cardSource.CardNames.Contains("X Antibody")))
                        {
                            return true;
                        }
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

            cardEffects.Add(
                CardEffectFactory.ChangeDigivolutionCostStaticEffect<int>(
                    changeValue: -2,
                    permanentCondition: PermanentCondition,
                    cardCondition: CardSourceCondition,
                    rootCondition: RootCondition,
                    isInheritedEffect: false,
                    card: card,
                    condition: Condition,
                    setFixedCost: false));
        }

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return all opponent's Digimon with the highest level to the bottom of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Return all of your opponent's Digimon with the highest level to the bottom of their owners' decks in any order.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMaxLevel(permanent, CardEffectCommons.OpponentOf(card));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
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
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        List<Permanent> selectedPermanents = new List<Permanent>();

                        foreach (Permanent permanent in CardEffectCommons.OpponentOf(card).GetBattleAreaDigimons())
                        {
                            if (CanSelectPermanentCondition(permanent))
                            {
                                if (!permanent.TopCard.CanNotBeAffected(activateClass))
                                {
                                    if (!NewModelContinuousScan.HasCannotReturnToLibrary(card.Context, permanent.InstanceId, activateClass.EffectSourceCard!.InstanceId))
                                    {
                                        selectedPermanents.Add(permanent);
                                    }
                                }
                            }
                        }

                        if (selectedPermanents.Count >= 1)
                        {
                            if (selectedPermanents.Count == 1)
                            {
                                await CardEffectCommons.DeckBouncePeremanentAndProcessAccordingToResult(selectedPermanents, activateClass, null, null);
                            }
                            else
                            {
                                List<CardSource> cardSources = selectedPermanents.Map(permanent => permanent.TopCard);

                                List<SkillInfo> skillInfos = cardSources.Map(cardSource =>
                                {
                                    ChangeBaseDPClass cardEffect = new ChangeBaseDPClass();
                                    cardEffect.SetUpICardEffect(" ", null, cardSource);

                                    return new SkillInfo(cardEffect, null, EffectTiming.None);
                                });

                                List<CardSource> selectedCards = new List<CardSource>();

                                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                selectCardEffect.SetUp(
                                    canTargetCondition: (cardSource) => true,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => false,
                                    selectCardCoroutine: null,
                                    afterSelectCardCoroutine: AfterSelectCardCoroutine1,
                                    message: "Specify the order to place the card at the bottom of the deck\n(cards will be placed back to the bottom of the deck so that cards with lower numbers are on top).",
                                    maxCount: cardSources.Count,
                                    canEndNotMax: false,
                                    isShowOpponent: false,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: cardSources,
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                                selectCardEffect.SetNotShowCard();
                                selectCardEffect.SetNotAddLog();
                                selectCardEffect.SetUpSkillInfos(skillInfos);

                                await selectCardEffect.Activate();

                                Task AfterSelectCardCoroutine1(List<CardSource> orderedCardSources)
                                {
                                    if (orderedCardSources.Count >= 1)
                                    {
                                        selectedCards = orderedCardSources.Clone();

                                        // AS-IS `ShowCardEffect(cardSources, "Deck Bottom Cards", ...)` = UI (stripped).
                                    }

                                    return Task.CompletedTask;
                                }

                                if (selectedCards.Count >= 1)
                                {
                                    List<Permanent> libraryPermanets = selectedCards.Map(cardSource => ICardEffect.ResolvePermanentOfThisCard(cardSource));

                                    if (libraryPermanets.Count >= 1)
                                    {
                                        // AS-IS `putLibraryBottomPermanent.SetNotShowCards()` = UI (stripped).
                                        await CardEffectCommons.DeckBouncePeremanentAndProcessAccordingToResult(libraryPermanets, activateClass, null, null);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 digivolution card to bottom of deck and trash Security", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("TrashSecurity_BT10_086");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving][Once Per Turn] By placing 1 [X Antibody] or level 6 card from this Digimon's digivolution cards at the bottom of its owner's deck, reveal all of your opponent's security cards, and trash 1 of them. Place the rest in your opponent's security stack face down. Then, your opponent shuffles their security stack.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody"))
                {
                    return true;
                }

                if (cardSource.Level == 6)
                {
                    if (cardSource.HasLevel)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectCardCondition1(CardSource cardSource)
            {
                return true;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool returnedToLibrary = false;

                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 digivolution card to return to the bottom of deck.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: null);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 digivolution card to return to the bottom of deck.",
                    "The opponent is selecting 1 digivolution card to return to the bottom of deck.");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                foreach (CardSource cardSource in selectedCards)
                {
                    if (!cardSource.IsToken)
                    {
                        returnedToLibrary = true;

                        await card.Context.ZoneMover.MoveToDeckBottomAsync(cardSource.Owner, cardSource.InstanceId);
                    }
                }

                if (returnedToLibrary)
                {
                    if (new Player(card.Context, card.Owner).Enemy!.SecurityCards.Count >= 1)
                    {
                        // AS-IS ShowCardEffect(enemy.SecurityCards, "Security Cards", ...) = UI (stripped).

                        int maxCount = 1;

                        CardSource? selectedCard = null;

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition1,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine1,
                            afterSelectCardCoroutine: AfterSelectCardCoroutine,
                            message: "Select 1 card to discard.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Security,
                            customRootCardList: new Player(card.Context, card.Owner).Enemy!.SecurityCards,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetIsSecurity();
                        selectCardEffect.SetUpCustomMessage_ShowCard("Trash card");

                        await selectCardEffect.Activate();

                        Task SelectCardCoroutine1(CardSource cardSource)
                        {
                            selectedCard = cardSource;
                            return Task.CompletedTask;
                        }

                        async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                        {
                            if (cardSources.Count >= 1)
                            {
                                await new IReduceSecurity(
                                    card.Context,
                                    CardEffectCommons.OpponentOf(card),
                                    refCollector: null,
                                    activateClass).ReduceSecurity();
                            }
                        }

                        if (selectedCard != null)
                        {
                            // AS-IS security-break visuals (securityBreakGlass / BreakSecurityEffect / EnterSecurityCardEffect
                            // / DestroySecurityEffect) = UI (stripped).

                            await CardObjectController.AddTrashCard(selectedCard);
                        }

                        // AS-IS `ContinuousController.instance.PlaySE(...ShuffleSE)` = UI (stripped).

                        await card.Context.ZoneMover.ShuffleSecurityAsync(CardEffectCommons.OpponentOf(card));
                    }
                }
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 digivolution card to bottom of deck and trash Security", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("TrashSecurity_BT10_086");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Once Per Turn] By placing 1 [X Antibody] or level 6 card from this Digimon's digivolution cards at the bottom of its owner's deck, reveal all of your opponent's security cards, and trash 1 of them. Place the rest in your opponent's security stack face down. Then, your opponent shuffles their security stack.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.CardNames.Contains("X Antibody") || cardSource.CardNames.Contains("XAntibody"))
                {
                    return true;
                }

                if (cardSource.Level == 6)
                {
                    if (cardSource.HasLevel)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectCardCondition1(CardSource cardSource)
            {
                return true;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool returnedToLibrary = false;

                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 digivolution card to return to the bottom of deck.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: null);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 digivolution card to return to the bottom of deck.",
                    "The opponent is selecting 1 digivolution card to return to the bottom of deck.");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                foreach (CardSource cardSource in selectedCards)
                {
                    if (!cardSource.IsToken)
                    {
                        returnedToLibrary = true;

                        await card.Context.ZoneMover.MoveToDeckBottomAsync(cardSource.Owner, cardSource.InstanceId);
                    }
                }

                if (returnedToLibrary)
                {
                    if (new Player(card.Context, card.Owner).Enemy!.SecurityCards.Count >= 1)
                    {
                        // AS-IS ShowCardEffect(enemy.SecurityCards, "Security Cards", ...) = UI (stripped).

                        int maxCount = 1;

                        CardSource? selectedCard = null;

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition1,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine1,
                            afterSelectCardCoroutine: AfterSelectCardCoroutine,
                            message: "Select 1 card to discard.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Security,
                            customRootCardList: new Player(card.Context, card.Owner).Enemy!.SecurityCards,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage_ShowCard("Trash card");

                        await selectCardEffect.Activate();

                        Task SelectCardCoroutine1(CardSource cardSource)
                        {
                            selectedCard = cardSource;
                            return Task.CompletedTask;
                        }

                        async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                        {
                            if (cardSources.Count >= 1)
                            {
                                await new IReduceSecurity(
                                    card.Context,
                                    CardEffectCommons.OpponentOf(card),
                                    refCollector: null,
                                    activateClass).ReduceSecurity();
                            }
                        }

                        if (selectedCard != null)
                        {
                            // AS-IS security-break visuals = UI (stripped).

                            await CardObjectController.AddTrashCard(selectedCard);
                        }

                        // AS-IS `ContinuousController.instance.PlaySE(...ShuffleSE)` = UI (stripped).

                        await card.Context.ZoneMover.ShuffleSecurityAsync(CardEffectCommons.OpponentOf(card));
                    }
                }
            }
        }

        return cardEffects;
    }
}
