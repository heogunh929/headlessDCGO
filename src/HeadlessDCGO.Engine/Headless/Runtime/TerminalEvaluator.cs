namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Bridges the runtime player-status store into the <see cref="PlayerRuleAdapter"/> terminal verdict (X-02).
/// Mirrors Unity AS-IS <c>AutoProcessing</c>, which consolidates win/loss by reading <c>Player.IsLose</c>:
/// loss events (e.g. <see cref="AttackPipeline"/> direct-hit, draw-phase deck-out) mark the loser via
/// <see cref="IHeadlessPlayerStatusController"/>, and the common loop's end-turn check evaluates the verdict
/// here. The adapter is built from the active player order plus the lose flags; no card zones are required
/// because the lose flag is the consolidated terminal signal.
/// </summary>
public static class TerminalEvaluator
{
    public static PlayerTerminalCheck? Evaluate(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<HeadlessPlayerId> players = context.TurnController.Current.PlayerOrder;
        if (players.Count == 0)
        {
            return null;
        }

        PlayerRuleAdapter? adapter = TryBuildAdapter(context, players);
        if (adapter is null)
        {
            return null;
        }

        foreach (PlayerState playerState in adapter.Zones.State.Players)
        {
            PlayerTerminalCheck check = adapter.EvaluateLoseFlag(playerState.PlayerId);
            if (check.IsTerminal)
            {
                return check;
            }
        }

        return null;
    }

    private static PlayerRuleAdapter? TryBuildAdapter(
        EngineContext context,
        IReadOnlyList<HeadlessPlayerId> players)
    {
        var playerStates = new List<PlayerState>(players.Count);
        var seen = new HashSet<HeadlessPlayerId>();
        foreach (HeadlessPlayerId playerId in players)
        {
            if (playerId.IsEmpty || !seen.Add(playerId))
            {
                continue;
            }

            PlayerState state = new(playerId);
            if (context.PlayerStatusController.IsLose(playerId))
            {
                state = state.SetFlag(PlayerRuleAdapter.LoseFlagKey, true);
            }

            playerStates.Add(state);
        }

        if (playerStates.Count == 0)
        {
            return null;
        }

        MatchState matchState = new(playerStates);
        HeadlessMemoryState memory = context.MemoryController.Current;
        return new PlayerRuleAdapter(
            new PlayerZoneAdapter(matchState),
            memory.Current,
            isSecurityLooking: false,
            minimumMemory: memory.Minimum,
            maximumMemory: memory.Maximum);
    }
}
