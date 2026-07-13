// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_010.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_010 (BT1/Red) — a Tamer.
//   [On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place
//             the remaining cards at the bottom of your deck in any order.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions, ActivateCoroutine = CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(revealCount:5,
// SimplifiedSelectCardConditionClass(IsTamer -> Mode.AddHand, maxCount:1), remainingCardsPlace: DeckBottom) —
// now resolves via the bridge-W3 AS-IS-signature overload (RevealLibrary.cs).
// Substrate-only translations: IEnumerator -> Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `card.Owner.LibraryCards.Count` (AS-IS Player field) -> the mirror zone-state read
// (`IZoneStateReader.GetCards`), the same translation the bridge's own SimplifiedRevealDeckTopCardsAndSelect
// guard uses internally.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_010 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top 5 cards of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsTamer;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    // AS-IS `card.Owner.LibraryCards.Count >= 1` (Player field) -> mirror zone-state read.
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
                            message: "Select 1 Tamer card.",
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
