// Source: DCGO/Assets/Scripts/CardEffect/BT13/Blue/BT13_028.cs — 1:1 headless mirror (W2-LevelEvoCost witness).
// TeslaJellymon (BT13_028, Digimon / Blue). Two AS-IS timing blocks:
//   [Hand][Main] (OnDeclaration) If you have [Kiyoshiro Higashimitarai], place 1 [TeslaJellymon] from hand as the
//     bottom digivolution card of 1 of your [Jellymon], then that Digimon digivolves into THIS card for a digivolution
//     cost of 3, IGNORING digivolution requirements. This is the IgnoreDigivolutionRequirement witness — the grant is
//     written to the owner's UntilCalculateFixedCostEffect bucket (a live AddDigivolutionRequirementClass whose
//     GetEvoCost returns 3 for this card onto the chosen [Jellymon]); the READ side that makes the otherwise-illegal
//     digivolve legal is DigivolveAction.TryGetAddedDigivolutionCost -> CardSource.AddedDigivolutionCosts region 1
//     (player.EffectList(None), CardSource.cs:2119) which folds exactly that bucket (Player.EffectList :394). NOT inert.
//   [End of Attack][Once Per Turn] (OnEndAttack) By returning 3 [Jellymon]-text cards from your trash to the bottom of
//     the deck, unsuspend this Digimon.
// Substrate translations only: IEnumerator->async Task; `yield return (StartCoroutine/ContinuousController...
//   .StartCoroutine)(X)` -> `await X`; `card.Owner.HandCards.Contains(card)` -> `CardEffectCommons.IsExistOnHand(card)`
//   (BT2_023 idiom); `card.Owner.HandCards.Count(pred)` -> `new Player(card.Context, card.Owner).HandCards.Count(pred)`;
//   `card.Owner.TrashCards` -> `new Player(card.Context, card.Owner).TrashCards`; `card.Owner
//   .CanIgnoreDigivolutionRequirement(...)` -> `new Player(card.Context, card.Owner).CanIgnoreDigivolutionRequirement(...)`
//   (Player.cs:555); `card.Owner.UntilCalculateFixedCostEffect.Add/Remove(getCardEffect)` -> `new Player(card.Context,
//   card.Owner).UntilCalculateFixedCostEffect.Add/Remove(getCardEffect)` (store-backed, BT7_112 idiom — same list);
//   `selectedPermanent.AddDigivolutionCardsBottom(list, activateClass)` -> `(list, activateClass.EffectSourceCard!
//   .InstanceId)` (BT9_109 idiom); the id-shaped SelectPermanent/MatchConditionPermanentCount call sites take
//   `CanSelectPermanentConditionById` adapting the verbatim Permanent predicate (BT9_109 idiom); `CardObjectController
//   .AddLibraryBottomCards(list)` -> per-card `IZoneMover.MoveToDeckBottomAsync` in list order (EX7_072 idiom, the named
//   helper is unported); `ShowCardEffect(...)` / SE = UI, stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT13.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT13_028 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 [Jellymon] digivolves into this card", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Hand][Main] If you have [Kiyoshiro Higashimitarai], by placing 1 [TeslaJellymon] from your hand as 1 of your [Jellymon]'s bottom digivolution card, that Digimon digivolves into this card for a digivolution cost of 3, ignoring digivolution requirements.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.CardNames.Contains("TeslaJellymon");
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Jellymon"))
                    {
                        if (!permanent.IsToken)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // Id-shape adapter for the id-typed W4 call sites (SelectPermanentEffect.SetUp canTargetCondition /
            // MatchConditionPermanentCount take Func<HeadlessEntityId, bool>): resolve the mirror Permanent for the
            // candidate id (BT9_109 idiom) and evaluate the VERBATIM AS-IS predicate above.
            bool CanSelectPermanentConditionById(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                && CanSelectPermanentCondition(new Permanent(card.Context, id, rec.OwnerId));

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.TopCard.CardNames.Contains("Kiyoshiro Higashimitarai") || permanent.TopCard.CardNames.Contains("KiyoshiroHigashimitarai")))
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    bool added = false;

                    Permanent? selectedPermanent = null;

                    if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            CardSource? selectedCard = null;

                            int maxCount = 1;

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

                            selectHandEffect.SetUpCustomMessage("Select 1 card to place at the bottom of digivolution cards.", "The opponent is selecting 1 card to place at the bottom of digivolution cards.");

                            await selectHandEffect.Activate();

                            Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCard = cardSource;

                                return Task.CompletedTask;
                            }

                            if (selectedCard != null)
                            {
                                maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentConditionById));

                                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                selectPermanentEffect.SetUp(
                                    selectPlayer: card.Owner,
                                    canTargetCondition: CanSelectPermanentConditionById,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: true,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: SelectPermanentCoroutine,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Custom,
                                    cardEffect: activateClass);

                                selectPermanentEffect.SetUpCustomMessage("Select 1 [Jellymon] that will get a digivolution card.", "The opponent is selecting 1 [Jellymon] that will get a digivolution card.");

                                await selectPermanentEffect.Activate();

                                async Task SelectPermanentCoroutine(Permanent permanent)
                                {
                                    selectedPermanent = permanent;

                                    if (selectedPermanent != null)
                                    {
                                        await selectedPermanent.AddDigivolutionCardsBottom(new List<CardSource>() { selectedCard }, activateClass.EffectSourceCard!.InstanceId);

                                        added = true;
                                    }
                                }
                            }
                        }
                    }

                    if (added)
                    {
                        if (selectedPermanent != null)
                        {
                            if (CardEffectCommons.IsExistOnHand(card))
                            {
                                #region ignore digivolution requirements

                                AddDigivolutionRequirementClass addEvolutionConditionClass = new AddDigivolutionRequirementClass();
                                addEvolutionConditionClass.SetUpICardEffect("Ignore Digivolution requirements", CanUseCondition1, card);
                                addEvolutionConditionClass.SetUpAddDigivolutionRequirementClass(getEvoCost: GetEvoCost);
                                Func<EffectTiming, ICardEffect> getCardEffect = GetCardEffect;
                                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(getCardEffect);

                                ICardEffect GetCardEffect(EffectTiming _timing)
                                {
                                    if (_timing == EffectTiming.None)
                                    {
                                        return addEvolutionConditionClass;
                                    }

                                    return null;
                                }

                                bool CanUseCondition1(Hashtable hashtable)
                                {
                                    return true;
                                }

                                int GetEvoCost(Permanent permanent, CardSource cardSource, CardEffectCommons.IgnoreRequirement ignore, bool checkAvailability)
                                {
                                    if (new Player(card.Context, card.Owner).CanIgnoreDigivolutionRequirement(permanent, cardSource))
                                    {
                                        if (CardSourceCondition(cardSource) && PermanentCondition(permanent))
                                        {
                                            return 3;
                                        }
                                    }

                                    return -1;
                                }

                                bool PermanentCondition(Permanent targetPermanent)
                                {
                                    return targetPermanent == selectedPermanent;
                                }

                                bool CardSourceCondition(CardSource cardSource)
                                {
                                    return cardSource == card;
                                }

                                #endregion

                                if (card.CanPlayCardTargetFrame(selectedPermanent.PermanentFrame, true, activateClass))
                                {
                                    await new PlayCardClass(
                                        cardSources: new List<CardSource>() { card },
                                        hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                                        payCost: true,
                                        targetPermanent: selectedPermanent,
                                        isTapped: false,
                                        root: SelectCardEffect.Root.Hand,
                                        activateETB: true).PlayCard();
                                }

                                #region release ignore digivolution requirements

                                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Remove(getCardEffect);

                                #endregion
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return cards from trash to the bottom of deck to unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Unsuspend_BT13_028");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack][Once Per Turn] By returning 3 cards with [Jellymon] in their text from your trash at the bottom of the deck in any order, unsuspend this Digimon.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.HasText("Jellymon");
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition) >= 3)
                {
                    int maxCount = 3;

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                    canTargetCondition: (cardSource) => CanSelectCardCondition(cardSource),
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => false,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: AfterSelectCardCoroutine,
                    message: "Select cards to place at the bottom of the deck\n(cards will be placed back to the bottom of the deck so that cards with lower numbers are on top).",
                    maxCount: maxCount,
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
                        if (cardSources.Count == 3)
                        {
                            foreach (CardSource cardSource in cardSources)
                            {
                                await card.Context.ZoneMover.MoveToDeckBottomAsync(cardSource.Owner, cardSource.InstanceId);
                            }

                            // AS-IS `ShowCardEffect(cardSources, "Deck Bottom Cards", ...)` = UI (stripped).

                            if (CardEffectCommons.IsExistOnBattleArea(card))
                            {
                                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                            }
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
