namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (RD-9) Single chokepoint for declaring an attack — shared by the player action
/// (<see cref="AttackPermanentAction"/>) and every effect-driven attack (Vortex / Overclock / Execute via
/// <see cref="EffectDrivenAttack"/>). AS-IS routes ALL attacks (declared or effect-driven) through the same
/// AttackProcess, so the "[When Attacking]" ally-attack window (AS-IS AttackProcess.cs:199,
/// EffectTiming.OnAllyAttack) fires identically. The headless effect-driven path previously called
/// AttackController.DeclareAttack directly and emitted NO windows, so a Vortex/Execute attacker's
/// "[When Attacking]" effect never fired. Both paths now go through <see cref="Declare"/>.
///
/// SCOPE (this pass): consolidates the DECLARE + attack-declaration window emit only. The attacker SUSPEND is
/// still performed by each caller before Declare (their suspend semantics differ — a player attack always
/// suspends via CanSuspend, an effect-driven attack suspends only when !WithoutTap). Routing suspend through the
/// sink's SuspendKind so it emits OnTappedAnyone / re-filters CanSuspend / records DPWhenSuspended (design item
/// RD9-87) is a separate behavioral change, deferred. The invented OnDeclaration-on-attack emit (a stopgap for
/// the not-yet-ported main-phase "[Main] skill declaration" action, design item RD9-90) is intentionally NOT
/// placed here — it stays on the player-action path only, so effect-driven attacks do not wrongly fire a
/// [Main] skill.
/// </summary>
public static class AttackDeclarationCommons
{
    /// <summary>Declare the attack on the controller and run the AS-IS declaration sequence — (MIG1) the sequence
    /// (field reset, counter snapshot, attacker suspend, OnAttack/OnAllyAttack windows, IsEndAttack + battle-area
    /// gates) now lives in the mirror <see cref="Assets.Scripts.Script.AttackProcess.Attack"/> (AS-IS
    /// AttackProcess.cs:73-253); this chokepoint keeps both callers on the identical path.</summary>
    public static HeadlessAttackState Declare(
        EngineContext context,
        HeadlessPlayerId declaringPlayer,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayer,
        HeadlessEntityId? targetId,
        bool isDirectAttack,
        bool withoutTap = false,
        HeadlessEntityId? attackEffectSourceId = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Substrate state write (AS-IS SetAttackerDefender + IsAttacking + AttackCount++).
        context.AttackController.DeclareAttack(
            declaringPlayer, attackerId, defendingPlayer, targetId, isDirectAttack);

        // AS-IS Attack() — suspend, snapshot, OnAttack + OnAllyAttack emits, gates. Completes synchronously
        // (no beforeOnAttack callback on these paths).
        Assets.Scripts.Script.AttackProcess
            .For(context)
            .Attack(attackerId, attackEffectSourceId, withoutTap)
            .GetAwaiter()
            .GetResult();

        return context.AttackController.Current;
    }
}
