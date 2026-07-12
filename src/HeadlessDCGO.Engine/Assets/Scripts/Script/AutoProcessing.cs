// Source: Assets/Scripts/Script/AutoProcessing.cs
// Decision: PORT
// Category: CoreEngine
// Migration: AS-IS mirror (goal 2) — RULE-PROCESSING half.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// ============================================================================================================
// (MIG2) 1:1 mirror of the AS-IS AutoProcessing rule-processing half (AutoProcessing.cs:143-567):
// IsRuleProcessing + the seven rule predicates + RuleProcess()'s while(DoRuleProcess()) eight-stage pass +
// the eight per-rule processes. GameFlowProcessor.RuleProcessAsync (the main loop's step 1 AND the window
// loop's between-picks rule processing, AS-IS MultipleSkills.cs:398-403) now routes through this mirror.
//
// SUBSTRATE TRANSLATION (same rules as the AttackProcess mirror):
//  - coroutine -> async Task; GManager.instance.* -> EngineContext services; UI (ShowCardEffect /
//    CreateDebuffEffect / RemoveDigivolveRootEffect) silently stripped (UnityNullObjectPolicy).
//  - coroutine SUSPENSION -> park + pause: a stage that opens a player choice (the link-max trim's
//    SelectCardEffect, AS-IS Permanent.RemoveLinkedCard(null, count)) requests the choice and RETURNS;
//    RunToStable pauses on the pending choice and the resolution handler (MetadataActionProcessor,
//    request-id prefix "rule:link-trim:") applies the selection; the NEXT rule-process invocation re-enters
//    from the top — every stage is fixpoint-gated by its predicate, so re-running is the AS-IS while-loop.
//    IsRuleProcessing is reset to false on the park-exit (the AS-IS flag stays true only while the coroutine
//    frame is live; a parked mirror has no live frame and must re-enter through DoRuleProcess).
//  - AS-IS while(DoRuleProcess()) can only hang if a predicate holds work no stage clears (AS-IS blocks on
//    the coroutine instead); the mirror adds a NO-PROGRESS BREAK per full pass as the substrate guard.
//
// STAGE -> SUBSTRATE MAP:
//  1 EndGameProcess (:386-405)              -> PlayerStatusController.IsLose scan + terminal outcome sink
//                                              (TurnStateMachine.EndGame mirror lands at goal 6).
//  2 TrashNonDigimonPermanentProcess (:409) -> NEW 1:1 (was entirely missing headless-side): breeding
//                                              non-Digimon -> DiscardEvoRoots + direct trash (NOT a destroy).
//  3 TrashNoDPPermanentProcess (:439)       -> DELEGATED to GameFlowProcessor.StateBasedDeletionSweepAsync
//  4 DigimonLackDPProcess (:469)            -> (one integrated scan: no-DP trash + DP<=0 destroy + parked
//                                              would-be-deleted finalize). The sweep is the VERIFIED D-1/D-2
//                                              batch-semantics carrier; splitting it back into the AS-IS
//                                              separate whole-board passes is design item R2-P2-4 and belongs
//                                              to the DestroyPermanentsClass mirror (goal 3/7).
//  5 BattleWithoutDigimon (:488-498)        -> NEW 1:1 via the AttackProcess mirror (IsEndAttack flag).
//  6 DigimonLackLinkConditionProcess (:502) -> NEW 1:1: per-permanent ITrashLinkCards (CardController.cs:5242
//                                              mirror) over the links that fail CanLinkToTargetPermanent.
//  7 DigimonLackLinkMaxCountProcess (:524)  -> NEW 1:1: Permanent.RemoveLinkedCard(null, excess) — the OWNER
//                                              SELECTS which link cards to trash (AS-IS SelectCardEffect,
//                                              mode Discard, root Custom = LinkedCards); NOT oldest-first.
//  8 CardFaceDownProcess (:541-564)         -> NEW 1:1: battle-area face-down top -> DiscardEvoRoots + trash.
//
// SCOPE (design item MIG2-TRIGGER-SURFACE): the TRIGGER-STACK half of AS-IS AutoProcessing
// (StackedSkillInfos / PutStackedSkill / AutoProcessCheck / GetSkillInfos / ActivateBackgroundEffects /
// StackSkillInfos / TriggeredSkillProcess / EndTurnCheck / TurnEndMinMemory / EndTurnProcess / cut-in state)
// is ONE inseparable unit with MultipleSkills' window loop and migrates at goal 7 — its headless seat today
// is WindowResolverWiring.CollectUnifiedSeed + WindowResolver + GameFlowProcessor.AutoProcessAsync (the
// verified Stage-5 pipeline). Surfacing those AS-IS methods here NOW would be fake shells over a different
// mechanism; the deferral is explicit, not silent.
//
// DoRuleProcess() carries ONE substrate extension beyond the AS-IS seven checks: "a decided deferred
// deletion awaits finalize" (GameFlowProcessor.HasStateBasedSweepWork). AS-IS Destroy() completes its
// would-be-deleted windows synchronously inside stage 4's coroutine, so the finalize work is invisible to
// its DoRuleProcess; headless parks that window (choice) and finishes the deletion in a later sweep pass,
// so the pass must be reachable through DoRuleProcess.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public sealed class AutoProcessing
{
    private readonly EngineContext _context;
    private readonly DeletionReplacementTiming _deletionReplacement = new();

    private AutoProcessing(EngineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>The per-context instance (AS-IS <c>GManager.instance.autoProcessing</c>).</summary>
    public static AutoProcessing For(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetService(out AutoProcessing? existing) && existing is not null)
        {
            return existing;
        }

        var created = new AutoProcessing(context);
        context.RegisterService(created);
        return created;
    }

    /// <summary>Request-id prefix of the link-max trim selection (AS-IS Permanent.RemoveLinkedCard(null, count)
    /// SelectCardEffect). MetadataActionProcessor routes its resolution through <see cref="ITrashLinkCards"/>
    /// (the AS-IS SelectCardEffect Mode.Discard linked-card branch, SelectCardEffect.cs:715-724).</summary>
    public const string LinkTrimRequestIdPrefix = "rule:link-trim:";

    // ===== AS-IS AutoProcessing.cs:144 ==========================================================================
    public bool IsRuleProcessing { get; set; }

    // ===== Rule predicates (AS-IS :146-280, 1:1) ================================================================

    // AS-IS :146-163 — a breeding-area permanent whose top is not (treated as) a Digimon.
    private bool IsNotDigimonInBreeding(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                // (substrate fixture guard) AS-IS every card carries a CEntity; a DEFINITION-LESS instance is an
                // abstract fixture whose type cannot be judged — not subject to the type-based trash rule.
                if (!permanent.TopCard.HasDefinition)
                {
                    return false;
                }

                if (Commons.IsExistOnBreedingArea(permanent.TopCard))
                {
                    if (!permanent.IsDigimon)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :165-193 — DP < 0 means the permanent HAS no DP at all (Permanent.DP is -1 when !HasDP; a defined
    // DP clamps to >= 0): a lingering Digi-Egg / un-played Option on the battle area.
    private bool IsNotHavingDP(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                // (substrate fixture guard = D-2 sweep decision) AS-IS real Digimon always print DP, so this
                // branch never meets a DP-less NON-EGG Digimon; headless abstract test fixtures do — they are
                // not subject to the DP rules.
                if (permanent.IsDigimon && !permanent.TopCard.IsDigiEgg && !permanent.IsDpDefined)
                {
                    return false;
                }

                if (permanent.DP < 0)
                {
                    if (permanent.IsPlaceToTrashDueToNotHavingDP)
                    {
                        if (permanent.IsDigimon)
                        {
                            return true;
                        }

                        if (permanent.TopCard.IsOption)
                        {
                            if (!permanent.IsPlayedOptionPermanent)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :195-215 — a Digimon whose effective DP dropped to exactly 0 (defined-DP lethal rule) and that is
    // not protected by a CanNotBeDestroyed continuous effect (the predicate-level immunity check is what stops
    // the while-loop from re-selecting a protected 0-DP Digimon forever).
    private bool IsDigimonLackDP(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                // (substrate fixture guard = D-2 sweep decision — see IsNotHavingDP) only a DEFINED dp is
                // subject to the lethal rule; mirrors GameFlowProcessor.HasLethalDp's defined-DP guard.
                if (!permanent.IsDpDefined)
                {
                    return false;
                }

                if (permanent.DP == 0)
                {
                    if (permanent.IsDigimon)
                    {
                        if (permanent.CanBeDestroyed())
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // AS-IS :217-232 — NOTE: this predicate is INERT in DoRuleProcess (its `return true` is commented out in
    // the original, :352-356), so BattleWithoutDigimon only ever runs when some OTHER rule work opens a pass.
    private bool IsAttackerNotADigimon()
    {
        AttackProcess attackProcess = AttackProcess.For(_context);

        if (!attackProcess.IsAttacking)
            return false;

        if (attackProcess.AttackingPermanent != null)
        {
            if (!attackProcess.AttackingPermanent.IsDigimon)
                return true;

            //if (GManager.instance.attackProcess.HasDefender && GManager.instance.attackProcess.DefendingPermanent == null)
            //    return true;
        }

        return false;
    }

    // AS-IS :234-248 — a link card that no longer satisfies its link condition against its host
    // (predicate uses allowBreeding: true; the PROCESS re-filters with allowBreeding: false — AS-IS asymmetry,
    // preserved verbatim).
    private bool IsDigimonLackLinkCondition(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                // (substrate fixture guard) AS-IS cannot produce a LINKED card with NO declared linkCondition —
                // CanLink gates the attach on one existing (CardSource.cs:3142) — so a condition-less link is a
                // fixture-only state, not subject to the trim.
                if (permanent.LinkedCards.Any(source => source.LinkConditionOf() is not null
                    && !source.CanLinkToTargetPermanent(permanent, false, true)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS :250-264 — more link cards than the (effect-folded) LinkedMax allows.
    private bool IsDigimonLackLinkCount(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (permanent.LinkedCards.Count > permanent.LinkedMax)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS :266-280 — a permanent whose top card is face down.
    private bool IsPermanentFaceDown(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (permanent.TopCard.IsFlipped)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ===== RuleProcess (AS-IS :282-316) =========================================================================

    /// <summary>AS-IS <c>RuleProcess()</c>: run the eight rule stages while <see cref="DoRuleProcess"/> reports
    /// outstanding rule work. Returns true when any pass changed state (the GameFlowProcessor progress bit).</summary>
    public async Task<bool> RuleProcess(CancellationToken cancellationToken = default)
    {
        bool progressedAny = false;

        while (DoRuleProcess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRuleProcessing = true;
            bool progressed = false;

            //敗北処理
            progressed |= EndGameProcess();

            if (_context.RuleQueryService.IsTerminal())
            {
                // AS-IS :291 `if (endGame) yield break`.
                IsRuleProcessing = false;
                return true;
            }

            //Trash Non Digimon from Breeding
            progressed |= await TrashNonDigimonPermanentProcess(cancellationToken).ConfigureAwait(false);

            //DPを持たないカードをトラッシュする処理
            progressed |= await TrashNoDPPermanentProcess(cancellationToken).ConfigureAwait(false);

            //DP不足処理
            progressed |= await DigimonLackDPProcess(cancellationToken).ConfigureAwait(false);

            // (substrate) a stage-3/4 destroy may have DEFERRED a would-be-deleted replacement decision — the
            // AS-IS coroutine opens that window synchronously inside Destroy(); headless opens the choice and
            // parks. Pause here so the owner decides BEFORE the next pass's sweep would finalize the death.
            if (_deletionReplacement.RequestChoice(_context))
            {
                IsRuleProcessing = false;
                return true;
            }

            //Battle as Tamer
            progressed |= BattleWithoutDigimon();

            //Link Lacking condition
            progressed |= await DigimonLackLinkConditionProcess(cancellationToken).ConfigureAwait(false);

            //Add link count correction
            (bool linkMaxProgressed, bool linkMaxParked) = await DigimonLackLinkMaxCountProcess(cancellationToken).ConfigureAwait(false);
            progressed |= linkMaxProgressed;
            if (linkMaxParked)
            {
                // The owner's trim selection is pending (AS-IS suspends inside SelectCardEffect.Activate).
                IsRuleProcessing = false;
                return true;
            }

            //Permanent Face Down
            progressed |= await CardFaceDownProcess(cancellationToken).ConfigureAwait(false);

            IsRuleProcessing = false;
            progressedAny |= progressed;

            if (!progressed)
            {
                // Substrate guard: DoRuleProcess still reports work no stage could clear — the AS-IS coroutine
                // would block here; the headless loop must yield back to RunToStable instead of spinning.
                _context.LogSink.Warn("[AutoProcessing] RuleProcess pass made no progress while DoRuleProcess reports work; yielding.");
                break;
            }
        }

        return progressedAny;
    }

    // ===== DoRuleProcess (AS-IS :319-380) =======================================================================

    /// <summary>AS-IS <c>DoRuleProcess()</c>: whether any rule work is outstanding.</summary>
    public bool DoRuleProcess()
    {
        if (IsRuleProcessing) return false;

        #region Whether it is necessary to perform game end processing
        if (Players().Count(playerId => _context.PlayerStatusController.IsLose(playerId)) >= 1)
        {
            return true;
        }
        #endregion

        #region Is it necessary to discard cards in breeding?
        if (HasMatchConditionPermanent(IsNotDigimonInBreeding, true))
        {
            return true;
        }
        #endregion

        #region Is it necessary to discard cards without DP?
        if (HasMatchConditionPermanent(IsNotHavingDP))
        {
            return true;
        }
        #endregion

        #region Is it necessary to deal with Digimon's DP shortage?
        if (HasMatchConditionPermanent(IsDigimonLackDP))
        {
            return true;
        }
        #endregion

        #region Is it necessary to deal with Battle Without Digimon?
        if (IsAttackerNotADigimon())
        {
            //return true;
        }
        #endregion

        #region Is it necessary to deal with Digimon's Link Cards?
        if (HasMatchConditionPermanent(IsDigimonLackLinkCondition, true))
        {
            return true;
        }
        #endregion

        #region Is it necessary to deal with Digimon's Link Count?
        if (HasMatchConditionPermanent(IsDigimonLackLinkCount, true))
        {
            return true;
        }
        #endregion

        #region Is it necessary to deal with card being face down?
        if (HasMatchConditionPermanent(IsPermanentFaceDown, true))
        {
            return true;
        }
        #endregion

        // (substrate extension — see header) a DECIDED deferred deletion awaiting its finalize sweep. AS-IS
        // resolves would-be-deleted windows synchronously inside stage 4, so this state cannot outlive its pass.
        if (Headless.Runtime.GameFlowProcessor.HasStateBasedSweepWork(_context))
        {
            return true;
        }

        return false;
    }

    // ===== Each rule processing (AS-IS :383-567) ================================================================

    #region Game end processing
    // AS-IS :386-405 — first losing player ends the game (enemy wins; both losing = draw). The IsLose scan +
    // winner resolution is the X-02 TerminalEvaluator (its doc: "mirrors Unity AS-IS AutoProcessing reading
    // Player.IsLose"); the terminal write is the TurnStateMachine.EndGame substrate half (goal 6).
    private bool EndGameProcess()
    {
        if (_context.RuleQueryService.IsTerminal())
        {
            return false;
        }

        PlayerTerminalCheck? check = TerminalEvaluator.Evaluate(_context);
        if (check is null || !check.IsTerminal)
        {
            return false;
        }

        // AS-IS :392-400 — the loser's enemy wins ONLY when the enemy is not ALSO losing; both losing in the
        // same pass is a DRAW (EndGame(null)). (Adversarial review P1-1: TerminalEvaluator names a winner
        // unconditionally, so the both-lose case must be re-checked here.)
        bool bothLose = check.WinnerPlayerId is { } winner && !winner.IsEmpty
            && _context.PlayerStatusController.IsLose(winner);

        if (_context.RuleQueryService is ITerminalOutcomeSink outcomeSink)
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
        else if (_context.RuleQueryService is ITerminalStateController terminalController)
        {
            terminalController.SetTerminal(true);
        }

        _context.LogSink.Info(
            $"[AutoProcessing] EndGameProcess terminal: {check.Reason} winner={check.WinnerPlayerId?.Value} loser={check.LosingPlayerId?.Value}.");
        return true;
    }
    #endregion

    #region Process of trashing cards in Breeding
    // AS-IS :409-435 — a non-Digimon breeding permanent is trashed DIRECTLY (DiscardEvoRoots + RemoveField +
    // AddTrashCard): not a destroy — no deletion triggers, no would-be-deleted windows. ShowCardEffect = UI.
    private async Task<bool> TrashNonDigimonPermanentProcess(CancellationToken cancellationToken)
    {
        List<Permanent> BreedingPermanents = PlayersForTurnPlayer()
            .SelectMany(player => GetBreedingAreaPermanents(player).Where(IsNotDigimonInBreeding))
            .ToList();

        bool progressed = false;
        if (BreedingPermanents.Count >= 1)
        {
            foreach (Permanent permanent in BreedingPermanents)
            {
                if (permanent != null)
                {
                    if (permanent.TopCard != null)
                    {
                        await DirectTrashPermanentAsync(permanent, ChoiceZone.BreedingArea, cancellationToken).ConfigureAwait(false);
                        progressed = true;
                    }
                }
            }
        }

        return progressed;
    }
    #endregion

    #region Process of trashing cards without DP
    // AS-IS :439-465 — a battle-area permanent matching IsNotHavingDP (a lingering Digi-Egg / un-played
    // Option) is trashed DIRECTLY (DiscardEvoRoots + RemoveField + AddTrashCard): NOT a destroy.
    // NOTE: the stage-4 sweep's own no-DP branch (IsNoDpTrashablePermanent) remains as a harmless second
    // net — this 1:1 pass runs FIRST (the AS-IS order), so the sweep's branch normally finds nothing.
    private async Task<bool> TrashNoDPPermanentProcess(CancellationToken cancellationToken)
    {
        List<Permanent> DigitamaPermanents = PlayersForTurnPlayer()
            .SelectMany(player => GetBattleAreaPermanents(player).Where(IsNotHavingDP))
            .ToList();

        bool progressed = false;
        if (DigitamaPermanents.Count >= 1)
        {
            foreach (Permanent permanent in DigitamaPermanents)
            {
                if (permanent != null)
                {
                    if (permanent.TopCard != null)
                    {
                        await DirectTrashPermanentAsync(permanent, ChoiceZone.BattleArea, cancellationToken).ConfigureAwait(false);
                        progressed = true;
                    }
                }
            }
        }

        return progressed;
    }
    #endregion

    #region Digimon DP shortage handling
    // AS-IS :469-484 — every DP<=0 Digimon dies in ONE DestroyPermanentsClass batch ({"DPZero", true}).
    // DELEGATED: GameFlowProcessor.StateBasedDeletionSweepAsync is the verified carrier — one scan, one
    // lazily-allocated dp-zero batch id (= the AS-IS single-batch StackSkillInfos), plus the parked
    // would-be-deleted finalizes with the D-1/D-2 batch semantics intact. Splitting those back into the
    // AS-IS separate whole-board passes is design item R2-P2-4 (DestroyPermanentsClass mirror, goal 3/7).
    private async Task<bool> DigimonLackDPProcess(CancellationToken cancellationToken)
    {
        return await Headless.Runtime.GameFlowProcessor
            .StateBasedDeletionSweepAsync(_context, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Battle Concerning Tamer
    // AS-IS :488-498 — an attack whose attacker is no longer a Digimon (or whose named defender vanished
    // while HasDefender) is force-ended via IsEndAttack.
    private bool BattleWithoutDigimon()
    {
        AttackProcess attackProcess = AttackProcess.For(_context);

        if (attackProcess.AttackingPermanent == null)
            return false;

        bool progressed = false;

        if (!attackProcess.AttackingPermanent.IsDigimon && !attackProcess.IsEndAttack)
        {
            attackProcess.IsEndAttack = true;
            progressed = true;
        }

        if (attackProcess.DefendingPermanent == null && attackProcess.HasDefender && !attackProcess.IsEndAttack)
        {
            attackProcess.IsEndAttack = true;
            progressed = true;
        }

        return progressed;
    }
    #endregion

    #region Link Card Lacking conditions handling
    // AS-IS :502-520 — links that fail their condition are trashed through ITrashLinkCards (the batch window
    // carrier). Predicate filtered with allowBreeding:true; the trash list re-filters with allowBreeding:false
    // (AS-IS asymmetry preserved).
    private async Task<bool> DigimonLackLinkConditionProcess(CancellationToken cancellationToken)
    {
        List<Permanent> LackLinkConditionPermanents = PlayersForTurnPlayer()
            .SelectMany(player => GetFieldPermanents(player).Where(IsDigimonLackLinkCondition))
            .ToList();

        bool progressed = false;
        if (LackLinkConditionPermanents.Count >= 1)
        {
            foreach (Permanent permanent in LackLinkConditionPermanents)
            {
                List<CardSource> selectedCards = permanent.LinkedCards.FindAll(source => source.LinkConditionOf() is not null
                    && !source.CanLinkToTargetPermanent(permanent, false));

                await new ITrashLinkCards(
                    permanent,
                    selectedCards,
                    null).TrashLinkCards(cancellationToken).ConfigureAwait(false);
                progressed = true;
            }
        }

        return progressed;
    }
    #endregion

    #region Link Card Lacking Max Count handling
    // AS-IS :524-537 — the OWNER SELECTS which excess link cards to trash (Permanent.RemoveLinkedCard(null,
    // count) opens a SelectCardEffect over LinkedCards, mode Discard, canEndNotMax:false). Opening the choice
    // parks this stage; the resolution routes through ITrashLinkCards (MetadataActionProcessor).
    private async Task<(bool Progressed, bool Parked)> DigimonLackLinkMaxCountProcess(CancellationToken cancellationToken)
    {
        List<Permanent> LackLinkCountPermanents = PlayersForTurnPlayer()
            .SelectMany(player => GetFieldPermanents(player).Where(IsDigimonLackLinkCount))
            .ToList();

        if (LackLinkCountPermanents.Count >= 1)
        {
            foreach (Permanent permanent in LackLinkCountPermanents)
            {
                await permanent.RemoveLinkedCard(null, (permanent.LinkedCards.Count - permanent.LinkedMax), cancellationToken: cancellationToken).ConfigureAwait(false);

                if (_context.ChoiceController.Current.IsPending)
                {
                    return (true, true);
                }
            }

            return (true, false);
        }

        return (false, false);
    }
    #endregion

    #region Process of trashing cards that are face down
    // AS-IS :541-564 — a face-down battle-area top is trashed directly (same DiscardEvoRoots + RemoveField +
    // AddTrashCard shape as the breeding stage; note the original has NO Count>=1 guard here).
    private async Task<bool> CardFaceDownProcess(CancellationToken cancellationToken)
    {
        List<Permanent> FacedownPermanents = PlayersForTurnPlayer()
            .SelectMany(player => GetBattleAreaPermanents(player).Where(IsPermanentFaceDown))
            .ToList();

        bool progressed = false;
        foreach (Permanent permanent in FacedownPermanents)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null)
                {
                    await DirectTrashPermanentAsync(permanent, ChoiceZone.BattleArea, cancellationToken).ConfigureAwait(false);
                    progressed = true;
                }
            }
        }

        return progressed;
    }
    #endregion

    // ===== Substrate helpers ====================================================================================

    /// <summary>AS-IS direct-trash shape (:417-431 / :447-461 / :547-561): DiscardEvoRoots (sources to trash),
    /// then RemoveField + AddTrashCard for the top — a rules trash, NOT a destroy (no deletion triggers, no
    /// would-be-deleted windows). ShowCardEffect is UI and is stripped.</summary>
    private async Task DirectTrashPermanentAsync(Permanent permanent, ChoiceZone zone, CancellationToken cancellationToken)
    {
        // AS-IS permanent.DiscardEvoRoots(): every non-top stack card (digivolution sources) to the trash.
        await DeletionSourceTrash.TrashEvoSourcesAsync(
            _context.CardInstanceRepository, _context.ZoneMover, permanent.InstanceId, gameEventQueue: null, cancellationToken,
            _context.MemoryController, _context.TurnController.Current.TurnPlayerId).ConfigureAwait(false);

        // Not a deletion: bindings drop, but no post-deletion keyword snapshot (nothing may respond).
        CardLeavePlayCleanup.OnLeftPlay(_context.EffectRegistry, permanent.InstanceId);
        await _context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(permanent.OwnerId, permanent.InstanceId, zone, ChoiceZone.Trash),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Mirror of AS-IS <c>CardEffectCommons.HasMatchConditionPermanent(Func&lt;Permanent,bool&gt;,
    /// isContainBreedingArea)</c> (CardEffectCommons.cs:641) with the context threaded (the AS-IS static reads
    /// GManager): any permanent of either player — battle area, plus breeding when asked — matches.</summary>
    private bool HasMatchConditionPermanent(Func<Permanent, bool> condition, bool isContainBreedingArea = false)
    {
        foreach (HeadlessPlayerId player in PlayersForTurnPlayer())
        {
            foreach (Permanent permanent in GetBattleAreaPermanents(player))
            {
                if (condition(permanent))
                {
                    return true;
                }
            }

            if (isContainBreedingArea)
            {
                foreach (Permanent permanent in GetBreedingAreaPermanents(player))
                {
                    if (condition(permanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>gameContext.Players</c> (seat order).</summary>
    private IReadOnlyList<HeadlessPlayerId> Players() =>
        _context.TurnController.Current.PlayerOrder.Where(player => !player.IsEmpty).ToArray();

    /// <summary>AS-IS <c>gameContext.Players_ForTurnPlayer</c> — the turn player first.</summary>
    private IReadOnlyList<HeadlessPlayerId> PlayersForTurnPlayer()
    {
        HeadlessPlayerId? turnPlayer = _context.TurnController.Current.TurnPlayerId;
        return Players()
            .OrderBy(player => turnPlayer is { } tp && player == tp ? 0 : 1)
            .ToArray();
    }

    /// <summary>AS-IS <c>Player.GetBattleAreaPermanents()</c> (Player.cs:617) as mirror views.</summary>
    private IEnumerable<Permanent> GetBattleAreaPermanents(HeadlessPlayerId player) =>
        GetZonePermanents(player, ChoiceZone.BattleArea);

    /// <summary>AS-IS <c>Player.GetBreedingAreaPermanents()</c> (Player.cs:640) as mirror views.</summary>
    private IEnumerable<Permanent> GetBreedingAreaPermanents(HeadlessPlayerId player) =>
        GetZonePermanents(player, ChoiceZone.BreedingArea);

    /// <summary>AS-IS <c>Player.GetFieldPermanents()</c> (Player.cs:665) — battle + breeding.</summary>
    private IEnumerable<Permanent> GetFieldPermanents(HeadlessPlayerId player) =>
        GetBattleAreaPermanents(player).Concat(GetBreedingAreaPermanents(player));

    private IEnumerable<Permanent> GetZonePermanents(HeadlessPlayerId player, ChoiceZone zone)
    {
        if (_context.ZoneMover is not IZoneStateReader zones)
        {
            yield break;
        }

        foreach (HeadlessEntityId cardId in zones.GetCards(player, zone).ToArray())
        {
            yield return new Permanent(_context, cardId, player);
        }
    }
}
