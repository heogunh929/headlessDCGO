// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotSuspend (…/GiveEffectToPermanent/CanNotSuspend.cs:34-69)
// and GainCantSuspendUntilOpponentTurnEnd (:8-28): grant the TARGET permanent a timed "can't suspend"
// restriction. Builds the AS-IS kind-class via CardEffectFactory.CantSuspendStaticEffect (PermanentCondition =
// permanent==target, live CanUseCondition = on-battle-area && (condition==null||condition()) &&
// !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration bucket via
// AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by Permanent.CanSuspend
// (ICanNotSuspendEffect scan over EffectList(None)), consumed by CanActivateSuspendCostEffect — the registry
// joint arm goes silent. The AS-IS coroutine only drove the CreateDebuffEffect UI visual (dropped). The public
// AS-IS-signature `Task` overloads thread the LIVE `activateClass` as the CanNotBeAffected cause; the
// CardSource-only substrate overloads (CardEffectCommons.cs) collapse the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCantSuspendUntilOpponentTurnEnd</c> (GiveEffect/GiveEffectToPermanent/
    /// CanNotSuspend.cs:8) — the AS-IS-signature overload: delegates to the shared body with the
    /// UntilOpponentTurnEnd duration and an always-true CanUse (AS-IS :17).</summary>
    public static async Task GainCantSuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)
    {
        if (targetPermanent is null) { await Task.CompletedTask; return; }              // AS-IS :10
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) { await Task.CompletedTask; return; }  // AS-IS :11
        if (activateClass is null || activateClass.EffectSourceCard is null) { await Task.CompletedTask; return; }  // AS-IS :12-13

        static bool CanUseCondition() => true;                                          // AS-IS :17
        string effectName = "Can't suspend until the end of this card's owner's turn";  // AS-IS :19

        GainCanNotSuspendImpl(                                                          // AS-IS :21-27
            targetPermanent, EffectDuration.UntilOpponentTurnEnd,
            card: activateClass.EffectSourceCard, cause: activateClass, CanUseCondition, effectName);
        await Task.CompletedTask;
    }

    /// <summary>1:1 mirror of AS-IS <c>GainCanNotSuspend</c> (GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs:34)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause. <paramref name="condition"/> is folded into the live CanUse.</summary>
    public static async Task GainCanNotSuspend(
        Permanent targetPermanent,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        Func<bool> condition,
        string effectName)
    {
        // AS-IS :36-39 guards.
        if (targetPermanent is null || !IsPermanentExistsOnBattleArea(targetPermanent)
            || activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotSuspendImpl(
            targetPermanent, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, condition, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overloads (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). <paramref name="cause"/> is the effect passed to the live
    /// <c>CanNotBeAffected</c> guard (AS-IS threads <c>activateClass</c>; the source-only path passes
    /// <see cref="BareCauseEffect"/>).</summary>
    private static bool GainCanNotSuspendImpl(
        Permanent? targetPermanent,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        Func<bool>? condition,
        string effectName)
    {
        if (targetPermanent is null) return false;                          // AS-IS :36
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;  // AS-IS :37
        if (card is null || cause is null) return false;                    // AS-IS :38-39

        bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;  // AS-IS :43

        bool CanUseCondition()                                                          // AS-IS :45-59
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (condition is null || condition())
                {
                    if (!targetPermanent.TopCard.CanNotBeAffected(cause))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        CardEffects.CanNotSuspendClass canNotSuspendClass = CardEffectFactory.CantSuspendStaticEffect(  // AS-IS :61
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(  // AS-IS :63
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotSuspendClass,
            timing: EffectTiming.None);

        // AS-IS :65-68 conditionally ran CreateDebuffEffect (a UI icon), immunity-gated — pure visual; dropped.
        return true;
    }
}
