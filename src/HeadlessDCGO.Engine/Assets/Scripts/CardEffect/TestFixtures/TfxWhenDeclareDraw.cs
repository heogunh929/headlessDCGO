// TEST FIXTURE. "[When Attacking] (OnDeclaration) Draw 1" — an activated draw returned at the OnDeclaration
// (attack-declaration) timing, resolved by the auto-processing bridge (v4). This is the timing Digi-Burst bodies
// (BT6_028) are declared at.
// (이연③-h) Draw body re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046), wrapped in an inline ActivateClass.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWhenDeclareDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[When Attacking] Draw 1.");
            effects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
