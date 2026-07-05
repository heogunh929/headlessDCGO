// TEST FIXTURE. A DigiXros card whose single material slot may be satisfied by a card FROM THE TRASH
// (AddMaxTrashCountDigiXros, count 1). Exercises the max-trash DigiXros material extension.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class TfxMaxTrashXros : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.DigiXrosWithExtraMaterialsEffect(
                card, costReduction: 0, maxTrashCount: _ => 1, maxUnderTamerCount: null,
                new SpecialPlayMaterial(cs => cs.CardNumber == "MAT", "MAT")));
        }
        return effects;
    }
}
