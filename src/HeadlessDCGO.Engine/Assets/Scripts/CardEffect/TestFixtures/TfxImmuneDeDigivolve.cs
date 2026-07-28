// TEST FIXTURE (not a real card). Returns a self-scope ImmuneFromDeDigivolve kind-class at EffectTiming.None
// (AS-IS ImmuneFromDeDigivolveClass — "Isn't affected by <De-Digivolve>"). Consumed by the AS-IS-literal live
// getter Permanent.ImmuneFromDeDigivolve() (R3-W3c-4c B5), reached uniformly through
// Permanent.ImmuneFromDeDigivolve (CardController IDegeneration / IMassDegeneration). Used by
// tests/FAILb-02. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxImmuneDeDigivolve : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            // self-scope (permanentCondition null → protects exactly this card's permanent).
            effects.Add(CardEffectFactory.ImmuneFromDeDigivolveStaticEffect(
                permanentCondition: null, isInheritedEffect: false, card, condition: null));
        }
        return effects;
    }
}
