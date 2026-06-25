namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// Bridges the queue-only <see cref="EffectScheduler"/> to actual card effect bodies.
/// Given a dequeued <see cref="EffectRequest"/>, looks up the bound
/// <see cref="IHeadlessCardEffect"/> via the registry and resolves it.
/// Requests with no bound effect body are treated as a no-op so the queue keeps
/// draining during incremental Phase 3.5 wiring.
/// </summary>
public static class CardEffectSchedulerResolver
{
    public static Func<EffectRequest, CancellationToken, Task<EffectResult>> Create(
        EffectRegistry registry,
        HeadlessCardEffectResolver? cardEffectResolver = null,
        Func<EffectRequest, IEffectMutationSink>? sinkFactory = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        HeadlessCardEffectResolver resolver = cardEffectResolver ?? new HeadlessCardEffectResolver();
        Func<EffectRequest, IEffectMutationSink> createSink =
            sinkFactory ?? (_ => new RecordingEffectMutationSink());

        return async (request, cancellationToken) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            EffectBinding? binding = registry.Find(request.EffectId);
            if (binding?.Effect is not { } effect)
            {
                return EffectResult.Success(
                    "No card effect body bound to request; skipped.",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["effectId"] = request.EffectId.Value,
                        ["timing"] = request.Timing,
                        ["unresolved"] = true,
                    });
            }

            IEffectMutationSink sink = createSink(request)
                ?? throw new InvalidOperationException("Effect mutation sink factory returned null.");

            EffectResult result = await resolver
                .ResolveAsync(effect, request, sink, cancellationToken)
                .ConfigureAwait(false);

            return WithSinkMetadata(result, sink);
        };
    }

    private static EffectResult WithSinkMetadata(EffectResult result, IEffectMutationSink sink)
    {
        Dictionary<string, object?>? extra = sink switch
        {
            RecordingEffectMutationSink recording => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mutationCount"] = recording.Count,
            },
            MatchStateMutationSink applied => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mutationCount"] = applied.AppliedCount,
                ["appliedMutationCount"] = applied.AppliedCount,
                ["unsupportedMutationCount"] = applied.UnsupportedCount,
                ["skippedMutationCount"] = applied.SkippedCount,
            },
            _ => null,
        };

        if (extra is null)
        {
            return result;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in result.Values)
        {
            values[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, object?> pair in extra)
        {
            values[pair.Key] = pair.Value;
        }

        return new EffectResult(result.Resolved, result.Message, values);
    }
}
