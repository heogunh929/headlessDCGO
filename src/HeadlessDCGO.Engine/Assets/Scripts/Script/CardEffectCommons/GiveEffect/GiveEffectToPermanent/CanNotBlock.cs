// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBlock.cs
// (J-1) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotBlock (…/GiveEffectToPermanent/CanNotBlock.cs:10-52):
// grant the TARGET permanent a timed "can't block THIS attacker" restriction. Builds the AS-IS kind-class via
// CardEffectFactory.CanNotBlockStaticEffect (AttackerCondition = caller wrap, DefenderCondition = defender==target,
// live CanUseCondition = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's
// duration bucket via AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by the interface scan
// (NewModelContinuousScan.CannotBlockJoint / Permanent.CanBlock over permanent.EffectList(None)), which the
// ContinuousRestrictionGate already unions — producer-only (the registry arm goes silent). AS-IS coroutine only
// drove the CreateDebuffEffect UI visual (dropped). The public AS-IS-signature `Task` overload threads the LIVE
// `activateClass` as the CanNotBeAffected cause; the CardSource-only substrate overload (CardEffectCommons.cs)
// collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotBlock</c> (GiveEffect/GiveEffectToPermanent/CanNotBlock.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause. <paramref name="attackerCondition"/> narrows WHICH attackers this permanent
    /// cannot block.</summary>
    public static async Task GainCanNotBlock(
        Permanent targetPermanent,
        Func<Permanent, bool> attackerCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // AS-IS :14-15 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotBlockImpl(
            targetPermanent, attackerCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). <paramref name="cause"/> is the effect passed to the live
    /// <c>CanNotBeAffected</c> guard (AS-IS threads <c>activateClass</c>; the source-only path passes
    /// <see cref="BareCauseEffect"/>).</summary>
    private static bool GainCanNotBlockImpl(
        Permanent? targetPermanent,
        Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (targetPermanent is null) return false;                          // AS-IS :12
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;  // AS-IS :13
        if (card is null || cause is null) return false;                    // AS-IS :14-15

        // (RD-J-01) AS-IS grants UNCONDITIONALLY — there is NO grant-time immunity guard (the AS-IS CanNotBeAffected
        // check is read-time inside CanUseCondition below, plus a dropped UI visual). The earlier invented grant-time
        // refusal is removed so a temporarily-immune target still receives the inert grant, which activates once
        // immunity lifts (the AS-IS re-application semantics the invented guard broke).

        // AS-IS :19-27 — the caller's `attackerCondition` narrows which attackers cannot be blocked (AS-IS names
        // the local param `defender`, kept verbatim).
        bool AttackerCondition(Permanent defender)
            => attackerCondition is null || attackerCondition(defender);

        bool DefenderCondition(Permanent attacker) => attacker == targetPermanent;  // AS-IS :29

        bool CanUseCondition()                                                      // AS-IS :31-42
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

        CardEffects.CannotBlockClass canNotAttackClass = CardEffectFactory.CanNotBlockStaticEffect(  // AS-IS :44
            attackerCondition: AttackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(  // AS-IS :46
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotAttackClass,
            timing: EffectTiming.None);

        // AS-IS :48-51 CreateDebuffEffect UI visual (immunity-gated) — dropped headless.
        return true;
    }
}
