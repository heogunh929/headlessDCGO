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

/// <summary>The Digivolve intent packet (card + digivolution target + resolved memory cost). Re-homed here from
/// the retired <c>Headless/Runtime/DigivolveAction.cs</c> when that invented action class was torn down: the
/// packet itself is pure substrate (the agent-facing LegalAction parameter shape the TurnFlowDriver converts to
/// the AS-IS <c>PlayCardAction(cardIndex, targetFrameID, …)</c>), so it lives with the other action payloads.
/// The retired class's <c>TryRead</c> half went with its only consumer (<c>DigivolveAction.ProcessAsync</c>,
/// retired — digivolve is pump-only and routes through <c>PlayCardClass.PlayCard()</c>).</summary>
/// <summary>(PLAYCARD-PAYLOAD re-migration) The PlayCard action payload, re-homed VERBATIM from the retired
/// substrate <c>PlayCardAction.cs</c> into this file — the live substrate holder for action payload records
/// (<see cref="DigivolveActionPayload"/> / <see cref="AttackActionPayload"/> / … ). It is pure action-parameter
/// transport (no game rule), so it has no AS-IS 원가; the re-housing precedent is
/// <c>Headless/Services/ZoneMoveMetadataKeys.cs</c>. Parameter keys and wire values are unchanged.</summary>
public sealed record PlayCardActionPayload(
    HeadlessEntityId CardId,
    int MemoryCost,
    ChoiceZone FromZone,
    ChoiceZone ToZone)
{
    /// <summary>(AD1-A) parameter key carrying the Assembly material ids (comma-joined, element order).</summary>
    public const string AssemblyMaterialsKey = "assemblyMaterials";

    /// <summary>(AD1-A) the Assembly materials this play consumes from the OWNER'S TRASH (empty = a normal
    /// play). AS-IS folds Assembly into the ordinary play flow (CardController.cs:753) — headless it is the
    /// same PlayCard action parameterized with the chosen full material set.</summary>
    public IReadOnlyList<HeadlessEntityId> AssemblyMaterials { get; init; } = Array.Empty<HeadlessEntityId>();

    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost,
            [HeadlessActionParameterKeys.FromZone] = FromZone,
            [HeadlessActionParameterKeys.ToZone] = ToZone
        };
        if (AssemblyMaterials.Count > 0)
        {
            parameters[AssemblyMaterialsKey] = string.Join(",", AssemblyMaterials.Select(m => m.Value));
        }

        return parameters;
    }

    public static bool TryRead(
        LegalAction action,
        [NotNullWhen(true)] out PlayCardActionPayload? payload,
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

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryCost, out int memoryCost))
        {
            payload = null;
            error = $"Missing action parameter: {HeadlessActionParameterKeys.MemoryCost}.";
            return false;
        }

        ChoiceZone fromZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.FromZone,
            ChoiceZone.Hand);
        ChoiceZone toZone = HeadlessActionPayloadReader.ReadZoneOrDefault(
            action,
            HeadlessActionParameterKeys.ToZone,
            ChoiceZone.BattleArea);

        IReadOnlyList<HeadlessEntityId> assemblyMaterials = Array.Empty<HeadlessEntityId>();
        if (action.Parameters.TryGetValue(AssemblyMaterialsKey, out object? rawMaterials) &&
            rawMaterials?.ToString() is { Length: > 0 } materialsValue)
        {
            assemblyMaterials = materialsValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => new HeadlessEntityId(id))
                .ToArray();
        }

        payload = new PlayCardActionPayload(cardId, memoryCost, fromZone, toZone) { AssemblyMaterials = assemblyMaterials };
        error = null;
        return true;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out int value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }

        if (rawValue is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }
}

public sealed record DigivolveActionPayload(
    HeadlessEntityId CardId,
    HeadlessEntityId TargetCardId,
    int MemoryCost)
{
    /// <summary>(W6-F) parameter key carrying the App-Fusion link material (the host's LINK card that is
    /// consumed into the fused stack's sources — AS-IS selectAppFusionEffect.AddToSources).</summary>
    public const string AppFusionLinkCardKey = "appFusionLinkCard";

    /// <summary>(W6-F) the App-Fusion link material; empty = a normal digivolution.</summary>
    public HeadlessEntityId AppFusionLinkCardId { get; init; }

    public IReadOnlyDictionary<string, object?> ToParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.CardId] = CardId,
            [HeadlessActionParameterKeys.TargetCardId] = TargetCardId,
            [HeadlessActionParameterKeys.MemoryCost] = MemoryCost
        };
        if (!AppFusionLinkCardId.IsEmpty)
        {
            parameters[AppFusionLinkCardKey] = AppFusionLinkCardId.Value;
        }

        return parameters;
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
