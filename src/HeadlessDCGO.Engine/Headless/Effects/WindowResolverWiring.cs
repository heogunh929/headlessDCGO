namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

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

        WindowResolverDeps deps = BuildSchedulerDeps(
            context, new FifoWindowChoicePort(), () => DrainSchedulerCutIns(context, collectorFactory));
        return new WindowResolver().RunWindowAsync(seed, deps, depth: 0, cancellationToken);
    }

    /// <summary>Re-collect the scheduler-path triggers emitted since the last pick (the cut-in drain): sync the
    /// zone-mover events into the queue, drain the pending game events, and collect each into triggers. Shared by
    /// the sync-window path and the main-loop deps so both drain cut-ins identically.</summary>
    private static IReadOnlyList<TimingWindowTrigger> DrainSchedulerCutIns(
        EngineContext context, Func<AutoProcessingTriggerCollector> collectorFactory)
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

    /// <summary>(Stage 5, Phase 3) Build the deps used to drive AND resume a MAIN-LOOP window: the production
    /// scheduler-path Gate / Commit / ResolveBody (as the sync windows), the live agent-driven
    /// <see cref="AgentWindowChoicePort"/> (order / optional choices routed through the choice controller), and the
    /// shared scheduler cut-in drain. The SAME builder is used by the main loop's initial drive and by the
    /// ResolveChoice resume, so a resumed window is driven with identical deps.
    /// (Phase 3b-ii will extend ResolveBody + the collect/drain to also dispatch the activated-effect bridge.)</summary>
    public static WindowResolverDeps BuildMainLoopDeps(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var port = new AgentWindowChoicePort(context.ChoiceController, context.WindowResolution);
        return BuildSchedulerDeps(
            context, port,
            () => DrainSchedulerCutIns(context, () => new AutoProcessingTriggerCollector(context.EffectRegistry)));
    }

    /// <summary>Counts effect resolutions (commits) across a live window run so the main loop can report progress
    /// to <c>RunToStableAsync</c> (which re-iterates while <c>resolved &gt; 0</c>).</summary>
    public sealed class LiveWindowRun
    {
        public int Progress { get; internal set; }
    }

    /// <summary>(3b-iii) Build the deps that DRIVE the LIVE main-loop window (the <c>AutoProcessAsync</c>
    /// replacement) and its resume. Same shape as <see cref="BuildMainLoopDeps"/> but with the full main-loop
    /// semantics: <see cref="GateLive"/>/<see cref="CommitLive"/> (end-game short-circuit + OnDeletion batch
    /// dedup), <see cref="ResolveBodyLiveAsync"/> (dispatch + one-shot fire-then-clear + F3 rule-processing
    /// between picks), and the UNIFIED cut-in drain (scheduler + activated markers). <paramref name="run"/>
    /// accumulates the resolution count for the caller's progress signal (optional on the resume path).</summary>
    public static WindowResolverDeps BuildLiveMainLoopDeps(EngineContext context, LiveWindowRun? run = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var port = new AgentWindowChoicePort(context.ChoiceController, context.WindowResolution);
        return new WindowResolverDeps(
            turnPlayerId: context.TurnController.Current.TurnPlayerId,
            gate: trigger => GateLive(context, trigger),
            commit: trigger =>
            {
                CommitLive(context, trigger);
                if (run is not null)
                {
                    run.Progress++;
                }
            },
            resolveBody: (trigger, ct) => ResolveBodyLiveAsync(context, trigger, ct),
            choicePort: port,
            drainNewTriggers: () => DrainUnifiedCutIns(context));
        // NOTE (A-1 / P1-4): NO skipCondition here — the main loop is AS-IS's general trigger process
        // (AutoProcessing.cs:137 passes skipCondition=null), which applies no same-effect dedup. Only the specific
        // cut-in windows pass HasExecutedSameEffect (via BuildSchedulerDeps). Do not "helpfully" add it here.
    }

    /// <summary>(3b-iii) Resolve a body for the LIVE loop: the shared dispatch (<see cref="ResolveBodyAsync"/>)
    /// then, only if it RESOLVED (not suspended), the per-pick post-processing AS-IS runs after every skill —
    /// a DELAYED one-shot player effect (AS-IS AddEffectToPlayer) has its binding removed now that it has fired
    /// (fire-then-clear, AFTER the body so the scheduler could still look it up), and state-based
    /// <see cref="GameFlowProcessor.RuleProcessAsync"/> runs BETWEEN picks (F3) so the next pass's gate + cut-in
    /// drain see the settled board (deletions, end-game).</summary>
    private static async Task<WindowResolveOutcome> ResolveBodyLiveAsync(
        EngineContext context, TimingWindowTrigger trigger, CancellationToken cancellationToken)
    {
        WindowResolveOutcome outcome = await ResolveBodyAsync(context, trigger, cancellationToken).ConfigureAwait(false);

        // (adversarial review P1, 2026-07-10) a SCHEDULER (bound-mutation) body that suspends returns Suspended,
        // which would make the window record an InFlightPick — but the LIVE loop has no path that re-drives a
        // window parked by a scheduler suspend (only the WindowChoice and DeferredActivations resumes exist), so
        // the remaining stack would be silently DROPPED, and the InFlightPick replay would re-enqueue the parked
        // scheduler head (double-resolve). Bound trigger reactors are non-interactive today; an interactive
        // reactor at a trigger timing MUST be an activated effect (which suspends as SuspendedExternally and
        // resumes via the activated-effect bridge / DeferredActivations). Enforce that invariant LOUDLY rather
        // than let it become a silent live divergence if a future card breaks it.
        if (outcome == WindowResolveOutcome.Suspended)
        {
            throw new NotSupportedException(
                "A scheduler-path trigger body suspended inside the trigger window. A bound mutation reactor must " +
                "be non-interactive; an interactive reactor at a trigger timing must be an activated effect " +
                "(resumed via the activated-effect bridge / DeferredActivations, not the window's in-flight pick).");
        }

        if (outcome != WindowResolveOutcome.Resolved)
        {
            return outcome; // SuspendedExternally — an activated body, resumed outside the window.
        }

        if (!IsActivatedBridge(trigger, out _, out _, out _, out _)
            && trigger.Request.Context.Values.TryGetValue(AutoProcessingTriggerCollector.DelayedOneShotKey, out object? oneShot)
            && oneShot is true)
        {
            HeadlessEntityId oneShotId = trigger.Request.EffectId;
            context.EffectRegistry.RemoveWhere(binding => binding.Request.EffectId == oneShotId);
        }

        // (F3) state-based rule processing between picks. INVARIANT: this is non-interactive — it sweeps
        // state-based deletions and end-game; would-be-deleted REPLACEMENT windows open at the RunToStable level
        // (DeletionReplacementTiming), not inside RuleProcessAsync (IsPreAwaiting cards are skipped).
        //
        // (A-4) ENFORCE that invariant rather than merely rely on it: if RuleProcessAsync ever raised an
        // agent-choice mid-window, DriveAsync catches only WindowChoicePendingException and would SILENTLY DROP the
        // window (the remaining stack + this frame), and a DeferredChoicePendingException would unwind even further.
        // Convert either into a LOUD failure — same policy as the scheduler-suspend guard above — so a future
        // interactive rule-process is a visible error, not a silent live divergence. A genuinely interactive
        // reactor must go through the window / activated-effect bridge, not state-based rule processing.
        try
        {
            await GameFlowProcessor.RuleProcessAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException)
        {
            throw new NotSupportedException(
                "State-based RuleProcessAsync raised an agent choice between window picks. Mid-window rule " +
                "processing MUST be non-interactive (it sweeps state-based deletions and end-game; replacement " +
                "windows open at RunToStable, not here). An interactive rule-process would be silently dropped by " +
                "the window driver — wire the reactor through the window / activated-effect bridge instead.", ex);
        }

        return WindowResolveOutcome.Resolved;
    }

    /// <summary>(3b-iii) The UNIFIED cut-in drain: sync the zone-mover events into the queue, drain them, and
    /// build the unified seed (scheduler triggers + activated markers) so a resolution's newly-emitted events
    /// re-enter the SAME window as a cut-in (RD-17), covering BOTH mutation and activated reactors.</summary>
    private static IReadOnlyList<TimingWindowTrigger> DrainUnifiedCutIns(EngineContext context)
    {
        context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
        IReadOnlyList<GameEvent> pending = context.GameEventQueue.DrainPending();
        if (pending.Count == 0)
        {
            return Array.Empty<TimingWindowTrigger>();
        }

        return CollectUnifiedSeed(context, pending);
    }

    /// <summary>(3b-iii) Build the UNIFIED window seed for a pass's drained events: the SCHEDULER half
    /// (<c>CollectAllTriggers</c> per event, de-duplicated by (effect-id, event-index) — RD-11's per-event
    /// SkillInfo — then enriched with the event subject + Kind reclassified from the bound effect) plus the
    /// ACTIVATED half (<see cref="CollectActivatedBridgeTriggers"/>). Unlike the batch collect, NO gate / cap /
    /// disable is applied here — those move to the window's per-pass Gate / commit-time Consume (P1-1: a trigger
    /// whose condition becomes true only after an earlier one resolves still fires this window).</summary>
    public static IReadOnlyList<TimingWindowTrigger> CollectUnifiedSeed(
        EngineContext context, IReadOnlyList<GameEvent> pendingEvents)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pendingEvents);

        var seed = new List<TimingWindowTrigger>();
        if (pendingEvents.Count > 0)
        {
            var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
            var seen = new HashSet<(HeadlessEntityId EffectId, int EventIndex)>();
            // (P0-2/RD-11) the AS-IS delete-PROCESS batch is ONE StackSkillInfo whose gate any-matches the whole
            // batch list — so an "on a Digimon deleted, +1 memory" effect is a SINGLE window entry firing once for
            // N simultaneous deletions, NOT N entries the player would be asked to order. The seed carries one
            // OnDeletion trigger per deleted card, so collapse them here to the FIRST that GATE-passes (enrich +
            // CanResolve on its subject), claimed on-fire so a subject-specific gate matching only a later deleted
            // card is not lost. This is the one collect-time gate the window keeps (the deletion condition is
            // stable within the pass — P1-1 re-evaluation does not apply); every other trigger stays ungated here
            // and is re-evaluated per pass by the window.
            var firedDeletion = new HashSet<HeadlessEntityId>();
            for (int eventIndex = 0; eventIndex < pendingEvents.Count; eventIndex++)
            {
                GameEvent gameEvent = pendingEvents[eventIndex];
                if (gameEvent.Type == GameEventType.Unknown)
                {
                    continue;
                }

                foreach (TimingWindowTrigger trigger in collector.CollectAllTriggers(gameEvent))
                {
                    if (!seen.Add((trigger.Request.EffectId, eventIndex)))
                    {
                        continue;
                    }

                    EffectRequest enriched = GameFlowProcessor.EnrichWithEventSubject(trigger.Request, gameEvent);

                    if (IsOnDeletion(trigger))
                    {
                        if (firedDeletion.Contains(trigger.Request.EffectId)
                            || EffectInvalidation.IsEffectsDisabled(context, trigger.Request.Context.SourceEntityId))
                        {
                            continue;
                        }

                        IHeadlessCardEffect? body = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect;
                        if (body is not null && !body.CanResolve(new CardEffectResolveContext(enriched)).CanResolve)
                        {
                            continue; // this deleted card's subject does not satisfy the gate — try the next copy.
                        }

                        firedDeletion.Add(trigger.Request.EffectId);
                    }

                    seed.Add(GameFlowProcessor.ReclassifyKind(
                        context,
                        new TimingWindowTrigger(enriched, trigger.Mode, trigger.Kind, trigger.Priority, trigger.Sequence)));
                }
            }
        }

        seed.AddRange(CollectActivatedBridgeTriggers(context, pendingEvents));
        return seed;
    }

    /// <summary>Build the scheduler-path deps for a window. <paramref name="choicePort"/> drives order/optional
    /// choices; <paramref name="drainNewTriggers"/> collects cut-in triggers emitted during resolution.</summary>
    public static WindowResolverDeps BuildSchedulerDeps(
        EngineContext context,
        IWindowChoicePort choicePort,
        Func<IReadOnlyList<TimingWindowTrigger>> drainNewTriggers,
        Func<IReadOnlyList<TimingWindowTrigger>, TimingWindowTrigger, bool>? skipCondition = null)
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
            drainNewTriggers: drainNewTriggers,
            skipCondition: skipCondition);
    }

    /// <summary>(A-1 / P1-4) The AS-IS cut-in <c>HasExecutedSameEffect</c> skip predicate (AutoProcessing.cs:623-627):
    /// suppress a candidate whose effect a prior commit in THIS window already resolved. AS-IS <c>IsSameEffect</c>
    /// (ICardEffect.cs:860) is "same effect instance OR same <c>EffectSourceCard</c> + same <c>HashString</c> + same
    /// root effect"; in the headless binding model that identity is the effect binding id (<c>Request.EffectId</c> —
    /// one binding per source-instance-and-effect), so same-EffectId is the faithful key. Pass this as
    /// <see cref="BuildSchedulerDeps"/>'s <c>skipCondition</c> ONLY for the specific cut-in windows AS-IS applies it
    /// to (TrashDigivolutionCards / TrashLinkCards / Unsuspend / SelectCount, CardController.cs:5189/5301/5709/727/
    /// 990) — NEVER the main-loop window (AS-IS AutoProcessing.cs:137 passes skipCondition=null, so the general
    /// trigger process dedups nothing; a blanket main-loop dedup would over-suppress and diverge from AS-IS).</summary>
    public static bool HasExecutedSameEffect(
        IReadOnlyList<TimingWindowTrigger> resolved, TimingWindowTrigger candidate)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(candidate);

        for (int i = 0; i < resolved.Count; i++)
        {
            if (resolved[i].Request.EffectId == candidate.Request.EffectId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a trigger can activate right now — the batch pipeline's collect-time predicate
    /// (AutoProcessAsync: IsEffectsDisabled + CanResolve + OnceFlags), re-evaluated per pass and at commit.
    /// An ACTIVATED-bridge marker gates itself inside <c>ActivatedEffectResolver</c> (the uniform ActivatedEffect
    /// case checks CanResolve + MaxCountPerTurn), so it passes here EXCEPT the OnUnTappedAnyone caller-cap.</summary>
    private static bool Gate(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner))
        {
            return MarkerGate(context, card, timing, owner);
        }

        return SchedulerGate(context, trigger);
    }

    /// <summary>(3b-iii live) the main-loop Gate: <see cref="Gate"/> plus an end-game short-circuit — once the
    /// match is decided no further trigger activates and the window exhausts (AS-IS RuleProcess ends the loop on
    /// end-game between picks). The OnDeletion delete-process batch dedup is done at COLLECT (CollectUnifiedSeed
    /// collapses the batch to one entry), NOT here — a window-scoped dedup would wrongly block a LATER cut-in
    /// delete-process (AS-IS fires each process separately).</summary>
    private static bool GateLive(EngineContext context, TimingWindowTrigger trigger)
    {
        if (context.RuleQueryService.IsTerminal())
        {
            return false;
        }

        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner))
        {
            return MarkerGate(context, card, timing, owner);
        }

        return SchedulerGate(context, trigger);
    }

    /// <summary>An activated-bridge marker gates itself in the resolver EXCEPT the OnUnTappedAnyone caller-cap
    /// (a card can be re-suspended/unsuspended within a turn — GameFlowProcessor:737-746), mirrored as a
    /// synthetic-key OnceFlag checked here (per pass) and consumed at commit (F5/RD-12).
    ///
    /// (RDx-A3 debt — adversarially verified 2026-07-10) GENUINE per-pass divergence: AS-IS re-checks each stacked
    /// effect's CanActivate (board CONDITION) EVERY pass (MultipleSkills.cs:122 / 164-165), excluding a
    /// currently-false effect from that pass's active set (so it does not compete for the order choice) yet keeping
    /// it stacked to re-test next pass. The scheduler half mirrors this (<see cref="SchedulerGate"/> re-checks
    /// body.CanResolve per pass); this MarkerGate does NOT — it returns true unconditionally (bar the OncePerTurn
    /// cap), deferring the condition to the resolver's own gate (ActivatedEffectResolver.cs:495 uniform
    /// CanResolve). Consequence: an activated effect whose CanActivate is board-dependent and false early but true
    /// after an earlier pick can be OFFERED and, if picked while false, no-ops in the resolver — losing AS-IS's
    /// per-pass deferral. LATENT (no ported activated effect flips CanActivate mid-window). NOT fixed here on
    /// purpose: a faithful per-pass gate must reuse the resolver's exact resolveCtx construction (triggerId +
    /// driving-event values, :469-495) — reproducing it approximately risks OVER/UNDER-gating the live main loop
    /// (a worse divergence than the latent gap). The faithful fix is to extract a shared CanActivateAt(card, timing,
    /// drivingEvent) from ActivatedEffectResolver's uniform gate and call it BOTH from the resolver and here.</summary>
    private static bool MarkerGate(EngineContext context, HeadlessEntityId card, EffectTiming timing, HeadlessPlayerId owner)
    {
        if (ActivatedBridgeTimings.OncePerTurn.Contains(timing))
        {
            return context.OnceFlags.CanActivate(SyntheticBridgeOnceRequest(card, timing, owner), maxCountPerTurn: 1);
        }

        return true;
    }

    /// <summary>A scheduler (mutation) trigger's gate: bound + not-disabled + CanResolve + once-cap available.</summary>
    private static bool SchedulerGate(EngineContext context, TimingWindowTrigger trigger)
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

    private static bool IsOnDeletion(TimingWindowTrigger trigger) =>
        string.Equals(trigger.Request.Timing, TriggerTimings.OnDeletion, StringComparison.Ordinal);

    /// <summary>Consume the once-per-turn use at commit (before the body — RD-12/F5). For an ACTIVATED-bridge
    /// marker only the OnUnTappedAnyone caller-cap is consumed here (its synthetic key); every other activated
    /// timing is uncapped at the caller (the resolver's own MaxCountPerTurn caps it).</summary>
    private static void Commit(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner))
        {
            MarkerCommit(context, card, timing, owner);
            return;
        }

        SchedulerCommit(context, trigger);
    }

    /// <summary>(3b-iii live) the main-loop Commit — currently identical to <see cref="Commit"/> (the OnDeletion
    /// batch dedup is handled at collect, not here); kept distinct so live-only commit concerns have a home.</summary>
    private static void CommitLive(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner))
        {
            MarkerCommit(context, card, timing, owner);
            return;
        }

        SchedulerCommit(context, trigger);
    }

    private static void MarkerCommit(EngineContext context, HeadlessEntityId card, EffectTiming timing, HeadlessPlayerId owner)
    {
        if (ActivatedBridgeTimings.OncePerTurn.Contains(timing))
        {
            context.OnceFlags.Consume(SyntheticBridgeOnceRequest(card, timing, owner), maxCountPerTurn: 1);
        }
    }

    private static void SchedulerCommit(EngineContext context, TimingWindowTrigger trigger)
    {
        int? maxCountPerTurn = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect?.Definition.MaxCountPerTurn;
        context.OnceFlags.Consume(trigger.Request, maxCountPerTurn);
    }

    /// <summary>Resolve one effect body — DISPATCHING on the trigger kind (Stage-5 RD 3b-ii unification):
    /// an ACTIVATED-bridge marker routes to <c>ActivatedEffectResolver</c> (the same seam the batch
    /// <c>BridgeActivatedTriggersAsync</c> and the action handlers use); every other trigger is a scheduler
    /// (mutation) body. A scheduler body enqueues THIS trigger then drains the scheduler
    /// (<c>ResolveAllAsync</c>) so it resolves this request plus any scheduler-cut-in it enqueues, leaving the
    /// queue empty for the next pick. Either resolver's suspend surfaces here as
    /// <see cref="WindowResolveOutcome.Suspended"/>.</summary>
    private static async Task<WindowResolveOutcome> ResolveBodyAsync(
        EngineContext context, TimingWindowTrigger trigger, CancellationToken cancellationToken)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out GameEvent? drivingEvent, out HeadlessPlayerId owner))
        {
            try
            {
                await Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver
                    .ResolveAsync(context, card, owner, timing, cancellationToken, drivingEvent: drivingEvent)
                    .ConfigureAwait(false);
                return WindowResolveOutcome.Resolved;
            }
            catch (DeferredChoicePendingException)
            {
                // The interactive activated body suspended for an agent choice. Record it EXACTLY as the batch
                // bridge did (GameFlowProcessor:754-761) so the same resume seam (MetadataActionProcessor
                // .ResolveChoiceAsync -> DeferredActivations.Pending) advances it — the body resumes OUTSIDE the
                // window (SuspendedExternally), so the window records no in-flight pick and, once the action
                // processor finishes the activation, re-drives to continue the remaining stack (3b-iii).
                context.DeferredActivations.Suspend(card, timing, owner, drivingEvent);
                return WindowResolveOutcome.SuspendedExternally;
            }
        }

        context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
        IReadOnlyList<EffectResult> results = await context.EffectScheduler.ResolveAllAsync(cancellationToken).ConfigureAwait(false);
        return results.Any(r => r.IsSuspended) ? WindowResolveOutcome.Suspended : WindowResolveOutcome.Resolved;
    }

    // ---- (RD 3b-ii) activated-effect bridge markers ------------------------------------------------------
    //
    // A card's ACTIVATED effects (draw/trash/delete/select) at a general trigger timing resolve through
    // ActivatedEffectResolver, not through a bound IHeadlessCardEffect. To fold them into the ONE window seed
    // (alongside the scheduler mutation triggers), each is carried as a marker-bearing TimingWindowTrigger:
    // the card is the request's SourceEntityId, the timing is the request's Timing, and a marker in the
    // resolve-context Values flags it (with its category + driving event). Gate / Commit / ResolveBody branch
    // on the marker. This mirrors the batch pipeline's BridgeActivatedTriggersAsync scan + resolve, but split
    // into collect (CollectActivatedBridgeTriggers) + per-pick dispatch so a suspend is resumable.

    /// <summary>Marker (in the trigger's resolve-context Values) flagging an activated-effect bridge trigger.</summary>
    public const string ActivatedBridgeKey = "activatedBridge";

    /// <summary>The bridge category (<see cref="ActivatedBridgeCategory"/> name) — informational; the caller cap
    /// keys off the timing (<see cref="ActivatedBridgeTimings.OncePerTurn"/>), not the category.</summary>
    public const string ActivatedBridgeCategoryKey = "activatedBridge.category";

    /// <summary>The driving <see cref="GameEvent"/> (for EVENT-BROADCAST timings) threaded to the resolver so
    /// per-card gates read the event subject + metadata; absent for subject/boundary timings.</summary>
    public const string ActivatedBridgeDrivingEventKey = "activatedBridge.drivingEvent";

    /// <summary>How the reacting card was found for an activated-bridge trigger (mirrors the batch scan's three
    /// branches). Carried on the marker for diagnosis; the actual cap model keys off the timing.</summary>
    public enum ActivatedBridgeCategory
    {
        /// <summary>The event's subject card resolves (attacker / deleted card) — once per event.</summary>
        Subject,

        /// <summary>Every battle-area card resolves at a turn boundary (no subject) — once per turn naturally.</summary>
        Boundary,

        /// <summary>Every battle-area card resolves per driving event; each card self-gates on the event.</summary>
        Broadcast,
    }

    /// <summary>Read the activated-bridge marker off a trigger. Returns false for a normal scheduler trigger.
    /// On true, <paramref name="card"/> = the reacting card, <paramref name="timing"/> = the parsed
    /// <see cref="EffectTiming"/>, <paramref name="drivingEvent"/> = the threaded event (or null),
    /// <paramref name="owner"/> = the card's owner (the resolver's controller).</summary>
    private static bool IsActivatedBridge(
        TimingWindowTrigger trigger,
        out HeadlessEntityId card,
        out EffectTiming timing,
        out GameEvent? drivingEvent,
        out HeadlessPlayerId owner)
    {
        card = default;
        timing = default;
        drivingEvent = null;
        owner = default;

        IReadOnlyDictionary<string, object?> values = trigger.Request.Context.Values;
        if (!values.TryGetValue(ActivatedBridgeKey, out object? marker) || marker is not true)
        {
            return false;
        }

        card = trigger.Request.Context.SourceEntityId;
        owner = trigger.Request.Context.OwnerPlayerId;
        if (!Enum.TryParse(trigger.Request.Timing, ignoreCase: false, out timing) || !Enum.IsDefined(timing))
        {
            return false;
        }

        if (values.TryGetValue(ActivatedBridgeDrivingEventKey, out object? ev))
        {
            drivingEvent = ev as GameEvent;
        }

        return true;
    }

    /// <summary>The synthetic once-per-turn key for a caller-capped bridge timing (OnUnTappedAnyone) — EXACTLY
    /// the AS-IS key (GameFlowProcessor:739-741) so the two paths share the cap if they ever coexist.</summary>
    private static EffectRequest SyntheticBridgeOnceRequest(HeadlessEntityId card, EffectTiming timing, HeadlessPlayerId owner) =>
        new(new HeadlessEntityId($"{card.Value}:bridgeOnce:{timing}"), owner, timing.ToString(), new EffectContext(owner, card));

    /// <summary>Build one activated-bridge marker trigger for a (card, timing) resolution. The synthetic effect
    /// id embeds the collect <paramref name="sequence"/> so distinct broadcast events (same card + timing) stay
    /// distinct triggers (AS-IS opens one window per event) and order-choice keys never collide.</summary>
    public static TimingWindowTrigger MakeActivatedBridgeTrigger(
        HeadlessEntityId card,
        EffectTiming timing,
        HeadlessPlayerId owner,
        GameEvent? drivingEvent,
        ActivatedBridgeCategory category,
        long sequence)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ActivatedBridgeKey] = true,
            [ActivatedBridgeCategoryKey] = category.ToString(),
        };
        if (drivingEvent is not null)
        {
            values[ActivatedBridgeDrivingEventKey] = drivingEvent;
        }

        var request = new EffectRequest(
            new HeadlessEntityId($"activatedBridge:{card.Value}:{timing}:{sequence}"),
            owner,
            timing.ToString(),
            new EffectContext(owner, owner, card, triggerEntityId: null, targetEntityIds: null, values: values));

        // Mandatory at the WINDOW level: the bridge always runs the resolver; any optional (yes/no) lives INSIDE
        // the resolver (uniform ActivatedEffect IsOptional -> Activate_Optional), matching the batch bridge which
        // calls ResolveAsync unconditionally.
        return new TimingWindowTrigger(request, EffectResolutionMode.MainStack, TimingWindowTriggerKind.Mandatory, priority: 0, sequence);
    }

    /// <summary>(RD 3b-ii) Synthesise the activated-effect bridge triggers for a pass's pending events —
    /// the ACTIVATED half of the unified window seed. Mirrors the batch <c>BridgeActivatedTriggersAsync</c>
    /// scan EXACTLY (subject-scoped: the event subject, de-duplicated; boundary: every battle-area card,
    /// de-duplicated; event-broadcast: every battle-area card PER event, no cross-event de-dup), but emits
    /// markers instead of resolving inline — the OnUnTappedAnyone cap that the batch scan consumed up-front now
    /// gates in <see cref="Gate"/>/<see cref="Commit"/>. Cards without a valid owning instance are skipped
    /// (same as the batch resolve loop).</summary>
    public static IReadOnlyList<TimingWindowTrigger> CollectActivatedBridgeTriggers(
        EngineContext context, IReadOnlyList<GameEvent> pendingEvents)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pendingEvents);
        if (pendingEvents.Count == 0)
        {
            return Array.Empty<TimingWindowTrigger>();
        }

        var toResolve = new List<(HeadlessEntityId Card, EffectTiming Timing, GameEvent? Event, ActivatedBridgeCategory Category)>();
        var seen = new HashSet<(HeadlessEntityId, EffectTiming)>();
        IZoneStateReader? zones = context.ZoneMover as IZoneStateReader;
        foreach (GameEvent gameEvent in pendingEvents)
        {
            foreach (string timingName in TriggerTimingMap.Derive(gameEvent))
            {
                if (!Enum.TryParse(timingName, ignoreCase: false, out EffectTiming timing))
                {
                    continue;
                }

                if (ActivatedBridgeTimings.SubjectScoped.Contains(timing))
                {
                    if (gameEvent.Subject is { IsEmpty: false } subject && seen.Add((subject, timing)))
                    {
                        toResolve.Add((subject, timing, null, ActivatedBridgeCategory.Subject));
                    }
                }
                else if (ActivatedBridgeTimings.Boundary.Contains(timing) && zones is not null)
                {
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.BattleArea))
                        {
                            if (seen.Add((cardId, timing)))
                            {
                                toResolve.Add((cardId, timing, null, ActivatedBridgeCategory.Boundary));
                            }
                        }
                    }
                }
                else if (ActivatedBridgeTimings.EventBroadcast.Contains(timing) && zones is not null)
                {
                    // No cross-event de-dup on purpose: each event is its own window. Within one event the
                    // battle-area scan visits a card exactly once.
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.BattleArea))
                        {
                            toResolve.Add((cardId, timing, gameEvent, ActivatedBridgeCategory.Broadcast));
                        }
                    }
                }
            }
        }

        var triggers = new List<TimingWindowTrigger>(toResolve.Count);
        long sequence = 0;
        foreach ((HeadlessEntityId card, EffectTiming timing, GameEvent? drivingEvent, ActivatedBridgeCategory category) in toResolve)
        {
            if (!context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? instance)
                || instance is null || instance.OwnerId.IsEmpty)
            {
                continue;
            }

            // (3b-iii) only bridge a card that ACTUALLY reacts at this timing — the batch scan visited every
            // battle-area card and let the resolver no-op, but in the ONE window a no-op marker would spuriously
            // compete with a real effect for the player's order choice (RD-14). This preserves the batch's
            // resolution outcome (no-effect cards did nothing) while removing the phantom stack entries.
            //
            // (A-2 / RD-6) mark ONLY a card with a genuine ACTIVATED effect (IActivatedCardEffect) at this timing —
            // the resolver's domain. HasActivatedEffectsAt EXCLUDES a scheduler-only effect (IHeadlessCardEffect,
            // e.g. TriggeredGainMemoryEffect / BT1_021 EoTLose3Memory): that effect is already collected by the
            // SCHEDULER half of the unified seed (a registered binding), so an activated marker for it would
            // DOUBLE-collect — the marker only no-ops in the resolver yet still competes for the window's order
            // choice (a spurious pending choice that suspends the pre-flip end-of-turn drain). Formerly this used
            // HasEffectsAt (ANY effect existence), which double-collected such cards; the double was masked while
            // the [End of Turn] window drained POST-flip (the memory body no-opped on the wrong owner anyway).
            //
            // (A-3, adversarially verified 2026-07-10) The reactivity check stays at COLLECT (not per-pass) — that
            // is AS-IS-FAITHFUL: AS-IS collects effect existence once too (GetSkillInfos / EffectList(timing),
            // AutoProcessing.cs:770-857); its window loop re-checks only CanActivate on ALREADY-stacked entries
            // (MultipleSkills.cs:122/164-165) and NEVER re-collects existence for the original timing. Moving it
            // per-pass would DIVERGE (admit an entry AS-IS never collects). The genuine per-pass gap is a DIFFERENT
            // thing — the marker's CanActivate/CanResolve is not re-tested per pass in MarkerGate (RDx-A3 debt).
            if (!Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver.HasActivatedEffectsAt(context, card, instance.OwnerId, timing))
            {
                continue;
            }

            triggers.Add(MakeActivatedBridgeTrigger(card, timing, instance.OwnerId, drivingEvent, category, sequence++));
        }

        return triggers;
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
