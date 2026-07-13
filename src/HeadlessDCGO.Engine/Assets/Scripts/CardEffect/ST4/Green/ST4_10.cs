// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_10.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_10 (ST4/Green).
//   [When Digivolving] Reveal the top 5 cards of your deck. Add 1 level 6 or higher Digimon card among
//   them to your hand. Place the remaining cards at the bottom of your deck in any order.
// AS-IS declares this under EffectTiming.OnEnterFieldAnyone (gated by CanTriggerWhenDigivolving) — the
// established headless bridge dispatch key for [When Digivolving] activated-select effects is
// EffectTiming.WhenDigivolving instead (OnEnterFieldAnyone activated selects never fire live in the headless
// resolver), matching the already-fixed BT1_025.cs precedent (same substrate substitution, gameplay conditions
// preserved verbatim — NOT dropped/subsumed as the prior pass's comment claimed).
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(...)`
// (an invented per-card wrapper, and the CanUseCondition/CanActivateCondition gates it dropped) with the
// literal AS-IS inline `new ActivateClass()` structure, calling the AS-IS-signature
// `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect` bridge (bridge W3) directly (see BT1_010.cs for the
// identically-shaped sibling).
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; AS-IS `CanSelectCardCondition(CardSource cardSource)` kept as a CardSource-shape predicate.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_10 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 5 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Reveal the top 5 cards of your deck. Add 1 level 6 or higher Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level >= 6)
                    {
                        if (cardSource.HasLevel)
                        {
                            return true;
                        }
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
                    revealCount: 5,
                    simplifiedSelectCardConditions:
                    new SimplifiedSelectCardConditionClass[]
                    {
                        new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition,
                            message: "Select 1 level 6 or higher Digimon card.",
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
