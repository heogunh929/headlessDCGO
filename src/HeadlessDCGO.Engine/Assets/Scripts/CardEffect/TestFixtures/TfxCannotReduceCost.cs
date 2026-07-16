// TEST FIXTURE (not a real card). Returns a CannotReduceCostClass kind-class at EffectTiming.None built from the
// static config the harness sets before placing the card (the restricted payer + which played card + cost scope).
// Consumed by the AS-IS-literal live scan Player.CanReduceCost (reached by the cost pipeline
// CardSource.GetPayingCostWithBaseCost — R2-C ③, the registry-key immunity flip). Used by
// tests/PRIM-P0.CannotReduceCost + tests/FAILa-05. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxCannotReduceCost : CEntity_Effect
{
    /// <summary>Which paying player the restriction targets (AS-IS playerCondition). Set by the harness before
    /// the fixture card is placed on the field.</summary>
    public static Func<Player, bool> PlayerCondition = _ => true;

    /// <summary>Which played card the restriction targets (AS-IS cardCondition). Set by the harness.</summary>
    public static Func<CardSource, bool> CardCondition = _ => true;

    /// <summary>Which cost path the restriction protects (AS-IS targetPermanentsCondition). Set by the harness.</summary>
    public static CostReductionScope CostKind = CostReductionScope.Both;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotReduceCostStaticEffect(
                playerCondition: PlayerCondition,
                cardCondition: CardCondition,
                isInheritedEffect: false,
                card: card,
                condition: null,
                costKind: CostKind));
        }
        return effects;
    }
}
