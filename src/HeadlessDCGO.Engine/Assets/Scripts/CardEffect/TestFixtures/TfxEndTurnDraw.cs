// TEST FIXTURE. "[End of Your Turn] draw 1" — a TRIGGERED ACTIVATED effect at the OnEndTurn BOUNDARY timing
// (no card subject). Exercises the v2 bridge (scan all battle cards at boundary timings). Gates to the owner's
// turn so it only fires on the controller's end-of-turn.
// (이연③-h) Draw body re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046), wrapped in an inline ActivateClass.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxEndTurnDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        // "[End of YOUR Turn]" — only on the owner's turn.
        if (timing == EffectTiming.OnEndTurn && CardEffectCommons.IsOwnerTurn(card))
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[End of Your Turn] Draw 1.");
            effects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
