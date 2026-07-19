// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnPermamemtReturnedToHand reactor: whenever any
// Digimon is returned to hand (a HAND bounce), its controller loses 1 memory — with NO once-per-turn cap. The
// hand-bounce-specific twin of TfxOnLeaveFieldCounter: OnPermamemtReturnedToHand fires ONLY on a field->Hand
// bounce (AS-IS HandBounceClaass.Bounce CardController.cs:2692), never on a deck bounce or a security put, so it
// isolates the RDW-01 hand-bounce arm. Uncapped so a batch of N simultaneous hand-bounces splits the observable:
// -1 IFF the reactor fired once (AS-IS single-list StackSkillInfos over the bounce list), -N per-CardMoved. Inert
// in actual play. Anyone-scoped, unconditional CanUse (the wiring is under test, not a scope gate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnReturnToHandCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnPermamemtReturnedToHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When any Digimon is returned to the hand, lose 1 memory (uncapped).";

            bool CanUseCondition(Hashtable hashtable) => true;

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(-1, activateClass);
            }
        }

        return effects;
    }
}
