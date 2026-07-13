// Source: DCGO/Assets/Scripts/CardEffect/BT2/Green/BT2_044.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_044 (BT2/Green).
//   [When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 Digimon card and 1 green Tamer
//   card among them to your hand. Place the remaining cards at the bottom of your deck in any order.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(...)` call
// (a CardEffectFactory-INVENTED wrapper) with the literal AS-IS inline `new ActivateClass()` structure +
// the REAL, already-bridged `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(revealCount:,
// simplifiedSelectCardConditions:, remainingCardsPlace:, activateClass:)` mutation helper (RevealLibrary.cs,
// param names/order verified 1:1 against AS-IS) — NOT the factory wrapper this batch retires.
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.Owner.LibraryCards.Count >= 1` -> `((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner,
// ChoiceZone.Library).Count >= 1` (same zone-state-read idiom `SimplifiedRevealDeckTopCardsAndSelect` itself
// uses internally for its own empty-library guard); `CardColor.Green` (AS-IS enum) -> `"Green"` (mirror
// `CardSource.HasCardColor(string)` string-color idiom, already used throughout the ported corpus).
// `SimplifiedSelectCardConditionClass(canTargetCondition:, message:, mode:, maxCount:, selectCardCoroutine:)`
// uses the REAL AS-IS constructor overload (RevealLibrary.cs, CardSource-shape predicate + SelectCardEffect.Mode),
// not the legacy id-shape/RevealDestination-shape constructor the old model used.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 3 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 Digimon card and 1 green Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level == 5)
                    {
                        if (cardSource.HasLevel)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition1(CardSource cardSource)
            {
                if (cardSource.IsTamer)
                {
                    if (cardSource.HasCardColor("Green"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
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
                await CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(
                    revealCount: 3,
                    simplifiedSelectCardConditions:
                    new SimplifiedSelectCardConditionClass[]
                    {
                        new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition,
                            message: "Select 1 level 5 Digimon card.",
                            mode: SelectCardEffect.Mode.AddHand,
                            maxCount: 1,
                            selectCardCoroutine: null),
                        new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition1,
                            message: "Select 1 green Tamer card.",
                            mode: SelectCardEffect.Mode.AddHand,
                            maxCount: 1,
                            selectCardCoroutine: null),
                    },
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass
                );
            }
        }

        return cardEffects;
    }
}
