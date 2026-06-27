namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum TriggerConditionKind
{
    OnPlay = 0,
    OnDigivolve = 1,
    WhenAttacking = 2,
}

public sealed record TriggerConditionRequest
{
    public TriggerConditionRequest(
        MatchState matchState,
        EffectContext context,
        TriggerConditionKind condition)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(context);

        if (!Enum.IsDefined(condition))
        {
            throw new ArgumentOutOfRangeException(nameof(condition), "Trigger condition kind must be known.");
        }

        MatchState = matchState;
        Context = context;
        Condition = condition;
    }

    public MatchState MatchState { get; }

    public EffectContext Context { get; }

    public TriggerConditionKind Condition { get; }
}

public sealed record TriggerConditionResult
{
    private TriggerConditionResult(
        bool isMatch,
        TriggerConditionKind condition,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        IsMatch = isMatch;
        Condition = condition;
        Reason = reason;
        Values = CopyValues(values);
    }

    public bool IsMatch { get; }

    public TriggerConditionKind Condition { get; }

    public string Reason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static TriggerConditionResult Match(
        TriggerConditionKind condition,
        string reason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new TriggerConditionResult(true, condition, reason, values ?? ReadOnlyDictionary<string, object?>.Empty);
    }

    public static TriggerConditionResult NoMatch(
        TriggerConditionKind condition,
        string reason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new TriggerConditionResult(false, condition, reason, values ?? ReadOnlyDictionary<string, object?>.Empty);
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

public static class TriggerConditionHelpers
{
    public const string IsEvolutionKey = "isEvolution";
    public const string TriggerConditionKey = "triggerCondition";
    public const string AttackEventTypeKey = "attackEventType";
    public const string AttackerIdKey = HeadlessActionParameterKeys.AttackerId;
    public const string AttackTargetIdKey = HeadlessActionParameterKeys.AttackTargetId;

    public static TriggerConditionResult Evaluate(TriggerConditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Condition switch
        {
            TriggerConditionKind.OnPlay => IsOnPlay(request.MatchState, request.Context),
            TriggerConditionKind.OnDigivolve => IsOnDigivolve(request.MatchState, request.Context),
            TriggerConditionKind.WhenAttacking => IsWhenAttacking(request.MatchState, request.Context),
            _ => TriggerConditionResult.NoMatch(request.Condition, "Unsupported trigger condition kind.")
        };
    }

    public static TriggerConditionResult IsOnPlay(
        MatchState matchState,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryFindSourceInBattleArea(matchState, context, out CardInstanceState? source, out string reason))
        {
            return TriggerConditionResult.NoMatch(TriggerConditionKind.OnPlay, reason, BaseValues(context));
        }

        if (ReadBool(context.Values, IsEvolutionKey) || source.SourceIds.Count > 0)
        {
            return TriggerConditionResult.NoMatch(
                TriggerConditionKind.OnPlay,
                "OnPlay does not match an evolution context.",
                BaseValues(context, source));
        }

        return TriggerConditionResult.Match(
            TriggerConditionKind.OnPlay,
            "Source card is in battle area and was not evolved.",
            BaseValues(context, source));
    }

    public static TriggerConditionResult IsOnDigivolve(
        MatchState matchState,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryFindSourceInBattleArea(matchState, context, out CardInstanceState? source, out string reason))
        {
            return TriggerConditionResult.NoMatch(TriggerConditionKind.OnDigivolve, reason, BaseValues(context));
        }

        bool isEvolutionContext = ReadBool(context.Values, IsEvolutionKey) || source.SourceIds.Count > 0;
        if (!isEvolutionContext)
        {
            return TriggerConditionResult.NoMatch(
                TriggerConditionKind.OnDigivolve,
                "OnDigivolve requires an evolution context or attached source ids.",
                BaseValues(context, source));
        }

        return TriggerConditionResult.Match(
            TriggerConditionKind.OnDigivolve,
            "Source card is in battle area and evolution context is present.",
            BaseValues(context, source));
    }

    public static TriggerConditionResult IsWhenAttacking(
        MatchState matchState,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryFindSourceInBattleArea(matchState, context, out CardInstanceState? source, out string reason))
        {
            return TriggerConditionResult.NoMatch(TriggerConditionKind.WhenAttacking, reason, BaseValues(context));
        }

        GameEvent? attackEvent = FindLatestAttackDeclared(matchState);
        if (attackEvent is null)
        {
            return TriggerConditionResult.NoMatch(
                TriggerConditionKind.WhenAttacking,
                "WhenAttacking requires an AttackDeclared event.",
                BaseValues(context, source));
        }

        if (!TryReadEntityId(attackEvent.Metadata, AttackerIdKey, out HeadlessEntityId attackerId))
        {
            return TriggerConditionResult.NoMatch(
                TriggerConditionKind.WhenAttacking,
                "AttackDeclared event did not include an attacker id.",
                BaseValues(context, source, attackEvent));
        }

        if (attackerId != context.SourceEntityId)
        {
            return TriggerConditionResult.NoMatch(
                TriggerConditionKind.WhenAttacking,
                "Effect source did not match the attacking card.",
                BaseValues(context, source, attackEvent, attackerId));
        }

        return TriggerConditionResult.Match(
            TriggerConditionKind.WhenAttacking,
            "Effect source matched the latest attacking card.",
            BaseValues(context, source, attackEvent, attackerId));
    }

    private static bool TryFindSourceInBattleArea(
        MatchState matchState,
        EffectContext context,
        [NotNullWhen(true)] out CardInstanceState? source,
        out string reason)
    {
        source = null;
        if (!matchState.CardInstances.TryGetValue(context.SourceEntityId, out CardInstanceState? foundSource))
        {
            reason = $"Source card '{context.SourceEntityId}' was not found.";
            return false;
        }

        if (foundSource.OwnerId != context.OwnerPlayerId)
        {
            reason = $"Source owner '{foundSource.OwnerId}' did not match context owner '{context.OwnerPlayerId}'.";
            return false;
        }

        PlayerState owner = matchState.GetPlayer(context.OwnerPlayerId);
        if (!owner.GetZone(ChoiceZone.BattleArea).Contains(context.SourceEntityId))
        {
            reason = $"Source card '{context.SourceEntityId}' was not in the battle area.";
            return false;
        }

        source = foundSource;
        reason = string.Empty;
        return true;
    }

    private static GameEvent? FindLatestAttackDeclared(MatchState matchState)
    {
        return matchState.Events
            .Where(gameEvent => gameEvent.Type == GameEventType.AttackDeclared)
            .OrderByDescending(gameEvent => gameEvent.Sequence)
            .FirstOrDefault();
    }

    private static IReadOnlyDictionary<string, object?> BaseValues(
        EffectContext context,
        CardInstanceState? source = null,
        GameEvent? gameEvent = null,
        HeadlessEntityId? attackerId = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [EffectContextAdapterKeys.SourcePlayerId] = context.SourcePlayerId.Value,
            [EffectContextAdapterKeys.OwnerPlayerId] = context.OwnerPlayerId.Value,
            [EffectContextAdapterKeys.SourceEntityId] = context.SourceEntityId.Value,
        };

        if (context.TriggerEntityId.HasValue)
        {
            values[EffectContextAdapterKeys.TriggerEntityId] = context.TriggerEntityId.Value.Value;
        }

        if (context.TargetEntityIds.Count > 0)
        {
            values[EffectContextAdapterKeys.TargetEntityIds] = context.TargetEntityIds
                .Select(id => id.Value)
                .ToArray();
        }

        if (source is not null)
        {
            values["sourceDefinitionId"] = source.DefinitionId.Value;
            values["sourceOwnerId"] = source.OwnerId.Value;
            values["sourceHasEvolutionSources"] = source.SourceIds.Count > 0;
        }

        if (gameEvent is not null)
        {
            values["eventSequence"] = gameEvent.Sequence;
            values[AttackEventTypeKey] = gameEvent.Type.ToString();
        }

        if (attackerId.HasValue)
        {
            values[AttackerIdKey] = attackerId.Value.Value;
        }

        return values;
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

    private static bool TryReadEntityId(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out HeadlessEntityId entityId)
    {
        entityId = default;
        if (!values.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue is HeadlessEntityId typed
            ? !typed.IsEmpty && Assign(typed, out entityId)
            : HeadlessEntityId.TryParse(rawValue.ToString(), out entityId);
    }

    private static bool Assign<T>(T input, out T output)
    {
        output = input;
        return true;
    }
}
