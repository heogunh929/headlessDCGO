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

    HeadlessAttackState ResolveAttack(string reason = "");

    HeadlessAttackState AdvancePhase(AttackPhase phase, string reason = "");

    HeadlessAttackState ClearAttack();

    HeadlessAttackState ResetTurnAttackState();
}
