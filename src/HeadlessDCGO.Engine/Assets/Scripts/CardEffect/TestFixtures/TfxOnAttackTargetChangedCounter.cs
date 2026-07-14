// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnAttackTargetChanged memory probe with the ANYONE gate
// (CanTriggerOnPermanentAttackTargetSwitch(_ => true)) — reacts to ANY attacker's target switch, from a TOP permanent
// (isInheritedEffect defaults to false). Complements the inherited witnesses (EX10_002 / ST15_02) by exercising the
// TOP-instance scan of the EventBroadcast bridge. Uncapped so the observable is unmasked:
//   * one target change fires it exactly ONCE (+1) — one emit, one subject (the attacker), no batch.
//   * an inherited-only reactor as a TOP permanent stays silent (membership) while this top probe fires.
// Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): 1:1 with ST15_02's OnAttackTargetChanged
// reactor except uncapped (ORDER=-1) and NOT inherited; `MemoryBody(1)` -> `card.Owner.AddMemory(1, activateClass)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnAttackTargetChangedCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When any attack target is switched, gain 1 memory (uncapped).";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(hashtable, _ => true);  // anyone

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
