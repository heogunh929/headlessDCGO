// TEST FIXTURE (not a real card). "[Your Turn] when an Option is used, gain 2 memory" — a TRIGGERED
// ACTIVATED effect at OnUseOption. Exercises the G2 dispatch: OnUseOption is emitted at option use
// (OptionActivateAction) and must now be broadcast by GameFlowProcessor to reacting field cards. Inert in
// actual play beyond this.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): `MemoryBody(2)` ->
// `card.Owner.AddMemory(2, activateClass)`; canUse:null (unconditional) -> CanUseCondition returning true.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnUseOptionMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnUseOption)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +2", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() => "[Your Turn] When an Option is used, gain 2 memory.";

            bool CanUseCondition(Hashtable hashtable) => true;

            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(2, activateClass);
            }
        }

        return effects;
    }
}
