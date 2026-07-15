// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [WhenPermanentWouldBeDeleted]
// returns a MANDATORY, NON-KEYWORD-named, window-form ActivateClass that cancels the deletion by setting the
// permanent's willBeRemoveField=false (the AS-IS survival model). Used by the C-Del 3b PRE-transport witness to
// prove the universal effect-delete sink now opens the AS-IS "would be deleted" cut-in window
// (WhenPermanentWouldBeDeleted → WhenRemoveField) and a replacement body spares the card.
//
// DELIBERATELY GATE-INVISIBLE (the 3b double-fire audit): the effect name is "TfxWouldBeDeletedSurvive", NOT a
// recognised replacement keyword (Evade/Barrier/…), so NewModelContinuousScan does not detect it; and it is a
// new-model ActivateClass returned via CardEffects (no EffectRegistry binding), so the gate's
// CustomWouldBeDeletedOption does not detect it either. HasPreOption is therefore FALSE → the sink's batch is
// NOT gate-deferred (DeferAll=false) → the AS-IS PRE cut-in window (the thing under test) is the sole firing
// path. No real card has the number "TfxWouldBeDeleted", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWouldBeDeleted : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxWouldBeDeletedSurvive", CanUseCondition, card);
            // isOptional:false — a MANDATORY replacement (the interactive/optional case is design item
            // RD-3B-INTERACTIVE and out of the 3b substrate scope), order -1, uncapped.
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[When this Digimon would be deleted] It is not deleted. (test fixture)");
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
                // AS-IS survival: cancel the pending deletion (willBeRemoveField=false). The sink's per-entry
                // survivor read then spares this card (never moving it to the trash).
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
