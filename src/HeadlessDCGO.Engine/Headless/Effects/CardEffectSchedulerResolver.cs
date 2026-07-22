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
        Func<EffectRequest, IEffectMutationSink>? sinkFactory = null,
        HeadlessDCGO.Engine.Headless.Runtime.IDeferredChoiceCoordinator? choiceCoordinator = null,
        bool strictUnbound = false)
    {
        // (④) The invented EffectRegistry is deleted. Its producer had already reached 0, so the
        // `registry.Find(request.EffectId)?.Effect` lookup ALWAYS returned null in production — this resolver
        // ALWAYS took the Unbound (strict→Failure) branch. Live effect bodies never rode this scheduler-resolve
        // path (they resolve through the trigger scheduler / direct card flow). Reduced to the always-unbound
        // branch, byte-for-byte with the prior behavior (sinkFactory/choiceCoordinator now inert).
        _ = sinkFactory;
        _ = choiceCoordinator;
        return (request, cancellationToken) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            // G3.5-RL-A4: strict gate — in test/dev a missing effect body is a hard FAILURE so the
            // coverage gap is caught immediately. Production keeps the lenient (countable) Unbound behaviour.
            if (strictUnbound)
            {
                return Task.FromResult(EffectResult.Failure(
                    $"Strict effect gate: no card effect body bound to '{request.EffectId.Value}' (timing '{request.Timing}').",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["effectId"] = request.EffectId.Value,
                        ["timing"] = request.Timing,
                        ["strictUnbound"] = true,
                    }));
            }

            // G3.5-RL-B3: report unbound (skeleton) effects as a distinct, countable status
            // instead of a silent success, while still letting the queue drain.
            return Task.FromResult(EffectResult.Unbound(
                "No card effect body bound to request; skipped.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["effectId"] = request.EffectId.Value,
                    ["timing"] = request.Timing,
                    ["unresolved"] = true,
                }));
        };
    }
}
