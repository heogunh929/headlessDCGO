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
            Reason: string.Empty);

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
            Reason = reason
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
