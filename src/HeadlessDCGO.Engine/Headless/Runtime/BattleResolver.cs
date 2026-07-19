namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class BattleResolver
{
    public const string DpKey = "dp";
    public const string DpModifiersKey = "dpModifiers";
    public const string DeletedByBattleKey = "deletedByBattle";

    /// <summary>HEADLESS defer-time value: the DP used in the battle comparison, stamped when the loss is
    /// FLAGGED (<see cref="FlagPendingBattleDeletion(EngineContext, CardInstanceRecord, int)"/>). Distinct from
    /// (R2-P1-3) <see cref="CardLeavePlayCleanup.DpJustBeforeRemoveFieldKey"/> — the AS-IS record-parameters
    /// block records THAT one later, after the PRE cut-in fixed the target list (CardController.cs:3762-3783);
    /// both keys are kept because their record moments differ (a cut-in DP change is visible only in the latter).</summary>
    public const string DpBeforeBattleKey = "dpBeforeBattle";
    public const string PreventBattleDeletionKey = "preventBattleDeletion";
    // (G-clean) HasPiercingKey / HasRetaliationKey deleted — the A군 4단계 IBattle-payload cutover physically
    // removed the manual Retaliation/Pierce block, leaving these flag consts with zero readers/writers.
    public const string HasIcecladKey = "hasIceclad";
    public const string SourceIdsKey = "sourceIds";

    /// <summary>(C-Del 3c-1b PRE cut-in transport) marks a battle loser whose AS-IS "would be deleted" PRE cut-in
    /// window (<c>WhenPermanentWouldBeDeleted → WhenRemoveField</c>) has already been opened this battle. It stops
    /// <see cref="OpenBattlePreCutInWindowAsync"/> from re-opening the window on a deferred re-entry
    /// (<see cref="FinalizeDeferredAsync"/> after an interactive replacement parked), and gates the survivor fix
    /// in <see cref="ResolveRoundAsync"/> (a windowed loser whose <c>willBeRemoveField</c> a replacement cleared is
    /// spared). Cleared on a spared survivor and reset on a trashed casualty (AS-IS resets willBeRemoveField at
    /// :3591).</summary>
    public const string BattlePreWindowedKey = "battlePreWindowed";

    /// <summary>(C-Del 3c-1b) Result of opening the battle PRE cut-in window: the drain completed inline
    /// (<see cref="Drained"/>) or an INTERACTIVE replacement paused it (<see cref="Deferred"/>) and the battle
    /// must park (promote-to-defer, 3c-1).</summary>
    private enum BattlePreWindowResult
    {
        Drained,
        Deferred,
    }

    public async Task<BattleResolutionResult> ResolveAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // (C2) The whole battle resolution runs under the ambient match scope: the mirror R1 getters it reads
        // (HasRetaliation/HasPierce via Permanent.EffectList) resolve GManager.instance, which a direct-call
        // harness does not otherwise scope. Nest-safe under RunToStableAsync's outer Enter.
        using AmbientMatchContext.Scope _battleScope = AmbientMatchContext.Enter(context);

        HeadlessAttackState attack = context.AttackController.Current;
        BattleParticipant? attacker = null;
        BattleParticipant? defender = null;

        string? validationFailure = ValidateBattle(
            context,
            attack,
            out attacker,
            out defender);
        if (validationFailure is not null)
        {
            return BattleResolutionResult.Failure(validationFailure, attack);
        }

        // (C2 seam 6) SYNCHRONOUS OnStartBattle window — AS-IS CardController.Battle :4553-4557 (payload) / :4600
        // (AutoProcessCheck). The legacy registry gate (`GetEffectsForTiming(OnStartBattle).Count > 0`) is REMOVED:
        // the mirror always builds the AS-IS `{AttackingPermanent, DefendingPermanent, DefendingCard}` payload and
        // drains it through GetSkillInfos → PutStackedSkill → AutoProcessCheck (an empty scan for the common
        // no-effect battle is byte-for-byte unchanged, so it stays cheap). Then re-read both participants so a
        // battle-start DP change affects the outcome. AutoProcessCheck here mirrors AS-IS's mid-battle rule pass at
        // :4600. Runs under RunToStableAsync's AmbientMatchContext scope.
        {
            using AmbientMatchContext.Scope _startBattleScope = AmbientMatchContext.Enter(context);
            var startAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);
            var startBattlePayload = new System.Collections.Hashtable
            {
                { "AttackingPermanent", new Assets.Scripts.Script.CardEffectCommons.Permanent(context, attacker!.InstanceId, attacker!.OwnerId) },
                { "DefendingPermanent", new Assets.Scripts.Script.CardEffectCommons.Permanent(context, defender!.InstanceId, defender!.OwnerId) },
                { "DefendingCard", new Assets.Scripts.Script.CardEffectCommons.CardSource(context, defender!.InstanceId, defender!.OwnerId, defender!.OwnerId) },
            };
            Assets.Scripts.Script.AutoProcessing
                .GetSkillInfos(startBattlePayload, Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnStartBattle)
                .ForEach(skillInfo => startAutoProcessing.PutStackedSkill(skillInfo));
            await startAutoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);

            if (context.ZoneMover is IZoneStateReader startReader)
            {
                if (TryReadParticipant(context, startReader, attacker!.OwnerId, attacker!.InstanceId, "Attacker", out BattleParticipant? a2) is null && a2 is not null) attacker = a2;
                if (TryReadParticipant(context, startReader, defender!.OwnerId, defender!.InstanceId, "Defender", out BattleParticipant? d2) is null && d2 is not null) defender = d2;
            }
        }

        // G3.5-RL-C2: who is deleted is the DP comparison adjusted by battle keywords.
        int comparison = CompareBattleStats(attacker!, defender!);
        var deleted = new List<BattleParticipant>();

        // (d-remediation) AS-IS Permanent.HasDP gate (CardController.Battle :4478): a DP battle only occurs when
        // BOTH participants HAVE a DP (Permanent.DP != -1). A DontHaveDP participant (resolved DP = the no-DP
        // sentinel) neither deletes nor is deleted by battle — the battle block is skipped entirely.
        if (attacker!.Dp <= Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.NoDpValue
            || defender!.Dp <= Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.NoDpValue)
        {
            return await ResolveRoundAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // (structure-1:1) AS-IS gates EACH battle loser with CanBeDestroyedByBattle(...) BEFORE adding it to
        // LoserPermanents (CardController.Battle :4618/4628/4631/4638) — not add-then-RemoveAll. PreventBattleDeletion
        // (CanNotBeDeletedByBattle flag) + continuous deletion-prevention REPLACEMENTS (R2-1/N-2) are the headless
        // form of CanBeDestroyedByBattle. A participant that cannot be battle-destroyed is never added as a loser.
        //
        // NOTE: Jamming is intentionally NOT a mutual-deletion rule here. In the original, Jamming is a conditional
        // CanNotBeDestroyedByBattle that protects the ATTACKER only when it battles a Security Digimon — that surface
        // lives in SecurityResolver (W5). This field battle (attacker vs a battle-area Digimon) is unaffected by Jamming.
        bool CanBeBattleDestroyed(BattleParticipant participant) =>
            !HasFlag(participant, PreventBattleDeletionKey) &&
            !BattleDeletionGate.PreventsBattleDeletion(context, participant.InstanceId);

        switch (comparison)
        {
            case > 0:
                if (CanBeBattleDestroyed(defender)) deleted.Add(defender);
                break;
            case < 0:
                if (CanBeBattleDestroyed(attacker)) deleted.Add(attacker);
                break;
            default:
                if (CanBeBattleDestroyed(attacker)) deleted.Add(attacker);
                if (CanBeBattleDestroyed(defender)) deleted.Add(defender);
                break;
        }

        // F-6.8: a battle deletion is OPTIONAL-replaceable. Flag the DP-losers as pending battle deletions,
        // then resolve the battle in ROUNDS (ResolveRoundAsync): each round applies confirmed Retaliation
        // and either parks for a would-be-deleted window (any flagged participant with an undeclined
        // replacement) or finalizes. A vanilla battle (no keyword, no retaliation) finalizes in this call.
        foreach (BattleParticipant participant in deleted)
        {
            FlagPendingBattleDeletion(context, participant);
        }

        return await ResolveRoundAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(F-6.8) Re-entered each time the pipeline advances <see cref="AttackPhase.DeletionReplacement"/>
    /// (a window resolved). Runs one battle-deletion round.</summary>
    public Task<BattleResolutionResult> FinalizeDeferredAsync(EngineContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveRoundAsync(context, cancellationToken);
    }

    private async Task<BattleResolutionResult> ResolveRoundAsync(EngineContext context, CancellationToken cancellationToken)
    {
        HeadlessAttackState attack = context.AttackController.Current;
        string? validationFailure = ValidateBattle(context, attack, out BattleParticipant? attacker, out BattleParticipant? defender);
        if (validationFailure is not null)
        {
            return BattleResolutionResult.Failure(validationFailure, attack);
        }

        // (RD-CBTL-01) The manual same-round Retaliation drag is RETIRED. [Retaliation] now fires 1:1 through the
        // AS-IS post-battle OnDestroyedAnyone window (FinalizeAsync opens it with the live IBattle payload;
        // RetaliationProcess issues a FRESH DestroyPermanentsClass over the winner, which opens the winner's OWN
        // would-be-deleted Evade/Barrier window). XOR with that window — a stale manual flag here would double-fire.

        // If any flagged participant still needs a would-be-deleted decision, park for its window.
        if (context.ZoneMover is IZoneStateReader zones &&
            new[] { attacker!, defender! }.Any(p => NeedsWindow(context, zones, p.InstanceId)))
        {
            return BattleResolutionResult.Deferred(attack);
        }

        // (C-Del 3c-1b PRE cut-in transport) No GATE window is pending — open the AS-IS "would be deleted" PRE
        // cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField) over the still-pending battle losers, the
        // same pair AS-IS DestroyPermanentsClass.Destroy() opens for LoserPermanents (CardController.Battle :4705 →
        // Destroy() :3690-3705, with the "battle" cause the LoserPermanents hashtable carries at :4700). The mirror
        // battle path never routes through the effect-delete sink (3b), so this is the BattleResolver analogue of
        // 3b's sink work. Runs ONLY on the non-gate-deferred branch (mirrors the sink's DeferAll=false), and a loser
        // whose replacement keyword is gate-recognised is EXCLUDED (double-fire 0 — see OpenBattlePreCutInWindow).
        if (context.ZoneMover is IZoneStateReader preZones)
        {
            BattlePreWindowResult preResult = await OpenBattlePreCutInWindowAsync(
                context, preZones, attacker!, defender!, cancellationToken).ConfigureAwait(false);
            if (preResult == BattlePreWindowResult.Deferred)
            {
                // An INTERACTIVE would-be-deleted replacement paused the cut-in drain — promote to defer (3c-1):
                // the losers stay pendingDeletion (+ the BattlePreWindowed marker stops re-opening on re-entry) and
                // the pipeline parks at AttackPhase.DeletionReplacement. The parked ForCutIn window carries the
                // choice; once the agent resolves it (setting willBeRemoveField) FinalizeDeferredAsync re-enters
                // this method — the already-windowed losers skip re-opening and finalize on willBeRemoveField below.
                return BattleResolutionResult.Deferred(attack);
            }
        }

        // (C-Del 3c-1b) SURVIVOR FIX — AS-IS Destroy() keeps in destroyTargetPermanents_Fixed only the marked
        // permanents whose willBeRemoveField is STILL true (:3729-3732); a PRE replacement that cleared it is
        // filtered OUT and never trashed. Reconciled to the battle path: a still-pending loser that was PRE-windowed
        // and whose willBeRemoveField a replacement CLEARED is SPARED (pendingDeletion cleared → drops out of the
        // casualty set below and stays on the field).
        foreach (BattleParticipant participant in new[] { attacker!, defender! })
        {
            if (IsStillPendingDeletion(context, participant.InstanceId)
                && WasBattlePreWindowed(context, participant.InstanceId)
                && !new Assets.Scripts.Script.CardEffectCommons.Permanent(context, participant.InstanceId, participant.OwnerId).willBeRemoveField)
            {
                SpareBattlePreWindowSurvivor(context, participant.InstanceId);
            }
        }

        // No more windows — finalize: the still-pending participants are the casualties.
        var deleted = new[] { attacker!, defender! }.Where(p => IsStillPendingDeletion(context, p.InstanceId)).ToList();
        return await FinalizeAsync(context, attacker!, defender!, deleted, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BattleResolutionResult> FinalizeAsync(
        EngineContext context,
        BattleParticipant attacker,
        BattleParticipant defender,
        List<BattleParticipant> deleted,
        CancellationToken cancellationToken)
    {
        bool defenderDeletedNow = deleted.Any(p => p.InstanceId == defender.InstanceId);

        var movementResults = new List<ZoneMoveResult>();
        // (deletion single-simultaneous window) AS-IS deletes EVERY battle loser together in one
        // DestroyPermanentsClass(LoserPermanents, hashtable) call (CardController.Battle :4705) — so while any
        // dying permanent's own effects resolve, the CO-losers are still present (on the field, effects still
        // live), being deleted simultaneously, not one-fully-then-the-next. Mirror that with TWO phases instead
        // of an interleaved per-loser (window → cleanup → move) loop:
        //   phase 1 — resolve every loser's SUBJECT-scoped knock-out window while ALL losers are still on the
        //             battle area with their bindings intact (AS-IS simultaneity: a co-loser is visible/live
        //             during another's window), and
        //   phase 2 — only then run leave-play cleanup + move-to-trash for all of them.
        // F-6.3/P1: AS-IS stacks AND resolves the dying permanent's effects DURING its deletion processing
        // (TriggeredSkillProcess after battle) and only then lets them lapse — so each window still resolves
        // BEFORE any leave-play cleanup drops a binding (the scheduler resolves requests through the registry; a
        // dropped binding would be a no-op). Two-phase preserves that AND the cross-loser simultaneity.
        foreach (BattleParticipant participant in deleted)
        {
            MarkDeletedByBattle(context, participant);
            await ResolveKnockOutWindowAsync(context, participant, cancellationToken).ConfigureAwait(false);
        }

        // (RD-CBTL-01 IBattle cutover) mirror AS-IS DestroyPermanentsClass(LoserPermanents)'s inline OnDeletion pair
        // (CardController.cs:4705 → :3736-3756) AND the OnDetermineDoSecurityCheck window (:4731-4737), both carrying
        // the REAL battle-result IBattle payload (no synthesis — re-expression of the actual outcome):
        //   * winners = the surviving participants; losers / losers_real = the participants actually deleted by this
        //     battle (`deleted` — replacement survivors never reach it). AS-IS name/color/level snapshot vs _real
        //     live distinction collapses to one InstanceId-identity view list (see BuildBattleResultPayload).
        //   * OnDestroyedAnyone / OnLeaveFieldAnyone are collected BEFORE the loser field->Trash moves (phase 2, all
        //     losers still on the battle area) — ONE hashtable list for BOTH losers (AS-IS single LoserPermanents
        //     list) with cardEffect=null and the live IBattle. This is what makes [Retaliation]
        //     (CanActivateRetaliation / RetaliationProcess, which read battle.hashtable's WinnerPermanents_real /
        //     LoserPermanents) fire through the window — the boolean byBattleCause marker used to leave it no-op.
        //   * OnDetermineDoSecurityCheck collects every [Pierce] ActivateClass triggerable by this result, filters
        //     by CanActivate, stacks the first (AS-IS PutStackedSkill). The drain (main-loop AutoProcessCheck) runs
        //     PierceProcess, which sets attackProcess.DoSecurityCheck — the AS-IS security-check gate.
        // Both windows drain at the main loop's AutoProcessCheck after the battle step (shared per-context stack).
        // Runs under RunToStableAsync's AmbientMatchContext scope (Enter is nest-safe).
        bool piercing = false;
        // (수리-2 / PRIM-P0 OnEndBattle) hoisted so the post-move OnEndBattle window (AS-IS CardController.cs:4718,
        // AFTER DestroyPermanentsClass.Destroy) can reuse the same battle hashtable.
        Assets.Scripts.Script.CardEffectCommons.IBattle battle;
        using (AmbientMatchContext.Enter(context))
        {
            var winnerPermanents = new[] { attacker, defender }
                .Where(p => !deleted.Any(d => d.InstanceId == p.InstanceId))
                .Select(p => new Assets.Scripts.Script.CardEffectCommons.Permanent(context, p.InstanceId, p.OwnerId))
                .ToList();
            var loserPermanents = deleted
                .Select(p => new Assets.Scripts.Script.CardEffectCommons.Permanent(context, p.InstanceId, p.OwnerId))
                .ToList();
            battle = Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.BuildBattleResultPayload(
                winnerPermanents: winnerPermanents,
                winnerPermanentsReal: winnerPermanents,
                loserPermanents: loserPermanents,
                loserPermanentsReal: loserPermanents,
                loserCard: null!,
                wasTie: CompareBattleStats(attacker, defender) == 0);

            var battleAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);

            // AS-IS builds a FRESH OnDeletionHashtable per StackSkillInfos (CardController.cs:3489-3509). The
            // AS-IS-signature overload adds the "battle" key when battle != null (HashtableSetting.cs:123), so
            // IsByBattle stays true and the winner/loser lists become readable by the keyword chain.
            if (deleted.Count > 0)
            {
                await battleAutoProcessing.StackSkillInfos(
                    Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.OnDeletionHashtable(
                        loserPermanents, cardEffect: null!, battle, isDPZero: false),
                    Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnDestroyedAnyone).ConfigureAwait(false);
                await battleAutoProcessing.StackSkillInfos(
                    Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.OnDeletionHashtable(
                        loserPermanents, cardEffect: null!, battle, isDPZero: false),
                    Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnLeaveFieldAnyone).ConfigureAwait(false);
            }

            // AS-IS :4731-4737 — GetSkillInfos(battle.hashtable, OnDetermineDoSecurityCheck).Filter(CanActivate);
            // if >= 1, PutStackedSkill(first). `piercing` (the pipeline park signal) is now the WINDOW result, not
            // the retired manual HasPierce read — the actual DoSecurityCheck flip happens in PierceProcess at drain.
            var pierceSkills = Assets.Scripts.Script.AutoProcessing
                .GetSkillInfos(battle.hashtable, Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnDetermineDoSecurityCheck)
                .Where(skillInfo => skillInfo.CardEffect != null && skillInfo.CardEffect.CanActivate(skillInfo.Hashtable))
                .ToList();
            if (pierceSkills.Count >= 1)
            {
                battleAutoProcessing.PutStackedSkill(pierceSkills[0]);
                piercing = true;
            }
        }

        // (D-1 / VR-8) AS-IS deletes EVERY battle loser in ONE DestroyPermanentsClass(LoserPermanents) call
        // (CardController.cs:4705) — a SINGLE StackSkillInfos(OnDeletion / OnLeaveFieldAnyone). Allocate ONE batch
        // id for all losers so an OnLeaveFieldAnyone / OnDeletion reactor fires ONCE for a two-loser battle.
        long battleBatchId = context.NextDeletionBatchId();
        foreach (BattleParticipant participant in deleted)
        {
            // (P1) leave-play cleanup BEFORE the move (and before Fortitude reads the keyword state):
            // snapshot the dead card's post-deletion keywords, then drop its bindings — previously the
            // battle path never dropped them, so a battle-deleted card's continuous effects kept applying.
            CardLeavePlayCleanup.OnDeleted(context.CardInstanceRepository, context.EffectRegistry, context, participant.InstanceId);
            // (C-Del 3c-1b) a PRE-windowed casualty kept willBeRemoveField=true (no replacement cleared it). AS-IS
            // Destroy() resets it as the card is trashed (:3591) — clear the transient PRE-window markers so no
            // stale flag rides the trashed instance.
            ClearInstanceFlag(context, participant.InstanceId, BattlePreWindowedKey);
            ClearInstanceFlag(context, participant.InstanceId, "willBeRemoveField");
            // (RD-4) trash the loser's digivolution sources before its top card leaves (AS-IS DiscardEvoRoots
            // at CardController.cs:3846 precedes the top's AddTrashCard). No trigger fires (direct trash-add);
            // unconditional like AS-IS except Decode/Partition (their POST window plays a source from None).
            await DeletionSourceTrash.TrashEvoSourcesAsync(
                context.CardInstanceRepository, context.ZoneMover, participant.InstanceId, gameEventQueue: null, cancellationToken,
                context.MemoryController, context.TurnController.Current.TurnPlayerId).ConfigureAwait(false);
            // (C-2 review P1) charge the leaving ACE's OWN top overflow — AS-IS RemoveField does so after
            // DiscardEvoRoots; the raw MoveAsync below skips the sink's ApplyAceOverflowOnLeave.
            AceOverflowGate.ApplyTopOverflowOnDelete(context, participant.InstanceId);
            movementResults.Add(await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(participant.OwnerId, participant.InstanceId, ChoiceZone.BattleArea, ChoiceZone.Trash,
                    Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.DeletionBatchIdKey] = battleBatchId }),
                cancellationToken).ConfigureAwait(false));
        }

        // (C-Del 3a RETIRED) C-6 Fortitude no longer replays via the invented gate — it fires through the AS-IS
        // OnDestroyedAnyone cut-in window (BattleResolver.FinalizeAsync collect-before-removal + AutoProcessCheck),
        // from the card's printed FortitudeEffect / granted GainFortitude bucket.

        // (RD-CBTL-01) Piercing is decided by the OnDetermineDoSecurityCheck window above (`piercing`), not the
        // retired manual HasPiercingKey / Permanent.HasPierce read: the window's CanTriggerPierce already gates on
        // "surviving attacker deleted the opponent" and CanActivatePierce on "defending player still has security",
        // and PierceProcess (drained by the main loop before the piercing follow-up) sets DoSecurityCheck.
        EffectDurationExpiry.ExpireBattleEnd(context.EffectRegistry);
        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Battle resolved by DP comparison.");
        // (W6 tail) carry the battle RESULT the AS-IS battle hashtable carried (WinnerPermanents /
        // LoserPermanents / *_real) so CanTriggerWhenDeleteOpponentDigimonByBattle / WhenWinBattle gates can
        // read it: winners = surviving participants, losers = DP-comparison losers (the deleted set here —
        // replacement survivors never reach `deleted`), losersReal = actually destroyed by this battle.
        var battleValues = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            // (F1-M0-2) collection payloads flatten to CSV-of-id-values via the shared convention helper so a
            // gate can read them back with EventCollectionMetadata.ReadIds (BuildUniformResolveContext threads
            // only primitive metadata — a raw List would be dropped). Byte-identical to the prior inline joins.
            ["winnerIds"] = EventCollectionMetadata.Flatten(new[] { attacker.InstanceId, defender.InstanceId }
                .Where(id => !deleted.Any(p => p.InstanceId == id))),
            ["loserIds"] = EventCollectionMetadata.Flatten(deleted.Select(p => p.InstanceId)),
            ["loserRealIds"] = EventCollectionMetadata.Flatten(deleted.Select(p => p.InstanceId)),
            ["attackerId"] = attacker.InstanceId.Value,
            ["defenderId"] = defender.InstanceId.Value,
        };
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndBattle, actor: attacker.OwnerId,
            extraMetadata: battleValues);

        // (수리-2 / PRIM-P0 OnEndBattle) AS-IS opens the "at the end of battle" window AFTER the loser deletion
        // completes — CardController.cs:4718 StackSkillInfos(hashtable, EffectTiming.OnEndBattle) with the SAME
        // battle hashtable (post Fix-Loser-Permanents). The old-model emit above reaches only registry bindings;
        // new-model ActivateClass reactors (EffectList(OnEndBattle)) fire only through this window. Stack-only —
        // the drain is the main loop's AutoProcessCheck, like the OnDestroyedAnyone pair above.
        using (AmbientMatchContext.Enter(context))
        {
            await Assets.Scripts.Script.AutoProcessing.For(context).StackSkillInfos(
                battle.hashtable, Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnEndBattle)
                .ConfigureAwait(false);
        }

        return BattleResolutionResult.Success(
            resolvedAttack,
            attacker.Dp,
            defender.Dp,
            deleted.Select(p => p.InstanceId).ToArray(),
            movementResults,
            attackerDeleted: deleted.Any(p => p.InstanceId == attacker.InstanceId),
            defenderDeleted: defenderDeletedNow,
            triggersPiercingSecurityCheck: piercing);
    }

    private static void FlagPendingBattleDeletion(EngineContext context, BattleParticipant participant) =>
        FlagPendingBattleDeletion(context, participant.Instance, participant.Dp);

    /// <summary>(C-5/VR-6) Record-based flagging shared with the SECURITY battle loss (SecurityResolver) —
    /// both battle kinds run the SAME pending/defer/finalize machine (AS-IS: a security battle exits through
    /// the same IBattle.Battle → DestroyPermanentsClass.Destroy as a field battle).</summary>
    internal static void FlagPendingBattleDeletion(EngineContext context, CardInstanceRecord instance, int dp)
    {
        var metadata = new Dictionary<string, object?>(instance.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.PendingDeletionKey] = true,
            [DeletedByBattleKey] = true,
            [DpBeforeBattleKey] = dp,
        };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    private static bool IsStillPendingDeletion(EngineContext context, HeadlessEntityId cardId) =>
        ReadInstanceFlag(context, cardId, GameFlowProcessor.PendingDeletionKey);

    // (G-clean) RetaliationFiredKey deleted — the manual Retaliation firing-half that set/read it is gone; it was
    // clear-only (never written, never read) after the A군 4단계 cutover.

    /// <summary>A pending battle deletion that still has an UNDECLINED would-be-deleted replacement — the
    /// owner has not yet decided, so a window must open before finalizing. (C-5/VR-6) internal: the
    /// security-battle loss defers/parks on exactly the same predicate.</summary>
    internal static bool NeedsWindow(EngineContext context, IZoneStateReader zones, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null ||
            !ReadFlag(record.Metadata, GameFlowProcessor.PendingDeletionKey) ||
            ReadFlag(record.Metadata, DeletionReplacementTiming.ReplacementDeclinedKey))
        {
            return false;
        }

        // (C5-witness) thread the registry so a KEYWORD-GRANTED replacement (BarrierSelfEffect /
        // EvadeSelfEffect / FragmentSelfEffect — the production path, registered via ContinuousKeywordGate)
        // defers the battle deletion like a metadata-flagged one. Previously only the test-set metadata
        // flags were visible here, so a real card's grant never opened the battle-loss PRE window.
        return DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, record, byBattle: true, context.EffectRegistry);
    }

    /// <summary>(C-Del 3c-1b PRE cut-in transport) Open the AS-IS "would be deleted" PRE cut-in window
    /// (<c>WhenPermanentWouldBeDeleted → WhenRemoveField</c>) over the still-pending battle losers — the mirror of
    /// <c>DestroyPermanentsClass.Destroy()</c> :3684-3724 for the battle path (which never routes through the
    /// effect-delete sink, so 3b's sink work does not reach it). Marks every windowed loser's
    /// <c>willBeRemoveField=true</c> (AS-IS :3684), stacks the two cut-in windows with the DERIVED byBattle cause
    /// (AS-IS carries the live IBattle from the LoserPermanents hashtable :4700; the mirror stamps ByBattleCauseKey,
    /// read by IsByBattle's dual-read), then drains the cut-in. A mandatory replacement clears willBeRemoveField
    /// inline (the caller's survivor fix spares it); an INTERACTIVE replacement pauses the drain → returns
    /// <see cref="BattlePreWindowResult.Deferred"/> WITHOUT re-throwing (the battle has no enclosing flush to
    /// swallow it — unlike the sink; the AttackPhase.DeletionReplacement park is the resume anchor and re-runs THIS
    /// method via FinalizeDeferredAsync, not ResolveAsync, so no OnStartBattle re-fire).
    /// GATE COEXISTENCE — double-fire 0 (3b invariant): a loser whose replacement keyword is gate-recognised
    /// (<c>HasPreOption(byBattle)</c> true — every ported Evade/Barrier/Fragment/ArmorPurge) is EXCLUDED; it already
    /// fired through the not-yet-retired gate (the NeedsWindow park above). Because HasPreOption scans the SAME
    /// recognised-keyword set, no ported keyword card is ever in the window's target list — it collects ONLY a
    /// gate-invisible would-be-deleted ActivateClass (a Tfx witness today; the ported keywords once 3c-2 retires the
    /// gate, which also removes this exclusion). A card already windowed this battle (interactive re-entry) is
    /// skipped via the BattlePreWindowed marker.</summary>
    private static async Task<BattlePreWindowResult> OpenBattlePreCutInWindowAsync(
        EngineContext context,
        IZoneStateReader zones,
        BattleParticipant attacker,
        BattleParticipant defender,
        CancellationToken cancellationToken)
    {
        var toDelete = new List<Assets.Scripts.Script.CardEffectCommons.Permanent>();
        foreach (BattleParticipant participant in new[] { attacker, defender })
        {
            if (!IsStillPendingDeletion(context, participant.InstanceId))
            {
                continue;   // a winner / a survivor already spared this round
            }

            if (WasBattlePreWindowed(context, participant.InstanceId))
            {
                continue;   // already windowed this battle (deferred re-entry after an interactive pause)
            }

            if (!zones.GetCards(participant.OwnerId, ChoiceZone.BattleArea).Contains(participant.InstanceId))
            {
                continue;   // already left the field
            }

            // (C-Del 3c-2b) The gate-recognised-keyword EXCLUDE is retired — the 8 PRE keywords
            // (Evade/Barrier/Fragment/ArmorPurge/…) now fire through THIS window (printed / granted ActivateClass
            // collected by GetSkillInfos). No double-fire: the gate firing-half is retired this batch. The retained
            // CustomWouldBeDeleted bridge (registry-only, disjoint from the window's EffectList) parked at NeedsWindow
            // above, so a still-pending loser reaching here carries no gate option.

            MarkInstance(context, participant.InstanceId, BattlePreWindowedKey);
            toDelete.Add(new Assets.Scripts.Script.CardEffectCommons.Permanent(context, participant.InstanceId, participant.OwnerId)
            {
                willBeRemoveField = true,   // AS-IS Destroy() :3684 — mark ALL targets before the cut-in
            });
        }

        if (toDelete.Count == 0)
        {
            return BattlePreWindowResult.Drained;
        }

        using AmbientMatchContext.Scope _preScope = AmbientMatchContext.Enter(context);
        var cutIn = Assets.Scripts.Script.AutoProcessing.ForCutIn(context);
        // AS-IS builds a FRESH WhenPermanentWouldRemoveFieldCheckHashtable per StackSkillInfos with the battle cause
        // (CardController.cs:3690-3705). The mirror has no live IBattle (battle-rehousing non-scope), so it passes
        // the DERIVED byBattle marker (IsByBattle dual-read). cardEffect stays null (RD-C1-CARDEFFECT-IDTHREAD).
        await cutIn.StackSkillInfos(
            Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(toDelete, byBattleCause: true),
            Assets.Scripts.Script.CardEffectCommons.EffectTiming.WhenPermanentWouldBeDeleted).ConfigureAwait(false);
        await cutIn.StackSkillInfos(
            Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(toDelete, byBattleCause: true),
            Assets.Scripts.Script.CardEffectCommons.EffectTiming.WhenRemoveField).ConfigureAwait(false);

        if (cutIn.HasAwaitingActivateEffects())   // AS-IS Destroy() :3707 gate
        {
            // AS-IS ShowDeleteEffect / ShrinkSecurityDigimonDisplay / HideDeleteEffect = UI (stripped).
            try
            {
                await cutIn.TriggeredSkillProcess(false, null).ConfigureAwait(false);
            }
            catch (Exception preEx) when (preEx is WindowChoicePendingException or DeferredChoicePendingException)
            {
                // (C-Del 3c-1b promote-to-defer) an INTERACTIVE would-be-deleted replacement paused the cut-in
                // drain. SWALLOW (no re-throw): the parked ForCutIn window keeps the pending choice; the losers are
                // already pendingDeletion (+ deletedByBattle) and BattlePreWindowed, so the sweep skips them
                // (IsBattleDeferred) and FinalizeDeferredAsync owns the finalize. Return Deferred so the pipeline
                // parks at DeletionReplacement.
                return BattlePreWindowResult.Deferred;
            }
        }

        return BattlePreWindowResult.Drained;
    }

    /// <summary>(C-Del 3c-1b) Whether this loser's battle PRE cut-in window was already opened this battle.
    /// (C-Del 3c-1c) internal — the SECURITY-battle loss (SecurityResolver) reuses the SAME marker so its
    /// deferred re-entry skips re-opening and drives the survivor/casualty split.</summary>
    internal static bool WasBattlePreWindowed(EngineContext context, HeadlessEntityId cardId) =>
        ReadInstanceFlag(context, cardId, BattlePreWindowedKey);

    /// <summary>(C-Del 3c-1c) Mark a battle loser (field or SECURITY-battle) as having had its PRE cut-in window
    /// opened this battle — the security path shares BattleResolver's marker so re-entry is coherent.</summary>
    internal static void MarkBattlePreWindowed(EngineContext context, HeadlessEntityId cardId) =>
        MarkInstance(context, cardId, BattlePreWindowedKey);

    /// <summary>(C-Del 3c-1c) Reset the transient PRE-window markers on a battle CASUALTY about to be trashed
    /// (AS-IS Destroy() resets willBeRemoveField at :3591) — the field path clears them inline in
    /// <see cref="FinalizeAsync"/>; the SECURITY-battle finalize (SecurityResolver) calls this.</summary>
    internal static void ClearBattlePreWindowMarkers(EngineContext context, HeadlessEntityId cardId)
    {
        ClearInstanceFlag(context, cardId, BattlePreWindowedKey);
        ClearInstanceFlag(context, cardId, "willBeRemoveField");
    }

    /// <summary>(C-Del 3c-1b) A PRE would-be-deleted replacement cancelled this battle loser's deletion
    /// (willBeRemoveField cleared) — AS-IS Destroy() filters it OUT of destroyTargetPermanents_Fixed (:3729-3732)
    /// and never trashes it. Clear its deferral so it drops out of the casualty set and is a clean live permanent
    /// again (pendingDeletion off; the transient willBeRemoveField / BattlePreWindowed / deletedByBattle markers
    /// removed — AS-IS resets willBeRemoveField at :3591). (C-Del 3c-1c) internal — reused by the SECURITY-battle
    /// deferred re-entry to spare a PRE-windowed survivor.</summary>
    internal static void SpareBattlePreWindowSurvivor(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.PendingDeletionKey] = false,
        };
        metadata.Remove(BattlePreWindowedKey);
        metadata.Remove("willBeRemoveField");
        metadata.Remove(DeletedByBattleKey);
        metadata.Remove(DeletionReplacementTiming.ReplacementDeclinedKey);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    // (RD-CBTL-01 / G-clean) IsConfirmedDoomed / IsOnBattleArea were only read by the retired manual same-round
    // Retaliation drag — removed with it. RetaliationFiredKey / HasRetaliationKey / HasPiercingKey were physically
    // deleted in the G-clean pass (the metadata-flag firing path is retired; the window reads the printed/granted
    // keyword EffectList instead).

    private static bool ReadInstanceFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null &&
        ReadFlag(record.Metadata, key);

    private static void MarkInstance(EngineContext context, HeadlessEntityId cardId, string key)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        context.CardInstanceRepository.Upsert(record with
        {
            Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = true }
        });
    }

    private static void ClearInstanceFlag(EngineContext context, HeadlessEntityId cardId, string key)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null ||
            !record.Metadata.ContainsKey(key))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata.Remove(key);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    /// <summary>(C-12 Iceclad) Mirrors AS-IS <c>CardController.CompareStats</c>: if EITHER combatant has
    /// Iceclad the battle is decided by digivolution-source count instead of DP; otherwise by DP. The
    /// result is clamped to [-1, 1] (attacker-relative: &gt;0 defender loses, &lt;0 attacker loses, 0 tie).</summary>
    private static int CompareBattleStats(BattleParticipant attacker, BattleParticipant defender)
    {
        if (HasFlag(attacker, HasIcecladKey) || HasFlag(defender, HasIcecladKey))
        {
            return Math.Clamp(SourceCount(attacker).CompareTo(SourceCount(defender)), -1, 1);
        }

        return Math.Clamp(attacker.Dp.CompareTo(defender.Dp), -1, 1);
    }

    /// <summary>The number of digivolution sources under a participant (AS-IS <c>DigivolutionCards.Count</c>).
    /// (C4) reads sourceIds through the shared tolerant parser — entity-id-typed lists previously read as 0
    /// and silently flipped the Iceclad comparison.</summary>
    private static int SourceCount(BattleParticipant participant) =>
        DeletionReplacementGate.ReadSourceIds(participant.Instance.Metadata).Count;

    private static bool HasFlag(BattleParticipant participant, string key)
    {
        return ReadFlag(participant.Instance.Metadata, key) || ReadFlag(participant.Definition.Metadata, key);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static string? ValidateBattle(
        EngineContext context,
        HeadlessAttackState attack,
        out BattleParticipant? attacker,
        out BattleParticipant? defender)
    {
        attacker = null;
        defender = null;

        if (!attack.IsPending)
        {
            return "No pending attack exists.";
        }

        if (!attack.AttackingPlayerId.HasValue || !attack.AttackerId.HasValue)
        {
            return "Pending attack has no attacker.";
        }

        if (!attack.DefendingPlayerId.HasValue || !attack.TargetId.HasValue || attack.IsDirectAttack)
        {
            return "Battle resolution requires a non-direct attack target.";
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return "Zone mover does not expose readable zone state.";
        }

        string? attackerFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.AttackingPlayerId.Value,
            attack.AttackerId.Value,
            "Attacker",
            out attacker);
        if (attackerFailure is not null)
        {
            return attackerFailure;
        }

        string? defenderFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.DefendingPlayerId.Value,
            attack.TargetId.Value,
            "Defender",
            out defender);
        if (defenderFailure is not null)
        {
            return defenderFailure;
        }

        return null;
    }

    // (P1) resolve the dead card's OnKnockOut triggers synchronously BEFORE its bindings drop (AS-IS:
    // battle → TriggeredSkillProcess → security; the dead card's effects run during its own deletion
    // processing, then lapse).
    private static Task ResolveKnockOutWindowAsync(EngineContext context, BattleParticipant participant, CancellationToken cancellationToken)
    {
        // (C2 decision-1) OnKnockOut is a headless-invented LATENT window: AS-IS has NO StackSkillInfos(OnKnockOut)
        // (battle-loser deletion is sink-owned via DestroyPermanentsClass — the loser's OnDestroyedAnyone runs
        // there, transported by decision-4 above), and 0 ported cards react to OnKnockOut (F1-DEAD-KNOCKOUT). The
        // mechanism is swapped off the legacy WindowResolver to the mirror stack: GetSkillInfos(OnKnockOut) →
        // PutStackedSkill (STACK-ONLY — draining is deferred to the main loop's AutoProcessCheck, NOT resolved here,
        // because a mid-FinalizeAsync AutoProcessCheck would run the state-based sweep and prematurely DP-zero the
        // battle losers before phase-2 moves them). With 0 reactors this is a no-op today; ADAPTATION documented
        // until battle rehousing.
        {
            using AmbientMatchContext.Scope _knockOutScope = AmbientMatchContext.Enter(context);
            var autoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);
            Assets.Scripts.Script.AutoProcessing
                .GetSkillInfos(new System.Collections.Hashtable(), Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnKnockOut)
                .ForEach(skillInfo => autoProcessing.PutStackedSkill(skillInfo));
        }

        _ = participant;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private static string? TryReadParticipant(
        EngineContext context,
        IZoneStateReader zoneReader,
        HeadlessPlayerId expectedOwner,
        HeadlessEntityId instanceId,
        string role,
        out BattleParticipant? participant)
    {
        participant = null;

        if (!context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return $"{role} '{instanceId}' was not found.";
        }

        if (instance.OwnerId != expectedOwner)
        {
            return $"{role} '{instanceId}' is not owned by player '{expectedOwner}'.";
        }

        if (!zoneReader.GetCards(expectedOwner, ChoiceZone.BattleArea).Contains(instanceId))
        {
            return $"{role} '{instanceId}' is not in the battle area.";
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null)
        {
            return $"{role} definition '{instance.DefinitionId}' was not found.";
        }

        // (K4) type judgement via the central chokepoint (AS-IS Permanent.IsDigimon incl. TreatAsDigimon).
        if (!IsDigimon(definition) && !new Assets.Scripts.Script.CardEffectCommons.Permanent(context, instanceId).IsDigimon)
        {
            return $"{role} '{instanceId}' is not a Digimon.";
        }

        if (!TryReadDp(instance.Metadata, definition.Metadata, out int baseDp))
        {
            return $"{role} '{instanceId}' has no battle DP.";
        }

        // (R1-a) effective DP = AS-IS Permanent.DP: the base printed DP folded LIVE with every field /
        // face-up-security / player IChangeDPEffect, plus LinkedDP and per-card Boosts, clamped at 0 (the
        // original GetDP rescans on each access). DP-reduction immunity is honoured inside the getter.
        _ = baseDp;
        int dp = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, instanceId, instance.OwnerId).DP;

        participant = new BattleParticipant(instanceId, instance.OwnerId, instance, definition, dp);
        return null;
    }

    private static IReadOnlyList<DpModifier> ReadDpModifiers(IReadOnlyDictionary<string, object?> metadata)
    {
        return metadata.TryGetValue(DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> modifiers
            ? modifiers.ToArray()
            : Array.Empty<DpModifier>();
    }

    private static bool IsDigimon(CardRecord definition)
    {
        return definition.IsCardType("Digimon");
    }

    private static bool TryReadDp(
        IReadOnlyDictionary<string, object?> instanceMetadata,
        IReadOnlyDictionary<string, object?> cardMetadata,
        out int dp)
    {
        return TryReadInt(instanceMetadata, DpKey, out dp) ||
            TryReadInt(cardMetadata, DpKey, out dp);
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue % 1 == 0:
                value = (int)doubleValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    /// <summary>(C-5/VR-6) Finalize-time marker settle for a SECURITY-battle loser — same as the field-battle
    /// <see cref="MarkDeletedByBattle(EngineContext, BattleParticipant)"/> (the DP-before-battle was already
    /// stamped by <see cref="FlagPendingBattleDeletion(EngineContext, CardInstanceRecord, int)"/> at defer time).</summary>
    internal static void MarkDeletedByBattle(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DeletedByBattleKey] = true,
            [GameFlowProcessor.PendingDeletionKey] = false,
        };
        metadata.Remove(DeletionReplacementTiming.ReplacementDeclinedKey);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static void MarkDeletedByBattle(
        EngineContext context,
        BattleParticipant participant)
    {
        var metadata = new Dictionary<string, object?>(participant.Instance.Metadata, StringComparer.Ordinal)
        {
            [DeletedByBattleKey] = true,
            [DpBeforeBattleKey] = participant.Dp,
            // F-6.8: the deletion is now actually applied -> clear any deferred-deletion flag so the
            // state-based sweep does not double-handle it.
            [GameFlowProcessor.PendingDeletionKey] = false,
        };
        // Clear the per-attack F-6.8 marker so a Fortitude-replayed card starts clean.
        metadata.Remove(DeletionReplacementTiming.ReplacementDeclinedKey);

        context.CardInstanceRepository.Upsert(participant.Instance with { Metadata = metadata });
    }

    private sealed record BattleParticipant(
        HeadlessEntityId InstanceId,
        HeadlessPlayerId OwnerId,
        CardInstanceRecord Instance,
        CardRecord Definition,
        int Dp);
}

public sealed record BattleResolutionResult(
    bool IsSuccess,
    string FailureReason,
    HeadlessAttackState Attack,
    int? AttackerDp,
    int? DefenderDp,
    IReadOnlyList<HeadlessEntityId> DeletedCardIds,
    IReadOnlyList<ZoneMoveResult> MovementResults,
    bool AttackerDeleted,
    bool DefenderDeleted,
    bool TriggersPiercingSecurityCheck = false,
    bool RequiresDeletionReplacement = false)
{
    /// <summary>(F-6.8) Battle deletion was deferred for an optional would-be-deleted replacement window;
    /// the pipeline parks at <see cref="AttackPhase.DeletionReplacement"/> and later calls
    /// <see cref="BattleResolver.FinalizeDeferredAsync"/> to finish the battle.</summary>
    public static BattleResolutionResult Deferred(HeadlessAttackState attack) =>
        new(true, string.Empty, attack, null, null, Array.Empty<HeadlessEntityId>(), Array.Empty<ZoneMoveResult>(),
            AttackerDeleted: false, DefenderDeleted: false, TriggersPiercingSecurityCheck: false, RequiresDeletionReplacement: true);

    public static BattleResolutionResult Failure(
        string failureReason,
        HeadlessAttackState attack)
    {
        return new BattleResolutionResult(
            false,
            failureReason,
            attack,
            null,
            null,
            Array.Empty<HeadlessEntityId>(),
            Array.Empty<ZoneMoveResult>(),
            AttackerDeleted: false,
            DefenderDeleted: false);
    }

    public static BattleResolutionResult Success(
        HeadlessAttackState attack,
        int attackerDp,
        int defenderDp,
        IReadOnlyList<HeadlessEntityId> deletedCardIds,
        IReadOnlyList<ZoneMoveResult> movementResults,
        bool attackerDeleted,
        bool defenderDeleted,
        bool triggersPiercingSecurityCheck = false)
    {
        return new BattleResolutionResult(
            true,
            string.Empty,
            attack,
            attackerDp,
            defenderDp,
            deletedCardIds.ToArray(),
            movementResults.ToArray(),
            AttackerDeleted: attackerDeleted,
            DefenderDeleted: defenderDeleted,
            TriggersPiercingSecurityCheck: triggersPiercingSecurityCheck);
    }
}
