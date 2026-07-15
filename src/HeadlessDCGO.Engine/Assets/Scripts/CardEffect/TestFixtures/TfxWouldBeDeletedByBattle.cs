// TEST FIXTURE (not a real card). Sibling of TfxWouldBeDeleted, but its [WhenPermanentWouldBeDeleted] replacement
// REQUIRES the deletion to be a BATTLE deletion (CardEffectCommons.IsByBattle) — the shape of AS-IS Barrier
// (Factory/Barrier.cs:64 IsByBattle). Used by the C-Del 3c-1b BATTLE PRE cut-in transport witness to prove the
// BattleResolver stamps the byBattle cause on the cut-in hashtable (WhenPermanentWouldRemoveFieldCheckHashtable
// byBattle overload → ByBattleCauseKey → IsByBattle dual-read): this card SURVIVES a battle deletion (byBattle
// true) but is NOT saved from an effect deletion (byBattle false), proving the cause is threaded, not synthesized.
//
// DELIBERATELY GATE-INVISIBLE (same audit as TfxWouldBeDeleted): the effect name is "TfxWouldBeDeletedByBattle",
// NOT a recognised replacement keyword, and it is a new-model ActivateClass with no EffectRegistry binding, so
// HasPreOption is FALSE → the battle path is NOT gate-deferred → the AS-IS PRE cut-in window (the thing under
// test) is the sole firing path. No real card has this number, so it is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWouldBeDeletedByBattle : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxWouldBeDeletedByBattle", CanUseCondition, card);
            // isOptional:false — MANDATORY, order -1, uncapped.
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[When this Digimon would be deleted by battle] It is not deleted. (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                // AS-IS Barrier shape: this permanent is a would-be-deleted target AND the cause is a BATTLE
                // deletion (IsByBattle) — so it fires on battle losses only, not on effect deletions.
                return CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent == targetPermanent) &&
                       CardEffectCommons.IsByBattle(hashtable);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent);
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                // AS-IS survival: cancel the pending deletion (willBeRemoveField=false). The battle survivor fix
                // then spares this card (never moving it to the trash).
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
