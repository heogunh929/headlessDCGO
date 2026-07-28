// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotReturnToHand (…/GiveEffectToPermanent/CanNotReturnToHand.cs
// :10-53): grant the TARGET permanent a timed "can't be returned to hand (by matching effects)" restriction.
// Builds the AS-IS kind-class via CardEffectFactory.CannotReturnToHandStaticEffect (PermanentCondition =
// permanent==target, the caller's `cardEffectCondition` gating WHICH causing effects are refused, live
// CanUseCondition = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration
// bucket via AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by Permanent.CannotReturnToHand /
// NewModelContinuousScan.HasCannotReturnToHand over EffectList(None) — the registry joint arm goes silent. The
// AS-IS coroutine only drove the CreateBuffEffect UI visual (dropped). The public AS-IS-signature `Task` overload
// threads the LIVE `activateClass` as the CanNotBeAffected cause and the REAL Func<ICardEffect,bool>
// cardEffectCondition (AS-IS 1:1); the CardSource-only substrate overload (CardEffectCommons.cs) collapses the
// cause to BareCauseEffect.For(sourceCard) and lifts its CardSource predicate to the causing effect's source card.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotReturnToHand</c> (GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause and the REAL <paramref name="cardEffectCondition"/> into the kind-class.</summary>
    public static async Task GainCanNotReturnToHand(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        // AS-IS :12-15 guards.
        if (targetPermanent is null || !IsPermanentExistsOnBattleArea(targetPermanent)
            || activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotReturnToHandImpl(
            targetPermanent, cardEffectCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs).</summary>
    private static bool GainCanNotReturnToHandImpl(
        Permanent? targetPermanent,
        Func<ICardEffect, bool>? cardEffectCondition,
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

        bool PermanentCondition(Permanent attacker) => attacker == targetPermanent;  // AS-IS :19

        bool CanUseCondition()                                                        // AS-IS :21-32
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

        CardEffects.CannotReturnToHandClass cannotReturnToHandClass = CardEffectFactory.CannotReturnToHandStaticEffect(  // AS-IS :34-40
            permanentCondition: PermanentCondition,
            cardEffectCondition: cardEffectCondition!,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(  // AS-IS :42-47
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: cannotReturnToHandClass,
            timing: EffectTiming.None);

        // AS-IS :49-52 conditionally ran CreateBuffEffect (a UI icon), immunity-gated — pure visual; dropped.
        return true;
    }
}
