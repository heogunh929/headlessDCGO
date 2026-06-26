namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public interface IHeadlessCardEffect
{
    CardEffectDefinition Definition { get; }

    CardEffectCanResolveResult CanResolve(CardEffectResolveContext context);

    ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default);
}

public sealed record CardEffectDefinition
{
    public CardEffectDefinition(
        HeadlessEntityId effectId,
        HeadlessEntityId sourceEntityId,
        string name,
        string timing,
        bool isOptional = false,
        bool isBackgroundProcess = false,
        int? maxCountPerTurn = null,
        string? hash = null)
    {
        if (effectId.IsEmpty)
        {
            throw new ArgumentException("Card effect id must not be empty.", nameof(effectId));
        }

        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Card effect source entity id must not be empty.", nameof(sourceEntityId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timing);

        if (maxCountPerTurn is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCountPerTurn),
                "Max count per turn must be positive when specified.");
        }

        EffectId = effectId;
        SourceEntityId = sourceEntityId;
        Name = name.Trim();
        Timing = timing.Trim();
        IsOptional = isOptional;
        IsBackgroundProcess = isBackgroundProcess;
        MaxCountPerTurn = maxCountPerTurn;
        Hash = string.IsNullOrWhiteSpace(hash) ? null : hash.Trim();
    }

    public HeadlessEntityId EffectId { get; }

    public HeadlessEntityId SourceEntityId { get; }

    public string Name { get; }

    public string Timing { get; }

    public bool IsOptional { get; }

    public bool IsBackgroundProcess { get; }

    public int? MaxCountPerTurn { get; }

    public string? Hash { get; }
}

public sealed record CardEffectResolveContext
{
    public CardEffectResolveContext(EffectRequest request)
        : this(request, Array.Empty<HeadlessEntityId>())
    {
    }

    public CardEffectResolveContext(
        EffectRequest request,
        IReadOnlyList<HeadlessEntityId>? requiredTargetEntityIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        HeadlessEntityId[] requiredTargets = (requiredTargetEntityIds ?? Array.Empty<HeadlessEntityId>()).ToArray();
        if (requiredTargets.Any(target => target.IsEmpty))
        {
            throw new ArgumentException(
                "Required target entity ids must not contain empty values.",
                nameof(requiredTargetEntityIds));
        }

        if (requiredTargets.Distinct().Count() != requiredTargets.Length)
        {
            throw new ArgumentException(
                "Required target entity ids must not contain duplicates.",
                nameof(requiredTargetEntityIds));
        }

        Request = request;
        RequiredTargetEntityIds = Array.AsReadOnly(requiredTargets);
    }

    public EffectRequest Request { get; }

    public EffectContext EffectContext => Request.Context;

    public IReadOnlyList<HeadlessEntityId> RequiredTargetEntityIds { get; }

    public bool HasRequiredTargets()
    {
        return RequiredTargetEntityIds.Count == 0
            || RequiredTargetEntityIds.All(target => EffectContext.TargetEntityIds.Contains(target));
    }
}

public sealed record CardEffectCanResolveResult
{
    public CardEffectCanResolveResult(
        bool canResolve,
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        CanResolve = canResolve;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Values = CopyValues(values);
    }

    public bool CanResolve { get; }

    public string? Message { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static CardEffectCanResolveResult Success(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new CardEffectCanResolveResult(true, message, values);
    }

    public static CardEffectCanResolveResult Failure(
        string? message,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new CardEffectCanResolveResult(false, message, values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("CanResolve result value keys must not be null or whitespace.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public interface IEffectMutationSink
{
    void Apply(EffectMutation mutation);

    /// <summary>
    /// Applies any asynchronous operations a synchronous <see cref="Apply"/> deferred (W2-follow:
    /// zone moves, draws). Called by the resolver after the effect body finishes. Default no-op so
    /// sinks that only do synchronous metadata writes need not implement it.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed record EffectMutation
{
    public EffectMutation(
        string kind,
        HeadlessEntityId sourceEntityId,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Mutation source entity id must not be empty.", nameof(sourceEntityId));
        }

        Kind = kind.Trim();
        SourceEntityId = sourceEntityId;
        Values = CopyValues(values);
    }

    public string Kind { get; }

    public HeadlessEntityId SourceEntityId { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Mutation value keys must not be null or whitespace.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public sealed class RecordingEffectMutationSink : IEffectMutationSink
{
    private readonly List<EffectMutation> _mutations = new();

    public int Count => _mutations.Count;

    public void Apply(EffectMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        _mutations.Add(mutation);
    }

    public IReadOnlyList<EffectMutation> Snapshot()
    {
        return _mutations.ToArray();
    }

    public void Clear()
    {
        _mutations.Clear();
    }
}

public sealed class HeadlessCardEffectResolver
{
    public async ValueTask<EffectResult> ResolveAsync(
        IHeadlessCardEffect effect,
        EffectRequest request,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mutations);

        cancellationToken.ThrowIfCancellationRequested();
        var context = new CardEffectResolveContext(request);
        CardEffectCanResolveResult check = effect.CanResolve(context)
            ?? CardEffectCanResolveResult.Failure("Card effect CanResolve returned null.");

        if (!check.CanResolve)
        {
            return EffectResult.Failure(
                check.Message ?? "Card effect cannot resolve.",
                MergeValues(effect, check.Values));
        }

        try
        {
            EffectResult? result = await effect
                .ResolveAsync(context, mutations, cancellationToken)
                .ConfigureAwait(false);

            return result ?? EffectResult.Failure(
                "Card effect ResolveAsync returned null.",
                MergeValues(effect, null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HeadlessDCGO.Engine.Headless.Runtime.DeferredChoicePendingException)
        {
            // W7: the effect is suspending to ask the agent for a choice. Propagate so the scheduler
            // resolver converts it into a Suspended result (effect stays queued and re-runs).
            throw;
        }
        catch (Exception ex)
        {
            return EffectResult.Failure(
                "Card effect resolver failed.",
                MergeValues(
                    effect,
                    new Dictionary<string, object?>
                    {
                        ["error"] = ex.Message,
                        ["errorType"] = ex.GetType().Name,
                    }));
        }
    }

    private static IReadOnlyDictionary<string, object?> MergeValues(
        IHeadlessCardEffect effect,
        IReadOnlyDictionary<string, object?>? values)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["effectId"] = effect.Definition.EffectId.Value,
            ["sourceEntityId"] = effect.Definition.SourceEntityId.Value,
            ["timing"] = effect.Definition.Timing,
        };

        if (values is not null)
        {
            foreach (KeyValuePair<string, object?> pair in values)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}
