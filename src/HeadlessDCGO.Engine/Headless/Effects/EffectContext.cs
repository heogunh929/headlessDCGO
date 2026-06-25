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
