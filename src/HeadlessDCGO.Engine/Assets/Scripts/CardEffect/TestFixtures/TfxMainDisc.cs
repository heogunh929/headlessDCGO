// TEST FIXTURE (not a real card). Two OptionSkill activated effects: one [Main]-tagged (gains 1 memory) and one
// NOT [Main]-tagged (draws 1 card). Used by tests/FAILa-13 to prove ActivateMainOfOptionSide / the [Security]
// [Main]-reuse resolve ONLY the [Main] effect (AS-IS OptionMainEffect discriminator — the "[Main]" description
// prefix), not every OptionSkill effect. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the [Main] discriminator is the
// EffectDiscription prefix, preserved verbatim; `MemoryBody(1)` -> AddMemory, `DrawBody(1)` -> DrawClass.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMainDisc : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            // [0] the [Main]-tagged effect: gain 1 memory.
            ActivateClass mainEffect = new ActivateClass();
            mainEffect.SetUpICardEffect("Memory +1", _ => true, card);
            mainEffect.SetUpActivateClass(_ => true, _ => card.Owner.AddMemory(1, mainEffect), -1, false, "[Main] Gain 1 memory.");
            effects.Add(mainEffect);

            // [1] a NON-[Main] effect: draw 1.
            ActivateClass drawEffect = new ActivateClass();
            drawEffect.SetUpICardEffect("Draw 1", _ => true, card);
            drawEffect.SetUpActivateClass(_ => true, DrawCoroutine, -1, false, "Draw 1 card.");
            effects.Add(drawEffect);

            async Task DrawCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, drawEffect).Draw();
            }
        }

        return effects;
    }
}
