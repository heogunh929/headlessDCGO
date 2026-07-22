// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs
// (J-1) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotAttack (…/GiveEffectToPermanent/CanNotAttack.cs:10-68):
// grant the TARGET permanent a timed "can't attack THIS defender" restriction. Builds the AS-IS kind-class via
// CardEffectFactory.CanNotAttackStaticEffect (AttackerCondition = attacker==target, DefenderCondition = caller
// wrap, live CanUseCondition = on-battle-area && !TopCard.CanNotBeAffected(cause)) and stores it in the target's
// duration bucket via AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by the interface scan
// (NewModelContinuousScan.CannotAttackJoint / Permanent.CanAttackTargetDigimon over permanent.EffectList(None)),
// which the ContinuousRestrictionGate already unions — so this is producer-only (the registry arm goes silent).
// Substrate convention: the AS-IS IEnumerator only drove the CreateDebuffEffect UI visual (dropped — no game
// state); the grant itself is synchronous (bool), matching sibling GainCanNotBeDeletedByBattle. The public
// AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause (AS-IS 1:1); the
// CardSource-only substrate overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotAttack</c> (GiveEffect/GiveEffectToPermanent/CanNotAttack.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause (AS-IS :40/:64). The AS-IS coroutine only drove the UI debuff visual, so this
    /// completes synchronously. <paramref name="defenderCondition"/> narrows WHICH defenders this permanent
    /// cannot attack.</summary>
    public static async Task GainCanNotAttack(
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

        GainCanNotAttackImpl(
            targetPermanent, defenderCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). <paramref name="card"/> is the AS-IS <c>activateClass.EffectSourceCard</c>;
    /// <paramref name="cause"/> is the effect passed to the live <c>CanNotBeAffected</c> guard (AS-IS threads
    /// <c>activateClass</c>; the source-only path passes <see cref="BareCauseEffect"/>).</summary>
    private static bool GainCanNotAttackImpl(
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

        // (B군 P0-1) grant-time !CanNotBeAffected refusal — the sync-bool rendering of AS-IS's immunity-gated
        // grant (the live CanUseCondition would gate an immune target to false anyway; here the bool short-circuits
        // to signal "not granted", matching sibling GainCanNotBeDeletedByBattle:80 and the retired funnel guard).
        if (targetPermanent.TopCard.CanNotBeAffected(cause)) return false;

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

        // AS-IS :64-67 conditionally ran CreateDebuffEffect (a UI debuff icon), immunity-gated — pure visual,
        // no game state; dropped headless.
        return true;
    }
}
