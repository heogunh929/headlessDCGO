// TEST FIXTURE (not a real card). Returns the intrinsic self ignore-color-condition effect (AS-IS
// IgnoreColorConditionClass / UseRequirements) at EffectTiming.None, so an option carrying it can be played
// ignoring its color requirement. Used by tests/RD2-OptionColor to prove the option's OWN ignore-color (which
// lives on an unregistered hand card) is dispatch-built and honoured. Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class TfxOptionIgnoreColor : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            // unconditional self ignore-color (gate null).
            // (P6 compile fix, semantics preserved) The factory UseRequirements was re-ported to the AS-IS 1:1
            // shape (required cardCondition; CanUse gated on a MATCHING OWNER FIELD PERMANENT, and it returns the
            // new-model IgnoreColorConditionClass, which BuildContinuousRequests does not lower — stage-B scan
            // pending). This fixture's tested semantics (tests/RD2-OptionColor: an option in hand with NO field
            // permanent is playable via its OWN ignore-color) ride the legacy continuous flag that
            // OptionColorRequirement.Matches path (1b) reads, so construct the exact effect the old
            // UseRequirements(card) with all-null optionals returned.
            effects.Add(new ContinuousSelfRestrictionEffect(
                card, DigivolveAction.IgnoreColorRequirementKey, isInheritedEffect: false, condition: null));
        }

        return effects;
    }
}
