namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class CardMovementPort
{
    private readonly EngineTrace? _trace;

    public CardMovementPort(MatchState state, EngineTrace? trace = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        _trace = trace;
    }

    public MatchState State { get; }

    public CardMovementProcessResult Move(CardMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            CardIdentityAdapter identity = new(State);
            CardIdentitySnapshot before = identity.Bind(request.CardId);
            CardIdentityAdapter moved = identity.MoveCard(request.ToZoneMoveRequest());
            CardIdentitySnapshot after = moved.Bind(request.CardId);
            GameEvent movementEvent = CreateMovementEvent(moved.State, request, before, after);
            MatchState stateWithEvent = ReplaceLastCardMovedEvent(moved.State, movementEvent);
            EffectContext effectContext = CreateEffectContext(request, after, movementEvent);

            _trace?.Record("card.movement", movementEvent.Message, movementEvent.Metadata);

            return CardMovementProcessResult.Success(
                stateWithEvent,
                before,
                after,
                movementEvent,
                effectContext);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CardMovementProcessResult.Failure(State, ex.Message);
        }
    }

    private static GameEvent CreateMovementEvent(
        MatchState movedState,
        CardMovementRequest request,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after)
    {
        GameEvent rawEvent = movedState.Events
            .LastOrDefault(candidate => candidate.Type == GameEventType.CardMoved)
            ?? throw new InvalidOperationException("Card movement did not emit a CardMoved event.");

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = request.PlayerId.Value,
            ["cardId"] = request.CardId.Value,
            ["cardDefinitionId"] = before.DefinitionId.Value,
            ["cardOwnerId"] = before.OwnerId.Value,
            ["fromZone"] = request.FromZone.ToString(),
            ["toZone"] = request.ToZone.ToString(),
            ["fromZoneOwnerId"] = before.ZoneOwnerId?.Value,
            ["toZoneOwnerId"] = after.ZoneOwnerId?.Value,
            ["fromIndex"] = before.ZoneIndex,
            ["toIndex"] = after.ZoneIndex,
            ["faceUp"] = request.FaceUp,
            ["isToken"] = before.IsToken,
            ["reason"] = request.Reason
        };

        return new GameEvent(
            rawEvent.Sequence,
            GameEventType.CardMoved,
            $"Card moved: {request.CardId} {request.FromZone}->{request.ToZone}",
            metadata);
    }

    private static MatchState ReplaceLastCardMovedEvent(MatchState state, GameEvent movementEvent)
    {
        GameEvent[] events = state.Events.ToArray();
        for (int index = events.Length - 1; index >= 0; index--)
        {
            if (events[index].Type == GameEventType.CardMoved)
            {
                events[index] = movementEvent;
                return state with { Events = events };
            }
        }

        throw new InvalidOperationException("Card movement did not emit a CardMoved event.");
    }

    private static EffectContext CreateEffectContext(
        CardMovementRequest request,
        CardIdentitySnapshot after,
        GameEvent movementEvent)
    {
        return new EffectContext(
            request.PlayerId,
            after.OwnerId,
            request.CardId,
            request.CardId,
            new[] { request.CardId },
            movementEvent.Metadata);
    }
}

public sealed record CardMovementRequest
{
    public CardMovementRequest(
        HeadlessPlayerId playerId,
        HeadlessEntityId cardId,
        ChoiceZone fromZone,
        ChoiceZone toZone,
        bool faceUp = false,
        string? reason = null)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        if (fromZone == ChoiceZone.Custom)
        {
            throw new ArgumentException("From zone must not be Custom.", nameof(fromZone));
        }

        if (toZone is ChoiceZone.None or ChoiceZone.Custom)
        {
            throw new ArgumentException("To zone must be a concrete player zone.", nameof(toZone));
        }

        if (fromZone == toZone)
        {
            throw new ArgumentException("From zone and to zone must be different.", nameof(toZone));
        }

        PlayerId = playerId;
        CardId = cardId;
        FromZone = fromZone;
        ToZone = toZone;
        FaceUp = faceUp;
        Reason = reason?.Trim() ?? string.Empty;
    }

    public HeadlessPlayerId PlayerId { get; }

    public HeadlessEntityId CardId { get; }

    public ChoiceZone FromZone { get; }

    public ChoiceZone ToZone { get; }

    public bool FaceUp { get; }

    public string Reason { get; }

    public ZoneMoveRequest ToZoneMoveRequest()
    {
        return new ZoneMoveRequest(PlayerId, CardId, FromZone, ToZone, FaceUp);
    }
}

public sealed record CardMovementProcessResult(
    bool IsSuccess,
    MatchState State,
    CardIdentitySnapshot? Before,
    CardIdentitySnapshot? After,
    GameEvent? MovementEvent,
    EffectContext? EffectContext,
    string FailureReason)
{
    public static CardMovementProcessResult Success(
        MatchState state,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after,
        GameEvent movementEvent,
        EffectContext effectContext)
    {
        return new CardMovementProcessResult(
            true,
            state,
            before,
            after,
            movementEvent,
            effectContext,
            string.Empty);
    }

    public static CardMovementProcessResult Failure(MatchState state, string failureReason)
    {
        return new CardMovementProcessResult(
            false,
            state,
            Before: null,
            After: null,
            MovementEvent: null,
            EffectContext: null,
            failureReason ?? string.Empty);
    }
}
