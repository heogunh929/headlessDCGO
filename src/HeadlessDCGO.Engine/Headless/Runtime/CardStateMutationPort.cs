namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class CardStateMutationPort
{
    private readonly EngineTrace? _trace;

    public CardStateMutationPort(MatchState state, EngineTrace? trace = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        _trace = trace;
    }

    public MatchState State { get; }

    public CardStateMutationProcessResult Mutate(CardStateMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            CardIdentityAdapter identity = new(State);
            CardIdentitySnapshot before = identity.Bind(request.CardId);
            CardIdentityAdapter mutated = request.Mutation switch
            {
                CardStateMutation.Suspend => identity.Suspend(request.CardId),
                CardStateMutation.Unsuspend => identity.Unsuspend(request.CardId),
                CardStateMutation.Reveal => identity.Reveal(request.CardId),
                CardStateMutation.Hide => identity.Hide(request.CardId),
                _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown card state mutation.")
            };
            CardIdentitySnapshot after = mutated.Bind(request.CardId);

            if (HasSameState(before, after))
            {
                return CardStateMutationProcessResult.NoOp(State, before, after);
            }

            GameEvent stateEvent = CreateStateEvent(State, request, before, after);
            MatchState stateWithEvent = mutated.State with
            {
                Version = State.Version + 1,
                Events = State.Events.Concat(new[] { stateEvent }).ToArray()
            };
            EffectContext effectContext = CreateEffectContext(request, after, stateEvent);

            _trace?.Record("card.state", stateEvent.Message, stateEvent.Metadata);

            return CardStateMutationProcessResult.Success(
                stateWithEvent,
                before,
                after,
                stateEvent,
                effectContext);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CardStateMutationProcessResult.Failure(State, ex.Message);
        }
    }

    private static bool HasSameState(CardIdentitySnapshot before, CardIdentitySnapshot after)
    {
        return before.IsSuspended == after.IsSuspended
            && before.IsFaceUp == after.IsFaceUp;
    }

    private static GameEvent CreateStateEvent(
        MatchState state,
        CardStateMutationRequest request,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mutation"] = request.Mutation.ToString(),
            ["cardId"] = request.CardId.Value,
            ["cardDefinitionId"] = before.DefinitionId.Value,
            ["cardOwnerId"] = before.OwnerId.Value,
            ["zone"] = before.Zone?.ToString(),
            ["zoneOwnerId"] = before.ZoneOwnerId?.Value,
            ["zoneIndex"] = before.ZoneIndex,
            ["beforeSuspended"] = before.IsSuspended,
            ["afterSuspended"] = after.IsSuspended,
            ["beforeFaceUp"] = before.IsFaceUp,
            ["afterFaceUp"] = after.IsFaceUp,
            ["reason"] = request.Reason
        };

        return new GameEvent(
            state.Version + 1,
            GameEventType.StateChanged,
            $"Card state changed: {request.CardId} {request.Mutation}",
            metadata);
    }

    private static EffectContext CreateEffectContext(
        CardStateMutationRequest request,
        CardIdentitySnapshot after,
        GameEvent stateEvent)
    {
        return new EffectContext(
            after.OwnerId,
            after.OwnerId,
            request.CardId,
            request.CardId,
            new[] { request.CardId },
            stateEvent.Metadata);
    }
}

public enum CardStateMutation
{
    Suspend,
    Unsuspend,
    Reveal,
    Hide
}

public sealed record CardStateMutationRequest
{
    public CardStateMutationRequest(
        HeadlessEntityId cardId,
        CardStateMutation mutation,
        string? reason = null)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        CardId = cardId;
        Mutation = mutation;
        Reason = reason?.Trim() ?? string.Empty;
    }

    public HeadlessEntityId CardId { get; }

    public CardStateMutation Mutation { get; }

    public string Reason { get; }
}

public sealed record CardStateMutationProcessResult(
    bool IsSuccess,
    bool DidMutate,
    MatchState State,
    CardIdentitySnapshot? Before,
    CardIdentitySnapshot? After,
    GameEvent? StateEvent,
    EffectContext? EffectContext,
    string FailureReason)
{
    public static CardStateMutationProcessResult Success(
        MatchState state,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after,
        GameEvent stateEvent,
        EffectContext effectContext)
    {
        return new CardStateMutationProcessResult(
            true,
            true,
            state,
            before,
            after,
            stateEvent,
            effectContext,
            string.Empty);
    }

    public static CardStateMutationProcessResult NoOp(
        MatchState state,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after)
    {
        return new CardStateMutationProcessResult(
            true,
            false,
            state,
            before,
            after,
            StateEvent: null,
            EffectContext: null,
            string.Empty);
    }

    public static CardStateMutationProcessResult Failure(MatchState state, string failureReason)
    {
        return new CardStateMutationProcessResult(
            false,
            false,
            state,
            Before: null,
            After: null,
            StateEvent: null,
            EffectContext: null,
            failureReason ?? string.Empty);
    }
}
