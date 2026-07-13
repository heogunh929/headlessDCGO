// Source: Assets/Scripts/CardEffect/BT2/BT2_050.cs
// Decision: PARTIAL STOP
// Category: CardEffect
// Migration: Ported per-card effect.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_050 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: BeforePayCost — <Digisorption -3>: AS-IS triggers an ActivateClass at digivolve-into-self, lets player
        //   optionally select & suspend 1 ally Digimon, then registers a ChangeCostClass -3 only if a Digimon was suspended.
        //   No headless primitive covers optional-suspend-as-cost combined with conditional BeforePayCostReduction;
        //   BeforePayCostReductionEffect has no optional-cost overload, and the suspend-then-reduce causality chain
        //   cannot be expressed with available factories.

        // STOP: None — AS-IS calls ChangeSelfSAttackStaticEffect(changeValue: () => count_of_own_suspended_excl_self,
        //   isInheritedEffect: false, card, condition: IsExistOnBattleArea && IsOwnerTurn && count >= 1).
        //   ChangeSelfSAttackStaticEffect (SecurityAttack stat) is not in the available CardEffectFactory symbol list;
        //   only ChangeSelfDPStaticEffect is listed, and substituting it would alter a different stat (DP vs. SecurityAttack).

        return cardEffects;
    }
}