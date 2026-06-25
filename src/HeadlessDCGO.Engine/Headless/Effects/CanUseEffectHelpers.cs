namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum CanUseEffectEvaluationKind
{
    Trigger = 0,
    Activate = 1,
    Use = 2,
}

public sealed record CanUseEffectCondition
{
    public CanUseEffectCondition(
        string key,
        object? expectedValue = null,
        bool mustExist = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
        ExpectedValue = expectedValue;
        MustExist = mustExist;
    }

    public string Key { get; }

    public object? ExpectedValue { get; }

    public bool MustExist { get; }

    public static CanUseEffectCondition RequireFlag(string key, bool expectedValue = true)
    {
        return new CanUseEffectCondition(key, expectedValue, mustExist: true);
    }

    public static CanUseEffectCondition ForbidFlag(string key)
    {
        return new CanUseEffectCondition(key, true, mustExist: false);
    }
}

public sealed record CanUseEffectRequest
{
    public CanUseEffectRequest(
        MatchState matchState,
        SkillInfo skillInfo,
        TriggerConditionKind? triggerCondition = null,
        IReadOnlyList<CanUseEffectCondition>? triggerConditions = null,
        IReadOnlyList<CanUseEffectCondition>? activationConditions = null)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(skillInfo);

        if (triggerCondition.HasValue && !Enum.IsDefined(triggerCondition.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(triggerCondition), "Trigger condition kind must be known.");
        }

        MatchState = matchState;
        SkillInfo = skillInfo;
        TriggerCondition = triggerCondition;
        TriggerConditions = CopyConditions(triggerConditions);
        ActivationConditions = CopyConditions(activationConditions);
    }

    public MatchState MatchState { get; }

    public SkillInfo SkillInfo { get; }

    public TriggerConditionKind? TriggerCondition { get; }

    public IReadOnlyList<CanUseEffectCondition> TriggerConditions { get; }

    public IReadOnlyList<CanUseEffectCondition> ActivationConditions { get; }

    public EffectContext Context => SkillInfo.Context;

    private static IReadOnlyList<CanUseEffectCondition> CopyConditions(
        IReadOnlyList<CanUseEffectCondition>? conditions)
    {
        return Array.AsReadOnly((conditions ?? Array.Empty<CanUseEffectCondition>()).ToArray());
    }
}

public sealed record CanUseEffectResult
{
    private CanUseEffectResult(
        bool canUse,
        CanUseEffectEvaluationKind kind,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        CanUse = canUse;
        Kind = kind;
        Reason = reason;
        Values = CopyValues(values);
    }

    public bool CanUse { get; }

    public CanUseEffectEvaluationKind Kind { get; }

    public string Reason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static CanUseEffectResult Allowed(
        CanUseEffectEvaluationKind kind,
        string reason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new CanUseEffectResult(true, kind, reason, values ?? ReadOnlyDictionary<string, object?>.Empty);
    }

    public static CanUseEffectResult Blocked(
        CanUseEffectEvaluationKind kind,
        string reason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new CanUseEffectResult(false, kind, reason, values ?? ReadOnlyDictionary<string, object?>.Empty);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
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

public static class CanUseEffectHelpers
{
    public const string UseCountThisTurnKey = "useCountThisTurn";
    public const string SourceDisabledKey = "isDisabled";
    public const string SourceCanActivateKey = "canActivate";
    public const string SourceIsTopKey = "isTopSource";
    public const string RequiresTopSourceKey = "requiresTopSource";

    public static CanUseEffectResult CanTrigger(CanUseEffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MatchState.IsTerminal)
        {
            return Blocked(
                CanUseEffectEvaluationKind.Trigger,
                request,
                "Terminal match state cannot trigger effects.");
        }

        if (IsOverMaxCount(request))
        {
            return Blocked(
                CanUseEffectEvaluationKind.Trigger,
                request,
                "Effect reached max count per turn.");
        }

        if (request.TriggerCondition.HasValue)
        {
            TriggerConditionResult trigger = TriggerConditionHelpers.Evaluate(
                new TriggerConditionRequest(
                    request.MatchState,
                    request.Context,
                    request.TriggerCondition.Value));
            if (!trigger.IsMatch)
            {
                return Blocked(
                    CanUseEffectEvaluationKind.Trigger,
                    request,
                    $"Trigger condition failed: {trigger.Reason}",
                    trigger.Values);
            }
        }

        CanUseEffectResult conditionResult = EvaluateConditions(
            request,
            request.TriggerConditions,
            CanUseEffectEvaluationKind.Trigger);
        if (!conditionResult.CanUse)
        {
            return conditionResult;
        }

        return Allowed(
            CanUseEffectEvaluationKind.Trigger,
            request,
            "Trigger conditions passed.");
    }

    public static CanUseEffectResult CanActivate(CanUseEffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsOverMaxCount(request))
        {
            return Blocked(
                CanUseEffectEvaluationKind.Activate,
                request,
                "Effect reached max count per turn.");
        }

        if (!TryFindSource(request, out CardInstanceState? source, out string reason))
        {
            return Blocked(CanUseEffectEvaluationKind.Activate, request, reason);
        }

        if (ReadBool(source.Flags, SourceDisabledKey) || ReadBool(source.Modifiers, SourceDisabledKey))
        {
            return Blocked(
                CanUseEffectEvaluationKind.Activate,
                request,
                "Effect source is disabled.");
        }

        if (!ReadBool(source.Modifiers, SourceCanActivateKey, defaultValue: true))
        {
            return Blocked(
                CanUseEffectEvaluationKind.Activate,
                request,
                "Effect source cannot activate.");
        }

        if (ReadBool(request.Context.Values, RequiresTopSourceKey) &&
            !ReadBool(source.Modifiers, SourceIsTopKey, defaultValue: true))
        {
            return Blocked(
                CanUseEffectEvaluationKind.Activate,
                request,
                "Effect requires the source to be the top card.");
        }

        CanUseEffectResult conditionResult = EvaluateConditions(
            request,
            request.ActivationConditions,
            CanUseEffectEvaluationKind.Activate);
        if (!conditionResult.CanUse)
        {
            return conditionResult;
        }

        return Allowed(
            CanUseEffectEvaluationKind.Activate,
            request,
            "Activation conditions passed.");
    }

    public static CanUseEffectResult CanUse(CanUseEffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CanUseEffectResult trigger = CanTrigger(request);
        if (!trigger.CanUse)
        {
            return Blocked(
                CanUseEffectEvaluationKind.Use,
                request,
                $"CanTrigger failed: {trigger.Reason}",
                trigger.Values);
        }

        CanUseEffectResult activate = CanActivate(request);
        if (!activate.CanUse)
        {
            return Blocked(
                CanUseEffectEvaluationKind.Use,
                request,
                $"CanActivate failed: {activate.Reason}",
                activate.Values);
        }

        return Allowed(
            CanUseEffectEvaluationKind.Use,
            request,
            "CanTrigger and CanActivate passed.");
    }

    private static CanUseEffectResult EvaluateConditions(
        CanUseEffectRequest request,
        IReadOnlyList<CanUseEffectCondition> conditions,
        CanUseEffectEvaluationKind kind)
    {
        foreach (CanUseEffectCondition condition in conditions)
        {
            bool exists = request.Context.Values.TryGetValue(condition.Key, out object? actualValue);
            if (!condition.MustExist && exists && ValuesEqual(condition.ExpectedValue, actualValue))
            {
                return Blocked(kind, request, $"Forbidden condition '{condition.Key}' was present.");
            }

            if (condition.MustExist && !exists)
            {
                return Blocked(kind, request, $"Required condition '{condition.Key}' was not present.");
            }

            if (condition.MustExist &&
                condition.ExpectedValue is not null &&
                !ValuesEqual(condition.ExpectedValue, actualValue))
            {
                return Blocked(kind, request, $"Condition '{condition.Key}' did not match expected value.");
            }
        }

        return Allowed(kind, request, "Typed conditions passed.");
    }

    private static bool IsOverMaxCount(CanUseEffectRequest request)
    {
        int? maxCount = request.SkillInfo.MaxCountPerTurn;
        if (!maxCount.HasValue)
        {
            return false;
        }

        int count = ReadInt(request.Context.Values, UseCountThisTurnKey);
        return count >= maxCount.Value;
    }

    private static bool TryFindSource(
        CanUseEffectRequest request,
        [NotNullWhen(true)] out CardInstanceState? source,
        out string reason)
    {
        if (!request.MatchState.CardInstances.TryGetValue(request.Context.SourceEntityId, out source))
        {
            reason = $"Effect source '{request.Context.SourceEntityId}' was not found.";
            return false;
        }

        if (source.OwnerId != request.Context.OwnerPlayerId)
        {
            reason = $"Effect source owner '{source.OwnerId}' did not match context owner '{request.Context.OwnerPlayerId}'.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static CanUseEffectResult Allowed(
        CanUseEffectEvaluationKind kind,
        CanUseEffectRequest request,
        string reason,
        IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        return CanUseEffectResult.Allowed(kind, reason, Values(request, extraValues));
    }

    private static CanUseEffectResult Blocked(
        CanUseEffectEvaluationKind kind,
        CanUseEffectRequest request,
        string reason,
        IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        return CanUseEffectResult.Blocked(kind, reason, Values(request, extraValues));
    }

    private static IReadOnlyDictionary<string, object?> Values(
        CanUseEffectRequest request,
        IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["effectId"] = request.SkillInfo.EffectId.Value,
            ["effectName"] = request.SkillInfo.Definition.Name,
            ["timing"] = request.SkillInfo.Timing,
            [EffectContextAdapterKeys.SourcePlayerId] = request.Context.SourcePlayerId.Value,
            [EffectContextAdapterKeys.OwnerPlayerId] = request.Context.OwnerPlayerId.Value,
            [EffectContextAdapterKeys.SourceEntityId] = request.Context.SourceEntityId.Value,
            [UseCountThisTurnKey] = ReadInt(request.Context.Values, UseCountThisTurnKey),
        };

        if (request.SkillInfo.MaxCountPerTurn.HasValue)
        {
            values["maxCountPerTurn"] = request.SkillInfo.MaxCountPerTurn.Value;
        }

        if (request.TriggerCondition.HasValue)
        {
            values[TriggerConditionHelpers.TriggerConditionKey] = request.TriggerCondition.Value.ToString();
        }

        if (extraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, bool> values,
        string key,
        bool defaultValue = false)
    {
        return values.TryGetValue(key, out bool value) ? value : defaultValue;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object?> values,
        string key,
        bool defaultValue = false)
    {
        if (!values.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) ? parsed : defaultValue,
            _ => defaultValue
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return 0;
        }

        return rawValue switch
        {
            int value => value,
            long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
            string value when int.TryParse(value, out int parsed) => parsed,
            _ => 0
        };
    }

    private static bool ValuesEqual(object? expectedValue, object? actualValue)
    {
        if (expectedValue is null)
        {
            return actualValue is null;
        }

        if (expectedValue is bool expectedBool)
        {
            return actualValue switch
            {
                bool actualBool => actualBool == expectedBool,
                string actualString when bool.TryParse(actualString, out bool parsed) => parsed == expectedBool,
                _ => false
            };
        }

        return Equals(expectedValue, actualValue)
            || string.Equals(expectedValue.ToString(), actualValue?.ToString(), StringComparison.Ordinal);
    }
}
