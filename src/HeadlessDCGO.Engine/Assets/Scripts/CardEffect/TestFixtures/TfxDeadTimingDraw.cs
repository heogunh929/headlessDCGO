// TEST FIXTURE (F1-DEAD). "Draw 1" returned at each of the 6 AS-IS DEAD timings (OnEndAttackPhase /
// OnEndBlockDesignation / OnEndCoinToss / OnEndMainPhase / OnGetDamage / OnKnockOut). No real card reacts at
// these timings (0 cards, verified), so this fixture exists ONLY to prove the M0 dead-timing INFRA works: the
// enum member exists, the ActivatedBridgeTimings set classification is picked up, and CollectActivatedBridgeTriggers
// synthesises an activated marker when a driving event at that timing is supplied. It is NOT an AS-IS fidelity
// mirror — the draw body is an arbitrary activated effect to make the marker resolvable.
// (이연③-h) Draw body re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046), wrapped in an inline ActivateClass.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDeadTimingDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing is EffectTiming.OnEndAttackPhase
            or EffectTiming.OnEndBlockDesignation
            or EffectTiming.OnEndCoinToss
            or EffectTiming.OnEndMainPhase
            or EffectTiming.OnGetDamage
            or EffectTiming.OnKnockOut)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", _ => true, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, "Draw 1 (dead-timing fixture).");
            effects.Add(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }
        return effects;
    }
}
