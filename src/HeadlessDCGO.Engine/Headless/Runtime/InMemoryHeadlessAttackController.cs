namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryHeadlessAttackController : IHeadlessAttackController
{
    private int _attackCount;

    public HeadlessAttackState Current { get; private set; } = HeadlessAttackState.Empty;

    public HeadlessAttackState DeclareAttack(
        HeadlessPlayerId attackingPlayerId,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId? targetId = null,
        bool isDirectAttack = false)
    {
        _attackCount++;
        Current = new HeadlessAttackState(
            _attackCount,
            attackingPlayerId,
            attackerId,
            defendingPlayerId,
            targetId,
            BlockerId: null,
            IsBlocked: false,
            isDirectAttack || !targetId.HasValue,
            IsPending: true,
            IsResolved: false,
            Reason: string.Empty,
            Phase: AttackPhase.Declared);

        return Current;
    }

    public HeadlessAttackState SelectBlocker(
        HeadlessEntityId blockerId,
        string reason = "")
    {
        if (!Current.IsPending || blockerId.IsEmpty)
        {
            return Current;
        }

        Current = Current with
        {
            TargetId = blockerId,
            BlockerId = blockerId,
            IsBlocked = true,
            IsDirectAttack = false,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Blocker selected." : reason.Trim()
        };

        return Current;
    }

    public HeadlessAttackState SwitchDefender(
        HeadlessEntityId targetId,
        string reason = "")
    {
        if (!Current.IsPending || targetId.IsEmpty)
        {
            return Current;
        }

        // Raid retargets the attack onto another Digimon: set the target and clear the direct-attack flag,
        // but (unlike SelectBlocker) do NOT mark it blocked — this is a redirect, not a block.
        Current = Current with
        {
            TargetId = targetId,
            IsDirectAttack = false,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Defender switched." : reason.Trim()
        };

        return Current;
    }

    // (MIG1 substrate) nullable retarget — AS-IS SwitchDefender(newDefendingPermanent) where the new defender may be
    // null (the attack becomes a direct/security attack). A redirect is not a block: blocked flags are cleared.
    public HeadlessAttackState RetargetDefender(
        HeadlessEntityId? targetId,
        string reason = "")
    {
        if (!Current.IsPending)
        {
            return Current;
        }

        Current = Current with
        {
            TargetId = targetId,
            BlockerId = null,
            IsBlocked = false,
            IsDirectAttack = targetId is null,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Defender retargeted." : reason.Trim()
        };

        return Current;
    }

    // (MIG1 substrate) AS-IS SwitchDefender death checks: `IsBlocking = false; yield break;` — keep the target.
    public HeadlessAttackState ClearBlockingFlag(string reason = "")
    {
        if (!Current.IsPending)
        {
            return Current;
        }

        Current = Current with
        {
            BlockerId = null,
            IsBlocked = false,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Blocking flag cleared." : reason.Trim()
        };

        return Current;
    }

    public HeadlessAttackState ResolveAttack(string reason = "")
    {
        if (!Current.IsPending)
        {
            return Current;
        }

        Current = Current with
        {
            IsPending = false,
            IsResolved = true,
            Reason = reason,
            Phase = AttackPhase.Resolved
        };

        return Current;
    }

    public HeadlessAttackState AdvancePhase(AttackPhase phase, string reason = "")
    {
        if (Current.Phase == AttackPhase.None && phase != AttackPhase.None)
        {
            return Current;
        }

        Current = Current with
        {
            Phase = phase,
            Reason = string.IsNullOrWhiteSpace(reason) ? Current.Reason : reason.Trim()
        };

        return Current;
    }

    public HeadlessAttackState ClearAttack()
    {
        Current = HeadlessAttackState.Empty with { AttackCount = _attackCount };
        return Current;
    }

    public HeadlessAttackState ResetTurnAttackState()
    {
        _attackCount = 0;
        Current = HeadlessAttackState.Empty;
        return Current;
    }

    public void ResetMatchState()
    {
        ResetTurnAttackState();
    }
}
