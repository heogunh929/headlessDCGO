namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessActionQueue
{
    private readonly Queue<ReplayActionRecord> _actions = new();
    private long _nextSequence;

    public int Count => _actions.Count;

    public void Enqueue(LegalAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enqueue(new ReplayActionRecord(_nextSequence++, action));
    }

    public void Enqueue(ReplayActionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _actions.Enqueue(record);
        _nextSequence = Math.Max(_nextSequence, record.Sequence + 1);
    }

    public bool TryPeek(out LegalAction? action)
    {
        if (!TryPeekRecord(out ReplayActionRecord? record) || record is null)
        {
            action = null;
            return false;
        }

        action = record.Action;
        return true;
    }

    public bool TryPeekRecord(out ReplayActionRecord? record)
    {
        if (_actions.Count == 0)
        {
            record = null;
            return false;
        }

        record = _actions.Peek();
        return true;
    }

    public bool TryDequeue(out LegalAction? action)
    {
        if (!TryDequeueRecord(out ReplayActionRecord? record) || record is null)
        {
            action = null;
            return false;
        }

        action = record.Action;
        return true;
    }

    public bool TryDequeueRecord(out ReplayActionRecord? record)
    {
        if (_actions.Count == 0)
        {
            record = null;
            return false;
        }

        record = _actions.Dequeue();
        return true;
    }

    public IReadOnlyList<LegalAction> Snapshot()
    {
        return _actions.Select(record => record.Action).ToArray();
    }

    public IReadOnlyList<ReplayActionRecord> ReplaySnapshot()
    {
        return _actions.ToArray();
    }

    public void Clear()
    {
        _actions.Clear();
        _nextSequence = 0;
    }
}

public sealed record ReplayActionRecord
{
    private string _sessionId = string.Empty;
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public ReplayActionRecord(
        long Sequence,
        LegalAction Action,
        string? SessionId = null,
        IReadOnlyDictionary<string, object?>? Metadata = null)
    {
        if (Sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Sequence), "Replay action sequence must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(Action);

        this.Sequence = Sequence;
        this.Action = Action;
        this.SessionId = string.IsNullOrWhiteSpace(SessionId) ? "local" : SessionId.Trim();
        this.Metadata = Metadata ?? new Dictionary<string, object?>();
    }

    public long Sequence { get; init; }

    public LegalAction Action { get; init; }

    public string SessionId
    {
        get => _sessionId;
        init => _sessionId = string.IsNullOrWhiteSpace(value) ? "local" : value.Trim();
    }

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = CopyMetadata(value);
    }

    public string Serialize()
    {
        var node = new JsonObject
        {
            ["sequence"] = Sequence,
            ["sessionId"] = SessionId,
            ["actionId"] = Action.Id.Value,
            ["playerId"] = Action.PlayerId.Value,
            ["actionType"] = Action.ActionType,
            ["parameters"] = ToJsonObject(Action.Parameters),
            ["metadata"] = ToJsonObject(Metadata),
        };

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static ReplayActionRecord Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonNode node = JsonNode.Parse(json)
            ?? throw new ArgumentException("Replay action record json must not be empty.", nameof(json));

        JsonObject obj = node.AsObject();
        long sequence = obj["sequence"]?.GetValue<long>()
            ?? throw new ArgumentException("Replay action record json is missing sequence.", nameof(json));
        string sessionId = obj["sessionId"]?.GetValue<string>() ?? "local";
        string actionId = obj["actionId"]?.GetValue<string>()
            ?? throw new ArgumentException("Replay action record json is missing actionId.", nameof(json));
        int playerId = obj["playerId"]?.GetValue<int>()
            ?? throw new ArgumentException("Replay action record json is missing playerId.", nameof(json));
        string actionType = obj["actionType"]?.GetValue<string>()
            ?? throw new ArgumentException("Replay action record json is missing actionType.", nameof(json));

        var action = new LegalAction(
            new HeadlessEntityId(actionId),
            new HeadlessPlayerId(playerId),
            actionType,
            FromJsonObject(obj["parameters"] as JsonObject));

        return new ReplayActionRecord(
            sequence,
            action,
            sessionId,
            FromJsonObject(obj["metadata"] as JsonObject));
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Replay action metadata keys must not be null or whitespace.", nameof(metadata));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }

    private static JsonObject ToJsonObject(IReadOnlyDictionary<string, object?> values)
    {
        var obj = new JsonObject();
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            obj[pair.Key] = ToJsonValue(pair.Value);
        }

        return obj;
    }

    private static JsonNode? ToJsonValue(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => JsonValue.Create(stringValue),
            int intValue => JsonValue.Create(intValue),
            long longValue => JsonValue.Create(longValue),
            bool boolValue => JsonValue.Create(boolValue),
            double doubleValue => JsonValue.Create(doubleValue),
            HeadlessEntityId entityId => JsonValue.Create(entityId.Value),
            HeadlessPlayerId playerId => JsonValue.Create(playerId.Value),
            Enum enumValue => JsonValue.Create(enumValue.ToString()),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static IReadOnlyDictionary<string, object?> FromJsonObject(JsonObject? obj)
    {
        if (obj is null)
        {
            return new Dictionary<string, object?>();
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode?> pair in obj)
        {
            values[pair.Key] = FromJsonValue(pair.Value);
        }

        return values;
    }

    private static object? FromJsonValue(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        JsonValue jsonValue = value.AsValue();
        if (jsonValue.TryGetValue(out int intValue))
        {
            return intValue;
        }

        if (jsonValue.TryGetValue(out long longValue))
        {
            return longValue;
        }

        if (jsonValue.TryGetValue(out bool boolValue))
        {
            return boolValue;
        }

        if (jsonValue.TryGetValue(out double doubleValue))
        {
            return doubleValue;
        }

        if (jsonValue.TryGetValue(out string? stringValue))
        {
            return stringValue;
        }

        return jsonValue.ToJsonString();
    }
}
