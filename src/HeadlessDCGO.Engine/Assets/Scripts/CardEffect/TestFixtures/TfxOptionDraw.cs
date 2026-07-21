// TEST FIXTURE (not a real card, CardType Option). Its [Main] (OptionSkill) draws 2 cards. Played as a nested
// effect by TfxPlayOption to exercise PlayOptionCardEffect (Build Order 5). Inert in actual play.
// (이연③-h) Draw body re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046), wrapped in an inline ActivateClass.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOptionDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 2", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "[Main] Draw 2.");
            effects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 2, activateClass).Draw();
            }
        }
        return effects;
    }
}
