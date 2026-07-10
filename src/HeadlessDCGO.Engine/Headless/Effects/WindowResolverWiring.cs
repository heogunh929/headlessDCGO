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
    /// (AutoProcessAsync: IsEffectsDisabled + CanResolve + OnceFlags), re-evaluated per pass and at commit.
    /// An ACTIVATED-bridge marker gates itself inside <c>ActivatedEffectResolver</c> (the uniform ActivatedEffect
    /// case checks CanResolve + MaxCountPerTurn), so it passes here EXCEPT the OnUnTappedAnyone caller-cap.</summary>
    private static bool Gate(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId bridgeCard, out EffectTiming bridgeTiming, out _, out HeadlessPlayerId bridgeOwner))
        {
            // Only OnUnTappedAnyone carries a caller-level once-per-turn cap (a card can be re-suspended and
            // unsuspended within a turn — GameFlowProcessor:737-746). Mirror it as a synthetic-key OnceFlag,
            // checked here (per pass) and consumed at Commit — the AS-IS TryActivate split into the window's
            // CanActivate-gate + Consume-at-commit (F5/RD-12). Every other bridged timing gates in the resolver.
            if (ActivatedBridgeTimings.OncePerTurn.Contains(bridgeTiming))
            {
                return context.OnceFlags.CanActivate(SyntheticBridgeOnceRequest(bridgeCard, bridgeTiming, bridgeOwner), maxCountPerTurn: 1);
            }

            return true;
        }

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

    /// <summary>Consume the once-per-turn use at commit (before the body — RD-12/F5). For an ACTIVATED-bridge
    /// marker only the OnUnTappedAnyone caller-cap is consumed here (its synthetic key); every other activated
    /// timing is uncapped at the caller (the resolver's own MaxCountPerTurn caps it).</summary>
    private static void Commit(EngineContext context, TimingWindowTrigger trigger)
    {
        if (IsActivatedBridge(trigger, out HeadlessEntityId bridgeCard, out EffectTiming bridgeTiming, out _, out HeadlessPlayerId bridgeOwner))
        {
            if (ActivatedBridgeTimings.OncePerTurn.Contains(bridgeTiming))
            {
                context.OnceFlags.Consume(SyntheticBridgeOnceRequest(bridgeCard, bridgeTiming, bridgeOwner), maxCountPerTurn: 1);
            }

            return;
        }

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
                // .ResolveChoiceAsync -> DeferredActivations.Pending) advances it. Coordinating that body-resume
                // with the window's OWN InFlightPick re-drive is the live cut-over (design RD 3b-iii); this
                // increment only SIGNALS the suspend — no live loop drives the activated body through here yet.
                context.DeferredActivations.Suspend(card, timing, owner, drivingEvent);
                return WindowResolveOutcome.Suspended;
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
