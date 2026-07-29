// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/CardController.cs::AS-IS Hashtable list 페이로드(예: WinnerPermanents)@CardController.cs:4694(hashtable.Add WinnerPermanents 부근)
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F1-M0-2) The primitive-flattening convention for threading a COLLECTION event payload (a list of entity
/// ids — winner/loser sets, discarded/added/deck-bottom sources, a leave batch, …) through the
/// activated-bridge resolve-context.
///
/// WHY: AS-IS <c>GetSkillInfos</c> passes the raw hashtable to a timing's gate, and the hashtable holds real
/// <c>List&lt;Permanent&gt;</c> objects (e.g. CardController.cs:4694 <c>hashtable.Add("WinnerPermanents", …)</c>,
/// read back by gates such as <c>GetLoserPermanentsFromHashtable(...).Contains(permanent)</c>). The headless
/// bridge threads only PRIMITIVE event metadata: <see cref="ActivatedEffectResolver.BuildUniformResolveContext"/>
/// (mirrored by <see cref="GameFlowProcessor"/>) copies a driving event's metadata into <c>event.&lt;key&gt;</c>
/// resolve-context values but keeps only <c>string/int/bool/long</c> — a <c>List</c> or <c>Func</c> payload is
/// DROPPED (regression point #7). So any collection a gate must read has to be flattened to a primitive first.
///
/// CONVENTION (already used ad-hoc across the engine — winnerIds/loserIds/loserRealIds in
/// <c>BattleResolver</c>, discardedCardIds/deckBottomCardIds in <c>DigivolutionStackHelpers</c>, addedCardIds
/// in <c>DigivolveAction</c>, …): a collection of <see cref="HeadlessEntityId"/> is emitted as a
/// <b>comma-separated string of the id VALUES</b> under a plural key (e.g. <c>winnerIds</c>), and a gate parses
/// it back with <see cref="ReadIds"/> / <see cref="ParseIds"/>. This class codifies that convention as a single
/// reusable pair so the M1+ broadcast timings that must read collection payloads all flatten identically (and so
/// the drop-list in BuildUniformResolveContext never silently swallows a gate input). Scalar payloads (a single
/// <c>attackerId</c>) need no flattening — pass the raw <c>id.Value</c> string.
/// </summary>
public static class EventCollectionMetadata
{
    /// <summary>The delimiter used by the CSV-of-id-values convention. A <see cref="HeadlessEntityId"/> value is
    /// an opaque token that never contains this character.</summary>
    public const char Separator = ',';

    /// <summary>Flatten a collection of entity ids to the CSV primitive stored under an <c>event.&lt;key&gt;</c>
    /// metadata key. Mirrors the existing <c>string.Join(",", ids.Select(id => id.Value))</c> call sites verbatim
    /// (byte-identical output), so routing an existing emit through it is behavior-neutral.</summary>
    public static string Flatten(IEnumerable<HeadlessEntityId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return string.Join(Separator, ids.Select(id => id.Value));
    }

    /// <summary>Parse a raw CSV metadata value (as stored by <see cref="Flatten"/>) back into entity ids. Empty
    /// / absent / non-string values yield an empty list. Mirrors the existing
    /// <c>value.Split(',', RemoveEmptyEntries | TrimEntries).Select(v =&gt; new HeadlessEntityId(v))</c> read
    /// sites verbatim.</summary>
    public static IReadOnlyList<HeadlessEntityId> ParseIds(object? raw)
    {
        if (raw?.ToString() is not { Length: > 0 } value)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return value
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => new HeadlessEntityId(v))
            .ToArray();
    }

    /// <summary>Read the entity ids threaded under the <c>event.&lt;key&gt;</c> resolve-context value (the
    /// <see cref="GameFlowProcessor.EventValuePrefix"/> namespace). Convenience over <see cref="ParseIds"/> for a
    /// gate holding the resolve-context <c>Values</c> dictionary.</summary>
    public static IReadOnlyList<HeadlessEntityId> ReadIds(IReadOnlyDictionary<string, object?> values, string key)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{key}", out object? raw)
            ? ParseIds(raw)
            : Array.Empty<HeadlessEntityId>();
    }
}
