// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_100.cs — an Option (two timings).
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS BT1_100.
//   [Main] (OptionSkill) "Until the end of your opponent's next turn, their Digimon with no digivolution cards
//   can't attack." [Security] (SecuritySkill) "Your opponent's Digimon with no digivolution cards can't attack
//   for the turn." BOTH: ActivateClass(CanUseCondition = CanTriggerOptionMainEffect / CanTriggerSecurityEffect,
//   ORDER=-1, ISOPTIONAL=false). ActivateCoroutine has NO SelectPermanentEffect step — it directly calls
//   CardEffectCommons.GainCanNotAttackPlayerEffect(attackerCondition = opponent battle-area Digimon with no
//   digivolution cards, defenderCondition = always-true no-op, effectDuration: UntilOpponentTurnEnd (Main) /
//   UntilEachTurnEnd (Security)).
// AS-IS structure kept verbatim: inline `new ActivateClass()` (twice) + local functions, including the AS-IS
// no-op `DefenderCondition(Permanent defender) => true`. Substrate translations only: IEnumerator->Task,
// StartCoroutine->await.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_100 : CEntity_Effect
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
                return "[Main] Until the end of your opponent's next turn, their Digimon with no digivolution cards can't attack.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool AttackerCondition(Permanent attacker)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(attacker, card))
                    {
                        if (attacker.HasNoDigivolutionCards)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                bool DefenderCondition(Permanent defender)
                {
                    return true;
                }

                await CardEffectCommons.GainCanNotAttackPlayerEffect(
                    attackerCondition: AttackerCondition,
                    defenderCondition: DefenderCondition,
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    activateClass: activateClass,
                    effectName: "Can't Attack");
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can't Attack", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Your opponent's Digimon with no digivolution cards can't attack for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool AttackerCondition(Permanent attacker)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(attacker, card))
                    {
                        if (attacker.HasNoDigivolutionCards)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                bool DefenderCondition(Permanent defender)
                {
                    return true;
                }

                await CardEffectCommons.GainCanNotAttackPlayerEffect(
                    attackerCondition: AttackerCondition,
                    defenderCondition: DefenderCondition,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass,
                    effectName: "Can't Attack");
            }
        }

        return cardEffects;
    }
}
