// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs
// (SKEL-Exhaust) 1:1 mirror of the AS-IS GainIgnoreDigivolutionRequirementPlayerEffect factory-wiring. Latent
// (0 callers). No coroutine (AS-IS already returns Func<EffectTiming, ICardEffect>); pure substrate wiring.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>GainIgnoreDigivolutionRequirementPlayerEffect</c>
    /// (GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs): grant the player an effect that ignores
    /// digivolution requirements and fixes the digivolution cost (until fixed-cost calculation), gated on the
    /// given permanent / card predicates; returned as an effect-timing selector for storage.</summary>
    public static Func<EffectTiming, ICardEffect>? GainIgnoreDigivolutionRequirementPlayerEffect(
        Func<Permanent, bool>? permanentCondition,
        Func<CardSource, bool>? cardCondition,
        bool ignoreDigivolutionRequirement,
        int digivolutionCost,
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

        CardSource card = activateClass.EffectSourceCard;

        bool PermanentCondition(Permanent permanent) => permanentCondition is null || permanentCondition(permanent);

        bool CardCondition(CardSource cardSource) => cardCondition is null || cardCondition(cardSource);

        var addDigivolutionRequirementClass = CardEffectFactory.AddDigivolutionRequirementStaticEffect(
            permanentCondition: PermanentCondition,
            cardCondition: CardCondition,
            ignoreDigivolutionRequirement: ignoreDigivolutionRequirement,
            digivolutionCost: digivolutionCost,
            isInheritedEffect: false,
            card: card,
            condition: null!,
            effectName: "Ignore Digivolution requirements and change digivolution cost");

        return GetCardEffectByEffectTiming(timing: EffectTiming.None, cardEffect: addDigivolutionRequirementClass);
    }
}
