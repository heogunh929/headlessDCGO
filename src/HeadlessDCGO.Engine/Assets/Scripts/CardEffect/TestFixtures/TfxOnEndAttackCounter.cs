// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnEndAttack reactor: whenever THIS Digimon's attack
// ends, its owner gains 1 memory — with NO once-per-turn cap. Uncapped so the observable is unmasked:
//   * one attack fires the reactor exactly ONCE (+1) — a per-attack event has a single subject (the attacker),
//     so there is no batch collapse to hide; +2 would signal a double-collect (hook + bridge).
//   * a card that did NOT attack does NOT gain (+0) — the self/attacker gate (CanTriggerOnAttack) rejects it.
//   * an attacker deleted before end-of-attack does NOT gain (+0) — the emit is guarded on attacker-alive.
// Self-scope (the reacting card must be a cardSource of the AttackingPermanent), mirroring every AS-IS OnEndAttack
// reactor. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): inline `new ActivateClass()` +
// SetUpICardEffect/SetUpActivateClass + local functions (recipe docs/audit/rebuild_p8_report_recipe.md). The
// `MemoryBody(1)` body becomes the AS-IS `card.Owner.AddMemory(1, activateClass)` coroutine idiom (BT1_081/ST15_02).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnEndAttackCounter : CEntity_Effect
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
                "[All Turns] When this Digimon's attack ends, gain 1 memory (uncapped).";

            // AS-IS CanUseCondition mirror: CanTriggerOnAttack (the reacting card is a cardSource of the
            // AttackingPermanent — the self/attacker gate shared by every OnEndAttack reactor).
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnAttack(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
