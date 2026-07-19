// TEST FIXTURE (not a real card). Returns a ChangeEndTurnMinMemory kind-class at EffectTiming.None (AS-IS
// BT14_081/BT17_069 "the turn-end condition is the opponent having 3 or more memory"). Consumed by the
// AS-IS-literal live scan AutoProcessing.TurnEndMinMemory (:645-672; the sole owner since the 4b B6
// retirement of the invented flow copy). Used by tests/FAILd-07.
// Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxChangeEndTurnMinMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.ChangeEndTurnMinMemoryStaticEffect(
                minMemory: 3, isInheritedEffect: false, card, condition: null));
        }
        return effects;
    }
}
