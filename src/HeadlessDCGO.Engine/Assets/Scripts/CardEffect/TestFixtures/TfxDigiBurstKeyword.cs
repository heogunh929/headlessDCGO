// TEST FIXTURE. "[Digi-Burst 2] gain Piercing" — the Digi-Burst body is a CONTINUOUS keyword grant (not an
// activated effect), so it is REGISTERED (not resolved). Exercises the continuous-inner Digi-Burst path.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiBurstKeyword : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(CardEffectFactory.DigiBurstEffect(
                card, count: 2, CardEffectFactory.PierceSelfEffect(false, card, null), "[Digi-Burst 2] This gains Piercing."));
        }
        return effects;
    }
}
