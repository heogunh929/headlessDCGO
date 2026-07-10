namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (Stage 5, Phase 2) Builds a production <see cref="WindowResolverDeps"/> from an <see cref="EngineContext"/>.
/// This first increment wires the SCHEDULER path only — triggers whose effect is a bound
/// <see cref="IHeadlessCardEffect"/> (the memory/DP mutation reactors resolved today by
/// <c>EffectScheduler.ResolveAllAsync</c>). The activated-effect bridge path (draw/trash/select via
/// <c>ActivatedEffectResolver</c>) is unified in a later increment.
///
/// The three gate/cap/resolve delegates lift the exact predicates the batch pipeline uses
/// (<c>GameFlowProcessor.AutoProcessAsync</c>): Gate = CanResolve + not-disabled + once-cap available; Commit =
/// OnceFlags.Consume (fired at commit, before the body, per RD-12/F5); ResolveBody = enqueue one + resolve one
/// through the production scheduler (which applies the sink + flush).
/// </summary>
public static class WindowResolverWiring
{
    /// <summary>(Phase 2 cut-over) Resolve a SYNCHRONOUS subject-scoped window (knock-out / start-battle / …)
    /// through the WindowResolver, behaviourally EQUIVALENT to the legacy
    /// <c>CollectAndEnqueueAll + ResolveAllAsync</c>: the legacy sync path enqueues the collected triggers in
    /// collection order (no MandatoryEffectOrdering, no optional prompt) and drains them, so a <see
    /// cref="FifoWindowChoicePort"/> (always pick the first active, auto-accept optionals) reproduces it exactly
    /// while routing through the new loop. Cut-ins the resolutions emit as EVENTS are drained via
    /// <paramref name="drainEvents"/>; scheduler self-enqueues are drained inside <c>ResolveBody</c>.</summary>
    public static Task RunSyncWindowAsync(
        EngineContext context,
        GameEvent windowEvent,
        Func<AutoProcessingTriggerCollector> collectorFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(windowEvent);
        ArgumentNullException.ThrowIfNull(collectorFactory);

        IReadOnlyList<TimingWindowTrigger> seed = collectorFactory().CollectAllTriggers(windowEvent);
        if (seed.Count == 0)
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<TimingWindowTrigger> DrainEvents()
        {
            context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
            IReadOnlyList<GameEvent> pending = context.GameEventQueue.DrainPending();
            if (pending.Count == 0)
            {
                return Array.Empty<TimingWindowTrigger>();
            }

            var collector = collectorFactory();
            var next = new List<TimingWindowTrigger>();
            foreach (GameEvent ev in pending)
            {
                next.AddRange(collector.CollectAllTriggers(ev));
            }

            return next;
        }

        WindowResolverDeps deps = BuildSchedulerDeps(context, new FifoWindowChoicePort(), DrainEvents);
        return new WindowResolver().RunWindowAsync(seed, deps, depth: 0, cancellationToken);
    }

    /// <summary>Build the scheduler-path deps for a window. <paramref name="choicePort"/> drives order/optional
    /// choices; <paramref name="drainNewTriggers"/> collects cut-in triggers emitted during resolution.</summary>
    public static WindowResolverDeps BuildSchedulerDeps(
        EngineContext context,
        IWindowChoicePort choicePort,
        Func<IReadOnlyList<TimingWindowTrigger>> drainNewTriggers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(choicePort);
        ArgumentNullException.ThrowIfNull(drainNewTriggers);

        return new WindowResolverDeps(
            turnPlayerId: context.TurnController.Current.TurnPlayerId,
            gate: trigger => Gate(context, trigger),
            commit: trigger => Commit(context, trigger),
            resolveBody: (trigger, ct) => ResolveBodyAsync(context, trigger, ct),
            choicePort: choicePort,
            drainNewTriggers: drainNewTriggers);
    }

    /// <summary>Whether a trigger can activate right now — the batch pipeline's collect-time predicate
    /// (AutoProcessAsync: IsEffectsDisabled + CanResolve + OnceFlags), re-evaluated per pass and at commit.</summary>
    private static bool Gate(EngineContext context, TimingWindowTrigger trigger)
    {
        IHeadlessCardEffect? body = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect;
        if (body is null)
        {
            return false;
        }

        if (EffectInvalidation.IsEffectsDisabled(context, trigger.Request.Context.SourceEntityId))
        {
            return false;
        }

        if (!body.CanResolve(new CardEffectResolveContext(trigger.Request)).CanResolve)
        {
            return false;
        }

        return context.OnceFlags.CanActivate(trigger.Request, body.Definition.MaxCountPerTurn);
    }

    /// <summary>Consume the once-per-turn use at commit (before the body — RD-12/F5).</summary>
    private static void Commit(EngineContext context, TimingWindowTrigger trigger)
    {
        int? maxCountPerTurn = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect?.Definition.MaxCountPerTurn;
        context.OnceFlags.Consume(trigger.Request, maxCountPerTurn);
    }

    /// <summary>Resolve one effect body through the production scheduler. Enqueue THIS trigger then drain the
    /// scheduler (<c>ResolveAllAsync</c>) so it resolves this request plus any scheduler-cut-in it enqueues,
    /// leaving the queue empty for the next pick — using <c>ResolveNextAsync</c> would resolve the queue HEAD,
    /// which is wrong if a prior body left a cut-in queued. The scheduler resolver applies the mutation sink and
    /// flushes; a suspend parks the head and surfaces here as <see cref="WindowResolveOutcome.Suspended"/>.</summary>
    private static async Task<WindowResolveOutcome> ResolveBodyAsync(
        EngineContext context, TimingWindowTrigger trigger, CancellationToken cancellationToken)
    {
        context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
        IReadOnlyList<EffectResult> results = await context.EffectScheduler.ResolveAllAsync(cancellationToken).ConfigureAwait(false);
        return results.Any(r => r.IsSuspended) ? WindowResolveOutcome.Suspended : WindowResolveOutcome.Resolved;
    }
}

/// <summary>(Phase 2) The equivalence port for a sync-window cut-over: reproduce the legacy
/// collection-order FIFO drain — always pick the first active trigger, always accept optionals (the legacy sync
/// path enqueued optionals directly, with no yes/no prompt). The interactive agent-driven port (RD-14/15 order
/// choice, RD-13 yes/no) is introduced when the MAIN loop cuts over (Phase 3).</summary>
public sealed class FifoWindowChoicePort : IWindowChoicePort
{
    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken cancellationToken) =>
        Task.FromResult<int?>(0);

    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
