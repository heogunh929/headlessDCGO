// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS ST15_02
// OnAttackTargetChanged block (ST15/Black) — the F1-Tier2 OnAttackTargetChanged INHERITED (anyone) witness.
//   [All Turns][Once Per Turn] "When an attack target is switched, gain 1 memory."
// AS-IS: ActivateClass on OnAttackTargetChanged, SetIsInheritedEffect(true), SetHashString("Memory+1_ST15_02"),
// ORDER=1 ([Once Per Turn]), ISOPTIONAL=false. CanUseCondition = IsExistOnBattleArea &&
// CanTriggerOnPermanentAttackTargetSwitch(permanent => true) [ANYONE]. CanActivateCondition =
// IsExistOnBattleArea && card.Owner.CanAddMemory(activateClass). ActivateCoroutine = card.Owner.AddMemory(1).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.CanAddMemory(activateClass)`
// -> `new Player(card.Context, card.Owner).CanAddMemory(activateClass)` (bridge-W4 Player handle, retires
// MIG5-CANADDMEMORY); `card.Owner.AddMemory(1, activateClass)` -> the mirror HeadlessPlayerId extension.
//
// The AS-IS timing==None (AddSelfDigivolutionRequirementStaticEffect, Koromon) and timing==OnStartMainPhase
// (Memory +1 with an IsOwnerTurn gate) effects are ORTHOGONAL to the OnAttackTargetChanged reactor under test
// and are deliberately OMITTED (same witness scoping as the other F1 witnesses).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST15.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST15_02 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Memory+1_ST15_02");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once Per Turn] When an attack target is switched, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(hashtable, permanent => true))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).CanAddMemory(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
