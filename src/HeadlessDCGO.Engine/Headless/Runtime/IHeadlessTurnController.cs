namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public interface IHeadlessTurnController : IHeadlessMatchStateResettable
{
    HeadlessTurnState Current { get; }

    void Initialize(
        IReadOnlyList<HeadlessPlayerId> playerIds,
        HeadlessPlayerId? firstPlayerId = null);

    HeadlessTurnState AdvancePhase();

    HeadlessTurnState EndTurn();

    HeadlessTurnState SetPhase(HeadlessPhase phase);
}
