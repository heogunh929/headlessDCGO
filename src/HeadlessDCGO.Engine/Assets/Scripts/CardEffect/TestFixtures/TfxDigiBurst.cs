// TEST FIXTURE. "[Digi-Burst 2] Draw 1" — trash 2 of this card's own digivolution sources, then draw 1
// (AS-IS IDigiBurst). Exercises the Digi-Burst subsystem. Resolved via OptionSkill for the test harness.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiBurst : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(CardEffectFactory.DigiBurstEffect(
                card, count: 2, CardEffectFactory.DrawCardsEffect(card, 1), "[Digi-Burst 2] Draw 1."));
        }
        return effects;
    }
}
