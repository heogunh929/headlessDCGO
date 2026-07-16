// TEST FIXTURE (not a real card). Returns a CannotIgnoreDigivolutionCondition kind-class at EffectTiming.None
// (AS-IS BT8_059 "Players can't ignore digivolution requirements", global lock). Consumed by the AS-IS-literal
// live veto scan Player.CanIgnoreDigivolutionRequirement (R3-W3c-4b D). Used by tests/FAILd-06. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxCannotIgnoreDigivolution : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CannotIgnoreDigivolutionConditionStaticEffect(
                (_, _) => true, card, condition: null));
        }
        return effects;
    }
}
