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
        Func<EffectRequest, IEffectMutationSink>? sinkFactory = null,
        HeadlessDCGO.Engine.Headless.Runtime.IDeferredChoiceCoordinator? choiceCoordinator = null,
        bool strictUnbound = false)
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
                // G3.5-RL-A4: strict gate — in test/dev a missing effect body is a hard FAILURE so the
                // coverage gap is caught immediately during Phase 4 porting instead of silently
                // draining as Unbound. Production keeps the lenient (countable) Unbound behaviour.
                if (strictUnbound)
                {
                    return EffectResult.Failure(
                        $"Strict effect gate: no card effect body bound to '{request.EffectId.Value}' (timing '{request.Timing}').",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["effectId"] = request.EffectId.Value,
                            ["timing"] = request.Timing,
                            ["strictUnbound"] = true,
                        });
                }

                // G3.5-RL-B3: report unbound (skeleton) effects as a distinct, countable status
                // instead of a silent success, while still letting the queue drain.
                return EffectResult.Unbound(
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

            // W7: harvest any agent answer supplied for a prior suspension and rewind the replay cursor
            // so this attempt replays choices in order.
            choiceCoordinator?.BeginResolution();

            EffectResult result;
            try
            {
                result = await resolver
                    .ResolveAsync(effect, request, sink, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HeadlessDCGO.Engine.Headless.Runtime.DeferredChoicePendingException ex)
            {
                // W7: the effect asked the agent for a choice. Report Suspended (Resolved=false) so the
                // scheduler leaves it queued and re-runs it once the agent answers. Do NOT flush the
                // sink or complete the resolution — the effect has not finished.
                return EffectResult.Suspended(
                    ex.Message,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["effectId"] = request.EffectId.Value,
                        ["timing"] = request.Timing,
                        ["deferredChoice"] = true,
                    });
            }

            // W2-follow: apply any asynchronous operations (zone moves, draws) the sink deferred.
            await sink.FlushAsync(cancellationToken).ConfigureAwait(false);

            // W7: the effect ran to completion — discard its accumulated answers before the next one.
            choiceCoordinator?.CompleteResolution();

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
