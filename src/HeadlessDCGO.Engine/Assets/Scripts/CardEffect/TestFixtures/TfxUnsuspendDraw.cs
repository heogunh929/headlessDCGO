// TEST FIXTURE. "[Your Turn][Once Per Turn] when this unsuspends, draw 1" — TRIGGERED ACTIVATED at
// OnUnTappedAnyone (subject-scoped, MULTI-fire per turn). Exercises the v3 once-per-turn cap.
// (이연③-h) Draw body re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046). The "[Once Per Turn]" cap is now the AS-IS
// MaxCountPerTurn=1 on the ActivateClass (ICardEffect.CanActivate / isOverMaxCountPerTurn), consumed by the
// resolver's register-before-body callback — the AS-IS cap mechanism, not the invented stub's implicit one.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxUnsuspendDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnUnTappedAnyone && CardEffectCommons.IsOwnerTurn(card))
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, "[Your Turn][Once Per Turn] When this unsuspends, Draw 1.");
            effects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
