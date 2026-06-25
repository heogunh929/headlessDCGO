namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Runtime store for the AS-IS per-player "lose" signal (Unity <c>Player.IsLose</c>). Loss events
/// (direct attack with no security, deck-out, surrender, card effects) mark a player here; the common
/// loop's end-turn check (<see cref="GameFlowProcessor"/>) reads the flag through
/// <see cref="HeadlessDCGO.Engine.Headless.State.PlayerRuleAdapter"/> and drives the terminal result (X-02).
/// </summary>
public interface IHeadlessPlayerStatusController : IHeadlessMatchStateResettable
{
    void MarkLose(HeadlessPlayerId playerId, string reason = "");

    bool IsLose(HeadlessPlayerId playerId);

    bool TryGetLoseReason(HeadlessPlayerId playerId, out string reason);

    IReadOnlyList<HeadlessPlayerId> LosingPlayers { get; }
}
