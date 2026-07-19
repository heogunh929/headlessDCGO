// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [WhenPermanentWouldBeDeleted]
// returns an OPTIONAL, window-form ActivateClass that survives by the AS-IS <Armor Purge> top-swap: trash ONLY
// the top card and promote the immediate under-source (DeDigivolveHelpers.ArmorPurgeTopAsync), then cancel the
// pending deletion (willBeRemoveField=false) so the sink's per-entry survivor read spares the permanent. Used
// as the current-model canon replacing the retired HasArmorPurgeKey metadata gate-key (R2-DeletionPipeline P1-4):
// the top-swap trash is NOT a departure — ArmorPurgeTopAsync strips the deletion markers and moves the top with
// the continuity metadata (no delete-batch id), so NO OnDeletion / OnLeaveFieldAnyone reactor fires for it.
//
// DELIBERATELY GATE-INVISIBLE (same audit as TfxWouldBeDeletedInteractive): the effect name is not a recognised
// replacement keyword and it is a new-model ActivateClass with no EffectRegistry binding, so HasPreOption is
// FALSE → the sink's batch is NOT gate-deferred → the AS-IS PRE cut-in window is the sole firing path. No real
// card has this number, so it is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class TfxArmorPurgeWouldBeDeleted : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxArmorPurgeWouldBeDeleted", CanUseCondition, card);
            // isOptional:true — <Armor Purge> is a "you may" replacement (the interactive cut-in pauses).
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true,
                "[When this Digimon would be deleted] You may trash the top card instead (Armor Purge). (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                // AS-IS CanTriggerArmorPurge shape: this permanent is one of the would-be-deleted targets.
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent) &&
                       CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent == targetPermanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // AS-IS ArmorPurgeProcess: trash the top card, promote the under-source (the permanent survives),
                // then cancel the pending deletion so the sink's survivor read spares the (now-promoted) permanent.
                if (targetPermanent != null)
                {
                    await DeDigivolveHelpers.ArmorPurgeTopAsync(
                        card.Context.CardInstanceRepository, card.Context.ZoneMover,
                        targetPermanent.InstanceId, card.Context.GameEventQueue);
                    targetPermanent.willBeRemoveField = false;
                }
            }
        }

        return cardEffects;
    }
}
