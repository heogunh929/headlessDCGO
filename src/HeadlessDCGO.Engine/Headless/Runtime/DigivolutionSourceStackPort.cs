namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class DigivolutionSourceStackPort
{
    private readonly EngineTrace? _trace;

    public DigivolutionSourceStackPort(MatchState state, EngineTrace? trace = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        _trace = trace;
    }

    public MatchState State { get; }

    public SourceStackProcessResult Mutate(SourceStackMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            CardIdentitySnapshot before = new CardIdentityAdapter(State).Bind(request.TargetCardId);
            MatchState mutated = request.Mutation switch
            {
                SourceStackMutation.AttachTop => Attach(State, request, SourceAttachPosition.Top),
                SourceStackMutation.AttachBottom => Attach(State, request, SourceAttachPosition.Bottom),
                SourceStackMutation.Detach => Detach(State, request),
                _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown source stack mutation.")
            };
            CardIdentitySnapshot after = new CardIdentityAdapter(mutated).Bind(request.TargetCardId);

            if (SameSources(before, after))
            {
                return SourceStackProcessResult.NoOp(State, before, after);
            }

            GameEvent sourceEvent = CreateSourceEvent(State, request, before, after);
            MatchState stateWithEvent = mutated with
            {
                Version = State.Version + 1,
                Events = State.Events.Concat(new[] { sourceEvent }).ToArray()
            };
            EffectContext effectContext = CreateEffectContext(request, after, sourceEvent);

            _trace?.Record("card.source", sourceEvent.Message, sourceEvent.Metadata);

            return SourceStackProcessResult.Success(
                stateWithEvent,
                before,
                after,
                sourceEvent,
                effectContext);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return SourceStackProcessResult.Failure(State, ex.Message);
        }
    }

    private static MatchState Attach(
        MatchState state,
        SourceStackMutationRequest request,
        SourceAttachPosition position)
    {
        CardInstanceState target = state.GetCardInstance(request.TargetCardId);
        ValidateTargetCanReceiveSource(target);
        ValidateSourceIds(request.SourceIds);

        foreach (HeadlessEntityId sourceId in request.SourceIds)
        {
            CardInstanceState source = state.GetCardInstance(sourceId);
            ValidateAttachPair(target, source);
        }

        MatchState detached = RemoveSourcesFromAllStacks(state, request.SourceIds);
        CardInstanceState currentTarget = detached.GetCardInstance(request.TargetCardId);
        List<HeadlessEntityId> sources = currentTarget.SourceIds.ToList();

        if (position == SourceAttachPosition.Top)
        {
            sources.InsertRange(0, request.SourceIds);
        }
        else
        {
            sources.AddRange(request.SourceIds);
        }

        return detached.WithCardInstance(currentTarget with { SourceIds = sources });
    }

    private static MatchState Detach(MatchState state, SourceStackMutationRequest request)
    {
        CardInstanceState target = state.GetCardInstance(request.TargetCardId);
        ValidateSourceIds(request.SourceIds);

        List<HeadlessEntityId> sources = target.SourceIds.ToList();
        foreach (HeadlessEntityId sourceId in request.SourceIds)
        {
            if (!sources.Remove(sourceId))
            {
                throw new InvalidOperationException($"Source id '{sourceId}' is not attached to card '{target.InstanceId}'.");
            }
        }

        return state.WithCardInstance(target with { SourceIds = sources });
    }

    private static MatchState RemoveSourcesFromAllStacks(
        MatchState state,
        IReadOnlyList<HeadlessEntityId> sourceIds)
    {
        MatchState current = state;
        HashSet<HeadlessEntityId> sourceSet = sourceIds.ToHashSet();
        foreach (CardInstanceState instance in state.CardInstances.Values)
        {
            if (!instance.SourceIds.Any(sourceSet.Contains))
            {
                continue;
            }

            HeadlessEntityId[] remaining = instance.SourceIds
                .Where(sourceId => !sourceSet.Contains(sourceId))
                .ToArray();
            current = current.WithCardInstance(instance with { SourceIds = remaining });
        }

        return current;
    }

    private static void ValidateTargetCanReceiveSource(CardInstanceState target)
    {
        if (target.HasFlag(CardIdentityAdapter.TokenFlagKey))
        {
            throw new InvalidOperationException($"Token card '{target.InstanceId}' cannot receive digivolution sources.");
        }
    }

    private static void ValidateAttachPair(CardInstanceState target, CardInstanceState source)
    {
        if (target.InstanceId == source.InstanceId)
        {
            throw new InvalidOperationException("A card cannot be attached as its own digivolution source.");
        }

        if (target.OwnerId != source.OwnerId)
        {
            throw new InvalidOperationException(
                $"Source card '{source.InstanceId}' is owned by player '{source.OwnerId}', not player '{target.OwnerId}'.");
        }

        if (source.HasFlag(CardIdentityAdapter.TokenFlagKey))
        {
            throw new InvalidOperationException($"Token card '{source.InstanceId}' cannot be attached as a digivolution source.");
        }
    }

    private static void ValidateSourceIds(IReadOnlyList<HeadlessEntityId> sourceIds)
    {
        if (sourceIds.Count == 0)
        {
            throw new ArgumentException("At least one source id is required.", nameof(sourceIds));
        }

        if (sourceIds.Any(sourceId => sourceId.IsEmpty))
        {
            throw new ArgumentException("Source ids must not contain empty ids.", nameof(sourceIds));
        }

        if (sourceIds.Distinct().Count() != sourceIds.Count)
        {
            throw new ArgumentException("Source ids must not contain duplicates.", nameof(sourceIds));
        }
    }

    private static bool SameSources(CardIdentitySnapshot before, CardIdentitySnapshot after)
    {
        return before.SourceIds.SequenceEqual(after.SourceIds);
    }

    private static GameEvent CreateSourceEvent(
        MatchState state,
        SourceStackMutationRequest request,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mutation"] = request.Mutation.ToString(),
            ["targetCardId"] = request.TargetCardId.Value,
            ["targetDefinitionId"] = before.DefinitionId.Value,
            ["ownerId"] = before.OwnerId.Value,
            ["sourceIds"] = request.SourceIds.Select(id => id.Value).ToArray(),
            ["beforeSourceIds"] = before.SourceIds.Select(id => id.Value).ToArray(),
            ["afterSourceIds"] = after.SourceIds.Select(id => id.Value).ToArray(),
            ["sourceCount"] = after.SourceIds.Count,
            ["reason"] = request.Reason
        };

        return new GameEvent(
            state.Version + 1,
            GameEventType.StateChanged,
            $"Source stack changed: {request.TargetCardId} {request.Mutation}",
            metadata);
    }

    private static EffectContext CreateEffectContext(
        SourceStackMutationRequest request,
        CardIdentitySnapshot after,
        GameEvent sourceEvent)
    {
        return new EffectContext(
            after.OwnerId,
            after.OwnerId,
            request.TargetCardId,
            request.TargetCardId,
            new[] { request.TargetCardId }.Concat(request.SourceIds).ToArray(),
            sourceEvent.Metadata);
    }
}

public enum SourceStackMutation
{
    AttachTop,
    AttachBottom,
    Detach
}

public enum SourceAttachPosition
{
    Top,
    Bottom
}

public sealed record SourceStackMutationRequest
{
    public SourceStackMutationRequest(
        HeadlessEntityId targetCardId,
        SourceStackMutation mutation,
        IReadOnlyList<HeadlessEntityId> sourceIds,
        string? reason = null)
    {
        if (targetCardId.IsEmpty)
        {
            throw new ArgumentException("Target card id must not be empty.", nameof(targetCardId));
        }

        ArgumentNullException.ThrowIfNull(sourceIds);

        TargetCardId = targetCardId;
        Mutation = mutation;
        SourceIds = sourceIds.ToArray();
        Reason = reason?.Trim() ?? string.Empty;
    }

    public HeadlessEntityId TargetCardId { get; }

    public SourceStackMutation Mutation { get; }

    public IReadOnlyList<HeadlessEntityId> SourceIds { get; }

    public string Reason { get; }
}

public sealed record SourceStackProcessResult(
    bool IsSuccess,
    bool DidMutate,
    MatchState State,
    CardIdentitySnapshot? Before,
    CardIdentitySnapshot? After,
    GameEvent? SourceEvent,
    EffectContext? EffectContext,
    string FailureReason)
{
    public static SourceStackProcessResult Success(
        MatchState state,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after,
        GameEvent sourceEvent,
        EffectContext effectContext)
    {
        return new SourceStackProcessResult(
            true,
            true,
            state,
            before,
            after,
            sourceEvent,
            effectContext,
            string.Empty);
    }

    public static SourceStackProcessResult NoOp(
        MatchState state,
        CardIdentitySnapshot before,
        CardIdentitySnapshot after)
    {
        return new SourceStackProcessResult(
            true,
            false,
            state,
            before,
            after,
            SourceEvent: null,
            EffectContext: null,
            string.Empty);
    }

    public static SourceStackProcessResult Failure(MatchState state, string failureReason)
    {
        return new SourceStackProcessResult(
            false,
            false,
            state,
            Before: null,
            After: null,
            SourceEvent: null,
            EffectContext: null,
            failureReason ?? string.Empty);
    }
}
