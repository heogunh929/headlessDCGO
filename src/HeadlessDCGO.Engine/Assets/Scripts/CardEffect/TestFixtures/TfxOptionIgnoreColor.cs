// TEST FIXTURE (not a real card). Returns the intrinsic self ignore-color-condition effect (AS-IS
// IgnoreColorConditionClass / UseRequirements) at EffectTiming.None, so an option carrying it can be played
// ignoring its color requirement. Used by tests/RD2-OptionColor to prove the option's OWN ignore-color (which
// lives on an unregistered hand card) is dispatch-built and honoured. Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOptionIgnoreColor : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            // (④) unconditional self ignore-color. The old-model ContinuousSelfRestrictionEffect (which wrote
            // DigivolveAction.IgnoreColorRequirementKey, read by the retired OptionColorRequirement path 1b) is
            // deleted. Emit the AS-IS new-model IgnoreColorConditionClass instead (IIgnoreColorConditionEffect),
            // which the LIVE path 1c (CardSource.IgnoreColorConditionActive — the AS-IS three-region ignore scan,
            // region 3 = the card itself) sees even for a hand card. cardCondition = this card; CanUse always true
            // (unconditional), preserving tests/RD2-OptionColor (an option in hand with NO field permanent is
            // playable via its OWN ignore-color).
            var ignore = new IgnoreColorConditionClass();
            ignore.SetUpICardEffect("Ignore color requirements", _ => true, card);
            ignore.SetUpIgnoreColorConditionClass(cardCondition: cs => cs == card);
            effects.Add(ignore);
        }

        return effects;
    }
}
