// TEST FIXTURE. Burst Digivolution: this hand card digivolves onto a battle-area "TARGET" Digimon while a
// "TAMER" is returned to the hand (AS-IS BurstDigivolutionCondition). Exercises SpecialPlayKind.Burst.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxBurstDigivolve : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.BurstDigivolveEffect(
                card,
                digimonCondition: cs => cs.CardNumber == "TARGET",
                tamerCondition: cs => cs.CardNumber == "TAMER",
                cost: 0));
        }
        return effects;
    }
}
