// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDigivolutionCost.cs
// (SKEL-Exhaust) 1:1 mirror of the AS-IS ChangeDigivolutionCostPlayerEffect factory-wiring. Latent (0 callers).
// No coroutine (AS-IS already returns Func<EffectTiming, ICardEffect>); pure substrate wiring.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>ChangeDigivolutionCostPlayerEffect</c> (GiveEffect/GiveEffectToPlayer/ChangeDigivolutionCost.cs):
    /// build a player-scope digivolution-cost delta (until fixed-cost calculation), gated on the given permanent /
    /// card / root predicates, and return it as an effect-timing selector for storage.</summary>
    public static Func<EffectTiming, ICardEffect>? ChangeDigivolutionCostPlayerEffect(
        Func<Permanent, bool>? permanentCondition,
        Func<CardSource, bool>? cardCondition,
        Func<SelectCardEffect.Root, bool>? rootCondition,
        int changeValue,
        bool setFixedCost,
        ICardEffect activateClass)
    {
        if (activateClass is null)
        {
            return null;
        }

        if (activateClass.EffectSourceCard is null)
        {
            return null;
        }

        bool Condition() => true;

        bool PermanentCondition(Permanent permanent) => permanentCondition is null || permanentCondition(permanent);

        bool CardCondition(CardSource cardSource) => cardCondition is null || cardCondition(cardSource);

        bool RootCondition(SelectCardEffect.Root root) => rootCondition is null || rootCondition(root);

        var changeCostClass = CardEffectFactory.ChangeDigivolutionCostStaticEffect<int>(
            changeValue: changeValue,
            permanentCondition: PermanentCondition,
            cardCondition: CardCondition,
            rootCondition: RootCondition,
            isInheritedEffect: false,
            card: activateClass.EffectSourceCard,
            condition: Condition,
            setFixedCost: setFixedCost);

        return GetCardEffectByEffectTiming(timing: EffectTiming.None, cardEffect: changeCostClass);
    }
}
