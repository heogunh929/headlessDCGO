namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class AutoProcessingTriggerCollector
{
    public const string TriggerTimingKey = "triggerTiming";
    public const string TimingKey = "timing";
    public const string EffectTimingKey = "effectTiming";
    public const string ResolutionModeKey = "resolutionMode";
    public const string TriggerKindKey = "triggerKind";
    public const string PriorityKey = "priority";
    public const string PlayerIdKey = "playerId";
    public const string SourceEntityIdKey = "sourceEntityId";
    public const string TargetEntityIdKey = "targetEntityId";
    public const string CardIdKey = "cardId";

    private readonly IEffectQueryService _effectQueryService;

    public AutoProcessingTriggerCollector(IEffectQueryService effectQueryService)
    {
        _effectQueryService = effectQueryService ?? throw new ArgumentNullException(nameof(effectQueryService));
    }

    public TriggerCollectionResult Collect(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        if (gameEvent.Type == GameEventType.Unknown)
        {
            return TriggerCollectionResult.Failure(
                gameEvent,
                string.Empty,
                "Unknown game events cannot collect trigger candidates.");
        }

        return CollectForTiming(gameEvent, ResolveTiming(gameEvent));
    }

    private TriggerCollectionResult CollectForTiming(GameEvent gameEvent, string timing)
    {
        IReadOnlyList<EffectRequest> effects = _effectQueryService.GetEffectsForTiming(timing);
        var triggers = new List<TimingWindowTrigger>(effects.Count);
        EffectResolutionMode mode = ResolveMode(gameEvent);
        TimingWindowTriggerKind kind = ResolveKind(gameEvent);
        int priority = ResolvePriority(gameEvent);

        foreach (EffectRequest effect in effects)
        {
            if (!MatchesEvent(effect, gameEvent))
            {
                continue;
            }

            triggers.Add(new TimingWindowTrigger(
                effect,
                mode,
                kind,
                priority,
                sequence: triggers.Count));
        }

        return TriggerCollectionResult.Success(gameEvent, timing, triggers);
    }

    public TriggerCollectionResult CollectAndEnqueue(
        GameEvent gameEvent,
        EffectScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        TriggerCollectionResult result = Collect(gameEvent);
        if (!result.IsSuccess)
        {
            return result;
        }

        foreach (TimingWindowTrigger trigger in result.Triggers)
        {
            scheduler.Enqueue(trigger.Request, trigger.Mode);
        }

        return result with { EnqueuedCount = result.Triggers.Count };
    }

    /// <summary>
    /// W1: collects and enqueues triggers across every canonical timing a single structured event
    /// opens (<see cref="TriggerTimingMap"/>), de-duplicating effects that match more than one
    /// timing. This is what the live loop uses so a CardMoved/Attack event fires the right card
    /// effects (OnPlay/OnDeletion/OnEnterField/...), not just an effect bound to the raw event name.
    /// </summary>
    public TriggerCollectionResult CollectAndEnqueueAll(
        GameEvent gameEvent,
        EffectScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (gameEvent.Type == GameEventType.Unknown)
        {
            return TriggerCollectionResult.Failure(
                gameEvent,
                string.Empty,
                "Unknown game events cannot collect trigger candidates.");
        }

        IReadOnlyList<TimingWindowTrigger> triggers = CollectAllTriggers(gameEvent);
        foreach (TimingWindowTrigger trigger in triggers)
        {
            scheduler.Enqueue(trigger.Request, trigger.Mode);
        }

        IReadOnlyList<string> timings = TriggerTimingMap.Derive(gameEvent);
        string primaryTiming = timings.Count > 0 ? timings[0] : gameEvent.Type.ToString();
        return TriggerCollectionResult.Success(gameEvent, primaryTiming, triggers)
            with { EnqueuedCount = triggers.Count };
    }

    /// <summary>
    /// (D-3) Collects the triggered effects a single event opens across every derived timing,
    /// de-duplicated by effect id, WITHOUT enqueuing them. The common loop uses this so it can order
    /// the whole batch (turn-player priority, mandatory-before-optional via
    /// <see cref="MandatoryEffectOrdering"/>) before enqueuing — the original resolves turn-player
    /// triggers first.
    /// </summary>
    public IReadOnlyList<TimingWindowTrigger> CollectAllTriggers(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        if (gameEvent.Type == GameEventType.Unknown)
        {
            return Array.Empty<TimingWindowTrigger>();
        }

        IReadOnlyList<string> timings = TriggerTimingMap.Derive(gameEvent);
        var seen = new HashSet<HeadlessEntityId>();
        var triggers = new List<TimingWindowTrigger>();

        foreach (string timing in timings)
        {
            TriggerCollectionResult perTiming = CollectForTiming(gameEvent, timing);
            foreach (TimingWindowTrigger trigger in perTiming.Triggers)
            {
                if (seen.Add(trigger.Request.EffectId))
                {
                    triggers.Add(trigger);
                }
            }
        }

        return triggers;
    }

    private static string ResolveTiming(GameEvent gameEvent)
    {
        if (TryReadString(gameEvent.Metadata, TriggerTimingKey, out string? triggerTiming))
        {
            return triggerTiming!;
        }

        if (TryReadString(gameEvent.Metadata, TimingKey, out string? timing))
        {
            return timing!;
        }

        if (TryReadString(gameEvent.Metadata, EffectTimingKey, out string? effectTiming))
        {
            return effectTiming!;
        }

        return gameEvent.Type.ToString();
    }

    private static EffectResolutionMode ResolveMode(GameEvent gameEvent)
    {
        return TryReadString(gameEvent.Metadata, ResolutionModeKey, out string? mode)
            && Enum.TryParse(mode, ignoreCase: false, out EffectResolutionMode parsedMode)
            && Enum.IsDefined(parsedMode)
                ? parsedMode
                : EffectResolutionMode.MainStack;
    }

    private static TimingWindowTriggerKind ResolveKind(GameEvent gameEvent)
    {
        return TryReadString(gameEvent.Metadata, TriggerKindKey, out string? kind)
            && Enum.TryParse(kind, ignoreCase: false, out TimingWindowTriggerKind parsedKind)
            && Enum.IsDefined(parsedKind)
                ? parsedKind
                : TimingWindowTriggerKind.Mandatory;
    }

    private static int ResolvePriority(GameEvent gameEvent)
    {
        if (!gameEvent.Metadata.TryGetValue(PriorityKey, out object? value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            string stringValue when int.TryParse(stringValue, out int parsedValue) => parsedValue,
            _ => 0
        };
    }

    private static bool MatchesEvent(
        EffectRequest effect,
        GameEvent gameEvent)
    {
        if (TryReadEntityId(gameEvent.Metadata, SourceEntityIdKey, out HeadlessEntityId sourceEntityId)
            && effect.Context.SourceEntityId != sourceEntityId)
        {
            return false;
        }

        if (TryReadPlayerId(gameEvent.Metadata, PlayerIdKey, out HeadlessPlayerId playerId)
            && effect.ControllerId != playerId
            && effect.Context.OwnerPlayerId != playerId
            && effect.Context.SourcePlayerId != playerId)
        {
            return false;
        }

        if (TryReadEntityId(gameEvent.Metadata, TargetEntityIdKey, out HeadlessEntityId targetEntityId)
            && !MatchesTargetEntity(effect.Context, targetEntityId))
        {
            return false;
        }

        if (TryReadEntityId(gameEvent.Metadata, CardIdKey, out HeadlessEntityId cardId)
            && !MatchesEntity(effect.Context, cardId))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesEntity(
        EffectContext context,
        HeadlessEntityId entityId)
    {
        return context.SourceEntityId == entityId
            || context.TriggerEntityId == entityId
            || context.TargetEntityIds.Contains(entityId);
    }

    private static bool MatchesTargetEntity(
        EffectContext context,
        HeadlessEntityId entityId)
    {
        return context.TriggerEntityId == entityId
            || context.TargetEntityIds.Contains(entityId);
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out string? value)
    {
        value = null;
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        value = rawValue switch
        {
            string stringValue => stringValue.Trim(),
            GameEventType eventType => eventType.ToString(),
            EffectResolutionMode mode => mode.ToString(),
            TimingWindowTriggerKind kind => kind.ToString(),
            _ => rawValue.ToString()?.Trim()
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            return false;
        }

        return true;
    }

    private static bool TryReadEntityId(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out HeadlessEntityId value)
    {
        value = default;
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue is HeadlessEntityId entityId
            ? !entityId.IsEmpty && Assign(entityId, out value)
            : HeadlessEntityId.TryParse(rawValue.ToString(), out value);
    }

    private static bool TryReadPlayerId(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out HeadlessPlayerId value)
    {
        value = default;
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        return rawValue is HeadlessPlayerId playerId
            ? !playerId.IsEmpty && Assign(playerId, out value)
            : HeadlessPlayerId.TryParse(rawValue.ToString(), out value);
    }

    private static bool Assign<T>(T input, out T output)
    {
        output = input;
        return true;
    }
}

public sealed record TriggerCollectionResult(
    bool IsSuccess,
    long EventSequence,
    GameEventType EventType,
    string Timing,
    IReadOnlyList<TimingWindowTrigger> Triggers,
    int EnqueuedCount,
    string FailureReason)
{
    public static TriggerCollectionResult Success(
        GameEvent gameEvent,
        string timing,
        IReadOnlyList<TimingWindowTrigger> triggers)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(triggers);

        return new TriggerCollectionResult(
            true,
            gameEvent.Sequence,
            gameEvent.Type,
            timing,
            triggers.ToArray(),
            EnqueuedCount: 0,
            FailureReason: string.Empty);
    }

    public static TriggerCollectionResult Failure(
        GameEvent gameEvent,
        string timing,
        string failureReason)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        return new TriggerCollectionResult(
            false,
            gameEvent.Sequence,
            gameEvent.Type,
            timing ?? string.Empty,
            Array.Empty<TimingWindowTrigger>(),
            EnqueuedCount: 0,
            FailureReason: failureReason ?? string.Empty);
    }
}
