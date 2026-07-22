// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBlock.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotBlockPlayerEffect (…/GiveEffectToPlayer/CanNotBlock.cs
// :10-70): the OWNING PLAYER gains a timed "its permanents can't block" restriction. Builds the AS-IS kind-class
// via CardEffectFactory.CanNotBlockStaticEffect where AttackerCondition folds on-battle-area +
// !TopCard.CanNotBeAffected(cause) + the caller's `attackerCondition` (the SUBJECT blocker filter) and
// DefenderCondition wraps the caller's `defenderCondition` (the attacker-being-blocked filter); CanUseCondition =
// true. Stores it in the owning player's duration bucket via AddEffectToPlayer(timing: EffectTiming.None). Read
// LIVE by Permanent.CanBlock (player arm) over player.EffectList(None) — the registry joint arm goes silent. AS-IS
// coroutine only drove the per-permanent CreateDebuffEffect UI visual (dropped). The public AS-IS-signature `Task`
// overload threads the LIVE `activateClass` as the CanNotBeAffected cause; the CardSource-only substrate overload
// (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotBlockPlayerEffect</c> (GiveEffectToPlayer/CanNotBlock.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause folded into the AttackerCondition.</summary>
    public static async Task GainCanNotBlockPlayerEffect(
        Func<Permanent, bool> attackerCondition,
        Func<Permanent, bool> defenderCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // AS-IS :17-18 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotBlockPlayerEffectImpl(
            attackerCondition, defenderCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS GainCanNotBlockPlayerEffect :10-70.</summary>
    private static bool GainCanNotBlockPlayerEffectImpl(
        Func<Permanent, bool>? attackerCondition,
        Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (card is null || cause is null) return false;   // AS-IS :17-18

        bool AttackerCondition(Permanent attacker)   // AS-IS :22-36
        {
            if (IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(cause))
                {
                    if (attackerCondition is null || attackerCondition(attacker))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool DefenderCondition(Permanent defender)   // AS-IS :38-46
            => defenderCondition is null || defenderCondition(defender);

        bool CanUseCondition() => true;   // AS-IS :48-51

        CardEffects.CannotBlockClass cannotBlockClass = CardEffectFactory.CanNotBlockStaticEffect(  // AS-IS :53-59
            attackerCondition: AttackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :61
            effectDuration: effectDuration,
            card: card,
            cardEffect: cannotBlockClass,
            timing: EffectTiming.None);

        // AS-IS :63-69 iterated PermanentsForTurnPlayer running CreateDebuffEffect (UI visual) — dropped headless.
        return true;
    }
}
