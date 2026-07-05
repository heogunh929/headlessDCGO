// TEST FIXTURE. A DigiXros card whose single material slot may be satisfied by a card UNDER A TAMER
// (digivolution source, count 1). Exercises the max-under-Tamer DigiXros material extension.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class TfxMaxUnderTamerXros : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.DigiXrosWithExtraMaterialsEffect(
                card, costReduction: 0, maxTrashCount: null, maxUnderTamerCount: _ => 1,
                new SpecialPlayMaterial(cs => cs.CardNumber == "MAT", "MAT")));
        }
        return effects;
    }
}
