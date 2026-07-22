// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotAttack.cs
// (J-1) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotAttackPlayerEffect (…/GiveEffectToPlayer/CanNotAttack.cs
// :10-70): the OWNING PLAYER gains a timed "its permanents can't attack" restriction. Builds the AS-IS kind-class
// via CardEffectFactory.CanNotAttackStaticEffect where the AttackerCondition folds on-battle-area +
// !TopCard.CanNotBeAffected(cause) + the caller's attacker filter, the DefenderCondition is the raw caller wrap,
// and CanUseCondition is constant true; stores it in the owning player's duration bucket via
// AddEffectToPlayer(timing: EffectTiming.None). Read LIVE by the interface scan
// (NewModelContinuousScan.CannotAttackJoint over player.EffectList(None) / Permanent.CanAttackTargetDigimon player
// region), which the ContinuousRestrictionGate already unions — producer-only (the registry/player-scope arm goes
// silent). AS-IS coroutine only drove the per-permanent CreateDebuffEffect UI visual (dropped). The public
// AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause; the
// CardSource-only substrate overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotAttackPlayerEffect</c> (GiveEffectToPlayer/CanNotAttack.cs:10) —
    /// the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause folded into the AttackerCondition. <paramref name="attackerCondition"/> rides
    /// the attacker filter; <paramref name="defenderCondition"/> the defender filter.</summary>
    public static async Task GainCanNotAttackPlayerEffect(
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

        GainCanNotAttackPlayerEffectImpl(
            attackerCondition, defenderCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). <paramref name="cause"/> is the effect folded into the AttackerCondition's
    /// live <c>CanNotBeAffected</c> guard (AS-IS threads <c>activateClass</c>; the source-only path passes
    /// <see cref="BareCauseEffect"/>).</summary>
    private static bool GainCanNotAttackPlayerEffectImpl(
        Func<Permanent, bool>? attackerCondition,
        Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (card is null || cause is null) return false;  // AS-IS :17-18

        bool AttackerCondition(Permanent attacker)        // AS-IS :22-36
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

        bool DefenderCondition(Permanent defender)        // AS-IS :38-46
            => defenderCondition is null || defenderCondition(defender);

        bool Condition() => true;                         // AS-IS :48-51

        CardEffects.CanNotAttackTargetDefendingPermanentClass canNotAttackClass = CardEffectFactory.CanNotAttackStaticEffect(  // AS-IS :53-59
            attackerCondition: AttackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: false,
            card: card,
            condition: Condition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :61
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotAttackClass,
            timing: EffectTiming.None);

        // AS-IS :63-69 iterated PermanentsForTurnPlayer running CreateDebuffEffect (UI visual) — dropped headless.
        return true;
    }
}
