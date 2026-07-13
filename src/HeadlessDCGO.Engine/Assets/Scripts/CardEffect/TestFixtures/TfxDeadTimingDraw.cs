// TEST FIXTURE (F1-DEAD). "Draw 1" returned at each of the 6 AS-IS DEAD timings (OnEndAttackPhase /
// OnEndBlockDesignation / OnEndCoinToss / OnEndMainPhase / OnGetDamage / OnKnockOut). No real card reacts at
// these timings (0 cards, verified), so this fixture exists ONLY to prove the M0 dead-timing INFRA works: the
// enum member exists, the ActivatedBridgeTimings set classification is picked up, and CollectActivatedBridgeTriggers
// synthesises an activated marker when a driving event at that timing is supplied. It is NOT an AS-IS fidelity
// mirror — the draw body is an arbitrary activated effect to make the marker resolvable.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return effects;
    }
}
