namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using EffectTiming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;

/// <summary>
/// Encapsulates the AS-IS common processing loop (Unity <c>TurnStateMachine</c>'s repeated
/// rule-process / auto-process / attack-advance / end-turn cycle) as a headless, re-entrant
/// step. The loop runs until no sub-step makes progress (stable state) or a choice becomes
/// pending, in which case it pauses so the next <c>HeadlessGameLoop</c> step can resume it.
///
/// Phase 3.5 scope: auto-processing collects triggered effects from pending game events
/// (<see cref="AutoProcessingTriggerCollector"/> driven by <see cref="GameEventQueue"/>, G3.5-006)
/// and resolves the scheduler (<see cref="EffectScheduler"/>, G3.5-001/002); the attack pipeline
/// (<see cref="AttackPipeline"/>, G3.5-005) is active. Rule processing and end-turn evaluation are
/// hooks that are filled in by G3.5-007 / G3.5-008.
/// </summary>
public sealed class GameFlowProcessor
{
    public const int MaxIterations = 256;

    private readonly AttackPipeline _attackPipeline;
    private readonly DeletionReplacementTiming _deletionReplacement = new();

    public GameFlowProcessor(AttackPipeline? attackPipeline = null)
    {
        _attackPipeline = attackPipeline ?? new AttackPipeline();
    }

    public async Task<FlowProcessResult> RunToStableAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool progressedAny = false;
        int resolvedTotal = 0;
        int iterations = 0;
        bool reachedStable = false;

        while (iterations < MaxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.ChoiceController.Current.IsPending)
            {
                return FlowProcessResult.Paused(progressedAny, resolvedTotal, iterations);
            }

            // (P6) settle holders whose chosen sacrifice's own window has resolved — BEFORE any window
            // reopens for them (spared when the ally died, back to dying when it saved itself).
            if (DeletionReplacementTiming.SettleAwaitingSacrifices(context))
            {
                progressedAny = true;
                continue;
            }

            // (W6 tail) run due turn-end self-deletions (AddSelfDeleteEffect markers promoted by the
            // end-turn cleanup) through the REAL deletion pipeline.
            if (await SweepDueTurnEndDeletionsAsync(context, cancellationToken).ConfigureAwait(false))
            {
                progressedAny = true;
                continue;
            }

            // (W6-S) fire parked delete-and-process continuations whose targets have all settled
            // (AS-IS DeletePeremanentAndProcessAccordingToResult resumes after the deletion pipeline).
            if (context.TryGetService(out DeletionOutcomeWatcher? outcomeWatcher) && outcomeWatcher is not null &&
                outcomeWatcher.Count > 0 &&
                await outcomeWatcher.SettleAsync(context, cancellationToken).ConfigureAwait(false))
            {
                progressedAny = true;
                continue;
            }

            // F-6.8: before the state-based sweep finishes any deferred deletion, open the would-be-deleted
            // replacement window for cards that carry an OPTIONAL replacement keyword, so the owner decides
            // (activate / skip) instead of it being auto-applied. Opening a choice pauses the loop.
            if (_deletionReplacement.RequestChoice(context))
            {
                return FlowProcessResult.Paused(progressedAny, resolvedTotal, iterations);
            }

            iterations++;
            bool progressed = false;

            // 1) Rule processing (state-based actions): cleanup of cards flagged for deletion
            //    (G3.5-RL-C1). Deck-out loss is marked at draw time and consolidated by EndTurnCheck.
            progressed |= await RuleProcessAsync(context, cancellationToken).ConfigureAwait(false);

            // 2) Auto-processing: collect triggers from pending game events (G3.5-006) and resolve
            //    the effect scheduler (G3.5-001/002).
            (int resolved, int collected) = await AutoProcessAsync(context, cancellationToken).ConfigureAwait(false);
            if (resolved > 0 || collected > 0)
            {
                progressed = true;
                resolvedTotal += resolved;
            }

            // 3) Attack pipeline single-step advance (G3.5-005): block → battle/security → end attack.
            if (context.AttackController.Current.Phase != AttackPhase.None)
            {
                AttackAdvanceResult attackResult = await _attackPipeline
                    .AdvanceAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                progressed |= attackResult.Progressed;
            }

            // 4) End-turn / win-loss evaluation (G3.5-008): PlayerRuleAdapter terminal verdict → match result.
            progressed |= EndTurnCheck(context);

            progressedAny |= progressed;

            if (!progressed)
            {
                reachedStable = true;
                break;
            }

            if (context.RuleQueryService.IsTerminal())
            {
                reachedStable = true;
                break;
            }
        }

        // GPT-#3: distinguish a genuine fixpoint (no progress / terminal) from exhausting the iteration
        // budget while still making progress — the latter signals a runaway trigger loop, not stability.
        if (!reachedStable)
        {
            context.LogSink.Warn(
                $"[GameFlowProcessor] RunToStable hit MaxIterations ({MaxIterations}) while still progressing; " +
                "possible runaway trigger loop.");
            return FlowProcessResult.MaxIterationsExceeded(progressedAny, resolvedTotal, iterations);
        }

        return FlowProcessResult.Stable(progressedAny, resolvedTotal, iterations);
    }

    /// <summary>Marks a card instance for state-based deletion. Effects set this flag; the rule
    /// process sweeps flagged cards off the field into the trash.</summary>
    public const string PendingDeletionKey = "pendingDeletion";

    private static readonly ChoiceZone[] FieldZones =
    {
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea
    };

    /// <summary>
    /// (G3.5-RL-C1 + D-2) State-based action pass. Sweeps off the field into the trash any field card
    /// that is either flagged <see cref="PendingDeletionKey"/> (the uniform effect-driven deletion path)
    /// OR a Digimon whose effective DP has dropped to 0 or below (AS-IS <c>DigimonLackDPProcess</c> /
    /// <c>TrashNoDPPermanentProcess</c> / <c>CutInProcess</c>: <c>DP &lt;= 0 &amp;&amp; IsDigimon</c>).
    /// Returns true when it acts so the common loop keeps iterating until the board is stable.
    /// </summary>
    // (Stage 5, F3) internal so the window loop's ResolveBody can run state-based rule processing BETWEEN picks
    // (AS-IS MultipleSkills.cs:398-403 RuleProcess after every pick), not just once per RunToStable iteration.
    internal static async Task<bool> RuleProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return false;
        }

        bool progressed = false;
        var deletionReplacement = new DeletionReplacementTiming();
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            if (playerId.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, zone).ToArray())
                {
                    bool pending = IsPendingDeletion(context, cardId);

                    // F-6.8: a card still awaiting its owner's would-be-deleted replacement decision is not
                    // swept yet — the deletion-replacement window resolves first (activate clears the flag,
                    // skip marks it declined so the next sweep finishes it). A BATTLE-deferred card
                    // (deletedByBattle) is finalized by BattleResolver.FinalizeDeferredAsync, never swept here.
                    if (pending && (deletionReplacement.IsPreAwaiting(context, cardId) || IsBattleDeferred(context, cardId)
                        // (P6) a holder waiting on its sacrifice's fate is settled by SettleAwaitingSacrifices.
                        || IsSacrificeAwaiting(context, cardId)))
                    {
                        continue;
                    }

                    // (P7) AS-IS TrashNoDPPermanentProcess (AutoProcessing.cs:439-465): a battle-area
                    // permanent with NO DP — a Digi-Egg or an un-played Option lingering on the field — is
                    // trashed DIRECTLY (DiscardEvoRoots + RemoveField + AddTrash; NOT a destroy, no
                    // deletion triggers/replacements). The AS-IS predicate's Digimon branch is unreachable
                    // for real cards (printed DP always exists; modifier-negative DP is the lethal rule),
                    // so the port scopes to the reachable types.
                    // AS-IS scans GetBattleAreaPermanents ONLY — a Digi-Egg in the breeding area is safe.
                    if (!pending && zone == ChoiceZone.BattleArea && IsNoDpTrashablePermanent(context, cardId))
                    {
                        await TrashNoDpPermanentAsync(context, playerId, cardId, zone, cancellationToken).ConfigureAwait(false);
                        progressed = true;
                        continue;
                    }

                    bool lethalDp = !pending && HasLethalDp(context, cardId);
                    if (!pending && !lethalDp)
                    {
                        continue;
                    }

                    if (pending)
                    {
                        // The deletion markers/windows were processed when the deletion was deferred —
                        // finishing it is the removal. (P1) leave-play cleanup before the move: snapshot the
                        // post-deletion keywords, then drop the dead card's bindings (previously leaked).
                        ClearPendingDeletion(context, cardId);
                        CardLeavePlayCleanup.OnDeleted(context.CardInstanceRepository, context.EffectRegistry, context, cardId);
                        // (P0-4/RD-4) this deferred-deletion finisher previously moved only the top card,
                        // stranding the dead permanent's digivolution sources in ChoiceZone.None. Mirror
                        // AS-IS DiscardEvoRoots (sources → trash, before the top) here too.
                        await DeletionSourceTrash.TrashEvoSourcesAsync(
                            context.CardInstanceRepository, context.ZoneMover, cardId, gameEventQueue: null, cancellationToken).ConfigureAwait(false);
                        await context.ZoneMover.MoveAsync(
                            new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash),
                            cancellationToken).ConfigureAwait(false);
                        // (P0-4) mandatory post-deletion Fortitude replay, as the sink/battle paths do.
                        await DeletionReplacementGate.TryFortitudeReplayAsync(
                            context.CardInstanceRepository, context.ZoneMover, cardId, cancellationToken, context.EffectRegistry).ConfigureAwait(false);
                        progressed = true;
                        continue;
                    }

                    // (B3) AS-IS DigimonLackDPProcess routes DP<=0 deaths through DestroyPermanentsClass —
                    // the SAME path as effect deletions (would-be-deleted windows, OnDeletion triggers,
                    // Fortitude, the DPZero flag). The previous raw zone move skipped all of it.
                    var sink = new MatchStateMutationSink(
                        context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
                        context.EffectRegistry, context.GameEventQueue, context: context);
                    sink.Apply(new EffectMutation(
                        MatchStateMutationSink.DeleteKind,
                        new HeadlessEntityId("rule:dp-zero"),
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value,
                            [MatchStateMutationSink.IsDpZeroKey] = true,
                        }));
                    await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
                    progressed = true;
                }
            }
        }

        return progressed;
    }

    /// <summary>
    /// (D-2) A field Digimon whose effective DP (base + typed modifiers, via <see cref="DpCalculator"/>)
    /// is 0 or below is destroyed as a state-based action. Only applies when DP is actually DEFINED — a
    /// Digimon with no printed DP at all is left alone (mirrors BattleResolver's "no battle DP" guard
    /// and avoids deleting DP-less abstract fixtures).
    /// </summary>
    // (P6) the holder parks while its chosen sacrifice decides its own would-be-deleted window.
    private static bool IsSacrificeAwaiting(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.ContainsKey(DeletionReplacementTiming.SacrificeAwaitingKey);

    /// <summary>(P7) mirror of AS-IS <c>IsPlaceToTrashDueToNotHavingDP</c> (default true; effects may clear).</summary>
    public const string PlaceToTrashDueToNoDpKey = "isPlaceToTrashDueToNotHavingDP";

    /// <summary>(P7) mirror of AS-IS <c>Permanent.IsPlayedOptionPermanent</c> — an Option "permanent" that a
    /// card effect legitimately keeps on the battle area is exempt.</summary>
    public const string IsPlayedOptionPermanentKey = "isPlayedOptionPermanent";

    /// <summary>(W6 tail) AS-IS <c>AddSelfDeleteEffect</c> marker: delete this permanent at turn end —
    /// value = "own" | "opponent" | "each" (relative to the CARD's owner). The end-turn cleanup promotes a
    /// matching marker to <see cref="DeleteAtTurnEndDueKey"/>; the loop then runs the real sink deletion.</summary>
    public const string DeleteAtTurnEndKey = "deleteAtTurnEnd";
    public const string DeleteAtTurnEndSourceKey = "deleteAtTurnEndSource";
    public const string DeleteAtTurnEndDueKey = "deleteAtTurnEndDue";

    /// <summary>(RD-3) AS-IS Burst Digivolve is TEMPORARY: at the end of the burst player's turn the burst
    /// permanent's TOP card is trashed (CardController.cs:1531-1538 IsBurstDigivolved +
    /// AddTrashTopCardAtTurnEnd). Stamped on the burst top at play; the end-turn cleanup promotes it to
    /// <see cref="BurstTrashAtTurnEndDueKey"/> on the owner's turn; the due sweep then trashes the top card
    /// and promotes the under-source (the permanent survives — same top-trash as Armor Purge).</summary>
    public const string BurstTrashAtTurnEndKey = "burstTrashAtTurnEnd";
    public const string BurstTrashAtTurnEndDueKey = "burstTrashAtTurnEndDue";

    // (P7) AS-IS IsNotHavingDP (AutoProcessing.cs:165-193): DP < 0 && IsPlaceToTrashDueToNotHavingDP &&
    // (IsDigimon — which includes Digi-Eggs — || un-played Option). Reachable types headless-side: a
    // Digi-Egg on the battle area, or an Option not marked isPlayedOptionPermanent.
    private static bool IsNoDpTrashablePermanent(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null ||
            !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null)
        {
            return false;
        }

        // A card with any defined DP is the lethal-DP rule's business, not this one.
        if (TryReadInt(instance.Metadata, BattleResolver.DpKey, out _) ||
            TryReadInt(definition.Metadata, BattleResolver.DpKey, out _))
        {
            return false;
        }

        if (instance.Metadata.TryGetValue(PlaceToTrashDueToNoDpKey, out object? optOut) && optOut is false)
        {
            return false;
        }

        bool isDigiEgg = definition.IsCardType("DigiEgg")
            || definition.IsCardType("Digitama");
        bool isUnplayedOption = definition.IsCardType("Option")
            && !(instance.Metadata.TryGetValue(IsPlayedOptionPermanentKey, out object? played) && played is true);
        return isDigiEgg || isUnplayedOption;
    }

    // (P7) AS-IS: DiscardEvoRoots (sources -> trash) then RemoveField + AddTrashCard — a direct trash, NOT a
    // destroy (no deletion triggers, no would-be-deleted windows).
    private static async Task TrashNoDpPermanentAsync(
        EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId cardId, ChoiceZone zone, CancellationToken cancellationToken)
    {
        if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null)
        {
            foreach (HeadlessEntityId sourceId in DeletionReplacementGate.ReadSourceIds(record.Metadata))
            {
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(playerId, sourceId, ChoiceZone.None, ChoiceZone.Trash, FaceUp: true),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        // Not a deletion: bindings drop, but no post-deletion keyword snapshot (nothing may respond).
        CardLeavePlayCleanup.OnLeftPlay(context.EffectRegistry, cardId);
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool HasLethalDp(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null ||
            !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null ||
            !definition.IsCardType("Digimon"))
        {
            return false;
        }

        if (!TryReadInt(instance.Metadata, BattleResolver.DpKey, out int baseDp) &&
            !TryReadInt(definition.Metadata, BattleResolver.DpKey, out baseDp))
        {
            return false; // no defined DP -> not subject to the DP<=0 rule
        }

        IReadOnlyList<DpModifier> modifiers =
            instance.Metadata.TryGetValue(BattleResolver.DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> typed
                ? typed.ToArray()
                : Array.Empty<DpModifier>();

        // N-2 / D-A1: the DP<=0 deletion rule also reflects continuous DP effects (a continuous -DP that
        // drops the card to 0 deletes it), mirroring the original CanBeDestroyed-via-DP check. No-op until
        // continuous DP effects are registered.
        int staticDp = DpCalculator.ComputeDp(baseDp, modifiers);
        return ContinuousDpGate.ResolveDp(context, cardId, staticDp) <= 0;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> metadata, string key, out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int i: value = i; return true;
            case long l when l >= int.MinValue && l <= int.MaxValue: value = (int)l; return true;
            case double d when d % 1 == 0 && d is >= int.MinValue and <= int.MaxValue: value = (int)d; return true;
            case string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int p): value = p; return true;
            default: return false;
        }
    }

    /// <summary>(F-6.8) A pending-deletion card flagged <c>deletedByBattle</c> is a deferred battle casualty
    /// finalized by <see cref="BattleResolver.FinalizeDeferredAsync"/>, not by the state-based sweep.</summary>
    private static bool IsBattleDeferred(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) && instance is not null &&
        instance.Metadata.TryGetValue(BattleResolver.DeletedByBattleKey, out object? raw) && raw is bool flag && flag;

    private static bool IsPendingDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        return context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) &&
            instance is not null &&
            instance.Metadata.TryGetValue(PendingDeletionKey, out object? raw) &&
            raw is bool flag &&
            flag;
    }

    private static void ClearPendingDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal)
        {
            [PendingDeletionKey] = false
        };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    /// <summary>
    /// (X-05 + D-3 + #2) Drives event-driven trigger collection then resolves the scheduler. Pending
    /// game events are collected into one batch, each trigger's Kind reclassified from the bound
    /// effect's <see cref="Effects.CardEffectDefinition.IsOptional"/>, then ordered by
    /// <see cref="MandatoryEffectOrdering"/> (turn-player first, mandatory before optional).
    /// <para>
    /// MANDATORY triggers are enqueued and resolve immediately. OPTIONAL ("you may") triggers are NOT
    /// auto-resolved: they go to the <see cref="EngineContext.OptionalPromptQueue"/> (turn player's
    /// first) and surface as an agent ResolveChoice decision (activate-which / skip), exactly like the
    /// AS-IS optional-trigger prompt. Opening a prompt pauses the loop via the pending choice.
    /// </para>
    /// </summary>
    private static async Task<(int Resolved, int Collected)> AutoProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
        IReadOnlyList<GameEvent> pendingEvents = context.GameEventQueue.DrainPending();

        // (Stage 5, 3b-iii) Build the UNIFIED window seed — the SCHEDULER mutation triggers PLUS the ACTIVATED
        // effect-bridge markers — WITHOUT collect-time gate/cap (those move to the window's per-pass Gate +
        // commit-time Consume), then drive the re-entrant window loop (AS-IS
        // MultipleSkills.ActivateMultipleSkills_OnePlayer): the controlling player orders simultaneous effects,
        // optionals confirm inline (yes/no), the once-use is consumed at commit, each body resolves through the
        // scheduler OR ActivatedEffectResolver, state-based rule-processing runs BETWEEN picks (F3), and
        // newly-emitted events recurse as cut-ins. A body / order / optional choice suspends the window; it is
        // parked in WindowResolution and the pending choice pauses RunToStable (resumed by the next ResolveChoice).
        IReadOnlyList<TimingWindowTrigger> seed =
            Effects.WindowResolverWiring.CollectUnifiedSeed(context, pendingEvents);
        if (seed.Count == 0)
        {
            // No triggers this pass — still drain any pre-enqueued scheduler request (one enqueued outside the
            // window pipeline) so nothing is stranded, matching the legacy ResolveAllAsync drain.
            IReadOnlyList<EffectResult> drained = await context.EffectScheduler
                .ResolveAllAsync(cancellationToken).ConfigureAwait(false);
            return (drained.Count(result => result.Resolved), 0);
        }

        var run = new Effects.WindowResolverWiring.LiveWindowRun();
        Effects.WindowResolverDeps deps = Effects.WindowResolverWiring.BuildLiveMainLoopDeps(context, run);
        Effects.WindowContinuation continuation = Effects.WindowResolver.CreateContinuation(seed);
        Effects.WindowRunResult result = await new Effects.WindowResolver()
            .DriveAsync(continuation, deps, cancellationToken).ConfigureAwait(false);

        if (result == Effects.WindowRunResult.Suspended)
        {
            context.WindowResolution.Suspend(continuation);
        }
        else
        {
            context.WindowResolution.Clear();
        }

        // `collected` = seed size: a non-empty seed IS work (even a pass that fully fizzles registers progress so
        // RunToStable re-iterates once and then settles when the drained queue yields an empty seed).
        return (run.Progress, seed.Count);
    }

    /// <summary>
    /// (#2) A trigger's mandatory/optional Kind is authoritative from the bound effect's
    /// <c>Definition.IsOptional</c> when the effect is registered with a body; otherwise the
    /// collection-time Kind (event metadata) is kept.
    /// </summary>
    /// <summary>(G11-002/004) Return a copy of <paramref name="request"/> whose resolve context carries the
    /// event's subject (the card the event is about) as the TriggerEntityId, so trigger-gates and effect
    /// bodies can read which card fired the trigger. No-op when already set or the event has no subject.</summary>
    /// <summary>(W6-T) prefix under which the DRIVING game event's metadata (+ its type) is threaded into
    /// the trigger's resolve-context values, so the AS-IS <c>CanTriggerX(hashtable, …)</c> gate mirrors can
    /// read the event exactly like the original hashtable (primitive_w6_design.md W6-T).</summary>
    public const string EventValuePrefix = "event.";
    public const string EventTypeKey = "event.type";

    private static async Task<bool> SweepDueTurnEndDeletionsAsync(EngineContext context, CancellationToken cancellationToken)
    {
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        bool any = false;
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea).ToArray())
            {
                if (!context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
                {
                    continue;
                }

                // (RD-3) a burst-digivolved permanent trashes ONLY its top card (the burst card) and reverts to
                // the prior form — NOT a full deletion. Handle it before the full-deletion path.
                if (record.Metadata.TryGetValue(BurstTrashAtTurnEndDueKey, out object? burstDue) && burstDue is true)
                {
                    var burstMeta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
                    burstMeta.Remove(BurstTrashAtTurnEndDueKey);
                    context.CardInstanceRepository.Upsert(record with { Metadata = burstMeta });
                    await DeDigivolveHelpers.ArmorPurgeTopAsync(
                        context.CardInstanceRepository, context.ZoneMover, id, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
                    any = true;
                    cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                if (!record.Metadata.TryGetValue(DeleteAtTurnEndDueKey, out object? due) || due is not true)
                {
                    continue;
                }

                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
                metadata.Remove(DeleteAtTurnEndDueKey);
                metadata.Remove(DeleteAtTurnEndKey);
                HeadlessEntityId sourceId = metadata.TryGetValue(DeleteAtTurnEndSourceKey, out object? src) && src?.ToString() is { Length: > 0 } v
                    ? new HeadlessEntityId(v)
                    : id;
                metadata.Remove(DeleteAtTurnEndSourceKey);
                context.CardInstanceRepository.Upsert(record with { Metadata = metadata });

                var sink = new Effects.MatchStateMutationSink(
                    context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
                    context.EffectRegistry, context.GameEventQueue, context: context);
                sink.Apply(new Effects.EffectMutation(
                    Effects.MatchStateMutationSink.DeleteKind, sourceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [Effects.MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
                await sink.FlushAsync().ConfigureAwait(false);
                any = true;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return any;
    }

    // (Stage 5, 3b-iii) internal so the window wiring's unified seed collect can enrich identically to the batch.
    internal static EffectRequest EnrichWithEventSubject(EffectRequest request, GameEvent gameEvent)
    {
        HeadlessEntityId? triggerId = request.Context.TriggerEntityId;

        // The subject is the card the event is about. TriggerEventEmitter sets it as the typed GameEvent.Subject
        // (attack/self-scoped windows, e.g. OnAllyAttack); prefer that. Fall back to the metadata forms — a
        // CardMoved event (e.g. a deletion driving OnDestroyedAnyone) carries it as a string CardId.
        if (triggerId is null && gameEvent.Subject is HeadlessEntityId directSubject && !directSubject.IsEmpty)
        {
            triggerId = directSubject;
        }

        if (triggerId is null &&
            (TryReadSubject(gameEvent, AutoProcessingTriggerCollector.SourceEntityIdKey, out string? subjectValue)
             || TryReadSubject(gameEvent, AutoProcessingTriggerCollector.CardIdKey, out subjectValue)))
        {
            triggerId = new HeadlessEntityId(subjectValue!);
        }

        // (W6-T) thread the event's PRIMITIVE metadata alongside — the AS-IS trigger gates read the
        // driving hashtable (isEvolution / Root / DPZero / byBattle …); the port mirror reads
        // "event.<key>" from the resolve-context values. Stored Funcs and complex payloads are skipped.
        var values = new Dictionary<string, object?>(request.Context.Values, StringComparer.Ordinal)
        {
            [EventTypeKey] = gameEvent.Type.ToString(),
        };
        foreach (KeyValuePair<string, object?> pair in gameEvent.Metadata)
        {
            if (pair.Value is string or int or bool or long)
            {
                values[$"{EventValuePrefix}{pair.Key}"] = pair.Value;
            }
        }

        var context = new EffectContext(
            request.Context.SourcePlayerId,
            request.Context.OwnerPlayerId,
            request.Context.SourceEntityId,
            triggerEntityId: triggerId,
            targetEntityIds: request.Context.TargetEntityIds,
            values: values);
        return new EffectRequest(request.EffectId, request.ControllerId, request.Timing, context);
    }

    private static bool TryReadSubject(GameEvent gameEvent, string key, out string? value)
    {
        if (gameEvent.Metadata.TryGetValue(key, out object? raw))
        {
            // TriggerEventEmitter stores the subject as a HeadlessEntityId (SourceEntityIdKey); other producers
            // (e.g. CardMoved) store a string CardId. Accept both, else self-scope gates never see the subject.
            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                value = s;
                return true;
            }

            if (raw is HeadlessEntityId id && !id.IsEmpty)
            {
                value = id.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    internal static TimingWindowTrigger ReclassifyKind(EngineContext context, TimingWindowTrigger trigger)
    {
        if (context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect is { } effect)
        {
            TimingWindowTriggerKind kind = effect.Definition.IsOptional
                ? TimingWindowTriggerKind.Optional
                : TimingWindowTriggerKind.Mandatory;
            if (trigger.Kind != kind)
            {
                return new TimingWindowTrigger(trigger.Request, trigger.Mode, kind, trigger.Priority, trigger.Sequence);
            }
        }

        return trigger;
    }

    /// <summary>
    /// (X-02) Consolidated win-loss evaluation, mirroring Unity AS-IS <c>AutoProcessing</c> reading
    /// <c>Player.IsLose</c>. The <see cref="TerminalEvaluator"/> builds a <see cref="PlayerRuleAdapter"/>
    /// from the active player order plus runtime lose flags; on a terminal verdict the winner/reason is
    /// pushed into the terminal state so <c>DcgoMatch.GetResult()</c> and the <c>GameEnded</c> event carry it.
    /// </summary>
    private static bool EndTurnCheck(EngineContext context)
    {
        if (context.RuleQueryService.IsTerminal())
        {
            return false;
        }

        PlayerTerminalCheck? check = TerminalEvaluator.Evaluate(context);
        if (check is null || !check.IsTerminal)
        {
            return false;
        }

        if (context.RuleQueryService is ITerminalOutcomeSink outcomeSink)
        {
            outcomeSink.SetTerminalOutcome(check.WinnerPlayerId, isDraw: false, check.Message);
        }
        else if (context.RuleQueryService is ITerminalStateController terminalController)
        {
            terminalController.SetTerminal(true);
        }

        context.LogSink.Info(
            $"[EndTurnCheck] Terminal: {check.Reason} winner={check.WinnerPlayerId?.Value} loser={check.LosingPlayerId?.Value}.");
        return true;
    }
}

public enum FlowProcessStatus
{
    Stable,
    PausedForChoice,

    /// <summary>(GPT-#3) The loop hit <see cref="GameFlowProcessor.MaxIterations"/> while still making
    /// progress — it did NOT reach a stable fixpoint. Signals a probable runaway trigger loop.</summary>
    MaxIterationsExceeded,
}

public sealed record FlowProcessResult(
    FlowProcessStatus Status,
    bool ProgressedAny,
    int ResolvedEffectCount,
    int Iterations)
{
    public bool PausedForChoice => Status == FlowProcessStatus.PausedForChoice;

    public bool IsStable => Status == FlowProcessStatus.Stable;

    public bool IsMaxIterationsExceeded => Status == FlowProcessStatus.MaxIterationsExceeded;

    public static FlowProcessResult Stable(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.Stable, progressedAny, resolvedEffectCount, iterations);
    }

    public static FlowProcessResult Paused(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.PausedForChoice, progressedAny, resolvedEffectCount, iterations);
    }

    public static FlowProcessResult MaxIterationsExceeded(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.MaxIterationsExceeded, progressedAny, resolvedEffectCount, iterations);
    }
}
