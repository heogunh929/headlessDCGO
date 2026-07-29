// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): 무대응(AS-IS 없음)
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record EffectContext
{
    public EffectContext(
        HeadlessPlayerId sourcePlayerId,
        HeadlessEntityId sourceEntityId,
        IReadOnlyDictionary<string, object?>? values = null)
        : this(
            sourcePlayerId,
            sourcePlayerId,
            sourceEntityId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values)
    {
    }

    public EffectContext(
        HeadlessPlayerId sourcePlayerId,
        HeadlessPlayerId ownerPlayerId,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? triggerEntityId,
        IReadOnlyList<HeadlessEntityId>? targetEntityIds,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        if (sourcePlayerId.IsEmpty)
        {
            throw new ArgumentException("Effect source player id must not be empty.", nameof(sourcePlayerId));
        }

        if (ownerPlayerId.IsEmpty)
        {
            throw new ArgumentException("Effect owner player id must not be empty.", nameof(ownerPlayerId));
        }

        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Effect source entity id must not be empty.", nameof(sourceEntityId));
        }

        if (triggerEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Effect trigger entity id must not be empty.", nameof(triggerEntityId));
        }

        HeadlessEntityId[] targets = (targetEntityIds ?? Array.Empty<HeadlessEntityId>()).ToArray();
        if (targets.Any(target => target.IsEmpty))
        {
            throw new ArgumentException("Effect target entity ids must not contain empty values.", nameof(targetEntityIds));
        }

        if (targets.Distinct().Count() != targets.Length)
        {
            throw new ArgumentException("Effect target entity ids must not contain duplicates.", nameof(targetEntityIds));
        }

        SourcePlayerId = sourcePlayerId;
        OwnerPlayerId = ownerPlayerId;
        SourceEntityId = sourceEntityId;
        TriggerEntityId = triggerEntityId;
        TargetEntityIds = Array.AsReadOnly(targets);
        Values = CopyValues(values);
    }

    public HeadlessPlayerId SourcePlayerId { get; }

    public HeadlessPlayerId OwnerPlayerId { get; }

    public HeadlessEntityId SourceEntityId { get; }

    public HeadlessEntityId? TriggerEntityId { get; }

    public IReadOnlyList<HeadlessEntityId> TargetEntityIds { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public bool HasValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Values.ContainsKey(key.Trim());
    }

    public bool TryGetValue<TValue>(string key, out TValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Values.TryGetValue(key.Trim(), out object? rawValue)
            && rawValue is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public TValue GetRequiredValue<TValue>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string normalizedKey = key.Trim();

        if (!Values.TryGetValue(normalizedKey, out object? rawValue))
        {
            throw new KeyNotFoundException($"Effect context value '{normalizedKey}' was not found.");
        }

        if (rawValue is TValue typedValue)
        {
            return typedValue;
        }

        string actualType = rawValue?.GetType().Name ?? "null";
        throw new InvalidOperationException(
            $"Effect context value '{normalizedKey}' must be {typeof(TValue).Name}; actual type was {actualType}.");
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Effect context value keys must not be null or whitespace.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
