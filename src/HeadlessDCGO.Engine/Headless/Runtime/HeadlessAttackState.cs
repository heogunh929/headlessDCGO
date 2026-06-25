namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record HeadlessAttackState(
    int AttackCount,
    HeadlessPlayerId? AttackingPlayerId,
    HeadlessEntityId? AttackerId,
    HeadlessPlayerId? DefendingPlayerId,
    HeadlessEntityId? TargetId,
    HeadlessEntityId? BlockerId,
    bool IsBlocked,
    bool IsDirectAttack,
    bool IsPending,
    bool IsResolved,
    string Reason,
    AttackPhase Phase = AttackPhase.None)
{
    public static HeadlessAttackState Empty { get; } = new(
        AttackCount: 0,
        AttackingPlayerId: null,
        AttackerId: null,
        DefendingPlayerId: null,
        TargetId: null,
        BlockerId: null,
        IsBlocked: false,
        IsDirectAttack: false,
        IsPending: false,
        IsResolved: false,
        Reason: string.Empty,
        Phase: AttackPhase.None);
}
