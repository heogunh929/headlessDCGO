// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_048.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_048 (BT1/Yellow).
//   [On Play] Reveal 4 cards from the top of your deck. Add all yellow Tamer cards among them to your hand.
//   Place the remaining cards at the bottom of your deck in any order.
// AS-IS structure kept verbatim: inline ActivateClass, ActivateCoroutine = the bridged
// `CardEffectCommons.RevealDeckTopCardsAndProcessForAll` (W3), fed a single AS-IS-ctor
// `SimplifiedSelectCardConditionClass` (CardSource-shape predicate + Mode.AddHand + maxCount -1).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

public sealed class BT1_048 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 4 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Reveal 4 cards from the top of your deck. Add all yellow Tamer cards among them to your hand. Place the remaining cards at the bottom of your deck in any order.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsTamer)
                {
                    if (cardSource.HasCardColor("Yellow"))
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
                    revealCount: 4,
                    simplifiedSelectCardCondition:
                    new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CanSelectCardCondition,
                            message: "",
                            mode: SelectCardEffect.Mode.AddHand,
                            maxCount: -1,
                            selectCardCoroutine: null),
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
