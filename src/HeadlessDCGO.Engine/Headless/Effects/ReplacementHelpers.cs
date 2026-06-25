namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum ReplacementEventKind
{
    RemoveFromField = 0,
    Delete = 1,
    DpReduction = 2,
    EffectMutation = 3,
}

public enum ReplacementActionKind
{
    Prevent = 0,
    Redirect = 1,
    Immune = 2,
}

public sealed record ReplacementEffect
{
    public ReplacementEffect(
        string id,
        ReplacementEventKind eventKind,
        ReplacementActionKind actionKind,
        HeadlessEntityId? targetEntityId = null,
        HeadlessEntityId? replacementEntityId = null,
        HeadlessEntityId? sourceEntityId = null,
        string? mutationKind = null,
        string? reason = null,
        int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(eventKind))
        {
            throw new ArgumentOutOfRangeException(nameof(eventKind), "Replacement event kind must be known.");
        }

        if (!Enum.IsDefined(actionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actionKind), "Replacement action kind must be known.");
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Replacement target id must not be empty.", nameof(targetEntityId));
        }

        if (replacementEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Replacement entity id must not be empty.", nameof(replacementEntityId));
        }

        if (sourceEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Replacement source id must not be empty.", nameof(sourceEntityId));
        }

        if (actionKind == ReplacementActionKind.Redirect && replacementEntityId is null)
        {
            throw new ArgumentException("Redirect replacement effects require a replacement entity id.", nameof(replacementEntityId));
        }

        Id = id.Trim();
        EventKind = eventKind;
        ActionKind = actionKind;
        TargetEntityId = targetEntityId;
        ReplacementEntityId = replacementEntityId;
        SourceEntityId = sourceEntityId;
        MutationKind = string.IsNullOrWhiteSpace(mutationKind) ? null : mutationKind.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? DefaultReason(actionKind, eventKind) : reason.Trim();
        Priority = priority;
    }

    public string Id { get; }

    public ReplacementEventKind EventKind { get; }

    public ReplacementActionKind ActionKind { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public HeadlessEntityId? ReplacementEntityId { get; }

    public HeadlessEntityId? SourceEntityId { get; }

    public string? MutationKind { get; }

    public string Reason { get; }

    public int Priority { get; }

    public static ReplacementEffect Prevent(
        string id,
        ReplacementEventKind eventKind,
        HeadlessEntityId targetEntityId,
        string? reason = null,
        int priority = 0)
    {
        return new ReplacementEffect(id, eventKind, ReplacementActionKind.Prevent, targetEntityId, reason: reason, priority: priority);
    }

    public static ReplacementEffect Redirect(
        string id,
        ReplacementEventKind eventKind,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId replacementEntityId,
        string? reason = null,
        int priority = 0)
    {
        return new ReplacementEffect(id, eventKind, ReplacementActionKind.Redirect, targetEntityId, replacementEntityId, reason: reason, priority: priority);
    }

    public static ReplacementEffect Immune(
        string id,
        ReplacementEventKind eventKind,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId? sourceEntityId = null,
        string? mutationKind = null,
        string? reason = null,
        int priority = 0)
    {
        return new ReplacementEffect(id, eventKind, ReplacementActionKind.Immune, targetEntityId, sourceEntityId: sourceEntityId, mutationKind: mutationKind, reason: reason, priority: priority);
    }

    private static string DefaultReason(ReplacementActionKind actionKind, ReplacementEventKind eventKind)
    {
        return actionKind switch
        {
            ReplacementActionKind.Prevent => $"{eventKind} is prevented.",
            ReplacementActionKind.Redirect => $"{eventKind} is redirected.",
            ReplacementActionKind.Immune => $"{eventKind} does not affect the target.",
            _ => "Replacement effect applied.",
        };
    }
}

public sealed record ReplacementRequest
{
    public ReplacementRequest(
        ReplacementEventKind eventKind,
        HeadlessEntityId targetEntityId,
        IReadOnlyList<ReplacementEffect>? replacements = null,
        HeadlessEntityId? sourceEntityId = null,
        string? mutationKind = null)
    {
        if (!Enum.IsDefined(eventKind))
        {
            throw new ArgumentOutOfRangeException(nameof(eventKind), "Replacement event kind must be known.");
        }

        if (targetEntityId.IsEmpty)
        {
            throw new ArgumentException("Replacement request target id must not be empty.", nameof(targetEntityId));
        }

        if (sourceEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Replacement request source id must not be empty.", nameof(sourceEntityId));
        }

        EventKind = eventKind;
        TargetEntityId = targetEntityId;
        Replacements = Array.AsReadOnly((replacements ?? Array.Empty<ReplacementEffect>()).ToArray());
        SourceEntityId = sourceEntityId;
        MutationKind = string.IsNullOrWhiteSpace(mutationKind) ? null : mutationKind.Trim();
    }

    public ReplacementEventKind EventKind { get; }

    public HeadlessEntityId TargetEntityId { get; }

    public IReadOnlyList<ReplacementEffect> Replacements { get; }

    public HeadlessEntityId? SourceEntityId { get; }

    public string? MutationKind { get; }
}

public sealed record ReplacementResult
{
    private ReplacementResult(
        bool isReplaced,
        ReplacementActionKind? actionKind,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId? replacementEntityId,
        string reason,
        IReadOnlyList<string> appliedReplacementIds,
        IReadOnlyList<string> skippedReplacementIds,
        IReadOnlyDictionary<string, object?> values)
    {
        IsReplaced = isReplaced;
        ActionKind = actionKind;
        TargetEntityId = targetEntityId;
        ReplacementEntityId = replacementEntityId;
        Reason = reason;
        AppliedReplacementIds = Array.AsReadOnly(appliedReplacementIds.ToArray());
        SkippedReplacementIds = Array.AsReadOnly(skippedReplacementIds.ToArray());
        Values = CopyValues(values);
    }

    public bool IsReplaced { get; }

    public ReplacementActionKind? ActionKind { get; }

    public HeadlessEntityId TargetEntityId { get; }

    public HeadlessEntityId? ReplacementEntityId { get; }

    public string Reason { get; }

    public IReadOnlyList<string> AppliedReplacementIds { get; }

    public IReadOnlyList<string> SkippedReplacementIds { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static ReplacementResult Success(
        bool isReplaced,
        ReplacementActionKind? actionKind,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId? replacementEntityId,
        string reason,
        IReadOnlyList<string> appliedReplacementIds,
        IReadOnlyList<string> skippedReplacementIds,
        IReadOnlyDictionary<string, object?> values)
    {
        return new ReplacementResult(isReplaced, actionKind, targetEntityId, replacementEntityId, reason, appliedReplacementIds, skippedReplacementIds, values);
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

public static class ReplacementHelpers
{
    public const string ReplacementsKey = "replacementEffects";
    public const string EventKindKey = "eventKind";
    public const string ActionKindKey = "actionKind";
    public const string TargetEntityIdKey = "targetEntityId";
    public const string ReplacementEntityIdKey = "replacementEntityId";
    public const string SourceEntityIdKey = "sourceEntityId";
    public const string MutationKindKey = "mutationKind";
    public const string ReasonKey = "reason";
    public const string PriorityKey = "priority";
    public const string PreventRemovalKey = "preventRemoval";
    public const string PreventDeletionKey = "preventDeletion";
    public const string ImmuneFromDpMinusKey = "immuneFromDpMinus";
    public const string ImmuneFromEffectsKey = "immuneFromEffects";

    public static ReplacementResult Evaluate(ReplacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ReplacementEffect[] relevant = request.Replacements
            .Where(replacement => replacement.EventKind == request.EventKind)
            .OrderBy(replacement => ActionOrder(replacement.ActionKind))
            .ThenByDescending(replacement => replacement.Priority)
            .ThenBy(replacement => replacement.Id, StringComparer.Ordinal)
            .ToArray();
        var applied = new List<string>();
        var skipped = new List<string>();
        ReplacementEffect? selected = null;

        foreach (ReplacementEffect replacement in relevant)
        {
            if (!CanApply(replacement, request))
            {
                skipped.Add(replacement.Id);
                continue;
            }

            selected = replacement;
            applied.Add(replacement.Id);
            break;
        }

        foreach (ReplacementEffect replacement in relevant)
        {
            if (selected is not null && replacement.Id == selected.Id)
            {
                continue;
            }

            if (!skipped.Contains(replacement.Id, StringComparer.Ordinal))
            {
                skipped.Add(replacement.Id);
            }
        }

        bool isReplaced = selected is not null;
        HeadlessEntityId? replacementEntityId = selected?.ReplacementEntityId;
        string reason = selected?.Reason ?? "No replacement effect matched.";

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["eventKind"] = request.EventKind.ToString(),
            ["targetEntityId"] = request.TargetEntityId.Value,
            ["replacementCount"] = request.Replacements.Count,
            ["isReplaced"] = isReplaced,
            ["actionKind"] = selected?.ActionKind.ToString(),
            ["replacementEntityId"] = replacementEntityId?.Value,
            ["reason"] = reason,
            ["appliedReplacementIds"] = applied.ToArray(),
            ["skippedReplacementIds"] = skipped.ToArray(),
        };

        if (request.SourceEntityId is HeadlessEntityId sourceEntityId)
        {
            values["sourceEntityId"] = sourceEntityId.Value;
        }

        if (request.MutationKind is not null)
        {
            values["mutationKind"] = request.MutationKind;
        }

        return ReplacementResult.Success(
            isReplaced,
            selected?.ActionKind,
            request.TargetEntityId,
            replacementEntityId,
            reason,
            applied,
            skipped,
            values);
    }

    public static IReadOnlyList<ReplacementEffect> ReadReplacements(
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null,
        IEnumerable<EffectRequest>? effectRequests = null)
    {
        var replacements = new List<ReplacementEffect>();
        if (card is not null)
        {
            replacements.AddRange(ReadReplacementsFromValues(card.Metadata));
        }

        if (instance is not null)
        {
            replacements.AddRange(ReadReplacementsFromValues(instance.Metadata));
        }

        if (state is not null)
        {
            replacements.AddRange(ReadReplacementsFromValues(state.Modifiers));
            replacements.AddRange(ReadReplacementsFromFlags(state.Flags));
        }

        if (effectRequests is not null)
        {
            foreach (EffectRequest request in effectRequests)
            {
                replacements.AddRange(ReadReplacementsFromValues(request.Context.Values, request.EffectId));
            }
        }

        return replacements
            .OrderBy(replacement => replacement.EventKind)
            .ThenBy(replacement => ActionOrder(replacement.ActionKind))
            .ThenByDescending(replacement => replacement.Priority)
            .ThenBy(replacement => replacement.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ReplacementEffect> QueryReplacements(
        IEffectQueryService effectQueryService,
        EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(effectQueryService);
        ArgumentNullException.ThrowIfNull(context);
        return ReadReplacements(effectRequests: effectQueryService.GetReplacementEffects(context));
    }

    public static ReplacementResult PreventRemoval(
        HeadlessEntityId targetEntityId,
        IReadOnlyList<ReplacementEffect> replacements,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new ReplacementRequest(ReplacementEventKind.RemoveFromField, targetEntityId, replacements, sourceEntityId));
    }

    public static ReplacementResult PreventDeletion(
        HeadlessEntityId targetEntityId,
        IReadOnlyList<ReplacementEffect> replacements,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new ReplacementRequest(ReplacementEventKind.Delete, targetEntityId, replacements, sourceEntityId));
    }

    public static ReplacementResult ImmuneFromDpReduction(
        HeadlessEntityId targetEntityId,
        IReadOnlyList<ReplacementEffect> replacements,
        HeadlessEntityId? sourceEntityId = null)
    {
        return Evaluate(new ReplacementRequest(ReplacementEventKind.DpReduction, targetEntityId, replacements, sourceEntityId, "ChangeDP"));
    }

    private static IEnumerable<ReplacementEffect> ReadReplacementsFromFlags(IReadOnlyDictionary<string, bool> flags)
    {
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            if (!pair.Value)
            {
                continue;
            }

            foreach (ReplacementEffect replacement in ReadSimpleReplacement(pair.Key, null))
            {
                yield return replacement;
            }
        }
    }

    private static IEnumerable<ReplacementEffect> ReadReplacementsFromValues(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId = null)
    {
        foreach (ReplacementEffect replacement in ReadSimpleReplacements(values, effectId))
        {
            yield return replacement;
        }

        if (!values.TryGetValue(ReplacementsKey, out object? rawReplacements) || rawReplacements is null)
        {
            yield break;
        }

        foreach (object? rawReplacement in FlattenObjects(rawReplacements))
        {
            if (TryReadReplacement(rawReplacement, effectId, out ReplacementEffect? replacement))
            {
                yield return replacement!;
            }
        }
    }

    private static IEnumerable<ReplacementEffect> ReadSimpleReplacements(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId)
    {
        foreach (string key in new[] { PreventRemovalKey, PreventDeletionKey, ImmuneFromDpMinusKey, ImmuneFromEffectsKey })
        {
            if (TryReadBool(values, key, out bool enabled) && enabled)
            {
                foreach (ReplacementEffect replacement in ReadSimpleReplacement(key, effectId))
                {
                    yield return replacement;
                }
            }
        }
    }

    private static IEnumerable<ReplacementEffect> ReadSimpleReplacement(string key, HeadlessEntityId? effectId)
    {
        yield return key switch
        {
            PreventRemovalKey => new ReplacementEffect(IdFor(effectId, key), ReplacementEventKind.RemoveFromField, ReplacementActionKind.Prevent),
            PreventDeletionKey => new ReplacementEffect(IdFor(effectId, key), ReplacementEventKind.Delete, ReplacementActionKind.Prevent),
            ImmuneFromDpMinusKey => new ReplacementEffect(IdFor(effectId, key), ReplacementEventKind.DpReduction, ReplacementActionKind.Immune, mutationKind: "ChangeDP"),
            ImmuneFromEffectsKey => new ReplacementEffect(IdFor(effectId, key), ReplacementEventKind.EffectMutation, ReplacementActionKind.Immune),
            _ => throw new ArgumentOutOfRangeException(nameof(key), "Unknown replacement metadata key."),
        };
    }

    private static bool TryReadReplacement(
        object? rawReplacement,
        HeadlessEntityId? effectId,
        out ReplacementEffect? replacement)
    {
        replacement = null;
        if (rawReplacement is ReplacementEffect typed)
        {
            replacement = typed;
            return true;
        }

        if (rawReplacement is not IReadOnlyDictionary<string, object?> values ||
            !TryReadEnum(values, EventKindKey, ReplacementEventKind.RemoveFromField, out ReplacementEventKind eventKind) ||
            !TryReadEnum(values, ActionKindKey, ReplacementActionKind.Prevent, out ReplacementActionKind actionKind))
        {
            return false;
        }

        HeadlessEntityId? targetEntityId = TryReadEntityId(values, TargetEntityIdKey, out HeadlessEntityId target)
            ? target
            : null;
        HeadlessEntityId? replacementEntityId = TryReadEntityId(values, ReplacementEntityIdKey, out HeadlessEntityId redirect)
            ? redirect
            : null;
        HeadlessEntityId? sourceEntityId = TryReadEntityId(values, SourceEntityIdKey, out HeadlessEntityId source)
            ? source
            : null;
        string? mutationKind = TryReadString(values, MutationKindKey, out string? parsedMutation)
            ? parsedMutation
            : null;
        string? reason = TryReadString(values, ReasonKey, out string? parsedReason)
            ? parsedReason
            : null;
        int priority = TryReadInt(values, PriorityKey, out int parsedPriority) ? parsedPriority : 0;
        string id = TryReadString(values, "id", out string? parsedId)
            ? parsedId!
            : IdFor(effectId, $"{eventKind}-{actionKind}");

        replacement = new ReplacementEffect(id, eventKind, actionKind, targetEntityId, replacementEntityId, sourceEntityId, mutationKind, reason, priority);
        return true;
    }

    private static bool CanApply(ReplacementEffect replacement, ReplacementRequest request)
    {
        if (replacement.TargetEntityId is HeadlessEntityId target && target != request.TargetEntityId)
        {
            return false;
        }

        if (replacement.SourceEntityId is HeadlessEntityId source &&
            (request.SourceEntityId is not HeadlessEntityId requestSource || requestSource != source))
        {
            return false;
        }

        return replacement.MutationKind is null ||
            request.MutationKind is not null &&
            string.Equals(replacement.MutationKind, request.MutationKind, StringComparison.Ordinal);
    }

    private static int ActionOrder(ReplacementActionKind actionKind)
    {
        return actionKind switch
        {
            ReplacementActionKind.Immune => 0,
            ReplacementActionKind.Prevent => 1,
            ReplacementActionKind.Redirect => 2,
            _ => 9,
        };
    }

    private static IEnumerable<object?> FlattenObjects(object raw)
    {
        if (raw is string)
        {
            yield return raw;
            yield break;
        }

        if (raw is System.Collections.IEnumerable values)
        {
            foreach (object? value in values)
            {
                yield return value;
            }

            yield break;
        }

        yield return raw;
    }

    private static string IdFor(HeadlessEntityId? effectId, string fallback)
    {
        return effectId is HeadlessEntityId id ? $"{id.Value}:{fallback}" : fallback;
    }

    private static bool TryReadBool(IReadOnlyDictionary<string, object?> values, string key, out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            bool boolValue => Set(boolValue, out value),
            string text when bool.TryParse(text, out bool parsed) => Set(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> values, string key, out int value)
    {
        value = 0;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            int intValue => Set(intValue, out value),
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => Set((int)longValue, out value),
            string text when int.TryParse(text, out int parsed) => Set(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadString(IReadOnlyDictionary<string, object?> values, string key, out string? value)
    {
        value = null;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        string? parsed = raw switch
        {
            string stringValue => stringValue,
            HeadlessEntityId entityId => entityId.Value,
            _ => raw.ToString(),
        };
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        value = parsed.Trim();
        return true;
    }

    private static bool TryReadEntityId(IReadOnlyDictionary<string, object?> values, string key, out HeadlessEntityId value)
    {
        value = default;
        if (!TryReadString(values, key, out string? text))
        {
            return false;
        }

        value = new HeadlessEntityId(text!);
        return !value.IsEmpty;
    }

    private static bool TryReadEnum<TEnum>(
        IReadOnlyDictionary<string, object?> values,
        string key,
        TEnum fallback,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = fallback;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        if (raw is TEnum typed)
        {
            value = typed;
            return true;
        }

        if (raw is string text && Enum.TryParse(text, ignoreCase: true, out TEnum parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool Set<T>(T input, out T output)
    {
        output = input;
        return true;
    }
}
