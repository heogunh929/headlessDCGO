// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ImmuneFromDPMinus.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainImmuneFromDPMinusPlayerEffect (…/GiveEffectToPlayer/
// ImmuneFromDPMinus.cs:10-55): the OWNING PLAYER gains a timed "its permanents are immune from DP-minus (by
// matching effects)" restriction. Builds the AS-IS kind-class via CardEffectFactory.ImmuneFromDPMinusStaticEffect
// where the PermanentCondition folds on-battle-area + !TopCard.CanNotBeAffected(cause) + the caller's predicate,
// and the caller's `cardEffectCondition` gates WHICH causing effects are ignored; CanUseCondition = true. Stores it
// in the owning player's duration bucket via AddEffectToPlayer(timing: EffectTiming.None). Read LIVE by
// Permanent.ImmuneFromDPMinus (player arm) over player.EffectList(None) — the registry joint arm goes silent. AS-IS
// coroutine only drove the per-permanent CreateBuffEffect UI visual (dropped). The public AS-IS-signature `Task`
// overload threads the LIVE `activateClass` as the CanNotBeAffected cause and the REAL Func<ICardEffect,bool>
// cardEffectCondition (AS-IS 1:1); the CardSource-only substrate overload (CardEffectCommons.cs) collapses the
// cause to BareCauseEffect.For(sourceCard) and lifts its CardSource predicate to the causing effect's source card.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainImmuneFromDPMinusPlayerEffect</c> (GiveEffectToPlayer/ImmuneFromDPMinus.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause and the REAL <paramref name="cardEffectCondition"/> into the kind-class.</summary>
    public static async Task GainImmuneFromDPMinusPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        // AS-IS :12-13 guards.
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainImmuneFromDPMinusPlayerEffectImpl(
            permanentCondition, cardEffectCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS GainImmuneFromDPMinusPlayerEffect :10-55.</summary>
    private static bool GainImmuneFromDPMinusPlayerEffectImpl(
        Func<Permanent, bool>? permanentCondition,
        Func<ICardEffect, bool>? cardEffectCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (card is null || cause is null) return false;   // AS-IS :12-13

        bool PermanentCondition(Permanent attacker)   // AS-IS :17-31
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

        bool CanUseCondition() => true;   // AS-IS :33-36

        CardEffects.ImmuneFromDPMinusClass immuneFromDPMinusClass = CardEffectFactory.ImmuneFromDPMinusStaticEffect(  // AS-IS :38-44
            permanentCondition: PermanentCondition,
            cardEffectCondition: cardEffectCondition!,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :46
            effectDuration: effectDuration,
            card: card,
            cardEffect: immuneFromDPMinusClass,
            timing: EffectTiming.None);

        // AS-IS :48-54 iterated PermanentsForTurnPlayer running CreateBuffEffect (UI visual) — dropped headless.
        return true;
    }
}
