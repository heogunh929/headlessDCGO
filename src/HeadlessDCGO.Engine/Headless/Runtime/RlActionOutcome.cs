namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace event-derived outcome with typed transition results once action handlers are final.
public sealed record RlActionOutcome(
    bool HasAction,
    bool WasProcessed,
    bool WasRejected,
    HeadlessEntityId? ActionId,
    HeadlessPlayerId? PlayerId,
    string? ActionType,
    string Message)
{
    public static RlActionOutcome Empty { get; } = new(
        HasAction: false,
        WasProcessed: false,
        WasRejected: false,
        ActionId: null,
        PlayerId: null,
        ActionType: null,
        Message: string.Empty);

    public static RlActionOutcome FromEvents(IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        GameEvent? selectedEvent = events
            .Where(IsActionOutcomeEvent)
            .LastOrDefault();

        if (selectedEvent is null)
        {
            return Empty;
        }

        return new RlActionOutcome(
            HasAction: true,
            WasProcessed: selectedEvent.Type == GameEventType.ActionProcessed,
            WasRejected: selectedEvent.Type == GameEventType.InvalidAction,
            ActionId: ReadEntityId(selectedEvent.Metadata, HeadlessActionParameterKeys.ActionId),
            PlayerId: ReadPlayerId(selectedEvent.Metadata, HeadlessActionParameterKeys.PlayerId),
            ActionType: ReadString(selectedEvent.Metadata, HeadlessActionParameterKeys.ActionType),
            Message: selectedEvent.Message);
    }

    private static bool IsActionOutcomeEvent(GameEvent gameEvent)
    {
        return gameEvent.Type is
            GameEventType.InvalidAction or
            GameEventType.ActionProcessed or
            GameEventType.ActionQueued;
    }

    private static HeadlessEntityId? ReadEntityId(
        IReadOnlyDictionary<string, object?> metadata,
        string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return null;
        }

        return rawValue switch
        {
            HeadlessEntityId entityId => entityId,
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => new HeadlessEntityId(stringValue),
            _ => null
        };
    }

    private static HeadlessPlayerId? ReadPlayerId(
        IReadOnlyDictionary<string, object?> metadata,
        string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return null;
        }

        if (rawValue is HeadlessPlayerId playerId)
        {
            return playerId;
        }

        if (rawValue is int intValue)
        {
            return new HeadlessPlayerId(intValue);
        }

        if (rawValue is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            return new HeadlessPlayerId((int)longValue);
        }

        if (rawValue is string stringValue &&
            int.TryParse(stringValue, out int parsedValue))
        {
            return new HeadlessPlayerId(parsedValue);
        }

        return null;
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> metadata,
        string key)
    {
        return metadata.TryGetValue(key, out object? rawValue) && rawValue is string stringValue
            ? stringValue
            : null;
    }
}
