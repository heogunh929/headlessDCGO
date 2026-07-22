// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNoReturnToDeck.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotReturnToDeckPlayerEffect (…/GiveEffectToPlayer/
// CanNoReturnToDeck.cs:10-61): the OWNING PLAYER gains a timed "its permanents can't be returned to deck (by
// matching effects)" restriction. Builds the AS-IS kind-class via CardEffectFactory.CannotReturnToDeckStaticEffect
// where the PermanentCondition folds on-battle-area + !TopCard.CanNotBeAffected(cause) + the caller's predicate,
// and the caller's `cardEffectCondition` gates WHICH causing effects are refused; CanUseCondition = true. Stores it
// in the owning player's duration bucket via AddEffectToPlayer(timing: EffectTiming.None). Read LIVE by
// Permanent.CannotReturnToLibrary (player arm) / NewModelContinuousScan.HasCannotReturnToLibrary — the registry
// joint arm goes silent. AS-IS coroutine only drove the per-permanent CreateBuffEffect UI visual (dropped). The
// public AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause and the
// REAL Func<ICardEffect,bool> cardEffectCondition (AS-IS 1:1); the CardSource-only substrate overload
// (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard) and lifts its CardSource predicate.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotReturnToDeckPlayerEffect</c> (GiveEffectToPlayer/CanNoReturnToDeck.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause and the REAL <paramref name="cardEffectCondition"/> into the kind-class.</summary>
    public static async Task GainCanNotReturnToDeckPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        // AS-IS :17-18 guards.
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotReturnToDeckPlayerEffectImpl(
            permanentCondition, cardEffectCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS GainCanNotReturnToDeckPlayerEffect :10-61.</summary>
    private static bool GainCanNotReturnToDeckPlayerEffectImpl(
        Func<Permanent, bool>? permanentCondition,
        Func<ICardEffect, bool>? cardEffectCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (card is null || cause is null) return false;   // AS-IS :17-18

        bool PermanentCondition(Permanent attacker)   // AS-IS :22-36
        {
            if (IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(cause))
                {
                    if (permanentCondition is null || permanentCondition(attacker))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;   // AS-IS :38-41

        CardEffects.CannotReturnToLibraryClass cannotReturnToLibraryClass = CardEffectFactory.CannotReturnToDeckStaticEffect(  // AS-IS :43-49
            permanentCondition: PermanentCondition,
            cardEffectCondition: cardEffectCondition!,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :51
            effectDuration: effectDuration,
            card: card,
            cardEffect: cannotReturnToLibraryClass,
            timing: EffectTiming.None);

        // AS-IS :53-59 iterated PermanentsForTurnPlayer running CreateBuffEffect (UI visual) — dropped headless.
        return true;
    }
}
