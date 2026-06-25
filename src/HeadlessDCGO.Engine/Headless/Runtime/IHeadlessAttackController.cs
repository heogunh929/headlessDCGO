namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public interface IHeadlessAttackController : IHeadlessMatchStateResettable
{
    HeadlessAttackState Current { get; }

    HeadlessAttackState DeclareAttack(
        HeadlessPlayerId attackingPlayerId,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId? targetId = null,
        bool isDirectAttack = false);

    HeadlessAttackState SelectBlocker(
        HeadlessEntityId blockerId,
        string reason = "");

    HeadlessAttackState ResolveAttack(string reason = "");

    HeadlessAttackState AdvancePhase(AttackPhase phase, string reason = "");

    HeadlessAttackState ClearAttack();

    HeadlessAttackState ResetTurnAttackState();
}
