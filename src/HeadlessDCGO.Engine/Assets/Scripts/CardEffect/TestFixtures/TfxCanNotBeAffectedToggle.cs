// TEST FIXTURE (not a real card). Identical to TfxCanNotBeAffected (a general opponent-effect immunity,
// CardEffectFactory.CanNotAffectedStaticEffect, self-only, opponent-relative skillCondition) EXCEPT the immunity
// is gated on the static <see cref="ImmunityActive"/> toggle. This lets a witness model a TEMPORARY immunity:
// grant an effect while immunity holds (it lands but is inert), then flip the toggle off and observe the SAME
// granted effect become ACTIVE — the AS-IS grant/re-application semantics the invented grant-time immunity guard
// (RD-J-01) broke. Used only by tests/Ba-P0-1. The immunity's CanUse re-reads the toggle live, so flipping it
// mid-test flips CardSource.CanNotBeAffected without rebuilding the card.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxCanNotBeAffectedToggle : CEntity_Effect
{
    /// <summary>Live toggle for the fixture's immunity. Default true (immune); a witness flips it to false to
    /// model immunity expiring. Reset to true after use — this is process-global static state.</summary>
    public static bool ImmunityActive = true;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotAffectedStaticEffect(
                permanentCondition: null,
                skillCondition: effect => CardEffectCommons.IsOpponentEffect(effect?.EffectSourceCard, card),
                isInheritedEffect: false, card, condition: () => ImmunityActive));
        }
        return effects;
    }
}
