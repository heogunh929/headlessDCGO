// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotBeBlocked (…/GiveEffectToPermanent/CanNotBeBlocked.cs
// :10-68): grant the TARGET permanent a timed "can't BE blocked by THIS blocker" restriction. Builds the AS-IS
// kind-class via CardEffectFactory.CanNotBlockStaticEffect with the roles MIRRORED vs GainCanNotBlock
// (AttackerCondition = attacker==target, DefenderCondition = caller `defenderCondition` wrap, live CanUseCondition
// = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration bucket via
// AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by the interface scan
// (NewModelContinuousScan.CanNotBeBlocked / CannotBlockJoint over EffectList(None)), which the
// ContinuousRestrictionGate (EvaluateBeBlocked, CannotBeBlockedKey) already unions — producer-only. The AS-IS
// coroutine only drove the CreateBuffEffect UI visual (dropped). The public AS-IS-signature `Task` overload
// threads the LIVE `activateClass` as the CanNotBeAffected cause; the CardSource-only substrate overload
// (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotBeBlocked</c> (GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause. <paramref name="defenderCondition"/> narrows WHICH blockers cannot block
    /// this permanent.</summary>
    public static async Task GainCanNotBeBlocked(
        Permanent targetPermanent,
        Func<Permanent, bool> defenderCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // AS-IS :19-20 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotBeBlockedImpl(
            targetPermanent, defenderCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs).</summary>
    private static bool GainCanNotBeBlockedImpl(
        Permanent? targetPermanent,
        Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (targetPermanent is null) return false;                          // AS-IS :17
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;  // AS-IS :18
        if (card is null || cause is null) return false;                    // AS-IS :19-20

        // (RD-J-01) AS-IS grants UNCONDITIONALLY — there is NO grant-time immunity guard (the AS-IS CanNotBeAffected
        // check is read-time inside CanUseCondition below, plus a dropped UI visual). The earlier invented grant-time
        // refusal is removed so a temporarily-immune target still receives the inert grant, which activates once
        // immunity lifts (the AS-IS re-application semantics the invented guard broke).

        bool AttackerCondition(Permanent attacker) => attacker == targetPermanent;  // AS-IS :24

        bool DefenderCondition(Permanent defender)                                  // AS-IS :26-34
            => defenderCondition is null || defenderCondition(defender);

        bool CanUseCondition()                                                      // AS-IS :36-47
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(cause))
                {
                    return true;
                }
            }

            return false;
        }

        CardEffects.CannotBlockClass canNotBlockClass = CardEffectFactory.CanNotBlockStaticEffect(  // AS-IS :49-55
            attackerCondition: AttackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(  // AS-IS :57-62
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotBlockClass,
            timing: EffectTiming.None);

        // AS-IS :64-67 conditionally ran CreateBuffEffect (a UI icon), immunity-gated — pure visual; dropped.
        return true;
    }
}
