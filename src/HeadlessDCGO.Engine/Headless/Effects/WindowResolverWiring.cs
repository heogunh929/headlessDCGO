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

    /// <summary>Resolve one effect body through the production scheduler (enqueue-one + resolve-one). The
    /// scheduler's resolver applies the mutation sink and flushes; a suspend surfaces as a pending choice.</summary>
    private static async Task<WindowResolveOutcome> ResolveBodyAsync(
        EngineContext context, TimingWindowTrigger trigger, CancellationToken cancellationToken)
    {
        context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
        EffectResult result = await context.EffectScheduler.ResolveNextAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuspended ? WindowResolveOutcome.Suspended : WindowResolveOutcome.Resolved;
    }
}
