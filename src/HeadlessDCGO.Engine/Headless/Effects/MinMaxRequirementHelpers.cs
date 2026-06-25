namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum MinMaxRequirementMetric
{
    DP = 0,
    PlayCost = 1,
    Level = 2,
}

public enum MinMaxRequirementMode
{
    Min = 0,
    Max = 1,
}

public sealed record MinMaxRequirementRequest
{
    public MinMaxRequirementRequest(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        MinMaxRequirementMetric metric,
        MinMaxRequirementMode mode,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        bool includeTamersForCost = true)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(cardDefinitions);

        if (ownerId.IsEmpty)
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(ownerId));
        }

        if (sourceInstanceId.IsEmpty)
        {
            throw new ArgumentException("Source instance id must not be empty.", nameof(sourceInstanceId));
        }

        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), "Min/max metric must be known.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Min/max mode must be known.");
        }

        MatchState = matchState;
        OwnerId = ownerId;
        SourceInstanceId = sourceInstanceId;
        Metric = metric;
        Mode = mode;
        CardDefinitions = CopyDefinitions(cardDefinitions);
        IncludeTamersForCost = includeTamersForCost;
    }

    public MatchState MatchState { get; }

    public HeadlessPlayerId OwnerId { get; }

    public HeadlessEntityId SourceInstanceId { get; }

    public MinMaxRequirementMetric Metric { get; }

    public MinMaxRequirementMode Mode { get; }

    public IReadOnlyDictionary<HeadlessEntityId, CardRecord> CardDefinitions { get; }

    public bool IncludeTamersForCost { get; }

    private static IReadOnlyDictionary<HeadlessEntityId, CardRecord> CopyDefinitions(
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions)
    {
        var copy = new Dictionary<HeadlessEntityId, CardRecord>();
        foreach (KeyValuePair<HeadlessEntityId, CardRecord> pair in definitions)
        {
            if (pair.Key.IsEmpty)
            {
                throw new ArgumentException("Definition id must not be empty.", nameof(definitions));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            if (pair.Key != pair.Value.Id)
            {
                throw new ArgumentException("Definition dictionary key must match the card record id.", nameof(definitions));
            }

            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<HeadlessEntityId, CardRecord>(copy);
    }
}

public sealed record MinMaxRequirementResult
{
    private MinMaxRequirementResult(
        bool isMatch,
        MinMaxRequirementMetric metric,
        MinMaxRequirementMode mode,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        IsMatch = isMatch;
        Metric = metric;
        Mode = mode;
        Reason = reason;
        Values = CopyValues(values);
    }

    public bool IsMatch { get; }

    public MinMaxRequirementMetric Metric { get; }

    public MinMaxRequirementMode Mode { get; }

    public string Reason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static MinMaxRequirementResult Match(
        MinMaxRequirementMetric metric,
        MinMaxRequirementMode mode,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new MinMaxRequirementResult(true, metric, mode, reason, values);
    }

    public static MinMaxRequirementResult NoMatch(
        MinMaxRequirementMetric metric,
        MinMaxRequirementMode mode,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new MinMaxRequirementResult(false, metric, mode, reason, values);
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

public static class MinMaxRequirementHelpers
{
    public const string DpKey = "dp";
    public const string CurrentDpKey = "currentDp";
    public const string BaseDpKey = "baseDp";
    public const string PlayCostKey = "playCost";
    public const string CostKey = "cost";
    public const string LevelKey = "level";

    public static MinMaxRequirementResult IsMinDP(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.DP,
            MinMaxRequirementMode.Min,
            cardDefinitions));
    }

    public static MinMaxRequirementResult IsMaxDP(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.DP,
            MinMaxRequirementMode.Max,
            cardDefinitions));
    }

    public static MinMaxRequirementResult IsMinCost(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        bool includeTamersForCost = true)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.PlayCost,
            MinMaxRequirementMode.Min,
            cardDefinitions,
            includeTamersForCost));
    }

    public static MinMaxRequirementResult IsMaxCost(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        bool includeTamersForCost = true)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.PlayCost,
            MinMaxRequirementMode.Max,
            cardDefinitions,
            includeTamersForCost));
    }

    public static MinMaxRequirementResult IsMinLevel(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.Level,
            MinMaxRequirementMode.Min,
            cardDefinitions));
    }

    public static MinMaxRequirementResult IsMaxLevel(
        MatchState matchState,
        HeadlessPlayerId ownerId,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions)
    {
        return Evaluate(new MinMaxRequirementRequest(
            matchState,
            ownerId,
            sourceInstanceId,
            MinMaxRequirementMetric.Level,
            MinMaxRequirementMode.Max,
            cardDefinitions));
    }

    public static MinMaxRequirementResult Evaluate(MinMaxRequirementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryFindSource(request, out CardInstanceState? source, out CardRecord? sourceDefinition, out string reason))
        {
            return NoMatch(request, reason);
        }

        if (!TryReadMetricValue(request.Metric, source, sourceDefinition, out int sourceValue))
        {
            return NoMatch(request, $"Source card '{request.SourceInstanceId}' does not expose {request.Metric}.");
        }

        MinMaxCandidate[] candidates = GetCandidates(request).ToArray();
        if (candidates.Length == 0)
        {
            return NoMatch(request, $"No owner battle area candidates expose {request.Metric}.");
        }

        int boundary = request.Mode == MinMaxRequirementMode.Min
            ? candidates.Min(candidate => candidate.Value)
            : candidates.Max(candidate => candidate.Value);
        bool matched = sourceValue == boundary;

        var values = BaseValues(request, source, sourceDefinition, sourceValue, boundary, candidates);
        return matched
            ? MinMaxRequirementResult.Match(
                request.Metric,
                request.Mode,
                $"Source value matched owner battle area {request.Mode.ToString().ToLowerInvariant()} {request.Metric}.",
                values)
            : MinMaxRequirementResult.NoMatch(
                request.Metric,
                request.Mode,
                $"Source value did not match owner battle area {request.Mode.ToString().ToLowerInvariant()} {request.Metric}.",
                values);
    }

    private static bool TryFindSource(
        MinMaxRequirementRequest request,
        [NotNullWhen(true)]
        out CardInstanceState? source,
        [NotNullWhen(true)]
        out CardRecord? sourceDefinition,
        out string reason)
    {
        source = null;
        sourceDefinition = null;
        if (!request.MatchState.CardInstances.TryGetValue(request.SourceInstanceId, out CardInstanceState? foundSource))
        {
            reason = $"Source card '{request.SourceInstanceId}' was not found.";
            return false;
        }

        if (foundSource.OwnerId != request.OwnerId)
        {
            reason = $"Source owner '{foundSource.OwnerId}' did not match owner '{request.OwnerId}'.";
            return false;
        }

        PlayerState owner = request.MatchState.GetPlayer(request.OwnerId);
        if (!owner.GetZone(ChoiceZone.BattleArea).Contains(request.SourceInstanceId))
        {
            reason = $"Source card '{request.SourceInstanceId}' was not in the battle area.";
            return false;
        }

        if (!request.CardDefinitions.TryGetValue(foundSource.DefinitionId, out CardRecord? foundDefinition))
        {
            reason = $"Source definition '{foundSource.DefinitionId}' was not found.";
            return false;
        }

        if (!MatchesMetricCardType(request, foundDefinition))
        {
            reason = $"Source card '{request.SourceInstanceId}' is not valid for {request.Metric}.";
            return false;
        }

        source = foundSource;
        sourceDefinition = foundDefinition;
        reason = string.Empty;
        return true;
    }

    private static IEnumerable<MinMaxCandidate> GetCandidates(MinMaxRequirementRequest request)
    {
        PlayerState owner = request.MatchState.GetPlayer(request.OwnerId);
        foreach (HeadlessEntityId instanceId in owner.GetZone(ChoiceZone.BattleArea)
            .OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            if (!request.MatchState.CardInstances.TryGetValue(instanceId, out CardInstanceState? instance) ||
                instance.OwnerId != request.OwnerId ||
                !request.CardDefinitions.TryGetValue(instance.DefinitionId, out CardRecord? definition) ||
                !MatchesMetricCardType(request, definition) ||
                !TryReadMetricValue(request.Metric, instance, definition, out int value))
            {
                continue;
            }

            yield return new MinMaxCandidate(instanceId, instance.DefinitionId, value);
        }
    }

    private static bool MatchesMetricCardType(MinMaxRequirementRequest request, CardRecord definition)
    {
        if (request.Metric is MinMaxRequirementMetric.DP or MinMaxRequirementMetric.Level)
        {
            return IsCardType(definition, "Digimon");
        }

        return IsCardType(definition, "Digimon") ||
            (request.IncludeTamersForCost && IsCardType(definition, "Tamer"));
    }

    private static bool TryReadMetricValue(
        MinMaxRequirementMetric metric,
        CardInstanceState instance,
        CardRecord definition,
        out int value)
    {
        value = 0;
        return metric switch
        {
            MinMaxRequirementMetric.DP => TryReadInt(instance.Modifiers, out value, DpKey, "DP", CurrentDpKey, BaseDpKey, "baseDP") ||
                TryReadInt(definition.Metadata, out value, DpKey, "DP", CurrentDpKey, BaseDpKey, "baseDP"),
            MinMaxRequirementMetric.PlayCost => TryReadInt(instance.Modifiers, out value, PlayCostKey, CostKey, "Cost") ||
                TryAssignNonNegative(definition.PlayCost, out value) ||
                TryReadInt(definition.Metadata, out value, PlayCostKey, CostKey, "Cost"),
            MinMaxRequirementMetric.Level => TryReadInt(instance.Modifiers, out value, LevelKey, "Level") ||
                TryReadInt(definition.Metadata, out value, LevelKey, "Level"),
            _ => false
        };
    }

    private static IReadOnlyDictionary<string, object?> BaseValues(
        MinMaxRequirementRequest request,
        CardInstanceState? source = null,
        CardRecord? sourceDefinition = null,
        int? sourceValue = null,
        int? boundaryValue = null,
        IReadOnlyList<MinMaxCandidate>? candidates = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ownerId"] = request.OwnerId.Value,
            ["sourceInstanceId"] = request.SourceInstanceId.Value,
            ["metric"] = request.Metric.ToString(),
            ["mode"] = request.Mode.ToString(),
            ["includeTamersForCost"] = request.IncludeTamersForCost,
        };

        if (source is not null)
        {
            values["sourceDefinitionId"] = source.DefinitionId.Value;
        }

        if (sourceDefinition is not null)
        {
            values["sourceCardType"] = sourceDefinition.CardType;
        }

        if (sourceValue.HasValue)
        {
            values["sourceValue"] = sourceValue.Value;
        }

        if (boundaryValue.HasValue)
        {
            values["boundaryValue"] = boundaryValue.Value;
        }

        if (candidates is not null)
        {
            values["candidateIds"] = candidates.Select(candidate => candidate.InstanceId.Value).ToArray();
            values["candidateValues"] = candidates.Select(candidate => candidate.Value).ToArray();
        }

        return values;
    }

    private static MinMaxRequirementResult NoMatch(MinMaxRequirementRequest request, string reason)
    {
        return MinMaxRequirementResult.NoMatch(request.Metric, request.Mode, reason, BaseValues(request));
    }

    private static bool IsCardType(CardRecord definition, string cardType)
    {
        return string.Equals(definition.CardType, cardType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> values,
        out int value,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            if (values.TryGetValue(key, out object? raw) && TryParseNonNegativeInt(raw, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryAssignNonNegative(int? raw, out int value)
    {
        value = 0;
        if (!raw.HasValue || raw.Value < 0)
        {
            return false;
        }

        value = raw.Value;
        return true;
    }

    private static bool TryParseNonNegativeInt(object? raw, out int value)
    {
        value = 0;
        switch (raw)
        {
            case int intValue when intValue >= 0:
                value = intValue;
                return true;
            case long longValue when longValue >= 0 && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            case double doubleValue when doubleValue >= 0 && doubleValue <= int.MaxValue && doubleValue % 1 == 0:
                value = (int)doubleValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0:
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private sealed record MinMaxCandidate(
        HeadlessEntityId InstanceId,
        HeadlessEntityId DefinitionId,
        int Value);
}
