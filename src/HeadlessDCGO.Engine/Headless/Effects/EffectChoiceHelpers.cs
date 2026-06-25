namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record EffectChoiceResolution
{
    private EffectChoiceResolution(
        bool isSuccess,
        ChoiceRequest request,
        ChoiceResult? result,
        EffectContext context,
        string? errorCode,
        string? message,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        IsSuccess = isSuccess;
        Request = request;
        Result = result;
        Context = context;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public ChoiceRequest Request { get; }

    public ChoiceResult? Result { get; }

    public EffectContext Context { get; }

    public string? ErrorCode { get; }

    public string? Message { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static EffectChoiceResolution Success(
        ChoiceRequest request,
        ChoiceResult result,
        EffectContext context,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new EffectChoiceResolution(true, request, result, context, null, null, values);
    }

    public static EffectChoiceResolution Failure(
        ChoiceRequest request,
        ChoiceResult? result,
        EffectContext context,
        string errorCode,
        string message,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new EffectChoiceResolution(false, request, result, context, errorCode, message, values);
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

public static class EffectChoiceHelpers
{
    public const string DefaultKeyPrefix = "choice";
    public const string TypeKey = "type";
    public const string PlayerIdKey = "playerId";
    public const string MessageKey = "message";
    public const string MinCountKey = "minCount";
    public const string MaxCountKey = "maxCount";
    public const string CanSkipKey = "canSkip";
    public const string SourceZoneKey = "sourceZone";
    public const string CandidateIdsKey = "candidateIds";
    public const string SelectableCandidateIdsKey = "selectableCandidateIds";
    public const string IsSkippedKey = "isSkipped";
    public const string SelectedIdsKey = "selectedIds";
    public const string SelectedCountKey = "selectedCount";
    public const string ValidationFailuresKey = "validationFailures";

    public static ChoiceCandidate Candidate(
        HeadlessEntityId id,
        string label,
        ChoiceZone zone,
        bool isSelectable = true,
        HeadlessPlayerId? ownerId = null)
    {
        return new ChoiceCandidate(id, label, zone, isSelectable, ownerId);
    }

    public static IReadOnlyList<ChoiceCandidate> CandidatesFromIds(
        IEnumerable<HeadlessEntityId> ids,
        ChoiceZone zone,
        HeadlessPlayerId? ownerId = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return Array.AsReadOnly(ids
            .Select(id => Candidate(id, id.Value, zone, isSelectable: true, ownerId))
            .ToArray());
    }

    public static ChoiceRequest CreateRequest(
        ChoiceType type,
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return new ChoiceRequest(
            type,
            playerId,
            message,
            minCount,
            maxCount,
            canSkip,
            sourceZone,
            candidates);
    }

    public static ChoiceRequest CreateCardRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return CreateRequest(ChoiceType.Card, playerId, message, minCount, maxCount, canSkip, sourceZone, candidates);
    }

    public static ChoiceRequest CreatePermanentRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return CreateRequest(ChoiceType.Permanent, playerId, message, minCount, maxCount, canSkip, ChoiceZone.BattleArea, candidates);
    }

    public static ChoiceRequest CreateCountRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IEnumerable<int>? candidateCounts = null)
    {
        if (minCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minCount), "Minimum count must not be negative.");
        }

        if (maxCount < minCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Maximum count must be greater than or equal to minimum count.");
        }

        int[] counts = (candidateCounts ?? Enumerable.Range(minCount, maxCount - minCount + 1))
            .Distinct()
            .OrderBy(count => count)
            .ToArray();
        if (counts.Length == 0)
        {
            throw new ArgumentException("Count choice requires at least one count candidate.", nameof(candidateCounts));
        }

        if (counts.Any(count => count < minCount || count > maxCount))
        {
            throw new ArgumentException("Count choice candidates must stay inside the request count range.", nameof(candidateCounts));
        }

        ChoiceCandidate[] candidates = counts
            .Select(count => Candidate(new HeadlessEntityId($"count:{count}"), count.ToString(), ChoiceZone.Custom))
            .ToArray();
        return CreateRequest(ChoiceType.Count, playerId, message, minCount, maxCount, canSkip, ChoiceZone.Custom, candidates);
    }

    public static EffectChoiceResolution ApplyResult(
        EffectContext context,
        ChoiceRequest request,
        ChoiceResult result,
        string keyPrefix = DefaultKeyPrefix)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        ChoiceResultValidation validation = result.Validate(request);
        IReadOnlyDictionary<string, object?> values = ResultValues(request, result, keyPrefix, validation.Failures);
        if (!validation.IsValid)
        {
            return EffectChoiceResolution.Failure(
                request,
                result,
                context,
                "invalid_effect_choice_result",
                validation.ToString(),
                values);
        }

        EffectContext nextContext = WithValues(context, values);
        return EffectChoiceResolution.Success(request, result, nextContext, values);
    }

    public static async Task<EffectChoiceResolution> ResolveAsync(
        EffectContext context,
        ChoiceRequest request,
        IChoiceProvider provider,
        string keyPrefix = DefaultKeyPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        try
        {
            ChoiceResult result = await provider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            return ApplyResult(context, request, result, keyPrefix);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EffectChoiceResolution.Failure(
                request,
                result: null,
                context,
                "effect_choice_provider_failed",
                ex.Message,
                RequestValues(request, keyPrefix));
        }
    }

    public static EffectContext WithValues(
        EffectContext context,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(values);

        var next = new Dictionary<string, object?>(context.Values, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            next[pair.Key.Trim()] = pair.Value;
        }

        return new EffectContext(
            context.SourcePlayerId,
            context.OwnerPlayerId,
            context.SourceEntityId,
            context.TriggerEntityId,
            context.TargetEntityIds,
            next);
    }

    public static IReadOnlyDictionary<string, object?> RequestValues(
        ChoiceRequest request,
        string keyPrefix = DefaultKeyPrefix)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        return new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [Key(keyPrefix, TypeKey)] = request.Type.ToString(),
                [Key(keyPrefix, PlayerIdKey)] = request.PlayerId.Value,
                [Key(keyPrefix, MessageKey)] = request.Message,
                [Key(keyPrefix, MinCountKey)] = request.MinCount,
                [Key(keyPrefix, MaxCountKey)] = request.MaxCount,
                [Key(keyPrefix, CanSkipKey)] = request.CanSkip,
                [Key(keyPrefix, SourceZoneKey)] = request.SourceZone.ToString(),
                [Key(keyPrefix, CandidateIdsKey)] = request.Candidates.Select(candidate => candidate.Id.Value).ToArray(),
                [Key(keyPrefix, SelectableCandidateIdsKey)] = request.SelectableCandidates.Select(candidate => candidate.Id.Value).ToArray(),
            });
    }

    private static IReadOnlyDictionary<string, object?> ResultValues(
        ChoiceRequest request,
        ChoiceResult result,
        string keyPrefix,
        IReadOnlyList<string> validationFailures)
    {
        var values = new Dictionary<string, object?>(RequestValues(request, keyPrefix), StringComparer.Ordinal)
        {
            [Key(keyPrefix, IsSkippedKey)] = result.IsSkipped,
            [Key(keyPrefix, SelectedIdsKey)] = result.SelectedIds.Select(id => id.Value).ToArray(),
            [Key(keyPrefix, SelectedCountKey)] = result.SelectedCount,
            [Key(keyPrefix, ValidationFailuresKey)] = validationFailures.ToArray(),
        };

        return new ReadOnlyDictionary<string, object?>(values);
    }

    private static string Key(string keyPrefix, string suffix)
    {
        return $"{keyPrefix.Trim()}.{suffix}";
    }
}
