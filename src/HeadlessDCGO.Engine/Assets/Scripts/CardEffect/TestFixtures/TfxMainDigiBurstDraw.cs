// TEST FIXTURE (not a real card). "[Main] <Digi-Burst 2> Draw 1" — a NON-uniform activated skill (Digi-Burst
// body) returned at OnDeclaration, the shape of ST4_13's real [Main] Digi-Burst. Exercises the declare-gate:
// AS-IS CanDigiBurst (the ActivateClass's CanUseCondition) offers this [Main] skill only when the permanent
// holds >= 2 TRASHABLE digivolution sources, so CanDeclareAt (the generic ActivateICardEffect `CanUse(null)`
// path) must NOT surface it for a permanent that cannot pay.
// (이연③-h) Re-written from the retired invented `CardEffectFactory.DigiBurstEffect` to the AS-IS inline
// `new IDigiBurst(permanent, N, activateClass)` idiom (ST4_13.cs): CanUseCondition = `CanDigiBurst()`;
// ActivateCoroutine awaits `IDigiBurst.DigiBurst()` (controller-selected sources + OnUseDigiburst + trash) THEN
// `new DrawClass(...).Draw()`. The DigiBurstActivatedEffect declare-gate special case is retired with it — the
// generic CanDeclareAt ActivateICardEffect path drives CanUse(null) -> CanDigiBurst.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMainDigiBurstDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[Main] <Digi-Burst 2> Draw 1.");
            effects.Add(activateClass);

            bool CanUseCondition(Hashtable _hashtable) =>
                new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).CanDigiBurst();

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).DigiBurst();
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
