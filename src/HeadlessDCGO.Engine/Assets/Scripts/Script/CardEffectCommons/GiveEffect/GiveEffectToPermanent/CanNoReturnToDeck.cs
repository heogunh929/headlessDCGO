// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNoReturnToDeck.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotReturnToDeck (…/GiveEffectToPermanent/CanNoReturnToDeck.cs
// :10-53): grant the TARGET permanent a timed "can't be returned to deck (by matching effects)" restriction.
// Builds the AS-IS kind-class via CardEffectFactory.CannotReturnToDeckStaticEffect (PermanentCondition =
// permanent==target, the caller's `cardEffectCondition` gating WHICH causing effects are refused, live
// CanUseCondition = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration
// bucket via AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by Permanent.CannotReturnToLibrary /
// NewModelContinuousScan.HasCannotReturnToLibrary over EffectList(None) — the registry joint arm goes silent. The
// AS-IS coroutine only drove the CreateBuffEffect UI visual (dropped). The public AS-IS-signature `Task` overload
// threads the LIVE `activateClass` as the CanNotBeAffected cause and the REAL Func<ICardEffect,bool>
// cardEffectCondition (AS-IS 1:1); the CardSource-only substrate overload (CardEffectCommons.cs) collapses the
// cause to BareCauseEffect.For(sourceCard) and lifts its CardSource predicate to the causing effect's source card.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotReturnToDeck</c> (GiveEffect/GiveEffectToPermanent/CanNoReturnToDeck.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause and the REAL <paramref name="cardEffectCondition"/> into the kind-class.</summary>
    public static async Task GainCanNotReturnToDeck(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        // AS-IS :12-15 guards.
        if (targetPermanent is null || !IsPermanentExistsOnBattleArea(targetPermanent)
            || activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotReturnToDeckImpl(
            targetPermanent, cardEffectCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs).</summary>
    private static bool GainCanNotReturnToDeckImpl(
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

        // (B군 P0-1) grant-time !CanNotBeAffected refusal — sync-bool rendering of AS-IS's immunity-gated grant.
        if (targetPermanent.TopCard.CanNotBeAffected(cause)) return false;

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

        CardEffects.CannotReturnToLibraryClass cannotReturnToLibraryClass = CardEffectFactory.CannotReturnToDeckStaticEffect(  // AS-IS :34-40
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
            cardEffect: cannotReturnToLibraryClass,
            timing: EffectTiming.None);

        // AS-IS :49-52 conditionally ran CreateBuffEffect (a UI icon), immunity-gated — pure visual; dropped.
        return true;
    }
}
