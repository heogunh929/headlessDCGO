// TEST FIXTURE (not a real card). Returns the intrinsic self ignore-color-condition effect (AS-IS
// IgnoreColorConditionClass / UseRequirements) at EffectTiming.None, so an option carrying it can be played
// ignoring its color requirement. Used by tests/RD2-OptionColor to prove the option's OWN ignore-color (which
// lives on an unregistered hand card) is dispatch-built and honoured. Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOptionIgnoreColor : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            // unconditional self ignore-color (cardCondition/condition null -> gate null).
            effects.Add(CardEffectFactory.UseRequirements(card));
        }

        return effects;
    }
}
