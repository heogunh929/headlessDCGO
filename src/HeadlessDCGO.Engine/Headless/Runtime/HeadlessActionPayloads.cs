namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record TerminalActionPayload(
    bool IsTerminal,
    HeadlessPlayerId? WinnerPlayerId = null,
    bool IsDraw = false,
    bool IsSurrender = false,
    string Reason = "")
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.IsTerminal] = IsTerminal,
            [HeadlessActionParameterKeys.WinnerPlayerId] = WinnerPlayerId,
            [HeadlessActionParameterKeys.IsDraw] = IsDraw,
            [HeadlessActionParameterKeys.IsSurrender] = IsSurrender,
            [HeadlessActionParameterKeys.Reason] = Reason
        };
    }

    public static bool TryRead(
        LegalAction action,
        bool defaultIsTerminal,
        [NotNullWhen(true)] out TerminalActionPayload? payload,
        out string? error)
    {
        bool isTerminal = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.IsTerminal,
            defaultIsTerminal);

        if (!HeadlessActionPayloadReader.TryReadOptionalPlayerId(
                action,
                HeadlessActionParameterKeys.WinnerPlayerId,
                out HeadlessPlayerId? winnerPlayerId,
                out error))
        {
            payload = null;
            return false;
        }

        bool isDraw = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.IsDraw,
            defaultValue: false);
        bool isSurrender = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.IsSurrender,
            defaultValue: false);
        string reason = HeadlessActionPayloadReader.ReadStringOrDefault(
            action,
            HeadlessActionParameterKeys.Reason,
            string.Empty);

        payload = new TerminalActionPayload(isTerminal, winnerPlayerId, isDraw, isSurrender, reason);
        return true;
    }
}

public sealed record CardActionPayload(HeadlessEntityId CardId)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out CardActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId cardId,
                out error))
        {
            payload = null;
            return false;
        }

        payload = new CardActionPayload(cardId);
        return true;
    }
}

public sealed record MoveCardActionPayload(
    HeadlessEntityId CardId,
    ChoiceZone FromZone,
    ChoiceZone ToZone,
    bool FaceUp = false)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.FromZone] = FromZone,
            [HeadlessActionParameterKeys.ToZone] = ToZone,
            [HeadlessActionParameterKeys.FaceUp] = FaceUp
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out MoveCardActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId cardId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!HeadlessActionPayloadReader.TryReadZone(
                action,
                HeadlessActionParameterKeys.ToZone,
                out ChoiceZone toZone,
                out error))
        {
            payload = null;
            return false;
        }

        ChoiceZone fromZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.FromZone,
            ChoiceZone.None);
        bool faceUp = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.FaceUp,
            defaultValue: false);

        payload = new MoveCardActionPayload(cardId, fromZone, toZone, faceUp);
        return true;
    }
}

public sealed record SecurityActionPayload(HeadlessEntityId CardId, bool FaceUp = false)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.FaceUp] = FaceUp
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out SecurityActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.CardId,
                out HeadlessEntityId cardId,
                out error))
        {
            payload = null;
            return false;
        }

        bool faceUp = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.FaceUp,
            defaultValue: false);

        payload = new SecurityActionPayload(cardId, faceUp);
        return true;
    }
}

public sealed record EffectActionPayload(
    HeadlessEntityId EffectId,
    string Timing,
    HeadlessEntityId SourceEntityId)
{
    public static EffectActionPayload Create(
        HeadlessEntityId effectId,
        string timing = "Manual",
        HeadlessEntityId? sourceEntityId = null)
    {
        return new EffectActionPayload(effectId, timing, sourceEntityId ?? effectId);
    }

    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.EffectId] = EffectId,
            [HeadlessActionParameterKeys.Timing] = Timing,
            [HeadlessActionParameterKeys.SourceEntityId] = SourceEntityId
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out EffectActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.EffectId,
                out HeadlessEntityId effectId,
                out error))
        {
            payload = null;
            return false;
        }

        string timing = HeadlessActionPayloadReader.ReadStringOrDefault(
            action,
            HeadlessActionParameterKeys.Timing,
            "Manual");
        HeadlessEntityId sourceEntityId = HeadlessActionPayloadReader.ReadEntityIdOrDefault(
            action,
            HeadlessActionParameterKeys.SourceEntityId,
            action.Id);

        payload = new EffectActionPayload(effectId, timing, sourceEntityId);
        return true;
    }
}

public sealed record AttackActionPayload(
    HeadlessEntityId AttackerId,
    HeadlessPlayerId DefendingPlayerId,
    HeadlessEntityId? TargetId,
    bool IsDirectAttack)
{
    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.AttackerId] = AttackerId,
            [HeadlessActionParameterKeys.DefendingPlayerId] = DefendingPlayerId,
            [HeadlessActionParameterKeys.AttackTargetId] = TargetId,
            [HeadlessActionParameterKeys.IsDirectAttack] = IsDirectAttack
        };
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out AttackActionPayload? payload,
        out string? error)
    {
        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action,
                HeadlessActionParameterKeys.AttackerId,
                out HeadlessEntityId attackerId,
                out error))
        {
            payload = null;
            return false;
        }

        if (!HeadlessActionPayloadReader.TryReadOptionalPlayerId(
                action,
                HeadlessActionParameterKeys.DefendingPlayerId,
                out HeadlessPlayerId? defendingPlayerId,
                out error) ||
            !defendingPlayerId.HasValue)
        {
            payload = null;
            error ??= $"Missing action parameter: {HeadlessActionParameterKeys.DefendingPlayerId}.";
            return false;
        }

        if (!HeadlessActionPayloadReader.TryReadOptionalEntityId(
                action,
                HeadlessActionParameterKeys.AttackTargetId,
                out HeadlessEntityId? targetId,
                out error))
        {
            payload = null;
            return false;
        }

        bool isDirectAttack = HeadlessActionPayloadReader.ReadBoolOrDefault(
            action,
            HeadlessActionParameterKeys.IsDirectAttack,
            defaultValue: !targetId.HasValue);

        payload = new AttackActionPayload(
            attackerId,
            defendingPlayerId.Value,
            targetId,
            isDirectAttack);
        return true;
    }
}

internal static class HeadlessActionPayloadReader
{
    public static bool TryReadEntityId(
        LegalAction action,
        string key,
        out HeadlessEntityId value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!action.Parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            error = $"Missing action parameter: {key}.";
            return false;
        }

        if (rawValue is HeadlessEntityId entityId)
        {
            value = entityId;
            error = null;
            return true;
        }

        if (rawValue is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
        {
            value = new HeadlessEntityId(stringValue);
            error = null;
            return true;
        }

        value = default;
        error = $"Invalid entity id parameter: {key}.";
        return false;
    }

    public static HeadlessEntityId ReadEntityIdOrDefault(
        LegalAction action,
        string key,
        HeadlessEntityId defaultValue)
    {
        return TryReadEntityId(action, key, out HeadlessEntityId value, out _)
            ? value
            : defaultValue;
    }

    public static bool TryReadOptionalEntityId(
        LegalAction action,
        string key,
        out HeadlessEntityId? value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!action.Parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = null;
            error = null;
            return true;
        }

        if (rawValue is HeadlessEntityId entityId)
        {
            value = entityId;
            error = null;
            return true;
        }

        if (rawValue is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                value = null;
                error = null;
                return true;
            }

            value = new HeadlessEntityId(stringValue);
            error = null;
            return true;
        }

        value = null;
        error = $"Invalid entity id parameter: {key}.";
        return false;
    }

    public static bool TryReadZone(
        LegalAction action,
        string key,
        out ChoiceZone value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!action.Parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            error = $"Missing action parameter: {key}.";
            return false;
        }

        if (rawValue is ChoiceZone zone)
        {
            value = zone;
            error = null;
            return true;
        }

        if (rawValue is string stringValue &&
            Enum.TryParse(stringValue, ignoreCase: true, out ChoiceZone parsedZone))
        {
            value = parsedZone;
            error = null;
            return true;
        }

        value = default;
        error = $"Invalid zone parameter: {key}.";
        return false;
    }

    public static ChoiceZone ReadZoneOrDefault(
        LegalAction action,
        string key,
        ChoiceZone defaultValue)
    {
        return TryReadZone(action, key, out ChoiceZone value, out _)
            ? value
            : defaultValue;
    }

    public static bool ReadBoolOrDefault(
        LegalAction action,
        string key,
        bool defaultValue)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!action.Parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        if (rawValue is bool boolValue)
        {
            return boolValue;
        }

        return rawValue is string stringValue && bool.TryParse(stringValue, out bool parsedValue)
            ? parsedValue
            : defaultValue;
    }

    public static string ReadStringOrDefault(
        LegalAction action,
        string key,
        string defaultValue)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return action.Parameters.TryGetValue(key, out object? rawValue) && rawValue is string stringValue
            ? stringValue
            : defaultValue;
    }

    public static bool TryReadOptionalPlayerId(
        LegalAction action,
        string key,
        out HeadlessPlayerId? value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!action.Parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = null;
            error = null;
            return true;
        }

        if (rawValue is HeadlessPlayerId playerId)
        {
            value = playerId;
            error = null;
            return true;
        }

        if (rawValue is int intValue)
        {
            value = new HeadlessPlayerId(intValue);
            error = null;
            return true;
        }

        if (rawValue is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                value = null;
                error = null;
                return true;
            }

            if (int.TryParse(stringValue, out int parsedValue))
            {
                value = new HeadlessPlayerId(parsedValue);
                error = null;
                return true;
            }
        }

        value = null;
        error = $"Invalid player id parameter: {key}.";
        return false;
    }
}
