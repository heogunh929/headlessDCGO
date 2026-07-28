// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_14.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_14 (Option).
//   [Main]     All of your Security Digimon get +7000 DP until the end of your opponent's next turn.
//   [Security] All of your Security Digimon get +7000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.PlayerScopeBuffSecurityDpEffect(...)` call (an
// invented helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure
// calling the REAL AS-IS `CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect(...)` (verified against
// DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffectToPlayer/ChangeCardDP.cs).
// AS-IS structure kept verbatim: `SetUpActivateClass(null, ...)` (CanActivateCondition IS null in both
// blocks), `SetIsSecurityEffect(true)` on the Security block only.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST1_14 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] All of your Security Digimon get +7000 DP until the end of your opponent's next turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect(
                    cardCondition: cardSource => cardSource.Owner == card.Owner,
                    changeValue: 7000,
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    activateClass: activateClass);
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"DP +7000 for your Security Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] All of your Security Digimon get +7000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect(
                    cardCondition: cardSource => cardSource.Owner == card.Owner,
                    changeValue: 7000,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
