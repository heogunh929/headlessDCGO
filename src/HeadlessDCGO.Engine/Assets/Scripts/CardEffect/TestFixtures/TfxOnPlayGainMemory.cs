// TEST FIXTURE (not a real card). "[On Play] gain 2 memory" — a MUTATION-style trigger (IHeadlessCardEffect)
// at OnEnterFieldAnyone. Exercises the G3 activateETB suppression: when a card is played by an effect with
// activateETB:false (BT3_109/110), the played card's OWN [On Play]/OnEnterField trigger must NOT fire, while
// activateETB:true fires it normally. Inert in actual play beyond this.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOnPlayGainMemory : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                EffectTiming.OnEnterFieldAnyone, amount: 2, isInheritedEffect: false, card: card,
                condition: null, description: "[On Play] Gain 2 memory."));
        }

        return effects;
    }
}
