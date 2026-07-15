// TEST FIXTURE (not a real card). Sibling of TfxWouldBeDeleted, but the [WhenPermanentWouldBeDeleted] ActivateClass
// is OPTIONAL (isOptional:true) — an interactive "will you use it?" agent choice — so the AS-IS PRE cut-in drain
// PAUSES on a ChoiceController request. Used by the C-Del 3c-1 substrate witness to prove the promote-to-defer
// substrate: the sink flags the batch pendingDeletion + swallows the pause (no enclosing-body re-run), the parked
// ForCutIn window carries the choice, and once the agent resolves it the resumed body sets willBeRemoveField=false
// (survives) or leaves it set (dies) and the GameFlowProcessor sweep finalizes on willBeRemoveField.
//
// DELIBERATELY GATE-INVISIBLE (same audit as TfxWouldBeDeleted): the effect name is "TfxWouldBeDeletedInteractive",
// NOT a recognised replacement keyword, and it is a new-model ActivateClass with no EffectRegistry binding, so
// HasPreOption is FALSE → the sink's batch is NOT gate-deferred (DeferAll=false) → the AS-IS PRE cut-in window (the
// thing under test) is the sole firing path. No real card has this number, so it is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWouldBeDeletedInteractive : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxWouldBeDeletedInteractive", CanUseCondition, card);
            // isOptional:true — a "you may" replacement, so the cut-in drain surfaces an agent choice and PAUSES.
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true,
                "[When this Digimon would be deleted] You may prevent it. (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                // AS-IS CanTriggerEvade shape: this permanent is one of the would-be-deleted targets.
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent) &&
                       CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent == targetPermanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent);
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                // AS-IS survival: cancel the pending deletion (willBeRemoveField=false). The sweep's promoted-
                // survivor check then spares this card (never moving it to the trash).
                if (targetPermanent != null)
                {
                    targetPermanent.willBeRemoveField = false;
                }

                return Task.CompletedTask;
            }
        }

        return cardEffects;
    }
}
