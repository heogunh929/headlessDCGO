// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnLeaveFieldAnyone reactor: whenever any Digimon
// leaves the battle area, its controller loses 1 memory — with NO once-per-turn cap. This is the witness the
// real AD1_025 cannot be (AD1_025 is [Once Per Turn], so its cap ALONE collapses N fires to one, hiding whether
// the D-2 batch-collapse is doing anything). Uncapped, the observable splits: a batch of N simultaneous leaves
// costs -1 IFF the collapse fires the reactor once, -N if the bridge fired per CardMoved. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the `MemoryBody(-1)` body becomes
// `card.Owner.AddMemory(-1, activateClass)` (memory LOSS). Anyone-scoped, unconditional CanUse (the batch-collapse
// itself is under test, not a scope gate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnLeaveFieldCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnLeaveFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When any Digimon leaves the battle area, lose 1 memory (uncapped).";

            // Anyone-scoped, unconditional (the batch-collapse itself is under test, not a scope gate); the
            // reactor stays on the field while OTHER cards leave.
            bool CanUseCondition(Hashtable hashtable) => true;

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(-1, activateClass);
            }
        }

        return effects;
    }
}
