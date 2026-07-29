// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/Player.cs; DCGO/Assets/Scripts/Script/AutoProcessing.cs::Player.IsLose / Player.SetLose (패배 플래그); AutoProcessing.EndGameProcess (IsLose 모아 종료판정→TurnStateMachine.EndGame)@Pla
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
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
        return new PlayerRuleAdapter(new PlayerZoneAdapter(matchState));
    }
}
