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

        // (C2 seam 1) The whole stable loop runs under the ambient match scope so every ported routine reached
        // through it (the state-based deletion sweep's decision-4 transport, the attack pipeline's inline
        // StackSkillInfos inserts, AutoProcessCheck's mirror scan) resolves GManager.instance to THIS match.
        // AmbientMatchContext.Enter is save/restore-nested, so the inner Enter in AutoProcessAsync is a no-op.
        using AmbientMatchContext.Scope _loopScope = AmbientMatchContext.Enter(context);

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

            // (C-Del 3c-2b) The sacrifice-awaiting settle step is retired — Scapegoat/Decoy resolve their
            // sacrifice inside the printed / granted ActivateClass run by the AS-IS PRE cut-in window (the
            // sacrificed ally's own would-be-deleted window opens there), so no invented holder-park needs settling.

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

    /// <summary>(MIG2) AS-IS <c>AutoProcessing.RuleProcess()</c> — the FULL eight-stage rule pass, now owned by
    /// the mirror (<see cref="Assets.Scripts.Script.AutoProcessing"/>). Both callers route through it: the main
    /// loop's step 1 and the window loop's between-picks rule processing (AS-IS MultipleSkills.cs:398-403).</summary>
    internal static async Task<bool> RuleProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        return await Assets.Scripts.Script.AutoProcessing
            .For(context)
            .RuleProcess(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(MIG2) Whether the state-based sweep has actionable work: a finalizable deferred deletion (not
    /// parked on a replacement decision / battle finisher / sacrifice / batch-mate), a no-DP trashable, or a
    /// lethal-DP Digimon. The mirror's <c>DoRuleProcess()</c> substrate extension — AS-IS resolves its
    /// would-be-deleted windows synchronously inside <c>DigimonLackDPProcess</c>, so this state is invisible to
    /// the original's checks.</summary>
    internal static bool HasStateBasedSweepWork(EngineContext context)
    {
        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return false;
        }

        var deletionReplacement = new DeletionReplacementTiming();
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            if (playerId.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, zone))
                {
                    bool pending = IsPendingDeletion(context, cardId);
                    if (pending && !(deletionReplacement.IsPreAwaiting(context, cardId) || IsBattleDeferred(context, cardId)
                        || IsHeldForUnsettledWatch(context, cardId)
                        || HasUndecidedBatchMate(context, deletionReplacement, cardId)))
                    {
                        return true;
                    }

                    if (!pending && zone == ChoiceZone.BattleArea && IsNoDpTrashablePermanent(context, cardId))
                    {
                        return true;
                    }

                    // (MIG2 review P2-3) battle-area-only, matching the sweep's AS-IS scope.
                    if (!pending && zone == ChoiceZone.BattleArea && HasLethalDp(context, cardId))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// (G3.5-RL-C1 + D-2) State-based deletion sweep. Sweeps off the field into the trash any field card
    /// that is either flagged <see cref="PendingDeletionKey"/> (the uniform effect-driven deletion path)
    /// OR a Digimon whose effective DP has dropped to 0 or below (AS-IS <c>DigimonLackDPProcess</c> /
    /// <c>TrashNoDPPermanentProcess</c> / <c>CutInProcess</c>: <c>DP &lt;= 0 &amp;&amp; IsDigimon</c>).
    /// Returns true when it acts so the common loop keeps iterating until the board is stable.
    /// (MIG2) Formerly the whole of RuleProcessAsync; now the CALLEE of the mirror's stage 3/4
    /// (<c>TrashNoDPPermanentProcess</c> runs its own 1:1 pass first; this sweep carries the DP-zero destroys
    /// + the parked-deletion finalizes with the verified D-1/D-2 batch semantics — splitting it back into the
    /// AS-IS separate whole-board passes is design item R2-P2-4, DestroyPermanentsClass mirror at goal 3/7).
    /// </summary>
    internal static async Task<bool> StateBasedDeletionSweepAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return false;
        }

        bool progressed = false;
        var deletionReplacement = new DeletionReplacementTiming();
        // (D-1 / VR-8) AS-IS DigimonLackDPProcess destroys ALL lack-DP permanents in ONE
        // DestroyPermanentsClass(LackPowerPermanents) (AutoProcessing.cs:471-482) = a SINGLE
        // StackSkillInfos(OnDeletion / OnLeaveFieldAnyone). Allocate ONE batch id for every DP-zero death in this
        // sweep (lazily, on the first) so an anyone-scoped reactor fires ONCE — not per card (over-fire).
        // (design item R2-P2-4) the DP-zero deletes interleave per-card with the pendingDeletion finalizes in
        // this one scan; AS-IS runs TrashNoDPPermanent / DigimonLackDP as separate whole-board passes.
        long? dpZeroBatchId = null;
        // (RD-C2-DEFERRED-DELETE-BATCH resolved) the OnDeletion cut-in for a deferred delete batch is opened ONCE
        // per batch id — over ALL its co-finalizing dying members — not per card (the former per-card StackSkillInfos
        // over-fired anyone-scoped reactors for a multi-card deferred batch). This set records the batch ids whose
        // OnDeletion window has already been stacked in THIS sweep pass; every member then does its own cleanup +
        // trash but re-uses the one window.
        var deferredOnDeletionBatchesOpened = new HashSet<long>();
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
                        // (C-Del 3c-2b-pre / RD-3C2BP-01) a PRE-window-promoted member whose "would be deleted"
                        // cut-in is STILL resolving is not finalized yet — AS-IS fixes destroyTargetPermanents_Fixed
                        // only AFTER the WHOLE PRE cut-in completes for every target. Without this, a 2nd+ window-form
                        // replacement in one Destroy is trashed mid-drain before its survival body runs.
                        || IsPromotedAwaitingCutInWindow(context, cardId)
                        // (C-Del 3c-2b / W6-S) WATCH-HOLD: while a DeletePeremanentAndProcessAccordingToResult
                        // continuation is parked (an unsettled DeletionOutcomeWatcher watch), only its TARGETS
                        // finalize in this pass — every other pending deletion HOLDS until the watch settles.
                        // AS-IS runs the nested delete (and its success/failure process — e.g. Scapegoat sparing
                        // its holder when the sacrifice died) INLINE inside the cut-in body, BEFORE the enclosing
                        // Destroy() fixes survivors; without this hold the sweep would trash the enclosing holder
                        // in the same pass as the sacrifice, before the parked successProcess clears its
                        // willBeRemoveField. The loop settles the watcher between passes, then the next pass
                        // finalizes the held member on its settled willBeRemoveField.
                        || IsHeldForUnsettledWatch(context, cardId)
                        // (R2-P1-1) BATCH-MATE gate: a decided member of a deferred delete batch holds until
                        // EVERY member of the same batch id has decided — AS-IS Destroy() trashes the fixed
                        // list in ONE post-cut-in pass (all targets' cut-ins resolve, THEN the per-permanent
                        // trash loop), so no member's [On Deletion] fires before another member's replacement
                        // decision. Once all are decided, the whole batch finalizes in THIS single sweep pass
                        // (one drain), so the (reactor, batch-id) collapse sees one batch, not split drains.
                        || HasUndecidedBatchMate(context, deletionReplacement, cardId)))
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

                    // (MIG2 review P2-3) AS-IS DigimonLackDPProcess scans GetBattleAreaPermanents ONLY
                    // (AutoProcessing.cs:471-473) — a breeding Digimon dropped to 0 DP is NOT destroyed.
                    bool lethalDp = !pending && zone == ChoiceZone.BattleArea && HasLethalDp(context, cardId);
                    if (!pending && !lethalDp)
                    {
                        continue;
                    }

                    if (pending)
                    {
                        // (C-Del 3c-1 promote-to-defer) PROMOTED-SURVIVOR: this card's deletion was promoted to a
                        // deferred replacement whose AS-IS PRE cut-in window resumed and cleared its
                        // willBeRemoveField — it SURVIVES (an interactive Evade/Barrier/Fragment/ArmorPurge fired).
                        // AS-IS Destroy() filters such a card OUT of destroyTargetPermanents_Fixed (:3482) so it is
                        // never trashed and never fires OnDeletion; reset the deferral markers (AS-IS resets
                        // willBeRemoveField at :3591) and leave it on the field.
                        if (IsPromotedSurvivor(context, cardId))
                        {
                            SparePromotedSurvivor(context, cardId);
                            progressed = true;
                            continue;
                        }

                        // (D-1 / VR-8) capture the ORIGINATING batch id the sink stashed at defer time BEFORE the
                        // cleanup below mutates the record's metadata, so the finalize move re-stamps it.
                        // (R2-P1-4) a deferred finalize IS a deletion and the deletion marker now DERIVES the
                        // OnDeletion/OnLeaveField timings — a park with no stashed id (a manually-flagged
                        // pendingDeletion, e.g. a bare-registry sink) gets a fresh batch id instead of moving
                        // unmarked (which would silently drop the dead card's [On Deletion]).
                        IReadOnlyDictionary<string, object?> deferredBatchMetadata =
                            ReadDeferredDeletionBatchMetadata(context, cardId)
                            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                [Effects.MatchStateMutationSink.DeletionBatchIdKey] = context.NextDeletionBatchId(),
                            };
                        // (C2 decision-4 deferred-delete transport / RD-C2-DEFERRED-DELETE-BATCH resolved) The sink
                        // DEFERRED this deletion (a member had a PRE would-be-deleted replacement option, or an
                        // interactive replacement was promoted) so it did NOT open the OnDeletion window at Destroy()
                        // time — the survivor set is known only now. Mirror AS-IS's post-PRE
                        // StackSkillInfos(OnDestroyed/OnLeaveField) pair here, collect-before-removal, while the top
                        // cards are still on the field. AS-IS opens it ONCE over destroyTargetPermanents_Fixed, so
                        // open it ONCE per deferred BATCH id over ALL its co-finalizing dying members (the batch-mate
                        // gate held every decided member until the batch decided together, so all dying members are
                        // on the field now). A card with no stashed batch id keeps its own single-card window.
                        long? deferredBatchId =
                            deferredBatchMetadata.TryGetValue(Effects.MatchStateMutationSink.DeletionBatchIdKey, out object? bidRaw) && bidRaw is long b && b != 0
                                ? b : null;
                        if (deferredBatchId is long batchId)
                        {
                            if (deferredOnDeletionBatchesOpened.Add(batchId))
                            {
                                await OpenDeferredBatchOnDeletionWindowAsync(context, zoneReader, batchId, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        else if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? dyingRecord) && dyingRecord is not null)
                        {
                            using AmbientMatchContext.Scope _deferredScope = AmbientMatchContext.Enter(context);
                            bool dyingDpZero = dyingRecord.Metadata.TryGetValue(Effects.MatchStateMutationSink.IsDpZeroKey, out object? dz) && dz is true;
                            var deferredAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);
                            // AS-IS builds a FRESH OnDeletionHashtable per StackSkillInfos (CardController.cs:3489-3509).
                            // (P1-1 C2r) The deferred finisher is the effect-delete path (a PRE replacement was
                            // declined), so byBattle = false; byEffect = !dyingDpZero (a DP-zero delete carries no
                            // CardEffect in AS-IS — DPZero=true / IsByEffect=false).
                            System.Collections.Hashtable DeferredOnDeletion() =>
                                Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.OnDeletionHashtable(
                                    new List<Assets.Scripts.Script.CardEffectCommons.Permanent>
                                    {
                                        new(context, cardId, dyingRecord.OwnerId),
                                    },
                                    byEffectCause: !dyingDpZero, byBattleCause: false, dyingDpZero);
                            await deferredAutoProcessing.StackSkillInfos(
                                DeferredOnDeletion(), Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnDestroyedAnyone).ConfigureAwait(false);
                            await deferredAutoProcessing.StackSkillInfos(
                                DeferredOnDeletion(), Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnLeaveFieldAnyone).ConfigureAwait(false);
                        }

                        // (C-Del 3c-1) a promoted-DIED card kept willBeRemoveField=true through the sweep (only a
                        // survivor's is cleared by the resumed window). AS-IS Destroy() resets it at :3591 as the
                        // card is trashed — clear it now so no stale marker rides the trashed instance.
                        ResetWillBeRemoveField(context, cardId);

                        // (P1) leave-play cleanup before the move: snapshot the
                        // post-deletion keywords, then drop the dead card's bindings (previously leaked).
                        ClearPendingDeletion(context, cardId);
                        CardLeavePlayCleanup.OnDeleted(context.CardInstanceRepository, context, cardId);
                        // (P0-4/RD-4) this deferred-deletion finisher previously moved only the top card,
                        // stranding the dead permanent's digivolution sources in ChoiceZone.None. Mirror
                        // AS-IS DiscardEvoRoots (sources → trash, before the top) here too.
                        await DeletionSourceTrash.TrashEvoSourcesAsync(
                            context.CardInstanceRepository, context.ZoneMover, cardId, gameEventQueue: null, cancellationToken,
                            context.MemoryController, context.TurnController.Current.TurnPlayerId).ConfigureAwait(false);
                        // (C-2 review P1) charge the leaving ACE's OWN top overflow (raw move skips the sink gate).
                        AceOverflowGate.ApplyTopOverflowOnDelete(context, cardId);
                        // (D-1 / VR-8) re-stamp the ORIGINATING batch id the sink stored when it deferred this
                        // deletion, so a declined would-be-deleted finishes the same batch its Destroy() opened.
                        await context.ZoneMover.MoveAsync(
                            new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash, Metadata: deferredBatchMetadata),
                            cancellationToken).ConfigureAwait(false);
                        // (C-Del 3a RETIRED) Fortitude no longer replays via the invented gate — it fires through
                        // the AS-IS OnDestroyedAnyone cut-in window (printed FortitudeEffect / granted bucket).
                        progressed = true;
                        continue;
                    }

                    // (B3) AS-IS DigimonLackDPProcess routes DP<=0 deaths through DestroyPermanentsClass —
                    // the SAME path as effect deletions (would-be-deleted windows, OnDeletion triggers,
                    // Fortitude, the DPZero flag). The previous raw zone move skipped all of it.
                    dpZeroBatchId ??= context.NextDeletionBatchId();
                    var sink = new MatchStateMutationSink(
                        context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
                        context.GameEventQueue, context: context,
                        explicitDeletionBatchId: dpZeroBatchId);
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

        // (RD-3C1-01) Replay the TRAILING staged ops of any interactive-PRE-promoted delete batch that has now
        // FULLY finalized (every member decided this pass — spared or trashed — so none still carries the batch's
        // pendingDeletion). The sink stashed these ops (the effect's statements AFTER `yield return Destroy()`, e.g.
        // the DRAW of "delete an enemy Digimon; then draw 1") when the interactive cut-in pause was swallowed. Run
        // them here — after the casualties were trashed and the batch OnDeletion was STACKED above, but before that
        // OnDeletion window drains (the next RunToStable AutoProcess pass) — exactly the AS-IS coroutine's position
        // resuming its trailing statements after Destroy() returns. A batch still awaiting its cut-in (members held
        // as IsPromotedAwaitingCutInWindow, still pending) is skipped; its ops replay on the pass that finalizes it.
        if (context.HasPromotedBatchTrailingOps)
        {
            foreach (long batchId in context.PromotedBatchTrailingOpBatchIds)
            {
                if (AnyPendingMemberOfBatch(context, zoneReader, batchId))
                {
                    continue;   // batch not fully finalized yet — hold the trailing ops for a later sweep pass
                }

                IReadOnlyList<Func<CancellationToken, Task>>? trailing = context.TakePromotedBatchTrailingOps(batchId);
                if (trailing is null)
                {
                    continue;
                }

                foreach (Func<CancellationToken, Task> op in trailing)
                {
                    await op(cancellationToken).ConfigureAwait(false);
                    progressed = true;
                }
            }
        }

        return progressed;
    }

    /// <summary>(RD-3C1-01) Whether any FIELD member still carries the given deferred delete <paramref name="batchId"/>
    /// as a pending deletion — i.e. the batch has not finished finalizing (a member is held awaiting its cut-in, or
    /// undecided). Used to gate the trailing-op replay so it fires only once every member of a promoted batch has been
    /// spared or trashed.</summary>
    private static bool AnyPendingMemberOfBatch(EngineContext context, IZoneStateReader zoneReader, long batchId)
    {
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            if (playerId.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, zone))
                {
                    if (!IsPendingDeletion(context, cardId) ||
                        !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
                        record is null ||
                        !record.Metadata.TryGetValue(MatchStateMutationSink.DeletionBatchIdKey, out object? raw) ||
                        raw is not long memberBatchId || memberBatchId != batchId)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// (D-2) A field Digimon whose effective DP (base + typed modifiers, via <see cref="DpCalculator"/>)
    /// is 0 or below is destroyed as a state-based action. Only applies when DP is actually DEFINED — a
    /// Digimon with no printed DP at all is left alone (mirrors BattleResolver's "no battle DP" guard
    /// and avoids deleting DP-less abstract fixtures).
    /// </summary>
    // (C-Del 3c-2b RETIRED) IsSacrificeAwaiting removed — the invented cross-card sacrifice holder-park
    // (SacrificeAwaitingKey) is retired; Scapegoat/Decoy resolve inside the AS-IS PRE cut-in window.

    /// <summary>(C-Del 3c-2b / W6-S) While a parked DeletePeremanentAndProcessAccordingToResult continuation
    /// (an unsettled <see cref="DeletionOutcomeWatcher"/> watch) exists, a pending deletion that is NOT one of
    /// its targets HOLDS — the targets finalize first, the loop fires the settled watch (whose success/failure
    /// process may spare the held member, AS-IS inline ordering), and the next pass finalizes the held member
    /// on its settled <c>willBeRemoveField</c>.</summary>
    private static bool IsHeldForUnsettledWatch(EngineContext context, HeadlessEntityId cardId) =>
        context.TryGetService(out DeletionOutcomeWatcher? watcher) && watcher is not null &&
        watcher.Count > 0 && !watcher.IsWatchTarget(cardId);

    /// <summary>(R2-P1-1) Whether another pending-deletion field card of the SAME delete batch (the batch id
    /// the sink stashed at defer time) is still awaiting a decision (PRE window / sacrifice sub-choice).
    /// While true, this (already-decided) member is NOT finalized — the batch finalizes together in one pass.
    /// Batch id 0 / absent (a context-less sink or a pre-batch defer) is never grouped.</summary>
    private static bool HasUndecidedBatchMate(
        EngineContext context, DeletionReplacementTiming deletionReplacement, HeadlessEntityId cardId)
    {
        if (context.ZoneMover is not IZoneStateReader zones ||
            !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null ||
            !record.Metadata.TryGetValue(Effects.MatchStateMutationSink.DeletionBatchIdKey, out object? raw) ||
            raw is not long batchId || batchId == 0)
        {
            return false;
        }

        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId mateId in zones.GetCards(player, zone))
                {
                    if (mateId == cardId ||
                        !context.CardInstanceRepository.TryGetInstance(mateId, out CardInstanceRecord? mate) || mate is null ||
                        !mate.Metadata.TryGetValue(Effects.MatchStateMutationSink.DeletionBatchIdKey, out object? mateRaw) ||
                        mateRaw is not long mateBatchId || mateBatchId != batchId ||
                        !IsPendingDeletion(context, mateId))
                    {
                        continue;
                    }

                    if (deletionReplacement.IsPreAwaiting(context, mateId))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

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
        CardLeavePlayCleanup.OnLeftPlay(cardId);
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
        _ = modifiers;
        if (new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId, instance.OwnerId).DP > 0)
        {
            return false;
        }

        // (MIG2) AS-IS IsDigimonLackDP (:205) gates on Permanent.CanBeDestroyed() at PREDICATE level — a
        // Delete/Prevent-protected 0-DP Digimon is never selected (the sink would refuse it anyway, but the
        // predicate-side check is what keeps the rule loop from re-selecting it forever).
        return new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId, instance.OwnerId).CanBeDestroyed();
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

    /// <summary>(D-1 / VR-8) Read the delete-batch id the sink stashed on a DEFERRED deletion (a would-be-deleted
    /// replacement it parked) so the deferred-finalize move re-stamps the ORIGINATING <c>Destroy()</c>'s batch.
    /// Null when absent (no batch was recorded — the CardMoved then carries no id and collapses with other
    /// unstamped moves, the pre-D1 behaviour).</summary>
    private static IReadOnlyDictionary<string, object?>? ReadDeferredDeletionBatchMetadata(
        EngineContext context, HeadlessEntityId cardId)
    {
        if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) &&
            instance is not null &&
            instance.Metadata.TryGetValue(MatchStateMutationSink.DeletionBatchIdKey, out object? raw) &&
            raw is long batchId)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.DeletionBatchIdKey] = batchId };
        }

        return null;
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

    /// <summary>(C-Del 3c-1) Whether this deferred deletion was PROMOTED to the AS-IS PRE cut-in window (an
    /// interactive Evade/Barrier/Fragment/ArmorPurge replacement) AND its resumed window body cleared
    /// <c>willBeRemoveField</c> — i.e. the replacement fired and the card SURVIVES. A promoted card that kept
    /// <c>willBeRemoveField</c> set (the replacement declined / did not fire) is NOT a survivor and finalizes
    /// normally.</summary>
    /// <summary>(C-Del 3c-2b-pre / RD-3C2BP-01) A PRE-window-promoted deferred deletion whose "would be deleted"
    /// cut-in window (the <c>ForCutIn</c> pool the sink opens the PRE pair on) is STILL resolving must NOT be
    /// finalized yet. AS-IS Destroy() fixes destroyTargetPermanents_Fixed — and trashes it — only AFTER the WHOLE
    /// PRE cut-in (<c>TriggeredSkillProcess</c>) has completed for EVERY target (CardController.cs:3471-3496). When
    /// TWO+ window-form replacements resolve in one Destroy, the state-based sweep is re-entered mid-drain
    /// (AutoProcessing.DigimonLackDPProcess) BETWEEN the picks; without this hold it would finalize a member whose
    /// survival body has not run yet (willBeRemoveField still true) and trash it, even though the resumed body would
    /// spare it. Hold while a cut-in window is suspended; once it fully resolves the sweep finalizes the whole batch
    /// on the settled willBeRemoveField (survivors spared / casualties trashed together).</summary>
    private static bool IsPromotedAwaitingCutInWindow(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null ||
            !(instance.Metadata.TryGetValue(MatchStateMutationSink.PreWindowPromotedKey, out object? promoted) && promoted is true))
        {
            return false;
        }

        return Assets.Scripts.Script.AutoProcessing.ForCutIn(context).executingMultipleSkills is not null;
    }

    private static bool IsPromotedSurvivor(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null ||
            !(instance.Metadata.TryGetValue(MatchStateMutationSink.PreWindowPromotedKey, out object? promoted) && promoted is true))
        {
            return false;
        }

        return !(instance.Metadata.TryGetValue("willBeRemoveField", out object? wbr) && wbr is true);
    }

    /// <summary>(C-Del 3c-1) Clear a promoted survivor's deferral: drop pendingDeletion + the promote marker +
    /// the (already-false) willBeRemoveField and the transient deferral cause keys, so the spared card is a clean
    /// live permanent again (AS-IS Destroy() never trashes it and resets willBeRemoveField at :3591).</summary>
    private static void SparePromotedSurvivor(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal)
        {
            [PendingDeletionKey] = false,
        };
        metadata.Remove(MatchStateMutationSink.PreWindowPromotedKey);
        metadata.Remove("willBeRemoveField");
        metadata.Remove(MatchStateMutationSink.DeletedByEffectKey);
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    /// <summary>(C-Del 3c-1) Reset a promoted-DIED card's <c>willBeRemoveField</c> flag before it is trashed
    /// (AS-IS Destroy() :3591). No-op for a gate-declined card (the flag was never set).</summary>
    private static void ResetWillBeRemoveField(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null ||
            !instance.Metadata.ContainsKey("willBeRemoveField"))
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal);
        metadata.Remove("willBeRemoveField");
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    /// <summary>(RD-C2-DEFERRED-DELETE-BATCH resolved) Open the AS-IS OnDeletion cut-in window ONCE over the whole
    /// deferred delete BATCH — every field member sharing <paramref name="batchId"/> that is still pending and is
    /// actually DYING (not a promoted survivor). Mirrors AS-IS Destroy()'s single
    /// <c>StackSkillInfos(OnDeletionHashtable(destroyTargetPermanents_Fixed))</c> pair (CardController.cs:3736-3756)
    /// so an anyone-scoped reactor fires ONCE for the batch, not once per member. Collect-before-removal: the top
    /// cards are still on the field (the per-card trash runs afterwards). cardEffect/battle = null; isDPZero from
    /// the members' stamped flag (uniform within one Destroy).</summary>
    private static async Task OpenDeferredBatchOnDeletionWindowAsync(
        EngineContext context, IZoneStateReader zoneReader, long batchId, CancellationToken cancellationToken)
    {
        var deadPermanents = new List<Assets.Scripts.Script.CardEffectCommons.Permanent>();
        bool anyDpZero = false;
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId mateId in zoneReader.GetCards(player, zone))
                {
                    if (!IsPendingDeletion(context, mateId) || IsPromotedSurvivor(context, mateId) ||
                        !context.CardInstanceRepository.TryGetInstance(mateId, out CardInstanceRecord? mate) || mate is null ||
                        !mate.Metadata.TryGetValue(MatchStateMutationSink.DeletionBatchIdKey, out object? mateRaw) ||
                        mateRaw is not long mateBatchId || mateBatchId != batchId)
                    {
                        continue;
                    }

                    deadPermanents.Add(new Assets.Scripts.Script.CardEffectCommons.Permanent(context, mateId, player));
                    anyDpZero |= mate.Metadata.TryGetValue(Effects.MatchStateMutationSink.IsDpZeroKey, out object? dz) && dz is true;
                }
            }
        }

        if (deadPermanents.Count == 0)
        {
            return;
        }

        using AmbientMatchContext.Scope _batchScope = AmbientMatchContext.Enter(context);
        var deferredAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);
        // AS-IS builds a FRESH OnDeletionHashtable per StackSkillInfos (CardController.cs:3736/3749).
        System.Collections.Hashtable BatchOnDeletion() =>
            Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.OnDeletionHashtable(
                deadPermanents, byEffectCause: !anyDpZero, byBattleCause: false, anyDpZero);
        await deferredAutoProcessing.StackSkillInfos(
            BatchOnDeletion(), Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnDestroyedAnyone).ConfigureAwait(false);
        await deferredAutoProcessing.StackSkillInfos(
            BatchOnDeletion(), Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnLeaveFieldAnyone).ConfigureAwait(false);
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
    /// <summary>(A-2 / RD-6) internal so <c>MetadataActionProcessor.EndTurnAsync</c> can drive the [End of Your
    /// Turn] window in the ENDING player's still-live frame (drain-before-flip) by looping this one drain pass —
    /// the same unified-seed window drive the main RunToStable loop uses, without re-entering RunToStable.</summary>
    internal static async Task<(int Resolved, int Collected)> AutoProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        // (C2 seam 1 — window SkillInfo cutover) Drive the MIRROR trigger stack instead of the legacy
        // CollectUnifiedSeed + WindowResolver. The AS-IS routines (ported into Assets/Scripts/Script) open their
        // trigger windows by calling `autoProcessing.StackSkillInfos(hashtable, timing)` inline at their emit
        // positions (the C1 R-timing inserts + the decision-4 deletion transport + the attack-family inserts);
        // those accumulate on the mirror stack DURING routine execution. Here we (a) convert each drained
        // GameEvent into the AS-IS (Hashtable, EffectTiming) supply calls the SkillWindowSupply layer can faithfully
        // reconstruct (the M-timing / turn-boundary cluster — everything else GAP-drops, opened by an inline insert
        // instead), (b) A3-sequence them into minimum-batch passes, and (c) stack + `AutoProcessCheck` per pass
        // (decision #2 granularity), so N cards of one Destroy() collapse into one window while an independent later
        // batch resolves in the next pass. AutoProcessCheck = RuleProcess -> RulesTiming stack ->
        // TriggeredSkillProcess (AS-IS AutoProcessing.cs:122-140), which drains the mirror stack through the live
        // MultipleSkills window loop. A body/order/optional choice suspends inside the loop (the choice port opens a
        // ChoiceController request and throws WindowChoicePendingException); the pending choice pauses RunToStable
        // and the next ResolveChoice resumes via AutoProcessing.ResumeSuspendedWindowsAsync (MetadataActionProcessor
        // seams). GManager.instance resolves off AmbientMatchContext, so the whole drive runs under an Enter scope.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);
        Assets.Scripts.Script.AutoProcessing autoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);

        context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
        IReadOnlyList<GameEvent> pendingEvents = context.GameEventQueue.DrainPending();

        var entries = new List<Effects.SkillWindowSupplyEntry>();
        foreach (GameEvent gameEvent in pendingEvents)
        {
            entries.AddRange(Effects.SkillWindowSupply.ConvertEvent(context, gameEvent));
        }

        // Progress signal for RunToStable: any drained event OR any skill already inline-stacked at entry is work
        // (so the loop re-iterates); a pass with no events and an empty stack that resolves nothing returns 0 and
        // RunToStable settles. AutoProcessCheck itself may emit events (a resolved body mutating state), which the
        // NEXT iteration picks up — so the loop does not settle prematurely while real work is emitting.
        int stackedAtEntry = autoProcessing.StackedSkillInfos.Count;

        IReadOnlyList<IReadOnlyList<Effects.SkillWindowSupplyEntry>> passes =
            Effects.SkillWindowSupply.SequenceByMinimumBatch(entries);

        // (C2 pause contract) A window ORDER choice (AgentSkillWindowChoicePort) / a body's agent choice opens a
        // ChoiceController request and THROWS to unwind the mirror loop's C# stack — that is the normal headless
        // suspension, not an error. Catch it here: the choice is already pending (so RunToStable pauses on the
        // next iteration) and the suspended MultipleSkills chain resumes via ResumeSuspendedWindowsAsync when the
        // agent answers (MetadataActionProcessor seams).
        try
        {
            if (passes.Count == 0)
            {
                // No supply-converted windows this pass — still drain any inline-stacked skills (R/deletion/attack
                // inserts) and run the rule / RulesTiming pass once through the mirror AutoProcessCheck.
                await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                foreach (IReadOnlyList<Effects.SkillWindowSupplyEntry> pass in passes)
                {
                    foreach (Effects.SkillWindowSupplyEntry entry in pass)
                    {
                        await autoProcessing.StackSkillInfos(entry.Hashtable, entry.Timing).ConfigureAwait(false);
                    }

                    await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is Effects.WindowChoicePendingException or DeferredChoicePendingException)
        {
            // Suspended for an agent choice — the drained work IS progress; RunToStable pauses on the pending choice.
            return (0, Math.Max(1, pendingEvents.Count + stackedAtEntry));
        }

        // Also drain any pre-enqueued scheduler request (one enqueued outside the window pipeline) so nothing is
        // stranded, matching the legacy ResolveAllAsync drain (still used by non-window scheduler enqueues).
        IReadOnlyList<EffectResult> drained = await context.EffectScheduler
            .ResolveAllAsync(cancellationToken).ConfigureAwait(false);

        // Progress = supply-converted windows (entries) + inline-stacked skills at entry. A drained event that
        // matched NO supply timing and stacked nothing is NOT work (so RunToStable settles instead of looping on a
        // non-matching event) — mirrors the old `seed.Count == 0 => no progress` fixpoint.
        int collected = entries.Count + stackedAtEntry;
        return (drained.Count(result => result.Resolved), collected);
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

                // (RD-3 / E2-01) a burst-digivolved permanent trashes ONLY its top card and reverts to the prior form
                // — NOT a full deletion. AS-IS (SelectBurstDigivolutionEffect.cs:315) reads permanent.TopCard LIVE, so
                // the trash target is the CURRENT top, even if a same-turn re-digivolve buried the burst card. The DUE
                // marker sits on the burst instance (top or now-buried source); locate it anywhere in the permanent's
                // stack and trash this permanent's live top. Handle it before the full-deletion path.
                HeadlessEntityId? burstMarkedId = null;
                if (record.Metadata.TryGetValue(BurstTrashAtTurnEndDueKey, out object? topBurstDue) && topBurstDue is true)
                {
                    burstMarkedId = id;
                }
                else
                {
                    foreach (HeadlessEntityId burstSourceId in DeletionReplacementGate.ReadSourceIds(record.Metadata))
                    {
                        if (context.CardInstanceRepository.TryGetInstance(burstSourceId, out CardInstanceRecord? source) && source is not null
                            && source.Metadata.TryGetValue(BurstTrashAtTurnEndDueKey, out object? srcBurstDue) && srcBurstDue is true)
                        {
                            burstMarkedId = burstSourceId;
                            break;
                        }
                    }
                }

                if (burstMarkedId is HeadlessEntityId markedId)
                {
                    if (context.CardInstanceRepository.TryGetInstance(markedId, out CardInstanceRecord? marked) && marked is not null)
                    {
                        var burstMeta = new Dictionary<string, object?>(marked.Metadata, StringComparer.Ordinal);
                        burstMeta.Remove(BurstTrashAtTurnEndDueKey);
                        context.CardInstanceRepository.Upsert(marked with { Metadata = burstMeta });
                    }

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
                    context.GameEventQueue, context: context);
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
        // (RC-6) The registry Find arm — which read the invented EffectRegistry trigger binding's Effect body to
        // reclassify a trigger's Kind (Optional/Mandatory) from its IsOptional flag — is excised: no real card
        // ever produced a trigger binding (producer 0), so the arm never hit, and the registry trigger-reader
        // half is retired. Kind now stands as the emitting pipeline stamped it (live path unchanged).
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

        // (MIG2 review P1-1) AS-IS EndGameProcess (AutoProcessing.cs:392-400): both players losing = DRAW
        // (EndGame(null)); the evaluator names a winner unconditionally, so re-check the "winner" here.
        bool bothLose = check.WinnerPlayerId is { } winner && !winner.IsEmpty
            && context.PlayerStatusController.IsLose(winner);
        if (context.RuleQueryService is ITerminalOutcomeSink outcomeSink)
        {
            if (bothLose)
            {
                outcomeSink.SetTerminalOutcome(null, isDraw: true, "Both players lose — draw.");
            }
            else
            {
                outcomeSink.SetTerminalOutcome(check.WinnerPlayerId, isDraw: false, check.Message);
            }
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
