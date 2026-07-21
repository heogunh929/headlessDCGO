// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_078.cs
// TRUE AS-IS-verbatim re-port (이연③-e). 1:1 mirror of the original BT1_078 (BT1/Green).
//   [When Attacking] Reveal 3 cards from the top of your deck. You can digivolve this card into 1 level 6
//   green Digimon card among them without paying its memory cost. Place the remaining cards at the bottom
//   of your deck in any order.
// AS-IS structure kept VERBATIM (RETIRES the invented `RevealSelectThenPlaySelectedEffect`/`RevealPlayMode`
// subtype): inline ActivateClass on OnAllyAttack, ActivateCoroutine =
//   1. the bridged coroutine-callable commons `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect`
//      (RevealLibrary.cs:179) — revealCount:3, ONE Custom-mode SimplifiedSelectCardCondition (maxCount:1,
//      canNoSelect:true) whose `selectCardCoroutine` CAPTURES the pick into `selectedCard` WITHOUT moving it
//      (Mode.Custom = no built-in move), remaining → RemainingCardsPlace.DeckBottom; THEN
//   2. if a card was captured and still passes CanPlayCardTargetFrame, `new PlayCardClass(...).PlayCard()`
//      digivolves it onto THIS card's own battle-area permanent for free (payCost:false, root:Library,
//      activateETB:true) — the AS-IS free digivolve-onto-self.
//
// The 3 ③-d fidelity risks are resolved by structural equivalence with AS-IS (not by re-implementation):
//   * PEEK-ONLY: the commons reads the top-3 via ZoneMover.GetCards(Library).Take(3) WITHOUT removing them
//     (AS-IS RevealLibraryClass.RevealLibrary peeks `LibraryCards[i]`); the Custom pick is not moved, so the
//     selected card is still physically in the library when PlayCardClass(root:Library) resolves it.
//   * IsBeingRevealed: AS-IS uses it only to suppress the OnDiscardLibrary broadcast on reveal-TRASHED cards
//     (RevealTrashFlagKey in the mirror). BT1_078 routes remaining to DeckBottom (not Trash), so the marker
//     is behaviourally inert here — no mirror gap.
//   * DRAW ORDER (isEvolution): the digivolve draw fires AFTER the remaining cards reach the deck bottom,
//     because PlayCardClass runs AFTER the commons returns (AS-IS statement order kept). PlayCardClass's
//     isEvolution path (CardController.cs:3096-3118 / :3495-3563, non-jogress/non-burst → CanEvolve) is fully
//     mirrored for this Library-root digivolve-onto-owned-permanent case (no STOP).
//
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)` -> `await X`;
// `card.Owner.LibraryCards.Count` -> `((IZoneStateReader)card.Context.ZoneMover).GetCards(Library).Count`
// (BT1_074 sibling idiom); `card.PermanentOfThisCard().PermanentFrame` (a `Permanent` arg for PlayCardClass)
// -> `ICardEffect.ResolvePermanentOfThisCard(card).PermanentFrame` (BT9_109 idiom); `HasCardColor(CardColor
// .Green)` -> `HasCardColor("Green")` (mirror string colors).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

public sealed class BT1_078 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 3 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Reveal 3 cards from the top of your deck. You can digivolve this card into 1 level 6 green Digimon card among them without paying its memory cost. Place the remaining cards at the bottom of your deck in any order. ";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level == 6)
                    {
                        if (cardSource.HasCardColor("Green"))
                        {
                            if (CardEffectCommons.IsExistOnBattleArea(card))
                            {
                                if (cardSource.CanPlayCardTargetFrame(ICardEffect.ResolvePermanentOfThisCard(card).PermanentFrame, false, activateClass))
                                {
                                    if (cardSource.HasLevel)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                CardSource selectedCard = null;

                await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                    revealCount: 3,
                    simplifiedSelectCardConditions:
                    new SimplifiedSelectCardConditionClass[]
                    {
                        new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition,
                            message: "Select 1 level 6 green Digimon card.",
                            mode: SelectCardEffect.Mode.Custom,
                            maxCount: 1,
                            selectCardCoroutine: SelectCardCoroutine),
                    },
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass,
                    canNoSelect: true);

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCard = cardSource;
                    return Task.CompletedTask;
                }

                if (selectedCard != null)
                {
                    if (selectedCard.CanPlayCardTargetFrame(ICardEffect.ResolvePermanentOfThisCard(card).PermanentFrame, false, activateClass))
                    {
                        await new PlayCardClass(
                            cardSources: new List<CardSource>() { selectedCard },
                            hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                            payCost: false,
                            targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                            isTapped: false,
                            root: SelectCardEffect.Root.Library,
                            activateETB: true).PlayCard();
                    }
                }
            }
        }

        return cardEffects;
    }
}
