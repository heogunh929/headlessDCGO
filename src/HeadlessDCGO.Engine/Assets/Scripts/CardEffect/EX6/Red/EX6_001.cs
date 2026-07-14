// Source: DCGO/Assets/Scripts/CardEffect/EX6/Red/EX6_001.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn][Once Per Turn]
// OnAddDigivolutionCards INHERITED (digivolution-source) branch — the card's ONLY effect (F1-Tier2 witness).
//   [Your Turn][Once Per Turn] When an effect places a card with the [Legend-Arms] trait in this Digimon's
//   digivolution cards, gain 1 memory.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpActivateClass(..., 1, false, ...) (ORDER 1 =
// once per turn, mandatory) + SetIsInheritedEffect(true) + SetHashString("Gain1Memory_EX6_001") (EX6_001.cs:14-58).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `permanent => permanent ==
// card.PermanentOfThisCard()` -> `permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId` (the established
// Permanent-vs-PermanentView identity idiom for an inherited source, BT22_003/BT2_002).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class EX6_001 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddDigivolutionCards)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain 1 Memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Gain1Memory_EX6_001");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Your Turn] [Once Per Turn] When an effect places a card with the [Legend-Arms] trait in this Digimon's digivolution cards, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnAddDigivolutionCard(
                                hashtable: hashtable,
                                permanentCondition: permanent => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId,
                                cardEffectCondition: cardEffect => cardEffect.EffectSourceCard != null,
                                cardCondition: cardSource => cardSource.ContainsTraits("Legend-Arms")))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
