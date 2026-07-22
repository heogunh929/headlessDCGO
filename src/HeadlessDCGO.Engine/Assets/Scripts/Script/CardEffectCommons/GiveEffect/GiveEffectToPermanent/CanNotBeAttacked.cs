// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotBeAttacked (…/GiveEffectToPermanent/CanNotBeAttacked.cs
// :10-68): grant the TARGET permanent a timed "can't BE attacked by THIS attacker" restriction. Builds the AS-IS
// kind-class via CardEffectFactory.CanNotAttackStaticEffect with the roles MIRRORED vs GainCanNotAttack
// (AttackerCondition = caller `attackerCondition` wrap, DefenderCondition = attacker==target, live CanUseCondition
// = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration bucket via
// AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by the interface scan
// (NewModelContinuousScan.CanNotBeAttacked / CannotAttackJoint over EffectList(None)), which the
// ContinuousRestrictionGate (EvaluateBeAttacked, CannotBeAttackedKey) already unions — producer-only (the registry
// joint arm goes silent). The AS-IS coroutine only drove the CreateBuffEffect UI visual (dropped). The public
// AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause (AS-IS 1:1); the
// CardSource-only substrate overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotBeAttacked</c> (GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause. <paramref name="attackerCondition"/> narrows WHICH attackers cannot attack
    /// this permanent.</summary>
    public static async Task GainCanNotBeAttacked(
        Permanent targetPermanent,
        Func<Permanent, bool> attackerCondition,
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

        GainCanNotBeAttackedImpl(
            targetPermanent, attackerCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). <paramref name="cause"/> is the effect passed to the live
    /// <c>CanNotBeAffected</c> guard (AS-IS threads <c>activateClass</c>; the source-only path passes
    /// <see cref="BareCauseEffect"/>).</summary>
    private static bool GainCanNotBeAttackedImpl(
        Permanent? targetPermanent,
        Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (targetPermanent is null) return false;                          // AS-IS :17
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;  // AS-IS :18
        if (card is null || cause is null) return false;                    // AS-IS :19-20

        // (B군 P0-1) grant-time !CanNotBeAffected refusal — sync-bool rendering of AS-IS's immunity-gated grant.
        if (targetPermanent.TopCard.CanNotBeAffected(cause)) return false;

        bool AttackerCondition(Permanent defender)                                  // AS-IS :24-32
            => attackerCondition is null || attackerCondition(defender);

        bool DefenderCondition(Permanent attacker) => attacker == targetPermanent;  // AS-IS :34

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

        CardEffects.CanNotAttackTargetDefendingPermanentClass canNotAttackClass = CardEffectFactory.CanNotAttackStaticEffect(  // AS-IS :49-55
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
            cardEffect: canNotAttackClass,
            timing: EffectTiming.None);

        // AS-IS :64-67 conditionally ran CreateBuffEffect (a UI icon), immunity-gated — pure visual; dropped.
        return true;
    }
}
