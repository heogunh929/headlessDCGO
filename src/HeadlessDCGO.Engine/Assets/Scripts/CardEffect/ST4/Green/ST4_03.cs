// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_03.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_03 (ST4/Green) — a Digimon.
//   [On Play] Reveal the top card of your deck. If it's a green Digimon card, add it to your hand.
//             Otherwise, place it at the bottom of your deck.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(...)`
// (an invented per-card wrapper) with the literal AS-IS inline `new ActivateClass()` structure, calling the
// AS-IS-signature `CardEffectCommons.RevealDeckTopCardsAndProcessForAll` bridge (bridge W3) directly, matching
// the AS-IS body which calls `RevealDeckTopCardsAndProcessForAll`, NOT `SimplifiedRevealDeckTopCardsAndSelect`
// (see BT1_010.cs for the sibling that genuinely uses the latter).
// AS-IS structure kept verbatim: inline ActivateClass, no SetIsInheritedEffect/SetHashString (AS-IS omits both).
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; AS-IS `CanSelectCardCondition(CardSource cardSource)` kept as a CardSource-shape predicate
// (the AS-IS-signature SimplifiedSelectCardConditionClass constructor takes `Func<CardSource,bool>` verbatim).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_03 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top card of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Reveal the top card of your deck. If it's a green Digimon card, add it to your hand. Otherwise, place it at the bottom of your deck.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
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
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
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
                await CardEffectCommons.RevealDeckTopCardsAndProcessForAll(
                    revealCount: 1,
                    simplifiedSelectCardCondition:
                        new SimplifiedSelectCardConditionClass(
                                canTargetCondition: CanSelectCardCondition,
                                message: "",
                                mode: SelectCardEffect.Mode.AddHand,
                                maxCount: -1,
                                selectCardCoroutine: null),
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass
                );
            }
        }

        return cardEffects;
    }
}
