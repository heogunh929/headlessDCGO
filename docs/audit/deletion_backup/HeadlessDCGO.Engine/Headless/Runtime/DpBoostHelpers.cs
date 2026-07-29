// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/Permanent.cs::Permanent.Boosts / AddBoost / RemoveBoost / DPBoost class (DP fold: foreach(DPBoost boost in Boosts) DP+=boost.DP)@672;674;682;687
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (DPBoost) AS-IS <c>Permanent.Boosts</c> / <c>AddBoost</c> / <c>RemoveBoost</c> / <c>DPBoost</c> (Permanent.cs:
/// 672-699): a per-card list of named additive DP boosts, upserted by ID, folded into DP at the very END of the
/// DP calculation (after the NotIsUpDown group + LinkedDP, before the final &gt;=0 clamp — Permanent.cs:653-663).
/// AS-IS's DP fold adds <c>boost.DP</c> unconditionally (the DPBoost.Condition is not consulted in the fold), so the
/// headless stores just id→dp on the instance metadata; a boost's lifetime is managed by its owner card (add/remove),
/// exactly as AS-IS manages Boosts imperatively rather than as a continuous effect.
/// </summary>
public static class DpBoostHelpers
{
    /// <summary>Instance-metadata key holding the id→dp boost map (Dictionary&lt;string,int&gt;).</summary>
    public const string DpBoostsKey = "dpBoosts";

    /// <summary>AS-IS <c>Permanent.AddBoost</c>: upsert a boost by id (replace its DP if the id already exists).</summary>
    public static void AddBoost(ICardInstanceRepository repository, HeadlessEntityId cardId, string id, int dp)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        Dictionary<string, int> boosts = ReadBoosts(record.Metadata);
        boosts[id] = dp;
        WriteBoosts(repository, record, boosts);
    }

    /// <summary>AS-IS <c>Permanent.RemoveBoost</c>: remove a boost by id (no-op if absent).</summary>
    public static void RemoveBoost(ICardInstanceRepository repository, HeadlessEntityId cardId, string id)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (string.IsNullOrWhiteSpace(id)
            || !repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        Dictionary<string, int> boosts = ReadBoosts(record.Metadata);
        if (boosts.Remove(id))
        {
            WriteBoosts(repository, record, boosts);
        }
    }

    /// <summary>Sum of the card's DP boosts (AS-IS <c>foreach (DPBoost boost in Boosts) DP += boost.DP</c>).</summary>
    public static int TotalBoost(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(DpBoostsKey, out object? raw)
            || raw is not IReadOnlyDictionary<string, int> boosts)
        {
            return 0;
        }

        int total = 0;
        foreach (int dp in boosts.Values)
        {
            total += dp;
        }

        return total;
    }

    private static Dictionary<string, int> ReadBoosts(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DpBoostsKey, out object? raw) && raw is IReadOnlyDictionary<string, int> existing
            ? new Dictionary<string, int>(existing, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

    private static void WriteBoosts(ICardInstanceRepository repository, CardInstanceRecord record, Dictionary<string, int> boosts)
    {
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        if (boosts.Count == 0)
        {
            metadata.Remove(DpBoostsKey);
        }
        else
        {
            metadata[DpBoostsKey] = boosts;
        }

        repository.Upsert(record with { Metadata = metadata });
    }
}
