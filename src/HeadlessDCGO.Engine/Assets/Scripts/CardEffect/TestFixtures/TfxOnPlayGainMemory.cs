// TEST FIXTURE (not a real card). "[On Play] gain 2 memory" — a MUTATION-style trigger (IHeadlessCardEffect)
// at OnEnterFieldAnyone. Exercises the G3 activateETB suppression: when a card is played by an effect with
// activateETB:false (BT3_109/110), the played card's OWN [On Play]/OnEnterField trigger must NOT fire, while
// activateETB:true fires it normally. Inert in actual play beyond this.
//
// R3-C2b-2: the production old-model AddMemoryTriggerEffect was deleted, but the G3 suppression test drives the
// STILL-LIVE registry-collection subsystem (AutoProcessingTriggerCollector.CollectAllTriggers reads the card's
// registered OnEnterField binding), so this fixture uses the test-scoped old-model TfxTriggeredMemoryEffect
// (registers a ToBinding registry binding) — the same registry path it used before.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOnPlayGainMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(new TfxTriggeredMemoryEffect(
                card, EffectTiming.OnEnterFieldAnyone, amount: 2, isInheritedEffect: false, condition: null,
                description: "[On Play] Gain 2 memory."));
        }

        return effects;
    }
}
