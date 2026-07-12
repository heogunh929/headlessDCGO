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

    // C-3 Raid: redirect the pending attack to a different defending Digimon (not a block). Mirrors AS-IS
    // attackProcess.SwitchDefender — sets a Digimon target without flagging the attack as blocked.
    HeadlessAttackState SwitchDefender(
        HeadlessEntityId targetId,
        string reason = "");

    // (MIG1 substrate) AS-IS SwitchDefender can retarget to NULL (the attack becomes a direct/security attack) —
    // pure state write: TargetId = targetId (nullable), IsBlocked/BlockerId cleared (a redirect is not a block).
    HeadlessAttackState RetargetDefender(
        HeadlessEntityId? targetId,
        string reason = "");

    // (MIG1 substrate) AS-IS SwitchDefender's death checks clear IsBlocking while keeping the target — pure state
    // write: IsBlocked = false, BlockerId = null.
    HeadlessAttackState ClearBlockingFlag(string reason = "");

    HeadlessAttackState ResolveAttack(string reason = "");

    HeadlessAttackState AdvancePhase(AttackPhase phase, string reason = "");

    HeadlessAttackState ClearAttack();

    HeadlessAttackState ResetTurnAttackState();
}
