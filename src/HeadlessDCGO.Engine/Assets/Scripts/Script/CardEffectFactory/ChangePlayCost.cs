// Source: Assets/Scripts/Script/CardEffectFactory/ChangePlayCost.cs
// AS-IS mirror of the original CardEffectFactory play-cost factories (ChangePlayCostStaticEffect<T> /
// MandatorySelfPlayCostReduction<T>). Structural-fidelity standard: same file location, same factory
// names/signatures as the original; behaviour lowered onto the headless play-cost engine. The factory
// methods are partials of the CardEffectFactory declared in CardPortingFramework.cs (CardEffectCommons
// namespace), so this file shares that namespace even though it mirrors the original's folder location.
//
// The original returns a registered ChangeCostClass (IChangeCostEffect.GetCost). The headless play-cost
// engine already pulls continuous cost modifiers from the EffectRegistry at play time
// (PlayCardAction -> ContinuousModifierGate.ResolvePlayCost, keyed by ModifierHelpers.PlayCostDeltaKey =
// NumericModifierMetric.PlayCost), respecting the CanReduceCost guard (the original's
// cardSource.Owner.CanReduceCost(...)). So each factory here lowers to a continuous play-cost modifier.
//
// Fidelity notes (documented, not silently dropped):
//  - The original splits "cost itself" (isChangePayingCost:false) from "paying cost"
//    (isChangePayingCost:true). The headless RL flow resolves a SINGLE play-cost number (what playing
//    actually costs in memory), so both lower to the same PlayCost metric — behaviour-equivalent for the
//    one observable cost, with no separate printed-vs-paid split to diverge on.
//  - permanentCondition is accepted for 1:1 source signature but, like ChangeDPStaticEffect, the headless
//    evaluates the plain `condition` gate; a card needing a board-permanent gate must fold it into
//    `condition`.
//  - setFixedCost:true (set the cost to a value) is not yet supported — the continuous self modifier only
//    emits an additive delta. It throws rather than silently treating "set" as "add".
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectFactory
{
    #region Static effect that changes play cost

    /// <summary>Original: <c>ChangePlayCostStaticEffect&lt;int&gt;</c> — continuous ±play cost. Negative
    /// <paramref name="changeValue"/> reduces, positive increases (the original's isUpDown = !setFixedCost,
    /// so a reduction is gated by CanReduceCost — enforced downstream by ContinuousModifierGate).</summary>
    public static ICardEffect ChangePlayCostStaticEffect(
        int changeValue,
        Func<Permanent, bool>? permanentCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        bool setFixedCost)
    {
        if (setFixedCost)
        {
            throw new NotSupportedException(
                "ChangePlayCostStaticEffect(setFixedCost:true) requires a fixed-play-cost continuous metric "
                + "that is not yet ported; only additive ±play cost is supported.");
        }

        return new ContinuousSelfModifierEffect(card, ModifierHelpers.PlayCostDeltaKey, changeValue, isInheritedEffect, condition);
    }

    /// <summary>Original: <c>ChangePlayCostStaticEffect&lt;Func&lt;int&gt;&gt;</c> — continuous ±play cost with
    /// a dynamic (read-time) value.</summary>
    public static ICardEffect ChangePlayCostStaticEffect(
        Func<int> changeValue,
        Func<Permanent, bool>? permanentCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        bool setFixedCost)
    {
        ArgumentNullException.ThrowIfNull(changeValue);
        if (setFixedCost)
        {
            throw new NotSupportedException(
                "ChangePlayCostStaticEffect(setFixedCost:true) requires a fixed-play-cost continuous metric "
                + "that is not yet ported; only additive ±play cost is supported.");
        }

        return new ContinuousSelfModifierEffect(card, ModifierHelpers.PlayCostDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);
    }

    #endregion

    #region Mandatory Self Cost Reduction

    /// <summary>Original: <c>MandatorySelfPlayCostReduction&lt;int&gt;</c> — reduce THIS card's play cost by
    /// <paramref name="changeValue"/> (a positive magnitude; the original does <c>cost -= _changeValue()</c>).
    /// Lowers to a continuous self play-cost modifier of <c>-changeValue</c>.</summary>
    public static ICardEffect MandatorySelfPlayCostReduction(
        int changeValue,
        CardSource card,
        Func<bool>? condition = null) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.PlayCostDeltaKey, -changeValue, isInheritedEffect: false, condition);

    /// <summary>Original: <c>MandatorySelfPlayCostReduction&lt;Func&lt;int&gt;&gt;</c> — dynamic-magnitude self
    /// play-cost reduction.</summary>
    public static ICardEffect MandatorySelfPlayCostReduction(
        Func<int> changeValue,
        CardSource card,
        Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(changeValue);
        return new ContinuousSelfModifierEffect(
            card, ModifierHelpers.PlayCostDeltaKey, changeValue: 0, isInheritedEffect: false, condition, dynamicValue: () => -changeValue());
    }

    #endregion
}
