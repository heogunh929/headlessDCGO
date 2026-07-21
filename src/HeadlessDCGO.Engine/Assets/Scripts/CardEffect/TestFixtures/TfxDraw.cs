// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Main] (OptionSkill) draws 2
// cards via the AS-IS `new DrawClass(...).Draw()` coroutine idiom (BT1_046). Used by tests/BT-PRE-A1 (G9-015).
// Inert in actual play (no real card numbered "TfxDraw").
// (이연③-h) Re-written from the retired invented `DrawEffect` to the AS-IS DrawClass idiom, wrapped in an
// inline ActivateClass.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 2", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[Main] Draw 2 cards.");
            cardEffects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 2, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
