namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public enum OnceFlagScope
{
    Turn = 0,
    Timing = 1,
}

public sealed record OnceFlagKey
{
    public OnceFlagKey(
        HeadlessEntityId effectId,
        HeadlessEntityId sourceEntityId,
        HeadlessPlayerId ownerPlayerId,
        OnceFlagScope scope,
        string? timing = null)
    {
        if (effectId.IsEmpty)
        {
            throw new ArgumentException("Once flag effect id must not be empty.", nameof(effectId));
        }

        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Once flag source entity id must not be empty.", nameof(sourceEntityId));
        }

        if (ownerPlayerId.IsEmpty)
        {
            throw new ArgumentException("Once flag owner player id must not be empty.", nameof(ownerPlayerId));
        }

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), "Once flag scope must be known.");
        }

        string? normalizedTiming = string.IsNullOrWhiteSpace(timing) ? null : timing.Trim();
        if (scope == OnceFlagScope.Timing && normalizedTiming is null)
        {
            throw new ArgumentException("Timing-scoped once flags require a timing value.", nameof(timing));
        }

        EffectId = effectId;
        SourceEntityId = sourceEntityId;
        OwnerPlayerId = ownerPlayerId;
        Scope = scope;
        Timing = normalizedTiming;
    }

    public HeadlessEntityId EffectId { get; }

    public HeadlessEntityId SourceEntityId { get; }

    public HeadlessPlayerId OwnerPlayerId { get; }

    public OnceFlagScope Scope { get; }

    public string? Timing { get; }

    public string Value => string.Join(
        ":",
        OwnerPlayerId.Value,
        SourceEntityId.Value,
        EffectId.Value,
        Scope,
        Timing ?? "*");
}

public sealed record OnceFlagState
{
    public OnceFlagState(
        long turnSequence = 0,
        HeadlessPlayerId? turnPlayerId = null,
        IReadOnlyDictionary<string, int>? useCounts = null)
    {
        if (turnSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnSequence), "Turn sequence must not be negative.");
        }

        if (turnPlayerId is { IsEmpty: true })
        {
            throw new ArgumentException("Turn player id must not be empty.", nameof(turnPlayerId));
        }

        TurnSequence = turnSequence;
        TurnPlayerId = turnPlayerId;
        UseCounts = CopyCounts(useCounts);
    }

    public long TurnSequence { get; }

    public HeadlessPlayerId? TurnPlayerId { get; }

    public IReadOnlyDictionary<string, int> UseCounts { get; }

    public static OnceFlagState Empty { get; } = new();

    public int GetUseCount(OnceFlagKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return UseCounts.TryGetValue(key.Value, out int count) ? count : 0;
    }

    private static IReadOnlyDictionary<string, int> CopyCounts(
        IReadOnlyDictionary<string, int>? useCounts)
    {
        if (useCounts is null)
        {
            return new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> pair in useCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            if (pair.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(useCounts), "Once flag counts must not be negative.");
            }

            if (pair.Value > 0)
            {
                copy[pair.Key.Trim()] = pair.Value;
            }
        }

        return new ReadOnlyDictionary<string, int>(copy);
    }
}

public sealed record OnceFlagResult
{
    private OnceFlagResult(
        bool isSuccess,
        bool canUse,
        OnceFlagState state,
        OnceFlagKey? key,
        int useCount,
        int maxCount,
        string failureReason,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(state);

        IsSuccess = isSuccess;
        CanUse = canUse;
        State = state;
        Key = key;
        UseCount = useCount;
        MaxCount = maxCount;
        FailureReason = failureReason ?? string.Empty;
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public bool CanUse { get; }

    public OnceFlagState State { get; }

    public OnceFlagKey? Key { get; }

    public int UseCount { get; }

    public int MaxCount { get; }

    public string FailureReason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static OnceFlagResult Success(
        bool canUse,
        OnceFlagState state,
        OnceFlagKey? key,
        int useCount,
        int maxCount,
        IReadOnlyDictionary<string, object?> values)
    {
        return new OnceFlagResult(true, canUse, state, key, useCount, maxCount, string.Empty, values);
    }

    public static OnceFlagResult Failure(
        OnceFlagState state,
        OnceFlagKey? key,
        string failureReason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new OnceFlagResult(false, false, state, key, 0, 0, failureReason, values ?? ReadOnlyDictionary<string, object?>.Empty);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class OnceFlagHelpers
{
    public const string FlagKeyValueKey = "onceFlag.key";
    public const string EffectIdKey = "onceFlag.effectId";
    public const string SourceEntityIdKey = "onceFlag.sourceEntityId";
    public const string OwnerPlayerIdKey = "onceFlag.ownerPlayerId";
    public const string ScopeKey = "onceFlag.scope";
    public const string TimingKey = "onceFlag.timing";
    public const string UseCountKey = "onceFlag.useCount";
    public const string MaxCountKey = "onceFlag.maxCount";
    public const string CanUseKey = "onceFlag.canUse";
    public const string TurnSequenceKey = "onceFlag.turnSequence";
    public const string TurnPlayerIdKey = "onceFlag.turnPlayerId";
    public const string ActiveFlagKeysKey = "onceFlag.activeKeys";

    public static OnceFlagKey ForRequest(
        EffectRequest request,
        OnceFlagScope scope = OnceFlagScope.Turn,
        string? timing = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new OnceFlagKey(
            request.EffectId,
            request.Context.SourceEntityId,
            request.Context.OwnerPlayerId,
            scope,
            scope == OnceFlagScope.Timing ? timing ?? request.Timing : timing);
    }

    public static OnceFlagResult CanUse(
        OnceFlagState state,
        OnceFlagKey key,
        int maxCount = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(key);
        if (maxCount <= 0)
        {
            return OnceFlagResult.Failure(
                state,
                key,
                "Once flag max count must be positive.",
                Values(state, key, 0, maxCount, canUse: false));
        }

        int count = state.GetUseCount(key);
        bool canUse = count < maxCount;
        return OnceFlagResult.Success(
            canUse,
            state,
            key,
            count,
            maxCount,
            Values(state, key, count, maxCount, canUse));
    }

    public static OnceFlagResult RegisterUse(
        OnceFlagState state,
        OnceFlagKey key,
        int maxCount = 1)
    {
        OnceFlagResult check = CanUse(state, key, maxCount);
        if (!check.IsSuccess)
        {
            return check;
        }

        if (!check.CanUse)
        {
            return OnceFlagResult.Failure(
                state,
                key,
                "Once flag reached max count for this turn scope.",
                check.Values);
        }

        var counts = new Dictionary<string, int>(state.UseCounts, StringComparer.Ordinal)
        {
            [key.Value] = check.UseCount + 1,
        };
        var nextState = new OnceFlagState(state.TurnSequence, state.TurnPlayerId, counts);
        int nextCount = nextState.GetUseCount(key);
        return OnceFlagResult.Success(
            canUse: nextCount < maxCount,
            nextState,
            key,
            nextCount,
            maxCount,
            Values(nextState, key, nextCount, maxCount, nextCount < maxCount));
    }

    public static OnceFlagResult RemoveUse(
        OnceFlagState state,
        OnceFlagKey key)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(key);

        int current = state.GetUseCount(key);
        if (current == 0)
        {
            return OnceFlagResult.Failure(
                state,
                key,
                "Once flag cannot remove a use that was not registered.",
                Values(state, key, 0, maxCount: 1, canUse: true));
        }

        var counts = new Dictionary<string, int>(state.UseCounts, StringComparer.Ordinal);
        if (current == 1)
        {
            counts.Remove(key.Value);
        }
        else
        {
            counts[key.Value] = current - 1;
        }

        var nextState = new OnceFlagState(state.TurnSequence, state.TurnPlayerId, counts);
        int nextCount = nextState.GetUseCount(key);
        return OnceFlagResult.Success(
            canUse: true,
            nextState,
            key,
            nextCount,
            maxCount: 1,
            Values(nextState, key, nextCount, maxCount: 1, canUse: true));
    }

    public static OnceFlagResult ResetTurn(
        OnceFlagState state,
        long nextTurnSequence,
        HeadlessPlayerId? nextTurnPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nextTurnSequence < 0)
        {
            return OnceFlagResult.Failure(state, null, "Next turn sequence must not be negative.");
        }

        if (nextTurnPlayerId is { IsEmpty: true })
        {
            return OnceFlagResult.Failure(state, null, "Next turn player id must not be empty.");
        }

        var nextState = new OnceFlagState(nextTurnSequence, nextTurnPlayerId);
        return OnceFlagResult.Success(
            canUse: true,
            nextState,
            key: null,
            useCount: 0,
            maxCount: 1,
            Values(nextState, key: null, useCount: 0, maxCount: 1, canUse: true));
    }

    public static EffectContext WithUseCount(
        EffectContext context,
        OnceFlagState state,
        OnceFlagKey key)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(key);

        var values = new Dictionary<string, object?>(context.Values, StringComparer.Ordinal)
        {
            [CanUseEffectHelpers.UseCountThisTurnKey] = state.GetUseCount(key),
            [FlagKeyValueKey] = key.Value,
            [UseCountKey] = state.GetUseCount(key),
            [TurnSequenceKey] = state.TurnSequence,
            [TurnPlayerIdKey] = state.TurnPlayerId?.Value,
        };

        return new EffectContext(
            context.SourcePlayerId,
            context.OwnerPlayerId,
            context.SourceEntityId,
            context.TriggerEntityId,
            context.TargetEntityIds,
            values);
    }

    public static IReadOnlyDictionary<string, object?> Values(
        OnceFlagState state,
        OnceFlagKey? key,
        int useCount,
        int maxCount,
        bool canUse)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [FlagKeyValueKey] = key?.Value,
                [EffectIdKey] = key?.EffectId.Value,
                [SourceEntityIdKey] = key?.SourceEntityId.Value,
                [OwnerPlayerIdKey] = key?.OwnerPlayerId.Value,
                [ScopeKey] = key?.Scope.ToString(),
                [TimingKey] = key?.Timing,
                [UseCountKey] = useCount,
                [MaxCountKey] = maxCount,
                [CanUseKey] = canUse,
                [TurnSequenceKey] = state.TurnSequence,
                [TurnPlayerIdKey] = state.TurnPlayerId?.Value,
                [ActiveFlagKeysKey] = state.UseCounts.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            });
    }
}
