// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/AttackProcess.cs::AttackProcess.Attack()@73
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using System;
using System.Threading;
using System.Threading.Tasks;
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
        // The SYNC declaration path (player action / effect-driven attacks with NO pre-OnAttack hook): the AS-IS
        // Attack() sequence completes synchronously because nothing inside it awaits an agent choice (StackSkillInfos
        // only STACKS). Kept byte-identical to the pre-RD-W3-7 shape (delegates to DeclareAsync with a null hook).
        return DeclareAsync(
                context, declaringPlayer, attackerId, defendingPlayer, targetId, isDirectAttack,
                withoutTap, attackEffectSourceId, beforeOnAttack: null)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>(RD-W3-7) The ASYNC-PAUSABLE declaration path — identical to <see cref="Declare"/> but threads the
    /// AS-IS <c>beforeOnAttackCoroutine</c> (SelectAttackEffect.SetBeforeOnAttackCoroutine → attackProcess.Attack's
    /// <c>beforeOnAttackCoroutine</c>, AttackProcess.cs:191, run AFTER the attacker suspend and BEFORE the [On Attack]
    /// window). The hook may open a select (AS-IS ST13_06's mandatory Jogress destroy), which parks the pump IN PLACE
    /// via the established DeferredChoiceProvider AWAIT-mode seam (WaitPendingChoiceUnderPump idiom) and resumes — so
    /// this path must be AWAITED (never blocked with GetResult, which would deadlock the single-threaded pump on the
    /// hook's parked choice). Non-hook callers stay on the sync <see cref="Declare"/> (unchanged).</summary>
    public static async Task<HeadlessAttackState> DeclareAsync(
        EngineContext context,
        HeadlessPlayerId declaringPlayer,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayer,
        HeadlessEntityId? targetId,
        bool isDirectAttack,
        bool withoutTap = false,
        HeadlessEntityId? attackEffectSourceId = null,
        Func<CancellationToken, Task>? beforeOnAttack = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Substrate state write (AS-IS SetAttackerDefender + IsAttacking + AttackCount++).
        context.AttackController.DeclareAttack(
            declaringPlayer, attackerId, defendingPlayer, targetId, isDirectAttack);

        // AS-IS Attack() — suspend, snapshot, beforeOnAttack hook, OnAttack + OnAllyAttack emits, gates.
        await Assets.Scripts.Script.AttackProcess
            .For(context)
            .Attack(attackerId, attackEffectSourceId, withoutTap, beforeOnAttack, cancellationToken)
            .ConfigureAwait(false);

        return context.AttackController.Current;
    }
}
