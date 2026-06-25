namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public static class EffectContextAdapterKeys
{
    public const string SourcePlayerId = "sourcePlayerId";
    public const string OwnerPlayerId = "ownerPlayerId";
    public const string SourceEntityId = "sourceEntityId";
    public const string TriggerEntityId = "triggerEntityId";
    public const string TargetEntityIds = "targetEntityIds";

    public static readonly IReadOnlyDictionary<string, string> LegacyAliases =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Card"] = SourceEntityId,
                ["Permanent"] = SourceEntityId,
                ["AttackingPermanent"] = SourceEntityId,
                ["CardEffect"] = SourceEntityId,
                ["Owner"] = OwnerPlayerId,
                ["Player"] = SourcePlayerId,
                ["Permanents"] = TargetEntityIds,
                ["TargetPermanents"] = TargetEntityIds,
                ["DiscardedCards"] = TargetEntityIds,
                ["battle"] = TriggerEntityId,
            });
}

public sealed record EffectContextAdapterInput
{
    public EffectContextAdapterInput(
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        Values = values;
        Aliases = aliases ?? EffectContextAdapterKeys.LegacyAliases;
    }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public IReadOnlyDictionary<string, string> Aliases { get; }
}

public sealed record EffectContextAdapterResult
{
    private EffectContextAdapterResult(
        bool isSuccess,
        EffectContext? context,
        string? errorCode,
        string? message)
    {
        IsSuccess = isSuccess;
        Context = context;
        ErrorCode = errorCode;
        Message = message;
    }

    public bool IsSuccess { get; }

    public EffectContext? Context { get; }

    public string? ErrorCode { get; }

    public string? Message { get; }

    public static EffectContextAdapterResult Success(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new EffectContextAdapterResult(true, context, null, null);
    }

    public static EffectContextAdapterResult Failure(string errorCode, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new EffectContextAdapterResult(false, null, errorCode.Trim(), message.Trim());
    }
}

public static class EffectContextAdapter
{
    public static EffectContextAdapterResult TryCreate(EffectContextAdapterInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        NormalizedPayload payload = Normalize(input.Values, input.Aliases);

        if (!TryReadPlayerId(payload.Values, EffectContextAdapterKeys.SourcePlayerId, required: true, out HeadlessPlayerId sourcePlayerId, out string? error))
        {
            return EffectContextAdapterResult.Failure("missing_source_player", error ?? "Source player id was invalid.");
        }

        if (!TryReadPlayerId(payload.Values, EffectContextAdapterKeys.OwnerPlayerId, required: false, out HeadlessPlayerId ownerPlayerId, out error))
        {
            return EffectContextAdapterResult.Failure("invalid_owner_player", error ?? "Owner player id was invalid.");
        }

        if (ownerPlayerId.IsEmpty)
        {
            ownerPlayerId = sourcePlayerId;
        }

        if (!TryReadEntityId(payload.Values, EffectContextAdapterKeys.SourceEntityId, required: true, out HeadlessEntityId sourceEntityId, out error))
        {
            return EffectContextAdapterResult.Failure("missing_source_entity", error ?? "Source entity id was invalid.");
        }

        if (!TryReadEntityId(payload.Values, EffectContextAdapterKeys.TriggerEntityId, required: false, out HeadlessEntityId triggerEntityId, out error))
        {
            return EffectContextAdapterResult.Failure("invalid_trigger_entity", error ?? "Trigger entity id was invalid.");
        }

        if (!TryReadTargetEntityIds(payload.Values, out IReadOnlyList<HeadlessEntityId> targetEntityIds, out error))
        {
            return EffectContextAdapterResult.Failure("invalid_target_entities", error ?? "Target entity ids were invalid.");
        }

        try
        {
            var context = new EffectContext(
                sourcePlayerId,
                ownerPlayerId,
                sourceEntityId,
                triggerEntityId.IsEmpty ? null : triggerEntityId,
                targetEntityIds,
                payload.ExtraValues);

            return EffectContextAdapterResult.Success(context);
        }
        catch (ArgumentException ex)
        {
            return EffectContextAdapterResult.Failure("invalid_context", ex.Message);
        }
    }

    public static EffectContext CreateOrThrow(EffectContextAdapterInput input)
    {
        EffectContextAdapterResult result = TryCreate(input);
        if (result.IsSuccess)
        {
            return result.Context!;
        }

        throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
    }

    public static IReadOnlyDictionary<string, object?> ExportValues(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [EffectContextAdapterKeys.SourcePlayerId] = context.SourcePlayerId,
            [EffectContextAdapterKeys.OwnerPlayerId] = context.OwnerPlayerId,
            [EffectContextAdapterKeys.SourceEntityId] = context.SourceEntityId,
        };

        if (context.TriggerEntityId is { } triggerEntityId)
        {
            values[EffectContextAdapterKeys.TriggerEntityId] = triggerEntityId;
        }

        if (context.TargetEntityIds.Count > 0)
        {
            values[EffectContextAdapterKeys.TargetEntityIds] = context.TargetEntityIds.ToArray();
        }

        foreach (KeyValuePair<string, object?> pair in context.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            values[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(values);
    }

    private static NormalizedPayload Normalize(
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyDictionary<string, string> aliases)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        var extras = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Effect context adapter keys must not be null or whitespace.", nameof(values));
            }

            string key = pair.Key.Trim();
            string normalizedKey = aliases.TryGetValue(key, out string? alias)
                ? alias
                : key;

            normalized[normalizedKey] = pair.Value;
        }

        foreach (KeyValuePair<string, object?> pair in normalized)
        {
            if (IsStructuralKey(pair.Key))
            {
                continue;
            }

            extras[pair.Key] = pair.Value;
        }

        return new NormalizedPayload(
            new ReadOnlyDictionary<string, object?>(normalized),
            new ReadOnlyDictionary<string, object?>(extras));
    }

    private static bool TryReadPlayerId(
        IReadOnlyDictionary<string, object?> values,
        string key,
        bool required,
        out HeadlessPlayerId playerId,
        out string? error)
    {
        if (!values.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            playerId = default;
            error = required ? $"Required player id key '{key}' was not found." : null;
            return !required;
        }

        if (rawValue is HeadlessPlayerId typed && !typed.IsEmpty)
        {
            playerId = typed;
            error = null;
            return true;
        }

        if (rawValue is int integer && integer > 0)
        {
            playerId = new HeadlessPlayerId(integer);
            error = null;
            return true;
        }

        if (rawValue is string text && HeadlessPlayerId.TryParse(text, out HeadlessPlayerId parsed))
        {
            playerId = parsed;
            error = null;
            return true;
        }

        playerId = default;
        error = $"Player id key '{key}' must be a positive integer, numeric string, or HeadlessPlayerId.";
        return false;
    }

    private static bool TryReadEntityId(
        IReadOnlyDictionary<string, object?> values,
        string key,
        bool required,
        out HeadlessEntityId entityId,
        out string? error)
    {
        if (!values.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            entityId = default;
            error = required ? $"Required entity id key '{key}' was not found." : null;
            return !required;
        }

        if (rawValue is HeadlessEntityId typed && !typed.IsEmpty)
        {
            entityId = typed;
            error = null;
            return true;
        }

        if (rawValue is string text && HeadlessEntityId.TryParse(text, out HeadlessEntityId parsed))
        {
            entityId = parsed;
            error = null;
            return true;
        }

        entityId = default;
        error = $"Entity id key '{key}' must be a non-empty string or HeadlessEntityId.";
        return false;
    }

    private static bool TryReadTargetEntityIds(
        IReadOnlyDictionary<string, object?> values,
        out IReadOnlyList<HeadlessEntityId> targetEntityIds,
        out string? error)
    {
        if (!values.TryGetValue(EffectContextAdapterKeys.TargetEntityIds, out object? rawValue) || rawValue is null)
        {
            targetEntityIds = Array.Empty<HeadlessEntityId>();
            error = null;
            return true;
        }

        IEnumerable<object?> rawItems = rawValue is string or HeadlessEntityId
            ? new[] { rawValue }
            : rawValue as IEnumerable<object?> ?? rawValue switch
            {
                IEnumerable<string> strings => strings.Cast<object?>(),
                IEnumerable<HeadlessEntityId> ids => ids.Cast<object?>(),
                _ => Array.Empty<object?>(),
            };

        if (!rawItems.Any() && rawValue is not Array { Length: 0 })
        {
            targetEntityIds = Array.Empty<HeadlessEntityId>();
            error = "Target entity ids must be an entity id, string, or enumerable of entity ids.";
            return false;
        }

        var parsed = new List<HeadlessEntityId>();
        foreach (object? item in rawItems)
        {
            if (item is HeadlessEntityId typed && !typed.IsEmpty)
            {
                parsed.Add(typed);
                continue;
            }

            if (item is string text && HeadlessEntityId.TryParse(text, out HeadlessEntityId id))
            {
                parsed.Add(id);
                continue;
            }

            targetEntityIds = Array.Empty<HeadlessEntityId>();
            error = "Target entity ids must contain only non-empty strings or HeadlessEntityId values.";
            return false;
        }

        if (parsed.Distinct().Count() != parsed.Count)
        {
            targetEntityIds = Array.Empty<HeadlessEntityId>();
            error = "Target entity ids must not contain duplicates.";
            return false;
        }

        targetEntityIds = parsed.AsReadOnly();
        error = null;
        return true;
    }

    private static bool IsStructuralKey(string key)
    {
        return string.Equals(key, EffectContextAdapterKeys.SourcePlayerId, StringComparison.Ordinal)
            || string.Equals(key, EffectContextAdapterKeys.OwnerPlayerId, StringComparison.Ordinal)
            || string.Equals(key, EffectContextAdapterKeys.SourceEntityId, StringComparison.Ordinal)
            || string.Equals(key, EffectContextAdapterKeys.TriggerEntityId, StringComparison.Ordinal)
            || string.Equals(key, EffectContextAdapterKeys.TargetEntityIds, StringComparison.Ordinal);
    }

    private sealed record NormalizedPayload(
        IReadOnlyDictionary<string, object?> Values,
        IReadOnlyDictionary<string, object?> ExtraValues);
}
