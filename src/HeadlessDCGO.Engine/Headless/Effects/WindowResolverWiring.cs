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

    /// <summary>(F1-M1 P1-2) Resolve the attack security-CHECK window for ONE revealed security card. Unlike the
    /// plain <see cref="RunSyncWindowAsync"/> (which seeds ONLY the scheduler collector and drains scheduler cut-ins,
    /// so the revealed card's ACTIVATED OnLoseSecurity is DROPPED — the check reveal's CardMoved is consumed by the
    /// scheduler-only drain and never re-collected), this seeds the UNIFIED seed and drains UNIFIED cut-ins:
    /// <list type="bullet">
    /// <item>the synthetic OnSecurityCheck window event → the checked card's OnSecurityCheck scheduler reactors
    /// (AS-IS <c>triggeredSkillInfos</c> = OnSecurityCheck SkillInfos, CardController.cs:3954-3957);</item>
    /// <item>the already-queued reveal CardMoved (Security→Trash) → its OnLoseSecurity/OnDiscardSecurity scheduler
    /// AND ACTIVATED reactors (AS-IS merges the per-card OnLoseSecurity SkillInfos into the SAME resolution via
    /// <c>IReduceSecurity(ref triggeredSkillInfos)</c>, CardController.cs:3982-3985 → :5448).</item>
    /// </list>
    /// Draining the reveal event UP FRONT also prevents the later main-loop drain from re-firing it (no double).
    /// AS-IS stacks both timings together and resolves them in one AutoProcessCheck, so a FIFO port (equivalence with
    /// the legacy sync path) reproduces it; an interactive activated reactor suspends externally (DeferredActivations)
    /// exactly as in the main-loop window.</summary>
    public static Task RunSecurityCheckWindowAsync(
        EngineContext context,
        GameEvent windowEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(windowEvent);

        // Drain the pending reveal event(s) so their unified (scheduler + activated) triggers merge into THIS window.
        context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
        IReadOnlyList<GameEvent> pending = context.GameEventQueue.DrainPending();

        var seed = new List<TimingWindowTrigger>();
        seed.AddRange(new AutoProcessingTriggerCollector(context.EffectRegistry).CollectAllTriggers(windowEvent));
        seed.AddRange(CollectUnifiedSeed(context, pending));
        if (seed.Count == 0)
        {
            return Task.CompletedTask;
        }

        WindowResolverDeps deps = BuildSchedulerDeps(
            context, new FifoWindowChoicePort(), () => DrainUnifiedCutIns(context));
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

        if (!IsActivatedBridge(trigger, out _, out _, out _, out _, out _)
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
            // (A-4 F3, scope corrected per the 2026-07-11 adversarial review) this loud guard enforces a HEADLESS
            // design constraint, NOT an AS-IS invariant: AS-IS RuleProcess IS interactive on at least two paths —
            // the link-overflow trim opens a mandatory select (AutoProcessing.cs:526-541 →
            // Permanent.RemoveLinkedCard → SelectCardEffect canNoSelect:false, Permanent.cs:1321-1344), and the
            // DP-lack deletion runs the would-be-deleted cut-in INLINE (AutoProcessing.cs:469-484 →
            // DestroyPermanentsClass.Destroy → autoProcessing_CutIn.TriggeredSkillProcess, CardController.cs:
            // ~3690-3718). Neither path is ported yet (headless has no link trimming; would-be-deleted defers to
            // RunToStable), so this throw is currently unreachable — but the moment either is ported 1:1, the
            // ported path must suspend/resume through the window machinery instead of tripping this guard.
            throw new NotSupportedException(
                "State-based RuleProcessAsync raised an agent choice between window picks. The HEADLESS rule " +
                "sweep is modeled non-interactive (replacement windows open at RunToStable, not here) — an " +
                "interactive rule-process would be silently dropped by the window driver. AS-IS RuleProcess IS " +
                "interactive on the link-trim / DP-lack cut-in paths; porting those requires window-driven " +
                "suspend/resume here, not this guard.", ex);
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
            // (D-1 / VR-8) key the collapse by (EffectId, delete-BATCH id): N cards of ONE delete-process (one
            // Destroy(), one batch id) still collapse to one fire, but an INDEPENDENT delete-process in the same
            // drain (a distinct batch id) fires the reactor again (AS-IS stacks each Destroy() separately).
            var firedDeletion = new HashSet<(HeadlessEntityId EffectId, long BatchId)>();
            for (int eventIndex = 0; eventIndex < pendingEvents.Count; eventIndex++)
            {
                GameEvent gameEvent = pendingEvents[eventIndex];
                if (gameEvent.Type == GameEventType.Unknown)
                {
                    continue;
                }

                // (F1-Tier2 OnEndAttack / design item F1-ENDATTACK-HOOK) the SCHEDULER half of OnEndAttack is owned by
                // EndAttackTriggerHook, which runs INLINE (off-queue) in AttackPipeline.AdvanceEndAttackAsync and
                // enqueues bound OnEndAttack reactors directly to the scheduler. The queued OnEndAttack event exists
                // ONLY to open the ACTIVATED bridge (CollectActivatedBridgeTriggers, below), so skip the scheduler
                // collect for it — otherwise a bound (IHeadlessCardEffect) OnEndAttack reactor would be collected TWICE
                // (once by the hook, once here) and fire twice. Activated effects are never registered (the bridge
                // reaches them via the separate scan), so this skip does not affect the activated half. 0 production
                // bound OnEndAttack reactors exist today; the PRIM-P0 NewTimingsFire fixture (a bound memory probe)
                // proves the single fire. The faithful long-term fix is to retire the hook so the unified seed owns
                // both halves (design item F1-ENDATTACK-HOOK).
                if (TriggerTimingMap.Derive(gameEvent).Contains(EndAttackTriggerHook.OnEndAttackTiming))
                {
                    continue;
                }

                long deletionBatchId = ReadDeletionBatchId(gameEvent);
                foreach (TimingWindowTrigger trigger in collector.CollectAllTriggers(gameEvent))
                {
                    if (!seen.Add((trigger.Request.EffectId, eventIndex)))
                    {
                        continue;
                    }

                    EffectRequest enriched = GameFlowProcessor.EnrichWithEventSubject(trigger.Request, gameEvent);

                    if (IsOnDeletion(trigger))
                    {
                        if (firedDeletion.Contains((trigger.Request.EffectId, deletionBatchId))
                            || EffectInvalidation.IsEffectsDisabled(context, trigger.Request.Context.SourceEntityId))
                        {
                            continue;
                        }

                        IHeadlessCardEffect? body = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect;
                        if (body is not null && !body.CanResolve(new CardEffectResolveContext(enriched)).CanResolve)
                        {
                            continue; // this deleted card's subject does not satisfy the gate — try the next copy.
                        }

                        firedDeletion.Add((trigger.Request.EffectId, deletionBatchId));
                    }

                    // (D-1 order) stamp the delete-batch id onto a deletion-derived trigger so the window loop
                    // sequences CROSS-batch deletions (ascending batch order) instead of opening a spurious
                    // cross-batch order choice. Only OnDestroyedAnyone (the collapsed bystander OnDeletion) and only
                    // a REAL id (non-zero) — an unstamped move (sentinel 0) stays batch-less (unaffected).
                    long? orderBatch = IsOnDeletion(trigger) && deletionBatchId != 0 ? deletionBatchId : null;
                    seed.Add(GameFlowProcessor.ReclassifyKind(
                        context,
                        new TimingWindowTrigger(enriched, trigger.Mode, trigger.Kind, trigger.Priority, trigger.Sequence)
                            { BatchId = orderBatch }));
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

    /// <summary>(A-1 / P1-4, equivalence fixed per the 2026-07-11 adversarial review) The AS-IS cut-in
    /// <c>HasExecutedSameEffect</c> skip predicate (AutoProcessing.cs:623-627): suppress a candidate whose effect a
    /// prior commit in the LIVE frames already resolved. AS-IS <c>IsSameEffect</c> (ICardEffect.cs:860-933) is
    /// "same instance OR (same <c>EffectSourceCard</c> ∧ same <c>HashString</c> ∧ same root)" — and because effect
    /// instances are recreated per query (Player.cs:830-880), the reference branch is dead and the fallback rules:
    /// <c>HashString</c> defaults to "" and BOTH-EMPTY compares EQUAL (:880-902), so the REAL AS-IS partition is
    /// per SOURCE CARD — two different unhashed effects of the same card count as "the same effect" (cards like
    /// ST16_11 SetHashString precisely to split that collapse; only dozens of cards do). A binding-id (EffectId)
    /// key was a NARROWER partition (per effect) that let a same-card sibling effect fire where AS-IS skips it —
    /// so the faithful key is the SOURCE CARD INSTANCE. Design item RDx-A1-HASH: when a SetHashString card is
    /// ported, mirror its hash onto the binding and refine this to (source card + hash); no such card is in the
    /// ported pool. Pass this as <see cref="BuildSchedulerDeps"/>'s <c>skipCondition</c> ONLY for cut-in windows
    /// AS-IS passes it to — the 5 CardController sites (727/990/5189/5301/5709) AND the per-card WhenDigisorption
    /// windows (BT2_045.cs:158-pattern, ~10 green cards) — NEVER the main-loop window (AS-IS AutoProcessing.cs:137
    /// passes null; a blanket main-loop dedup would over-suppress). AttackProcess.cs:296 passes a DIFFERENT
    /// skipCondition (HasCounterEffect) with mainStack=true — not this predicate.</summary>
    public static bool HasExecutedSameEffect(
        IReadOnlyList<TimingWindowTrigger> resolved, TimingWindowTrigger candidate)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(candidate);

        for (int i = 0; i < resolved.Count; i++)
        {
            if (resolved[i].Request.Context.SourceEntityId == candidate.Request.Context.SourceEntityId)
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
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out GameEvent? drivingEvent, out HeadlessPlayerId owner, out bool inherited))
        {
            return MarkerGate(context, card, timing, owner, drivingEvent, inherited);
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

        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out GameEvent? drivingEvent, out HeadlessPlayerId owner, out bool inherited))
        {
            return MarkerGate(context, card, timing, owner, drivingEvent, inherited);
        }

        return SchedulerGate(context, trigger);
    }

    /// <summary>An activated-bridge marker gates itself in the resolver EXCEPT the OnUnTappedAnyone caller-cap
    /// (a card can be re-suspended/unsuspended within a turn — GameFlowProcessor:737-746), mirrored as a
    /// synthetic-key OnceFlag checked here (per pass) and consumed at commit (F5/RD-12).
    ///
    /// (RDx-A3, fixed) per-pass CanActivate re-check: AS-IS re-checks each stacked skill's CanActivate (board
    /// CONDITION) EVERY window pass (MultipleSkills.cs:122 / 164-165), excluding a currently-false effect from that
    /// pass's active set (so it does not compete for the order choice) yet keeping it stacked to re-test. The
    /// scheduler half mirrors this (<see cref="SchedulerGate"/> re-checks body.CanResolve per pass); this marker gate
    /// now does too, via <c>ActivatedEffectResolver.CanActivateAt</c> — which reuses the resolver's OWN uniform
    /// CanResolve gate against the SHARED resolve-context (<c>BuildUniformResolveContext</c>), so there is no
    /// over/under-gating from a divergent reconstruction. A marker whose activated effects are all currently
    /// un-resolvable is gate-false this pass (not offered for the order choice) but stays stacked to re-test —
    /// instead of being offered and no-opping in the resolver, which would consume the AS-IS per-pass deferral.</summary>
    private static bool MarkerGate(
        EngineContext context, HeadlessEntityId card, EffectTiming timing, HeadlessPlayerId owner, GameEvent? drivingEvent, bool inherited)
    {
        // (RDx-A3) per-pass board-condition gate — the activated analogue of SchedulerGate's per-pass CanResolve,
        // reusing the resolver's own uniform CanResolve via the shared resolve-context (no reconstruction drift).
        // (F1-M1-INHERITSCAN) inherited-scan for a source reactor so only its IsInheritedEffect effects are gated.
        if (!Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver.CanActivateAt(context, card, owner, timing, drivingEvent, inheritedScan: inherited))
        {
            return false;
        }

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

    /// <summary>(D-1 / VR-8) Read the delete-BATCH id stamped on a card's CardMoved (field-&gt;trash) event by the
    /// deleting path (sink / battle / DP-zero sweep / deferred finalize / security battle) — one id per AS-IS
    /// <c>DestroyPermanentsClass.Destroy()</c>. The OnDeletion / OnLeaveFieldAnyone collapse keys off (reactor, this
    /// id): same id = one batch = one fire; a distinct id = an independent delete-process = a separate fire.
    /// (R2-P1-4) the deletion marker is now REQUIRED for a CardMoved to derive OnDeletion at all
    /// (TriggerTimingMap), so a move-driven deletion trigger always carries a real id; the sentinel 0 remains
    /// only for EXPLICIT-timing events (a synthetic window opened via the timing-override metadata, no move) —
    /// those collapse all-together, preserving the pre-D1 whole-pass dedup for synthetic windows.</summary>
    private static long ReadDeletionBatchId(GameEvent? gameEvent) =>
        gameEvent is not null
            && gameEvent.Metadata.TryGetValue(MatchStateMutationSink.DeletionBatchIdKey, out object? raw)
            && raw is long id
                ? id
                : 0L;

    /// <summary>(F1-M1 P1-1) Read the security-LOSS batch id stamped on a security card's CardMoved (Security-&gt;non-
    /// Security) event by the effect-driven trash path (<c>IZoneMover.TrashSecurityAsync</c>) — one id per AS-IS
    /// <c>IReduceSecurity.ReduceSecurity()</c> == one <c>StackSkillInfos(OnLoseSecurity)</c>. The OnLoseSecurity
    /// collapse keys off (reactor, this id): same id = one batch = one fire; a distinct id = an independent
    /// security removal = a separate fire. The sentinel 0 (an UNSTAMPED security move — the attack security-CHECK
    /// per-card reveal is unstamped BY DESIGN, resolved in its own per-iteration window so it fires per-card as AS-IS
    /// merges OnLoseSecurity into each OnSecurityCheck resolution) collapses all-together within a single drain.</summary>
    private static long ReadSecurityLossBatchId(GameEvent? gameEvent) =>
        gameEvent is not null
            && gameEvent.Metadata.TryGetValue(MatchStateMutationSink.SecurityLossBatchIdKey, out object? raw)
            && raw is long id
                ? id
                : 0L;

    /// <summary>(F1-Tier1 OnDiscard*) Read the DISCARD batch id stamped on a discarded card's CardMoved. A
    /// Hand/Library discard carries <see cref="MatchStateMutationSink.DiscardBatchIdKey"/> (one id per sink flush ==
    /// one AS-IS StackSkillInfos(OnDiscardHand/Library)); a Security discard reuses the security-loss id
    /// (<see cref="MatchStateMutationSink.SecurityLossBatchIdKey"/>, one id per IReduceSecurity ==
    /// StackSkillInfos(OnDiscardSecurity)). Falling back to the security-loss key lets the OnDiscardSecurity collapse
    /// share the SAME substrate as OnLoseSecurity without a duplicate stamp. Sentinel 0 = an unstamped (non-effect)
    /// move — those collapse all-together within one drain.</summary>
    private static long ReadDiscardBatchId(GameEvent? gameEvent) =>
        gameEvent is not null
            && gameEvent.Metadata.TryGetValue(MatchStateMutationSink.DiscardBatchIdKey, out object? raw)
            && raw is long id
                ? id
                : ReadSecurityLossBatchId(gameEvent);

    /// <summary>(F1-Tier1 OnAddHand) Read the ADD-HAND batch id stamped on an added card's -&gt;Hand CardMoved by the
    /// effect-driven draw / return-to-hand path (<c>IZoneMover.Draw/AddToHandAsync</c>) — one id per sink flush ==
    /// one AS-IS <c>AddHandCards</c> == one <c>StackSkillInfos(OnAddHand)</c> over the whole added list. The OnAddHand
    /// collapse keys off (reactor, this id): same id = one batch = one fire; a distinct id = an independent hand-add
    /// = a separate fire. Sentinel 0 (an unstamped move — a turn/mulligan/setup draw, which also carries no cause id)
    /// collapses all-together and fails the CardEffect!=null gate downstream.</summary>
    private static long ReadAddHandBatchId(GameEvent? gameEvent) =>
        gameEvent is not null
            && gameEvent.Metadata.TryGetValue(MatchStateMutationSink.AddHandBatchIdKey, out object? raw)
            && raw is long id
                ? id
                : 0L;

    /// <summary>(F1-Tier1 OnAddSecurity, design item F1-ADD-COUNTER P2-1) Read the ADD-SECURITY batch id stamped on
    /// a card's -&gt;Security CardMoved by an effect / recovery / replacement / player security add — one distinct
    /// id PER card (OnAddSecurity is NOT collapsed), allocated from the SHARED deletion counter
    /// (<c>EngineContext.NextSecurityAddBatchId</c>) so it sequences ASCENDING within the ONE globally-unique id
    /// space that deletion / discard / add-hand / security-loss also use — a mixed drain never collides in the
    /// window's raw-<c>BatchId</c> cross-batch ordering (<c>WindowResolver.FilterToMinimumBatch</c>). Falls back to
    /// the driving event's monotonic <c>Sequence</c> only for an UNSTAMPED security move (a context-less setup deal
    /// or a bare unit test) so those still sequence per-card; such moves never co-drain with a unified-space effect
    /// batch. This replaces the former unconditional <c>drivingEvent.Sequence</c> stamp, which lived in a DIFFERENT
    /// counter space and broke the cross-timing ordering invariant when OnAddSecurity co-drained with a deletion /
    /// discard / add-hand batch.</summary>
    private static long ReadAddSecurityBatchId(GameEvent? gameEvent) =>
        gameEvent is null
            ? 0L
            : gameEvent.Metadata.TryGetValue(MatchStateMutationSink.AddSecurityBatchIdKey, out object? raw)
                && raw is long id
                    ? id
                    : gameEvent.Sequence;

    /// <summary>Consume the once-per-turn use at commit (before the body — RD-12/F5). For an ACTIVATED-bridge
    /// marker only the OnUnTappedAnyone caller-cap is consumed here (its synthetic key); every other activated
    /// timing is uncapped at the caller (the resolver's own MaxCountPerTurn caps it).</summary>
    private static void Commit(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner, out _))
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
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out _, out HeadlessPlayerId owner, out _))
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
        if (IsActivatedBridge(trigger, out HeadlessEntityId card, out EffectTiming timing, out GameEvent? drivingEvent, out HeadlessPlayerId owner, out bool inherited))
        {
            try
            {
                // windowDispatched: the marker's collect gate (CanCollectAt = AS-IS CanTrigger) already ran when
                // it was synthesised; execution entry re-checks ONLY the CanActivate half (AS-IS
                // AutoProcessing.cs:1068 — CanTrigger is never re-evaluated on a stacked skill).
                // (F1-M1-INHERITSCAN) inheritedScan for a source reactor so only its inherited effects resolve.
                await Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver
                    .ResolveAsync(context, card, owner, timing, cancellationToken, drivingEvent: drivingEvent, windowDispatched: true, inheritedScan: inherited)
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
                context.DeferredActivations.Suspend(card, timing, owner, drivingEvent, windowDispatched: true);
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

    /// <summary>(F1-M1-INHERITSCAN) Marks an activated-bridge trigger whose reacting card is a DIGIVOLUTION SOURCE
    /// (an inherited effect) rather than a top permanent. Gate / ResolveBody then run the resolver in
    /// INHERITED-scan mode (only the source's <c>IsInheritedEffect</c> activated effects are considered — the
    /// AS-IS <c>Permanent.EffectList_ForCard</c> non-top membership). Absent (false) for a top-card reactor
    /// (which contributes only its non-inherited effects).</summary>
    public const string ActivatedBridgeInheritedKey = "activatedBridge.inherited";

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
        out HeadlessPlayerId owner,
        out bool inherited)
    {
        card = default;
        timing = default;
        drivingEvent = null;
        owner = default;
        inherited = false;

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

        // (F1-M1-INHERITSCAN) a source (inherited) reactor carries this flag so Gate/ResolveBody run the resolver
        // in inherited-scan mode (only its IsInheritedEffect activated effects); a top reactor has it absent.
        inherited = values.TryGetValue(ActivatedBridgeInheritedKey, out object? inh) && inh is true;

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
        long sequence,
        bool inherited = false)
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

        if (inherited)
        {
            values[ActivatedBridgeInheritedKey] = true;
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

        // (F1-M1-INHERITSCAN) each entry carries whether the reacting card is a digivolution SOURCE (Inherited) so
        // the resolver runs in inherited-scan mode for it; a subject-scoped reactor is never a source (the event
        // subject is a top card), so it is always Inherited=false.
        var toResolve = new List<(HeadlessEntityId Card, EffectTiming Timing, GameEvent? Event, ActivatedBridgeCategory Category, bool Inherited)>();
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
                        toResolve.Add((subject, timing, null, ActivatedBridgeCategory.Subject, false));
                    }
                }
                else if (ActivatedBridgeTimings.Boundary.Contains(timing) && zones is not null)
                {
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (BridgeScanEntry entry in ScanZones(context, zones, player))
                        {
                            if (seen.Add((entry.Card, timing)))
                            {
                                toResolve.Add((entry.Card, timing, null, ActivatedBridgeCategory.Boundary, entry.Inherited));
                            }
                        }
                    }
                }
                else if (ActivatedBridgeTimings.EventBroadcast.Contains(timing) && zones is not null)
                {
                    // No cross-event de-dup on purpose: each event is its own window. Within one event the
                    // zone scan visits a card exactly once.
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (BridgeScanEntry entry in ScanZones(context, zones, player))
                        {
                            toResolve.Add((entry.Card, timing, gameEvent, ActivatedBridgeCategory.Broadcast, entry.Inherited));
                        }
                    }
                }
            }
        }

        var triggers = new List<TimingWindowTrigger>(toResolve.Count);
        long sequence = 0;
        // (D-2 / VR-9) OnLeaveFieldAnyone batch collapse — the ACTIVATED-half mirror of the scheduler half's
        // firedDeletion (CollectUnifiedSeed). AS-IS stacks the WHOLE simultaneous-leave list as ONE
        // StackSkillInfos(OnLeaveFieldAnyone) whose gate any-matches (CardController.cs:3748), so an OnLeaveFieldAnyone
        // reactor fires ONCE per batch. Headless emits one CardMoved (leave) per card, so toResolve holds one
        // (reactor, event) pair per leave event; collapse to the FIRST that GATE-passes (CanCollectAt below) per
        // reactor card — claimed on-fire so a subject-specific gate matching only a LATER leaver is not lost.
        // Scoped to OnLeaveFieldAnyone: every other EventBroadcast timing (OnUseOption/OnTappedAnyone/…) opens one
        // window per event (no collapse), and self-scoped WhenRemoveField rides the scheduler half (per-card).
        // (D-1 / VR-8) key by (reactor, delete-BATCH id): the driving CardMoved (leave) event carries the batch id
        // of the Destroy() that removed it, so a reactor fires once per batch yet an INDEPENDENT leave-batch in the
        // same drain (a distinct id) fires it again.
        var firedLeaveBatch = new HashSet<(HeadlessEntityId Card, long BatchId)>();
        // (F1-M1 P1-1) OnLoseSecurity batch collapse — the same pattern as firedLeaveBatch, keyed by
        // (reactor, security-loss BATCH id). AS-IS the effect-driven IDestroySecurity trashes N security cards then
        // calls IReduceSecurity ONCE (CardController.cs:4358-4363), a SINGLE StackSkillInfos(OnLoseSecurity) broadcast
        // (hashtable {Player}), so an OnLoseSecurity reactor fires ONCE for the whole batch. Headless emits one
        // CardMoved (Security->Trash) per card; TrashSecurityAsync stamps all N with ONE shared id, so collapse to the
        // FIRST gate-passing removal per reactor. An INDEPENDENT security removal in the same drain (a distinct id)
        // fires the reactor again (AS-IS: each IReduceSecurity broadcasts separately). The per-card attack security
        // CHECK reveal is UNSTAMPED (id 0) and resolved in its OWN per-iteration window, so it keeps AS-IS's per-card
        // merge into each OnSecurityCheck resolution (a single drain never co-holds two check reveals).
        var firedSecurityLossBatch = new HashSet<(HeadlessEntityId Card, long BatchId)>();
        // (F1-Tier1 OnDiscard*) discard batch collapse — same pattern, keyed by (reactor, timing, discard/security-loss
        // batch id). AS-IS fires ONE StackSkillInfos(OnDiscard*) for the whole discarded list, so a reactor fires ONCE
        // per batch. Headless emits one CardMoved per discarded card (all sharing one batch id per sink flush /
        // IReduceSecurity), so collapse to the FIRST gate-passing discard per reactor per batch — the AS-IS any-match.
        // Keyed by timing too, because one sink can share its discard id across BOTH a Hand and a Library trash while
        // AS-IS fires OnDiscardHand and OnDiscardLibrary as SEPARATE StackSkillInfos.
        var firedDiscardBatch = new HashSet<(HeadlessEntityId Card, EffectTiming Timing, long BatchId)>();
        // (F1-Tier1 OnAddHand) add-hand batch collapse — mirror of the OnDiscard* collapse, keyed by (reactor,
        // add-hand batch id). AS-IS fires ONE StackSkillInfos(OnAddHand) for the whole added list, so an OnAddHand
        // reactor fires ONCE per hand-add batch. Headless emits one ->Hand CardMoved per added card sharing ONE
        // add-hand id per sink flush, so collapse to the FIRST gate-passing add per reactor per batch — the AS-IS
        // any-match over CardSources. OnAddSecurity is NOT collapsed: AS-IS fires it PER SINGLE card (per IAddSecurity),
        // so its per-CardMoved derivation already matches AS-IS (a batch collapse would WRONGLY under-fire it).
        var firedAddHandBatch = new HashSet<(HeadlessEntityId Card, long BatchId)>();
        foreach ((HeadlessEntityId card, EffectTiming timing, GameEvent? drivingEvent, ActivatedBridgeCategory category, bool inherited) in toResolve)
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
            // per-pass would DIVERGE (admit an entry AS-IS never collects).
            if (!Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver.HasActivatedEffectsAt(context, card, instance.OwnerId, timing, inheritedScan: inherited))
            {
                continue;
            }

            // (RDx-A3 split) the AS-IS collect FILTER on top of existence: CanTrigger = once-per-turn cap +
            // CanUseCondition (ICardEffect.cs:319-358), evaluated ONCE here — a uniform effect whose CanUse half
            // is false at collect is never stacked (AS-IS GetSkillInfos filters it out), and one collected here
            // is NOT canUse-re-checked per pass (MarkerGate re-checks only the CanActivate half, mirroring
            // MultipleSkills.cs:122/164-165).
            if (!Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver.CanCollectAt(context, card, instance.OwnerId, timing, drivingEvent, inheritedScan: inherited))
            {
                continue;
            }

            // (D-2 / VR-9) batch collapse: an OnLeaveFieldAnyone reactor fires ONCE per simultaneous-leave batch —
            // the first leave subject that gate-passes (CanCollectAt above) claims the reactor; later leave events
            // in the same seed drain are skipped. Placed AFTER the gate so a reactor whose gate matches only a
            // later leaver (any-match) still fires on that leaver.
            if (timing == EffectTiming.OnLeaveFieldAnyone
                && !firedLeaveBatch.Add((card, ReadDeletionBatchId(drivingEvent))))
            {
                continue;
            }

            // (F1-M1 P1-1) OnLoseSecurity batch collapse — mirror of the OnLeaveFieldAnyone collapse above, keyed by
            // the security-loss batch id, placed AFTER the gate so a reactor whose player-gate matches only a later
            // removal still fires on it (within one batch all N cards belong to the SAME losing player, so the
            // player-gate is uniform and the first gate-passing removal claims the reactor).
            if (timing == EffectTiming.OnLoseSecurity
                && !firedSecurityLossBatch.Add((card, ReadSecurityLossBatchId(drivingEvent))))
            {
                continue;
            }

            // (F1-Tier1) OnDiscard* batch collapse — mirror of the OnLoseSecurity collapse, placed AFTER the gate so a
            // reactor whose cardCondition matches only a LATER discarded card (any-match) still fires on it.
            if ((timing == EffectTiming.OnDiscardHand
                    || timing == EffectTiming.OnDiscardSecurity
                    || timing == EffectTiming.OnDiscardLibrary)
                && !firedDiscardBatch.Add((card, timing, ReadDiscardBatchId(drivingEvent))))
            {
                continue;
            }

            // (F1-Tier1 OnAddHand) add-hand batch collapse — placed AFTER the gate so a reactor whose
            // cardEffectSourceCondition matches only a LATER added card (any-match) still fires on it. OnAddSecurity
            // is deliberately excluded (AS-IS per-card firing).
            if (timing == EffectTiming.OnAddHand
                && !firedAddHandBatch.Add((card, ReadAddHandBatchId(drivingEvent))))
            {
                continue;
            }

            TimingWindowTrigger bridgeTrigger = MakeActivatedBridgeTrigger(card, timing, instance.OwnerId, drivingEvent, category, sequence++, inherited);

            // (D-1 order) stamp the delete-batch id onto a deletion-derived activated trigger (OnLeaveFieldAnyone —
            // the cross-card bystander leave reactor, e.g. AD1_025) so the window loop sequences CROSS-batch leaves
            // in ascending batch order rather than opening a spurious cross-batch order choice. Only a REAL id
            // (non-zero); an unstamped leave (sentinel 0) stays batch-less. Subject-scoped OnDestroyedAnyone carries
            // no driving event here (self-reactor), so it is left batch-less.
            if (timing == EffectTiming.OnLeaveFieldAnyone)
            {
                long leaveBatch = ReadDeletionBatchId(drivingEvent);
                if (leaveBatch != 0)
                {
                    bridgeTrigger = bridgeTrigger with { BatchId = leaveBatch };
                }
            }

            // (F1-M1 P1-1) stamp the security-loss batch id (shared sequence with deletion ids) so the window loop
            // sequences an independent security-loss batch that co-drains with another batch in ascending temporal
            // order instead of opening a spurious cross-batch order choice. Only a REAL id (non-zero).
            if (timing == EffectTiming.OnLoseSecurity)
            {
                long lossBatch = ReadSecurityLossBatchId(drivingEvent);
                if (lossBatch != 0)
                {
                    bridgeTrigger = bridgeTrigger with { BatchId = lossBatch };
                }
            }

            // (F1-Tier1) stamp the discard batch id so co-draining independent discard batches sequence in ascending
            // temporal order rather than opening a spurious cross-batch order choice. Only a REAL id (non-zero).
            if (timing == EffectTiming.OnDiscardHand
                || timing == EffectTiming.OnDiscardSecurity
                || timing == EffectTiming.OnDiscardLibrary)
            {
                long discardBatch = ReadDiscardBatchId(drivingEvent);
                if (discardBatch != 0)
                {
                    bridgeTrigger = bridgeTrigger with { BatchId = discardBatch };
                }
            }

            // (F1-Tier1 OnAddHand) stamp the add-hand batch id so co-draining independent hand-add batches sequence
            // in ascending temporal order rather than opening a spurious cross-batch order choice. Only a REAL id.
            if (timing == EffectTiming.OnAddHand)
            {
                long addHandBatch = ReadAddHandBatchId(drivingEvent);
                if (addHandBatch != 0)
                {
                    bridgeTrigger = bridgeTrigger with { BatchId = addHandBatch };
                }
            }

            // (F1-Tier1 OnAddSecurity, design item F1-ADD-COUNTER P2-1) OnAddSecurity is PER-CARD (AS-IS fires each
            // IAddSecurity's OnAddSecurity in its OWN StackSkillInfos, resolved SEQUENTIALLY). Headless co-drains the
            // N ->Security CardMoved of a recovery (or several adds) into ONE window; without a distinguishing BatchId
            // those N same-reactor triggers would collide into a single spurious cross-fire ORDER CHOICE (AS-IS never
            // asks — it resolves them in add order). Stamp each with its per-card SHARED-counter add-security id
            // (ReadAddSecurityBatchId), allocated from the SAME space as deletion / discard / add-hand / security-loss,
            // so the window sequences them ASCENDING (= add order = the AS-IS sequential resolution) AND a co-draining
            // deletion / discard / add-hand batch orders correctly against them (the former drivingEvent.Sequence lived
            // in a DIFFERENT counter space — a cross-timing raw-BatchId collision, adversarial review P2-1).
            if (timing == EffectTiming.OnAddSecurity && drivingEvent is not null)
            {
                bridgeTrigger = bridgeTrigger with { BatchId = ReadAddSecurityBatchId(drivingEvent) };
            }

            triggers.Add(bridgeTrigger);
        }

        return triggers;
    }

    /// <summary>(C5-witness) The Boundary/EventBroadcast bridge's per-player collection scope. AS-IS
    /// GetSkillInfos (AutoProcessing.cs:770-857) scans, for EVERY timing: field permanents, TRASH cards and
    /// HAND cards (plus player effects and face-up security, which the headless models separately) — a
    /// trash-resident inherited trigger like EX8_051's "when effects trash this card from digivolution cards
    /// of a [Mineral]/[Rock] Digimon" (OnDigivolutionCardDiscarded, CanActivate = IsExistOnTrash) is
    /// collected from the TRASH region. Previously only the battle area was scanned, so such effects never
    /// fired. Behavior-neutral for battle-area effects: every mirrored card guards its own zone
    /// (IsExistOnBattleArea / IsExistOnTrash) exactly like AS-IS, and non-reactive cards are filtered by
    /// HasActivatedEffectsAt below.</summary>
    private static IEnumerable<BridgeScanEntry> ScanZones(EngineContext context, IZoneStateReader zones, HeadlessPlayerId player)
    {
        foreach (HeadlessEntityId topId in zones.GetCards(player, ChoiceZone.BattleArea))
        {
            // The top permanent's OWN (non-inherited) effects.
            yield return new BridgeScanEntry(topId, Inherited: false);

            // (F1-M1-INHERITSCAN) also visit the top permanent's DIGIVOLUTION SOURCES — AS-IS
            // Permanent.EffectList_ForCard (Permanent.cs:1503-1546) iterates the WHOLE stack and exposes each
            // NON-TOP source's INHERITED effects while the source is non-flipped and the permanent is a Digimon.
            // The former top-only scan missed every inherited activated reactor sitting under another Digimon
            // (design item F1-M1-INHERITSCAN — affects every activated bridge timing). Reuse the same stack read
            // (DigivolutionStackReader) + the AS-IS membership gate (InheritedEffectHelpers.ActiveInheritedSources:
            // non-flipped sources of a Digimon permanent) that the C-3 CONTINUOUS inherited scan uses, so the
            // source-vs-top split is byte-identical across the continuous and activated halves. Trash/hand cards
            // have no digivolution stack (a trashed permanent's sources are separate top-level trash cards), so
            // only the battle area yields sources.
            HeadlessDCGO.Engine.Headless.State.DigivolutionStack stack =
                HeadlessDCGO.Engine.Headless.State.DigivolutionStackReader.Read(
                    context.CardInstanceRepository, context.CardRepository, topId);
            if (!stack.IsEmpty && stack.UnderCards.Count > 0)
            {
                bool hostIsDigimon =
                    new Assets.Scripts.Script.CardEffectCommons.Permanent(context, topId, player).IsDigimon;
                foreach (HeadlessEntityId source in
                         Assets.Scripts.Script.CardEffectCommons.InheritedEffectHelpers.ActiveInheritedSources(
                             stack, id => IsSourceFlipped(context, id), permanentIsDigimon: hostIsDigimon))
                {
                    yield return new BridgeScanEntry(source, Inherited: true);
                }
            }
        }

        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.Trash))
        {
            yield return new BridgeScanEntry(cardId, Inherited: false);
        }

        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.Hand))
        {
            yield return new BridgeScanEntry(cardId, Inherited: false);
        }
    }

    /// <summary>AS-IS <c>cardSource.IsFlipped</c> — a face-down (flipped) digivolution source contributes no
    /// inherited effect (Permanent.cs:1508). Mirrors the flip predicate ContinuousFieldMembership reads.</summary>
    private static bool IsSourceFlipped(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
        && rec.Metadata.TryGetValue("isFlipped", out object? raw) && raw is true;

    /// <summary>(F1-M1-INHERITSCAN) One scanned bridge candidate: a card that may react at a timing, plus whether
    /// it is a digivolution SOURCE (inherited effect) rather than a top permanent.</summary>
    private readonly record struct BridgeScanEntry(HeadlessEntityId Card, bool Inherited);
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
