// TEST FIXTURE (not a real card). An OPTIONAL, CAPPED [Main] declaration skill (<Draw 1>), for the AS-IS
// declaration-path register order: the main loop registers the per-turn use BEFORE the optional prompt
// (TurnStateMachine.cs:1183-1186 — register, then ActivateEffectProcess asks yes/no), so DECLINING a declared
// capped [Main] skill leaves the use CONSUMED (that path has no RemoveUse). The resolver's `declarative` flavor
// mirrors this: consume before the optional yes/no. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): ISOPTIONAL=true, ORDER=1; the
// declared [Main] path registers the use before the OptionalSkill yes/no. `DrawBody(1)` -> `new DrawClass(...).Draw()`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxMainOptionalDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() => "[Main] [Once Per Turn] You may draw 1 card.";

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
