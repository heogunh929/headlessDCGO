// Source: DCGO/Assets/Scripts/CardEffect/BT22/Green/BT22_044.cs — "Palmon".
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn][Once Per Turn]
// OnAddDigivolutionCards SELF (top-instance) branch — the F1-Tier2 OnAddDigivolutionCards witness.
//   [Your Turn][Once Per Turn] When effects place Digimon cards with the [CS] trait in this Digimon's digivolution
//   cards, gain 1 memory.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpActivateClass(..., 1, false, ...) (ORDER 1 =
// once per turn, mandatory) + SetHashString("GainMemory_BT22_044") (BT22_044.cs:35-77). Substrate translations only:
// IEnumerator->Task, StartCoroutine->await; `permanent == card.PermanentOfThisCard()` -> `permanent.InstanceId ==
// card.PermanentOfThisCard().TopInstanceId`; `cardSource.HasCSTraits` -> `cardSource.EqualsTraits("CS")` (the
// established trait-property mirror). The AS-IS `CanAddMemory` disjunct on CanActivate + the body guard are kept 1:1.
//
// The AS-IS timing==None (AddSelfDigivolutionRequirementStaticEffect, alt-digivolve) and OnDeclaration (inherited ESS
// [Main] top->bottom + Draw 1) effects are ORTHOGONAL to the OnAddDigivolutionCards reactor under test and remain
// deliberately OMITTED (same witness scoping as the prior pass).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT22_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddDigivolutionCards)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("+1 memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("GainMemory_BT22_044");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When effects place Digimon cards with the [CS] trait in this Digimon's digivolution cards, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAddDigivolutionCard(hashtable, IsThisPermanent, null, IsCsDigimon);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && card.Owner.CanAddMemory(activateClass);
            }

            bool IsThisPermanent(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent)
                    && permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId;
            }

            bool IsCsDigimon(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.EqualsTraits("CS");
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (card.Owner.CanAddMemory(activateClass))
                {
                    await card.Owner.AddMemory(1, activateClass);
                }
            }
        }

        return cardEffects;
    }
}
