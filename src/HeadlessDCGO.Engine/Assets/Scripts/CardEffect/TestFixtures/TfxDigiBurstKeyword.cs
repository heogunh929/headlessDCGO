// TEST FIXTURE. "[Digi-Burst 2] gain Piercing" — the Digi-Burst body is a CONTINUOUS keyword grant (not an
// activated effect), so it is REGISTERED (not resolved). Exercises the continuous-inner Digi-Burst path.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiBurstKeyword : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            // (R6-Da'-4 / RD-P6B-6) the body is a CONTINUOUS keyword-static grant — pass the keyword's live-read
            // timing (Pierce reads OnDetermineDoSecurityCheck, NewModelContinuousScan.HasPierce) so the resolver
            // registers it into the permanent's AS-IS duration bucket rather than running its no-op coroutine.
            effects.Add(CardEffectFactory.DigiBurstEffect(
                card, count: 2, CardEffectFactory.PierceSelfEffect(false, card, null), "[Digi-Burst 2] This gains Piercing.",
                grantTiming: EffectTiming.OnDetermineDoSecurityCheck));
        }
        return effects;
    }
}
