// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/CardController.cs::isEvolution 공유 스텝: DigivolveCount_ThisTurn++ ; new DrawClass(owner,1,null).Draw()@1526
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (RD-1) The shared post-digivolve step that AS-IS performs at its single play chokepoint
/// (CardController.cs:1526-1529): on EVERY digivolution — normal, Jogress/DNA, Burst, Blast, App-Fusion,
/// and effect-driven free digivolve — the player increments their per-turn digivolve counter AND draws one
/// card. (DigiXros and Assembly are explicitly <c>!isEvolution</c> in AS-IS and do NOT draw.)
///
/// The headless port has no single digivolve chokepoint (DigivolveAction / SpecialPlayAction /
/// effect-driven paths are separate), so each digivolution-completion site calls this after the new top has
/// physically entered play — matching the AS-IS position (draw BEFORE the WhenDigivolving / OnEnterField
/// windows resolve, which in headless are queued events drained after the action).
/// </summary>
public static class DigivolveCommons
{
    /// <summary>Increment the digivolve counter and draw one card for <paramref name="player"/>, mirroring
    /// AS-IS <c>DigivolveCount_ThisTurn++; new DrawClass(owner, 1, null).Draw()</c>. The draw opens the OnDraw
    /// window (AS-IS every DrawClass stacks OnDraw, CardController.cs:1948-1960).</summary>
    public static async Task OnDigivolveCompletedAsync(EngineContext context, HeadlessPlayerId player, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (player.IsEmpty)
        {
            return;
        }

        context.PlayerTurnCounters.Increment(player, PlayerTurnCounterController.DigivolveCountKey);
        await context.ZoneMover.DrawAsync(player, 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnDraw, actor: player);
    }
}
