// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/AutoProcessing.cs::AutoProcessing.StackSkillInfos(hashtable@timing)
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Publishes a "timing window" game event (W1) at action points that produce no zone move — turn
/// boundaries, digivolution, draw, security checks. The event carries an explicit
/// <see cref="AutoProcessingTriggerCollector.TriggerTimingKey"/> so <see cref="TriggerTimingMap"/>
/// opens exactly that timing; effects bound to it are then collected by the common loop.
///
/// Scoping (W4): when a <paramref name="subject"/> card is supplied the timing window is scoped to
/// that card — the subject is written to <see cref="AutoProcessingTriggerCollector.SourceEntityIdKey"/>
/// so only the subject's own effect fires (a revealed <c>[Security]</c> card, the card that just
/// digivolved). When no subject is supplied (turn boundaries, draw) no card filter is set, so every
/// effect registered for the timing is collected and self-gates via its own condition (mirroring the
/// AS-IS <c>StackSkillInfos(timing)</c>).
/// </summary>
public static class TriggerEventEmitter
{
    public static void Emit(
        GameEventQueue queue,
        string timing,
        HeadlessPlayerId? actor = null,
        HeadlessEntityId? subject = null,
        IReadOnlyDictionary<string, object?>? extraMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentException.ThrowIfNullOrWhiteSpace(timing);

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = timing,
        };

        // F-8.5: context flags an effect's condition reads (isJogress/isDigiXros/DPZero, ...).
        if (extraMetadata is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraMetadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        // W4: a card-scoped timing window (security check, digivolution) only fires the subject's own
        // effect. The collector filters by SourceEntityId, so a [Security] effect on the revealed card
        // resolves while OnSecurityCheck effects bound to other cards stay dormant.
        if (subject is { IsEmpty: false } scopedSubject)
        {
            metadata[AutoProcessingTriggerCollector.SourceEntityIdKey] = scopedSubject;
        }

        queue.Publish(new GameEvent(0, GameEventType.StateChanged, $"Timing window: {timing}", metadata)
        {
            Actor = actor,
            Subject = subject,
            Cause = timing,
        });
    }
}
