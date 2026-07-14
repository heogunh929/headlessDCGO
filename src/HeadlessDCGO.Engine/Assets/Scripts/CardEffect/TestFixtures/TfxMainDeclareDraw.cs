// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) UNIFORM activated effect at OnDeclaration —
// "[Main] [Once Per Turn] draw 1" — the shape of a player-declared [Main] skill (AS-IS OnDeclaration
// ActivateICardEffect, e.g. BT11_061 / BT13_050). Exercises B-2 (P1-5): the MainSkillActivateAction offers it as
// an ActivateMain legal move while under cap, resolving it consumes the per-turn use (shared resolver), and once
// spent CanDeclareAt no longer offers it — until ResetForTurn. Non-interactive body so it always executes.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the DECLARED [Main] path gates on
// CanUse(null) at declaration, so CanUseCondition tolerates a null hashtable; ORDER=1 (once-per-turn). `DrawBody(1)`
// -> the AS-IS `new DrawClass(...).Draw()` coroutine idiom (BT1_046).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMainDeclareDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() => "[Main] [Once Per Turn] Draw 1.";

            bool CanUseCondition(Hashtable hashtable) => true;

            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return effects;
    }
}
