// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs
// (J-2) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotUnsuspend (…/GiveEffectToPermanent/CanNotUnsuspend.cs:69-105)
// and its two wrappers GainCantUnsuspendNextActivePhase (:10-41) / GainCantUnsuspendUntilOpponentTurnEnd (:45-66):
// grant the TARGET permanent a timed "can't unsuspend" restriction. Builds the AS-IS kind-class via
// CardEffectFactory.CantUnsuspendStaticEffect (PermanentCondition = permanent==target; live CanUseCondition =
// on-battle-area && caller-condition && !TopCard.CanNotBeAffected(cause)) and stores it in the target's duration
// bucket via AddEffectToPermanent(timing: EffectTiming.None). Read LIVE by the AS-IS-literal interface scan
// Permanent.CanUnsuspend (Permanent.cs:3022) — an ICanNotUnsuspendEffect scan over permanent.EffectList(None) —
// consumed by the unsuspend step at TurnStateMachine.ActivePhaseAsync:222. RD-RC-02 resolution: this is the SOLE
// live home (the invented registry CannotUnsuspendKey arm has no reader — NewModelContinuousScan's unsuspend arm was
// intentionally removed; RestrictionScan callers do not include it). AS-IS coroutine only drove the CreateDebuffEffect
// UI visual (dropped — no game state). The public AS-IS-signature `Task` overloads thread the LIVE `activateClass` as
// the CanNotBeAffected cause (AS-IS 1:1); the CardSource-only substrate core (CardEffectCommons.cs) collapses the
// cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotUnsuspend</c> (GiveEffectToPermanent/CanNotUnsuspend.cs:69) — the
    /// AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the <c>CanNotBeAffected</c>
    /// cause. <paramref name="condition"/> is the caller's extra CanUse gate (e.g. BT7_055's
    /// IsPermanentExistsOnBattleArea).</summary>
    public static async Task GainCanNotUnsuspend(
        Permanent targetPermanent,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        Func<bool> condition,
        string effectName)
    {
        // AS-IS :73-74 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotUnsuspendImpl(
            targetPermanent, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, condition, effectName);
        await Task.CompletedTask;
    }

    /// <summary>1:1 mirror of AS-IS <c>GainCantUnsuspendUntilOpponentTurnEnd</c>
    /// (GiveEffectToPermanent/CanNotUnsuspend.cs:45) — AS-IS <c>CanUseCondition() => true</c>,
    /// <see cref="EffectDuration.UntilOpponentTurnEnd"/>. Threads the LIVE <paramref name="activateClass"/> cause.</summary>
    public static async Task GainCantUnsuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)
    {
        // AS-IS :47-50 guards.
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        static bool CanUseCondition() => true;   // AS-IS :56

        string effectName = "Can't unsuspend until the end of this card's owner's turn";   // AS-IS :58

        GainCanNotUnsuspendImpl(
            targetPermanent, EffectDuration.UntilOpponentTurnEnd,
            card: activateClass.EffectSourceCard, cause: activateClass, CanUseCondition, effectName);
        await Task.CompletedTask;
    }

    /// <summary>1:1 mirror of AS-IS <c>GainCantUnsuspendNextActivePhase</c>
    /// (GiveEffectToPermanent/CanNotUnsuspend.cs:10) — AS-IS CanUse = <c>IsOpponentTurn(card) &amp;&amp;
    /// TurnPhase == Active</c>, <see cref="EffectDuration.UntilNextUntap"/>. Threads the LIVE
    /// <paramref name="activateClass"/> cause. (No live caller today — BT7_055 uses the CardSource-only core
    /// directly — but mirrored 1:1 for fidelity.)</summary>
    public static async Task GainCantUnsuspendNextActivePhase(Permanent targetPermanent, ICardEffect activateClass)
    {
        // AS-IS :12-15 guards.
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        CardSource card = activateClass.EffectSourceCard;   // AS-IS :17

        bool CanUseCondition()   // AS-IS :19-29
        {
            if (IsOpponentTurn(card))
            {
                if (new GameContext(card.Context).TurnPhase == GameContext.phase.Active)
                {
                    return true;
                }
            }

            return false;
        }

        string effectName = "Can't unsuspend during next unsuspend phase";   // AS-IS :31

        GainCanNotUnsuspendImpl(
            targetPermanent, EffectDuration.UntilNextUntap,
            card: card, cause: activateClass, CanUseCondition, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overloads (above) and the CardSource-only substrate
    /// core (CardEffectCommons.cs). <paramref name="card"/> is the AS-IS <c>activateClass.EffectSourceCard</c>;
    /// <paramref name="cause"/> is the effect passed to the live <c>CanNotBeAffected</c> guard (AS-IS threads
    /// <c>activateClass</c>; the source-only path passes <see cref="BareCauseEffect"/>). Mirrors AS-IS
    /// GainCanNotUnsuspend :69-105.</summary>
    private static bool GainCanNotUnsuspendImpl(
        Permanent? targetPermanent,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        Func<bool>? condition,
        string effectName)
    {
        if (targetPermanent is null) return false;                          // AS-IS :71
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;  // AS-IS :72
        if (card is null || cause is null) return false;                    // AS-IS :73-74

        // (RD-J-01) AS-IS grants UNCONDITIONALLY — there is NO grant-time immunity guard (the AS-IS CanNotBeAffected
        // check is read-time inside CanUseCondition below, plus a dropped UI visual). The earlier invented grant-time
        // refusal is removed so a temporarily-immune target still receives the inert grant, which activates once
        // immunity lifts (the AS-IS re-application semantics the invented guard broke).

        bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;  // AS-IS :76

        bool CanUseCondition()   // AS-IS :78-93
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (condition is null || condition())
                {
                    if (!targetPermanent.TopCard.CanNotBeAffected(cause))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        CardEffects.CanNotUnsuspendClass canNotUnsuspendClass = CardEffectFactory.CantUnsuspendStaticEffect(  // AS-IS :96
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(  // AS-IS :98
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotUnsuspendClass,
            timing: EffectTiming.None);

        // AS-IS :100-103 conditionally ran CreateDebuffEffect (a UI debuff icon), immunity-gated — pure visual,
        // no game state; dropped headless.
        return true;
    }
}
