// TEST FIXTURE (not a real card). A GATE-LESS OnEndAttack memory probe: CanUse checks ONLY the self/attacker
// membership (CanTriggerOnAttack — subject-based, zone-INDEPENDENT) and CanActivate is unconditional (NO
// IsExistOnBattleArea gate). This isolates the emit-time attacker-alive guard in AttackPipeline.AdvanceEndAttackAsync:
//   * WITH the guard: a self-deleted / battle-knocked-out attacker (now in the trash) produces NO OnEndAttack event,
//     so this reactor stays silent (+0).
//   * WITHOUT the guard: the queued event would still be published, ScanZones visits the trash, and this gate-less
//     reactor self-gates true (SubjectPermanentContains: TriggerEntityId == card) and OVER-fires (+1).
// The ordinary TfxOnEndAttackCounter carries an IsExistOnBattleArea CanActivate gate, so it can NOT detect a broken
// emit guard (its own gate rejects a trashed attacker regardless) — hence this gate-less sibling. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the gate-less shape is preserved —
// CanUseCondition = CanTriggerOnAttack only, CanActivateCondition = unconditional true (NO IsExistOnBattleArea).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnEndAttackCounterNoGate : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[test] gate-less OnEndAttack memory probe (isolates the emit-time attacker-alive guard).";

            // self/attacker membership ONLY, zone-independent.
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerOnAttack(hashtable, card);

            // NO IsExistOnBattleArea gate — unconditional.
            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
