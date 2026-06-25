namespace HeadlessDCGO.Engine.Headless.Effects;

public sealed class EffectScheduler
{
    private readonly EffectResolutionQueue _queue;
    private readonly Func<EffectRequest, CancellationToken, Task<EffectResult>> _resolver;

    public EffectScheduler()
        : this(new EffectResolutionQueue())
    {
    }

    public EffectScheduler(
        EffectResolutionQueue queue,
        Func<EffectRequest, CancellationToken, Task<EffectResult>>? resolver = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _resolver = resolver ?? DefaultResolverAsync;
    }

    public bool HasPendingEffects => _queue.Count > 0;

    public int PendingCount => _queue.Count;

    public int TotalEnqueuedCount { get; private set; }

    public int TotalResolvedCount { get; private set; }

    public int LastResolvedCount { get; private set; }

    public void Enqueue(EffectRequest request)
    {
        Enqueue(request, EffectResolutionMode.Unknown);
    }

    public void Enqueue(
        EffectRequest request,
        EffectResolutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);
        _queue.Enqueue(new PendingEffect(request, mode));
        TotalEnqueuedCount++;
    }

    public void Clear()
    {
        _queue.Clear();
        TotalEnqueuedCount = 0;
        TotalResolvedCount = 0;
        LastResolvedCount = 0;
    }

    public async Task<EffectResult> ResolveNextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_queue.TryPeek(out PendingEffect? effect) || effect is null)
        {
            LastResolvedCount = 0;
            return EffectResult.Failure("No pending effects.");
        }

        EffectResult result;
        try
        {
            result = await _resolver(effect.Request, cancellationToken).ConfigureAwait(false)
                ?? CreateFailure(effect, "Effect resolver returned a null result.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = CreateFailure(
                effect,
                "Effect resolver failed.",
                new Dictionary<string, object?>
                {
                    ["error"] = ex.Message,
                    ["errorType"] = ex.GetType().Name,
                });
        }

        if (!result.Resolved)
        {
            LastResolvedCount = 0;
            return result;
        }

        if (!_queue.TryDequeue(out PendingEffect? resolvedEffect)
            || resolvedEffect is null
            || !ReferenceEquals(effect, resolvedEffect))
        {
            LastResolvedCount = 0;
            return CreateFailure(effect, "Effect queue head changed before resolution completed.");
        }

        TotalResolvedCount++;
        LastResolvedCount = 1;
        return result;
    }

    public async Task<IReadOnlyList<EffectResult>> ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        List<EffectResult> results = new();
        int resolvedCount = 0;

        while (HasPendingEffects)
        {
            EffectResult result = await ResolveNextAsync(cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Resolved)
            {
                break;
            }

            resolvedCount++;
        }

        LastResolvedCount = resolvedCount;
        return results;
    }

    private static Task<EffectResult> DefaultResolverAsync(
        EffectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EffectResult.Success());
    }

    private static EffectResult CreateFailure(
        PendingEffect effect,
        string message,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        var mergedValues = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["effectId"] = effect.Request.EffectId.Value,
            ["mode"] = effect.Mode.ToString(),
        };

        if (values is not null)
        {
            foreach (KeyValuePair<string, object?> pair in values)
            {
                mergedValues[pair.Key] = pair.Value;
            }
        }

        return EffectResult.Failure(message, mergedValues);
    }
}
