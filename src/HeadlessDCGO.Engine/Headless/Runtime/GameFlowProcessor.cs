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
    private static async Task<bool> RuleProcessAsync(
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
                        await context.ZoneMover.MoveAsync(
                            new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash),
                            cancellationToken).ConfigureAwait(false);
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

        int collected = 0;
        // (PRIM-P0 B.O.5) delayed one-shot player effects (AS-IS AddEffectToPlayer) that fire this pass — their
        // bindings are removed AFTER resolution (fire-then-clear), never before (the scheduler re-looks-up the
        // binding by id at resolve time, so early removal would leave the enqueued request unbound).
        var oneShotFired = new List<HeadlessEntityId>();
        IReadOnlyList<GameEvent> pendingEvents = context.GameEventQueue.DrainPending();
        if (pendingEvents.Count > 0)
        {
            var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
            var batch = new List<TimingWindowTrigger>();
            var seen = new HashSet<HeadlessEntityId>();
            foreach (GameEvent gameEvent in pendingEvents)
            {
                if (gameEvent.Type == GameEventType.Unknown)
                {
                    continue;
                }

                foreach (TimingWindowTrigger trigger in collector.CollectAllTriggers(gameEvent))
                {
                    if (!seen.Add(trigger.Request.EffectId))
                    {
                        continue;
                    }

                    // D-7: skip effects whose source card is invalidated by a continuous "disable
                    // effects" effect (AS-IS ICardEffect.IsDisabled). Checked before the per-turn gate so
                    // a disabled effect does not consume its once-per-turn use.
                    if (EffectInvalidation.IsEffectsDisabled(context, trigger.Request.Context.SourceEntityId))
                    {
                        continue;
                    }

                    // (G11-002/004) Thread the event subject (the card the event is about, e.g. the deleted
                    // permanent) into the trigger's resolve context so trigger-gates can read it — both for
                    // the gate check below and for the actual resolution downstream.
                    EffectRequest enriched = EnrichWithEventSubject(trigger.Request, gameEvent);
                    IHeadlessCardEffect? effectBody = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect;

                    // (G11-004) Evaluate the effect's gate (condition + trigger-precondition) BEFORE the
                    // per-turn cap is consumed, so a trigger whose condition is not met (e.g. a non-0-DP
                    // deletion) does NOT waste its once-per-turn use. Original AS-IS folds these into one
                    // CanUseCondition; the headless splits gate (CanResolve) from cap (OnceFlag).
                    if (effectBody is not null && !effectBody.CanResolve(new CardEffectResolveContext(enriched)).CanResolve)
                    {
                        continue;
                    }

                    // F-4: gate once-per-turn / max-count-per-turn effects. An effect bound with a
                    // CardEffectDefinition.MaxCountPerTurn cap that has already activated its limit this
                    // turn is skipped; passing effects register a use. Effects without a cap always pass.
                    int? maxCountPerTurn = effectBody?.Definition.MaxCountPerTurn;
                    if (!context.OnceFlags.TryActivate(enriched, maxCountPerTurn))
                    {
                        continue;
                    }

                    if (enriched.Context.Values.TryGetValue(AutoProcessingTriggerCollector.DelayedOneShotKey, out object? oneShot)
                        && oneShot is true)
                    {
                        oneShotFired.Add(enriched.EffectId);
                    }

                    batch.Add(ReclassifyKind(context, new TimingWindowTrigger(
                        enriched, trigger.Mode, trigger.Kind, trigger.Priority, trigger.Sequence)));
                }
            }

            collected = EnqueueOrdered(context, batch);
        }

        IReadOnlyList<EffectResult> results = await context.EffectScheduler
            .ResolveAllAsync(cancellationToken)
            .ConfigureAwait(false);

        // (PRIM-P0 B.O.5) fire-then-clear: remove the delayed one-shot bindings that just resolved so they never
        // fire again (AS-IS AddEffectToPlayer clears its list after firing).
        if (oneShotFired.Count > 0)
        {
            context.EffectRegistry.RemoveWhere(binding => oneShotFired.Contains(binding.Request.EffectId));
        }

        // (PRIM triggered-activated bridge) the scheduler above only resolves IHeadlessCardEffect (mutation)
        // triggers (memory/DP/…). A card's ACTIVATED effects (draw/trash/delete/select) at a general trigger
        // timing are otherwise dropped — resolve them here via ActivatedEffectResolver (the same seam the action
        // handlers use). No-op unless the subject has activated effects at that timing; non-activated effects are
        // skipped by the resolver, so there is no double-resolution.
        int activatedProgress = await BridgeActivatedTriggersAsync(context, pendingEvents, cancellationToken).ConfigureAwait(false);

        // #2: after mandatory effects resolve, surface the next queued optional-trigger prompt to the
        // agent. Counts as progress so the loop re-iterates and pauses on the now-pending choice.
        bool openedPrompt = RequestNextOptionalPrompt(context);

        return (results.Count(result => result.Resolved) + activatedProgress, collected + (openedPrompt ? 1 : 0));
    }

    /// <summary>(PRIM triggered-activated bridge) SUBJECT-scoped timings — the ACTIVATED effects of the card the
    /// event is about (the attacker / the deleted card) resolve. These fire once per event (per attack / per
    /// deletion), so no per-turn cap is needed. ALLOW-LIST excludes action-wired timings (OnEnterFieldAnyone →
    /// PlayCardAction, WhenDigivolving → DigivolveAction, OptionSkill/SecuritySkill) so nothing double-fires.</summary>
    private static readonly IReadOnlySet<EffectTiming> SubjectScopedActivatedTimings = new HashSet<EffectTiming>
    {
        EffectTiming.OnAllyAttack,
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnUnTappedAnyone,
        // OnTappedAnyone moved to EventBroadcastActivatedTimings: "[Your Turn] when an OPPONENT'S Digimon is
        // suspended, …" lives on a DIFFERENT card (e.g. a Tamer, ST4_14) than the suspended subject, so it
        // needs the cross-card broadcast, not the subject-scoped path. The suspend event carries subject =
        // the tapped card, which the broadcast threads for each listener's CanTriggerWhenPermanentSuspends.
        // (v4) attack-declaration window — emitted with subject = attacker alongside OnAttack/OnAllyAttack
        // (AttackPermanentAction:151), so "[When Attacking]" activated effects (incl. Digi-Burst bodies declared
        // at OnDeclaration) resolve at declaration. Once per declaration event; a card gates itself.
        EffectTiming.OnDeclaration,
    };

    /// <summary>(v3) Subject-scoped timings that can fire MULTIPLE times per turn (a card can be re-suspended and
    /// unsuspended). Their activated triggers are capped ONCE PER TURN via <see cref="OnceFlagController"/> — the
    /// AS-IS default for on-unsuspend triggers is "[Once Per Turn]", and under-firing a re-unsuspend is safer than
    /// over-firing (illegal repeat advantage). memory/DP on-unsuspend triggers keep using their IHeadlessCardEffect
    /// forms (scheduler-capped) and are not bridged here.</summary>
    private static readonly IReadOnlySet<EffectTiming> OncePerTurnBridgeTimings = new HashSet<EffectTiming>
    {
        EffectTiming.OnUnTappedAnyone,
    };

    /// <summary>(v2) BOUNDARY timings — carry no card subject (a turn boundary emitted with only an actor). The
    /// ACTIVATED effects of EVERY battle-area card resolve (each card gates "[End of YOUR turn]" itself). These
    /// fire exactly ONCE per turn (one such event per turn), so the per-turn cap is natural — no OnceFlags needed.</summary>
    private static readonly IReadOnlySet<EffectTiming> BoundaryActivatedTimings = new HashSet<EffectTiming>
    {
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
        EffectTiming.OnStartMainPhase,
        // (Other emitted windows use TriggerTimings names with no matching EffectTiming enum member yet —
        // OnEndAttackPhase/OnEndMainPhase/OnDraw — so they need enum + name reconciliation first.)
        // NOTE: OnEndBattle moved to EventBroadcastActivatedTimings below — it carries battle-result metadata
        // (winnerIds/loserIds) that "[End of Battle] when this deletes an opponent in battle" gates need, and
        // the boundary branch discards the driving event. Broadcast per-battle-event is also more correct than
        // the boundary's once-per-pass model when a turn has multiple battles.
    };

    /// <summary>(Phase D broadcast) EVENT-BROADCAST timings — the AS-IS fires these via a global
    /// StackSkillInfos(hashtable, timing) that offers the event to EVERY field card; each card's OWN gate
    /// (e.g. <c>CanTriggerOnTrashDigivolutionCard</c>) decides scope (self / ally / opponent host). The
    /// bridge scans every battle-area card and passes the DRIVING EVENT through to the resolver, so gates
    /// read the event subject (TriggerEntityId = the host permanent, NOT the listener) and the event's
    /// metadata ("event.discardedCardIds" …) — mirror of the scheduler path's <see cref="TriggerTimingMap"/>
    /// broadcast allow-list, for ACTIVATED effects. Fires once per event (a second trash that turn fires
    /// again, as AS-IS), so no per-turn cap here; a card caps itself via MaxCountPerTurn.
    /// KNOWN BOUNDARY: AS-IS GetSkillInfos also offers the timing to trash / hand / face-up security
    /// cards; this scan covers the battle area only (same model as the boundary scan) — every current
    /// listener at these timings lives on the field. Extend the scan zones when a trash/hand listener
    /// is actually ported.</summary>
    private static readonly IReadOnlySet<EffectTiming> EventBroadcastActivatedTimings = new HashSet<EffectTiming>
    {
        EffectTiming.OnDigivolutionCardDiscarded,
        // (Phase D) [End of Battle] — emitted actor-only with battle-result metadata (winnerIds/loserIds/…,
        // BattleResolver). Broadcast to every field card, threading the driving event so gates like
        // CanTriggerWhenDeleteOpponentDigimonByBattle read "event.winnerIds"/"event.loserIds"; each card
        // self-gates. One resolve per battle event (a turn with N battles opens N windows, as AS-IS); a
        // "[Once Per Turn]" card caps itself via MaxCountPerTurn.
        EffectTiming.OnEndBattle,
        // (Phase D) [Your Turn] "when an opponent's Digimon is suspended, …" — the reacting effect lives on a
        // different card (Tamer, ST4_14) than the suspended subject, so it broadcasts. The event carries
        // subject = the suspended card; each listener's CanTriggerWhenPermanentSuspends self-gates on it.
        EffectTiming.OnTappedAnyone,
        // (G2) [Your Turn]/[All Turns] "when an Option is used, …" — the reacting effect lives on a DIFFERENT
        // card (a Tamer/Digimon on the field) than the option card being used. The OnUseOption window IS emitted
        // (OptionActivateAction.cs:94, subject = the option card, which is already in trash by then so it does
        // NOT self-fire), but was in none of the dispatch sets, so the event was dropped. Broadcast it to every
        // battle-area card, threading the driving event so each listener's CanTriggerWhenUseOption /
        // CanTriggerWhenOwnerUseOption self-gates on the used option (subject) + its cost. Fires once per option
        // use; a "[Once Per Turn]" reactor caps itself via MaxCountPerTurn.
        EffectTiming.OnUseOption,
    };

    private static async Task<int> BridgeActivatedTriggersAsync(
        EngineContext context, IReadOnlyList<GameEvent> pendingEvents, CancellationToken cancellationToken)
    {
        if (pendingEvents.Count == 0)
        {
            return 0;
        }

        // Collect the (card, timing) resolutions this pass, de-duplicated, then resolve. Subject-scoped timings
        // resolve the event subject; boundary timings scan every battle-area card (their events carry no subject);
        // event-broadcast timings scan every battle-area card PER EVENT, carrying the driving event through so the
        // per-card gates decide scope (two distinct events fire twice — the AS-IS opens one window per trash).
        var toResolve = new List<(HeadlessEntityId Card, EffectTiming Timing, GameEvent? Event)>();
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

                if (SubjectScopedActivatedTimings.Contains(timing))
                {
                    if (gameEvent.Subject is { IsEmpty: false } subject && seen.Add((subject, timing)))
                    {
                        toResolve.Add((subject, timing, null));
                    }
                }
                else if (BoundaryActivatedTimings.Contains(timing) && zones is not null)
                {
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.BattleArea))
                        {
                            if (seen.Add((cardId, timing)))
                            {
                                toResolve.Add((cardId, timing, null));
                            }
                        }
                    }
                }
                else if (EventBroadcastActivatedTimings.Contains(timing) && zones is not null)
                {
                    // No cross-event de-dup on purpose: each event is its own window. Within one event the
                    // battle-area scan visits a card exactly once.
                    foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
                    {
                        foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.BattleArea))
                        {
                            toResolve.Add((cardId, timing, gameEvent));
                        }
                    }
                }
            }
        }

        int progressed = 0;
        foreach ((HeadlessEntityId card, EffectTiming timing, GameEvent? drivingEvent) in toResolve)
        {
            if (!context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? instance)
                || instance is null || instance.OwnerId.IsEmpty)
            {
                continue;
            }

            // (v3) once-per-turn cap for multi-fire timings (on-unsuspend): the turn-scoped OnceFlagController
            // (reset at turn end) gates one activation per (card, timing) per turn.
            if (OncePerTurnBridgeTimings.Contains(timing))
            {
                var onceRequest = new EffectRequest(
                    new HeadlessEntityId($"{card.Value}:bridgeOnce:{timing}"),
                    instance.OwnerId, timing.ToString(), new EffectContext(instance.OwnerId, card));
                if (!context.OnceFlags.TryActivate(onceRequest, maxCountPerTurn: 1))
                {
                    continue;
                }
            }

            try
            {
                progressed += await Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver
                    .ResolveAsync(context, card, instance.OwnerId, timing, cancellationToken, drivingEvent: drivingEvent)
                    .ConfigureAwait(false);
            }
            catch (DeferredChoicePendingException)
            {
                // An interactive activated trigger suspended — record it (with the driving event, so a broadcast
                // gate re-evaluates against the same subject/metadata on resume) and stop; RunToStableAsync's
                // IsPending check pauses on the next iteration (same pattern as OnPlayReactivation).
                context.DeferredActivations.Suspend(card, timing, instance.OwnerId, drivingEvent);
                return progressed;
            }
        }

        return progressed;
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
                if (!context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null ||
                    !record.Metadata.TryGetValue(DeleteAtTurnEndDueKey, out object? due) || due is not true)
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

    private static EffectRequest EnrichWithEventSubject(EffectRequest request, GameEvent gameEvent)
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

    private static TimingWindowTrigger ReclassifyKind(EngineContext context, TimingWindowTrigger trigger)
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
    /// (D-3 + #2) Enqueues mandatory triggers (turn-player first) to resolve immediately, and routes
    /// optional triggers to the <see cref="OptionalPromptQueue"/> grouped by controller (turn player
    /// first) so they become an agent decision. Returns the number of triggers handled.
    /// </summary>
    private static int EnqueueOrdered(EngineContext context, IReadOnlyList<TimingWindowTrigger> batch)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        HeadlessTurnState turn = context.TurnController.Current;
        HeadlessPlayerId turnPlayer = turn.TurnPlayerId ?? default;

        // No established turn player -> preserve collection order (cannot apply player priority).
        if (turnPlayer.IsEmpty)
        {
            foreach (TimingWindowTrigger trigger in batch)
            {
                context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            }

            return batch.Count;
        }

        HeadlessPlayerId? nonTurnPlayer = null;
        foreach (HeadlessPlayerId candidate in turn.PlayerOrder)
        {
            if (!candidate.IsEmpty && candidate != turnPlayer)
            {
                nonTurnPlayer = candidate;
                break;
            }
        }

        MandatoryEffectOrderResult ordering = new MandatoryEffectOrdering().Order(batch, turnPlayer, nonTurnPlayer);
        if (!ordering.IsSuccess)
        {
            foreach (TimingWindowTrigger trigger in batch)
            {
                context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            }

            return batch.Count;
        }

        int handled = 0;

        // Mandatory (turn-player first) + unknown-controller triggers resolve immediately.
        foreach (TimingWindowTrigger trigger in ordering.OrderedMandatoryTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            handled++;
        }

        foreach (TimingWindowTrigger trigger in ordering.UnknownPlayerTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            handled++;
        }

        // #2: optional triggers -> agent prompt, grouped by controller (turn player's prompt first).
        handled += QueueOptionalPrompts(context, ordering.DeferredOptionalTriggers, turnPlayer, nonTurnPlayer);

        return handled;
    }

    /// <summary>(#2) Enqueues one optional-trigger prompt per controller (turn player first) into the
    /// OptionalPromptQueue. Returns the number of optional triggers queued.</summary>
    private static int QueueOptionalPrompts(
        EngineContext context,
        IReadOnlyList<TimingWindowTrigger> optionalTriggers,
        HeadlessPlayerId turnPlayer,
        HeadlessPlayerId? nonTurnPlayer)
    {
        if (optionalTriggers.Count == 0)
        {
            return 0;
        }

        int queued = 0;
        foreach (HeadlessPlayerId? player in new[] { (HeadlessPlayerId?)turnPlayer, nonTurnPlayer })
        {
            if (player is not { } owner || owner.IsEmpty)
            {
                continue;
            }

            TimingWindowTrigger[] forPlayer = optionalTriggers
                .Where(trigger => trigger.Request.ControllerId == owner)
                .ToArray();
            if (forPlayer.Length == 0)
            {
                continue;
            }

            context.OptionalPromptQueue.EnqueuePrompt(forPlayer, owner);
            queued += forPlayer.Length;
        }

        return queued;
    }

    /// <summary>(#2) Opens the next queued optional-trigger prompt as a pending choice (if any and no
    /// choice is already pending). Returns true when a prompt was opened.</summary>
    private static bool RequestNextOptionalPrompt(EngineContext context)
    {
        if (!context.OptionalPromptQueue.HasPendingPrompt || context.ChoiceController.Current.IsPending)
        {
            return false;
        }

        return context.OptionalPromptQueue.RequestNextChoice(context.ChoiceController).IsSuccess;
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
