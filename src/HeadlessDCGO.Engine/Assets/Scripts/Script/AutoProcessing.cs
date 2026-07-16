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
// SCOPE (design item MIG2-TRIGGER-SURFACE — RESOLVED in stages): the TRIGGER-STACK half of AS-IS
// AutoProcessing (StackedSkillInfos / PutStackedSkill / AutoProcessCheck / GetSkillInfos /
// ActivateBackgroundEffects / StackSkillInfos / TriggeredSkillProcess / cut-in state) landed as the LIVE
// window unit at the R3-C2 window-SkillInfo cutover (MultipleSkills is the drain; GameFlowProcessor.
// AutoProcessAsync is the outer drive). The TURN-END trio (EndTurnCheck / TurnEndMinMemory /
// EndTurnProcess) landed at R4 P2b as the AS-IS-position mirror, DORMANT until the R4 S3 driver flip
// (the live turn-end evaluation until then = HeadlessMainPhaseFlow + MetadataActionProcessor.EndTurnAsync).
//
// DoRuleProcess() carries ONE substrate extension beyond the AS-IS seven checks: "a decided deferred
// deletion awaits finalize" (GameFlowProcessor.HasStateBasedSweepWork). AS-IS Destroy() completes its
// would-be-deleted windows synchronously inside stage 4's coroutine, so the finalize work is invisible to
// its DoRuleProcess; headless parks that window (choice) and finishes the deletion in a later sweep pass,
// so the pass must be reachable through DoRuleProcess.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
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

    /// <summary>(P6C1) The per-context CUT-IN instance (AS-IS <c>GManager.autoProcessing_CutIn</c>,
    /// GManager.cs:112 — a SECOND AutoProcessing component whose <see cref="StackedSkillInfos"/> stack the play
    /// pipeline uses for the BeforePayCost/AfterPayCost cut-in windows, kept separate from the main trigger
    /// stack). Cached under its own service key so it never aliases <see cref="For"/>.</summary>
    public static AutoProcessing ForCutIn(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetService(out CutInInstanceHolder? holder) && holder is not null)
        {
            return holder.Value;
        }

        var created = new CutInInstanceHolder(new AutoProcessing(context));
        context.RegisterService(created);
        return created.Value;
    }

    /// <summary>(P6C1) Service-registry key wrapper for <see cref="ForCutIn"/>.</summary>
    private sealed class CutInInstanceHolder
    {
        public CutInInstanceHolder(AutoProcessing value)
        {
            Value = value;
        }

        public AutoProcessing Value { get; }
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

    // ============================================================================================================
    // (P6 stage A) AS-IS trigger-stack half — the flip's LIVE collection primitives (no central registry: every
    // pass re-enumerates the game state; see docs/audit/rebuild_p6_stageA_notes.md §1). 1:1 ports of AS-IS
    // AutoProcessing.cs:24 (StackedSkillInfos) / :57-118 (PutStackedSkill) / :770-887 (GetSkillInfos) /
    // :893-978 (ActivateBackgroundEffects) / :984-989 (StackSkillInfos). Substrate translation: coroutine →
    // async Task, StartCoroutine(X) → await X. The stacked list is NOT yet drained by the window loop (the
    // mirror WindowResolver collects its own queued-event seed) — design item P6A-STACKED-DRAIN; today the only
    // engine caller is the Activate_Effect_Execute tail (StackSkillInfos(null, AfterEffectsActivate),
    // AS-IS ICardEffect.cs:1283), and 0 ported cards return AfterEffectsActivate effects, so nothing stacks.
    // ============================================================================================================

    #region effect stack (AS-IS AutoProcessing.cs:24)
    public List<SkillInfo> StackedSkillInfos { get; set; } = new List<SkillInfo>();
    #endregion

    // ============================================================================================================
    // (R3-W1b, batch W1) AS-IS trigger-stack DRAIN half — LIVE since the R3-C2 flip (commit 8f155d02). The
    // MultipleSkills window loop, the component pool, the cut-in accounting, TriggeredSkillProcess,
    // HasAwaitingActivateEffects, and AutoProcessCheck. TriggeredSkillProcess's drain runs whenever
    // StackedSkillInfos is NON-EMPTY, which GameFlowProcessor.AutoProcessAsync (seam 7) + the inline StackSkillInfos
    // inserts / deletion transport now populate, so availableMultipleSkills.ActivateMultipleSkills is reached. The
    // three cut-in callers (CardController.cs:2623/2887/3258) still run over an EMPTY cut-in stack in every
    // currently-exercised scenario (RDW-06 two-pass counter is C2b). The old WindowResolver trigger window is DEAD
    // (0 live callers — grep-verified at the flip). Substrate: coroutine -> Task.
    // ============================================================================================================

    #region Skill Processing Component pool (AS-IS AutoProcessing.cs:13-54)

    // AS-IS AutoProcessing.cs:14 — `public List<MultipleSkills> multipleSkills`. SUBSTRATE: the AS-IS pool is a
    // scene-serialized fixed List (BattleScene.unity), its size NOT in source. The mirror grows the pool on demand
    // (see availableMultipleSkills), so the window's cut-in recursion never nulls out for lack of a free component;
    // termination is unchanged (each pass removes the resolved skill from the stack). Each MultipleSkills carries
    // this context so its choice port binds to the same match.
    private readonly List<MultipleSkills> _multipleSkills = new List<MultipleSkills>();
    public List<MultipleSkills> multipleSkills => _multipleSkills;

    // AS-IS AutoProcessing.cs:20-35 — the first idle component. ADAPTATION: on an exhausted pool the AS-IS returns
    // null (a fixed-depth cap); the mirror grows a fresh idle component instead (documented substrate — the pool
    // size is a scene value absent from source).
    public MultipleSkills availableMultipleSkills
    {
        get
        {
            foreach (MultipleSkills _multipleSkills in multipleSkills)
            {
                if (!_multipleSkills.IsUsing)
                {
                    return _multipleSkills;
                }
            }

            MultipleSkills created = new MultipleSkills(_context);
            _multipleSkills.Add(created);
            return created;
        }
    }

    // AS-IS AutoProcessing.cs:39-53 — the innermost in-use component (deepest cut-in frame).
    public MultipleSkills executingMultipleSkills
    {
        get
        {
            for (int i = 0; i < multipleSkills.Count; i++)
            {
                if (multipleSkills[multipleSkills.Count - 1 - i].IsUsing)
                {
                    return multipleSkills[multipleSkills.Count - 1 - i];
                }
            }

            return null;
        }
    }

    #endregion

    #region put the effect on the stack

    // AS-IS AutoProcessing.cs:57-118.
    public void PutStackedSkill(SkillInfo skillInfo)
    {
        if (skillInfo == null) return;
        if (skillInfo.CardEffect == null) return;
        if (skillInfo.CardEffect.EffectSourceCard == null) return;
        if (!(skillInfo.CardEffect is ActivateICardEffect)) return;

        CardSource card = skillInfo.CardEffect.EffectSourceCard;
        // ADAPTATION (2): AS-IS `card.PermanentOfThisCard()` returns a Permanent; the mirror accessor returns a
        // PermanentView — bridge via ICardEffect.ResolvePermanentOfThisCard (null when off-field, as AS-IS).
        Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);

        if (!skillInfo.CardEffect.IsOptionEffect) //If explicitly set to be an option effect, override setting it to any other kind
        {
            //Inherited and Link effects can only ever be digimon effects
            if (skillInfo.CardEffect.IsInheritedEffect || skillInfo.CardEffect.IsLinkedEffect)
            {
                skillInfo.CardEffect.SetIsDigimonEffect(true);
            }
            else
            {
                #region set the flag whether it is Digimon's effect
                if (permanent != null)
                {
                    if (permanent.IsDigimon)
                    {
                        skillInfo.CardEffect.SetIsDigimonEffect(true);
                    }
                }

                // ADAPTATION (gap1 SECURITYDIGIMON): AS-IS `card == GManager.instance.attackProcess.SecurityDigimon`
                // compares CardSources; the mirror AttackProcess.SecurityDigimon is the card INSTANCE id.
                if (GManager.instance.attackProcess.SecurityDigimon is { } securityDigimon && card.InstanceId == securityDigimon)
                {
                    skillInfo.CardEffect.SetIsDigimonEffect(true);
                }
                #endregion

                #region set the flag whether it is Tamer's effect
                if (permanent != null)
                {
                    if (permanent.IsTamer)
                    {
                        skillInfo.CardEffect.SetIsTamerEffect(true);
                    }
                }

                else if (card.IsTamer)
                {
                    skillInfo.CardEffect.SetIsTamerEffect(true);
                }
                #endregion
            }
        }

        #region set permanent and topCard when triggered
        ((ActivateICardEffect)skillInfo.CardEffect).PermanentWhenTriggered = permanent;

        if (permanent != null)
        {
            ((ActivateICardEffect)skillInfo.CardEffect).TopCardWhenTriggered = permanent.TopCard;
        }
        #endregion

        StackedSkillInfos.Add(skillInfo);
    }

    #endregion

    #region Get skill infos

    // AS-IS AutoProcessing.cs:770-887 — the LIVE collection scan (players' granted effects, field permanents,
    // trash, hand, non-flipped security), filter `is ActivateICardEffect` && !IsBackgroundProcess &&
    // CanTrigger(hashtable).
    public static List<SkillInfo> GetSkillInfos(Hashtable hashtable, EffectTiming timing, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        List<SkillInfo> skillInfos = new List<SkillInfo>();

        #region player effect
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (ICardEffect cardEffect in player.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (!cardEffect.IsBackgroundProcess)
                    {
                        if (cardEffect.CanTrigger(hashtable))
                        {
                            skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                        }
                    }
                }
            }
        }
        #endregion

        #region Effects of permanents in play
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (!cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Trash card effect
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (CardSource cardSource in player.TrashCards)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (!cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Effects of cards in hand
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (CardSource cardSource in player.HandCards)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (!cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Effects of faceup security
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (CardSource source in player.SecurityCards)
            {
                if (source.IsFlipped)
                    continue;

                foreach (ICardEffect cardEffect in source.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (!cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                            }
                        }
                    }
                }
            }
        }
        #endregion

        return skillInfos;
    }

    #endregion

    #region Activate background effects

    // AS-IS AutoProcessing.cs:893-978 — a background (`IsBackgroundProcess`) effect activates IMMEDIATELY at
    // stack time: CanUse gate → RegisterUseEffectThisTurn → Activate(hashtable). Same zone scan order as
    // GetSkillInfos minus the security region (AS-IS scans player/field/trash/hand only here).
    public static async Task ActivateBackgroundEffects(Hashtable hashtable, EffectTiming timing, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        #region Player effect
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (ICardEffect cardEffect in player.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
            {
                if (cardEffect is ActivateICardEffect)
                {
                    if (cardEffect.IsBackgroundProcess)
                    {
                        if (cardEffect.CanUse(hashtable))
                        {
                            cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                            await ((ActivateICardEffect)cardEffect).Activate(hashtable);
                        }
                    }
                }
            }
        }
        #endregion

        #region Effects of permanents in play
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanUse(hashtable))
                            {
                                cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                                await ((ActivateICardEffect)cardEffect).Activate(hashtable);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Trash card effect
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (CardSource cardSource in player.TrashCards)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanUse(hashtable))
                            {
                                cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                                await ((ActivateICardEffect)cardEffect).Activate(hashtable);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Effects of cards in hand
        foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
        {
            foreach (CardSource cardSource in player.HandCards)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanUse(hashtable))
                            {
                                cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                                await ((ActivateICardEffect)cardEffect).Activate(hashtable);
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }

    #endregion

    #region Stack skillInfos

    // AS-IS AutoProcessing.cs:984-989.
    public async Task StackSkillInfos(Hashtable hashtable, EffectTiming timing, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        GetSkillInfos(hashtable, timing, cardEffectCondition).ForEach(skillInfo => PutStackedSkill(skillInfo));

        await ActivateBackgroundEffects(hashtable, timing, cardEffectCondition);
    }

    #endregion

    #region Get skillInfos of cards

    // (P6C1) AS-IS AutoProcessing.cs:993-1021 — the per-CARD-LIST collection scan (the play pipeline feeds it
    // the card being played, filtered to off-field/off-hand/off-trash/off-security roots). Same filter chain as
    // GetSkillInfos; `cardSource.PermanentOfThisCard() == null` bridged via ICardEffect.ResolvePermanentOfThisCard
    // (the established adaptation (2) — the mirror accessor returns a PermanentView).
    public static List<SkillInfo> GetSkillInfosOfCards(Hashtable hashtable, EffectTiming timing, List<CardSource> cardSources, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        List<SkillInfo> skillInfos = new List<SkillInfo>();

        #region Effects of the card list
        foreach (CardSource cardSource in cardSources)
        {
            if (ICardEffect.ResolvePermanentOfThisCard(cardSource) == null)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (!cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                skillInfos.Add(new SkillInfo(cardEffect, hashtable, timing));
                            }
                        }
                    }
                }
            }
        }
        #endregion

        return skillInfos;
    }

    #endregion

    #region Activate background effects of cards

    // (R3-B) AS-IS AutoProcessing.cs:1024-1049 — the per-CARD-LIST analogue of ActivateBackgroundEffects: a
    // background (`IsBackgroundProcess`) effect of an OFF-FIELD card (PermanentOfThisCard()==null) activates
    // IMMEDIATELY at stack time (CanUse gate → RegisterUseEffectThisTurn → Activate). Same filter chain as
    // GetSkillInfosOfCards; the AS-IS `PermanentOfThisCard() == null` off-field guard is bridged via
    // ICardEffect.ResolvePermanentOfThisCard (the established adaptation (2) — the mirror accessor returns a
    // PermanentView). Pure live re-enumeration — reads NO EffectRegistry/PendingEffect/EffectBinding.
    public static async Task ActivateBackgroundEffectsOfCards(Hashtable hashtable, EffectTiming timing, List<CardSource> cardSources, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        #region Effect of card list
        foreach (CardSource cardSource in cardSources)
        {
            if (ICardEffect.ResolvePermanentOfThisCard(cardSource) == null)
            {
                foreach (ICardEffect cardEffect in cardSource.EffectList(timing).Filter(cardEffect => cardEffectCondition == null || cardEffectCondition(cardEffect)))
                {
                    if (cardEffect is ActivateICardEffect)
                    {
                        if (cardEffect.IsBackgroundProcess)
                        {
                            if (cardEffect.CanUse(hashtable))
                            {
                                cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                                await ((ActivateICardEffect)cardEffect).Activate(hashtable);
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }

    #endregion

    #region Stack skillInfos of cards

    // (R3-B) AS-IS AutoProcessing.cs:1053-1058, IEnumerator→Task, StartCoroutine(X)→await X. Live re-enumeration:
    // collect the card-list triggers (GetSkillInfosOfCards) onto the stack, then activate this list's background
    // effects. NO EffectRegistry read — the AS-IS live GetSkillInfosOfCards scan is the sole source.
    public async Task StackSkillInfosOfCards(Hashtable hashtable, EffectTiming timing, List<CardSource> cardSources, Func<ICardEffect, bool> cardEffectCondition = null)
    {
        GetSkillInfosOfCards(hashtable, timing, cardSources, cardEffectCondition).ForEach(skillInfo => PutStackedSkill(skillInfo));

        await ActivateBackgroundEffectsOfCards(hashtable, timing, cardSources, cardEffectCondition);
    }

    #endregion

    #region Trigger effect processing

    // (P6C1) AS-IS AutoProcessing.cs:18 — the skills excluded from the next drain pass.
    public List<SkillInfo> skipSkillInfos = new List<SkillInfo>();

    // (R3-W1b) AS-IS AutoProcessing.cs:572-600, IEnumerator→Task, StartCoroutine(X)→await X. The AS-IS :574
    // ShrinkSecurityDigimonDisplay call is UI (stripped — see the HasAwaitingActivateEffects note). The empty-stack
    // fast path is exact (the AfterPayCost drain of a play with no collected cut-in effect is a no-op). The
    // non-empty drain hands the batch to `availableMultipleSkills.ActivateMultipleSkills(...)` (AS-IS :589-595 —
    // the RD-R3W1b-01 STOP is now resolved: MultipleSkills is the live 1:1 mirror), then re-stacks the
    // AfterEffectsActivate timing (:597). LIVE since the R3-C2 flip: this IS the drain that empties StackedSkillInfos
    // (driven by AutoProcessCheck). Only the CUT-IN recursion arm stays unexercised — the cut-in stack is empty in
    // every currently-exercised scenario (RDW-06 two-pass counter is C2b).
    public async Task TriggeredSkillProcess(bool CheckNewTriggredSkill_mainStack, Func<List<SkillInfo>, SkillInfo, bool> skipCondition)
    {
        if (StackedSkillInfos.Count > 0)
        {
            List<SkillInfo> skillInfos = StackedSkillInfos.Filter(skillInfo => skillInfo != null && !skipSkillInfos.Contains(skillInfo));

            if (skillInfos.Count >= 1)
            {
                //Clear the list of triggering skills not included in the resolution timing
                foreach (SkillInfo skillInfo in skillInfos)
                {
                    StackedSkillInfos.Remove(skillInfo);
                }

                //Process the list of skills being triggered
                if (availableMultipleSkills != null)
                {
                    await availableMultipleSkills.ActivateMultipleSkills(
                        skillInfos,
                        this,
                        CheckNewTriggredSkill_mainStack,
                        skipCondition);
                }

                await StackSkillInfos(null, EffectTiming.AfterEffectsActivate);
            }
        }
    }

    #endregion

    #region Resume suspended windows (batch W1b)

    // (R3-W1b batch W1b) Drain-level resume of the SUSPENDED MultipleSkills chain, DEEPEST-FIRST. The window's
    // loop position lives on the C# call stack (turn/non-turn split, while(true) pass loop, cut-in recursion via
    // nested TriggeredSkillProcess); a WindowChoicePendingException / DeferredChoicePendingException unwinds that
    // stack, so the SURVIVING chain is the pool's still-IsUsing instances (each externalised its own cursor on the
    // continuation). A suspend can nest — a parent pick's TriggeredSkillProcess spawned a child window that
    // suspended — so the DEEPEST in-use instance (executingMultipleSkills) must resume first; when it completes,
    // the enclosing TriggeredSkillProcess TAIL (StackSkillInfos(null, AfterEffectsActivate), AutoProcessing.cs:597
    // — the line the unwind skipped) is re-run for ITS OWNING drain (the AutoProcessing that spawned it,
    // MultipleSkills.OwningAutoProcessing), and then the next-outer instance resumes at its own pass head (AS-IS:
    // the parent's remaining steps after the child call ARE "loop to pass head", MultipleSkills.cs:405-421). Each
    // ResumeAsync may RE-suspend (the pending exception propagates out — the seam re-parks and a later call
    // re-enters the same chain, idempotently). LIVE since the R3-C2 flip: the window loop drives real windows, so an
    // in-use instance exists whenever a window suspends for an agent choice; the seam-2 ResolveChoice path calls this
    // to resume the suspended chain. It is a no-op only while the pool is genuinely idle (no suspended window).
    //
    // OWNERSHIP: the tail runs on `deepest.OwningAutoProcessing` (the drain that owns this window), not necessarily
    // `this`. For the main-stack triggered recursion every instance's owning drain IS the main AutoProcessing (its
    // pool), so a two-deep chain replays two tails on it (AS-IS: the child's D1 tail then the parent's D0 tail).
    // The play-pipeline CUT-IN pool (autoProcessing_CutIn) is a separate instance whose stack is empty in every
    // currently-exercised scenario (design §5.5 / batch W1 note), so cross-pool chains do not arise here.
    public async Task ResumeSuspendedWindowsAsync(CancellationToken cancellationToken = default)
    {
        while (executingMultipleSkills is { } deepest)
        {
            AutoProcessing owningDrain = deepest.OwningAutoProcessing;

            await deepest.ResumeAsync(cancellationToken).ConfigureAwait(false);

            // AS-IS TriggeredSkillProcess tail (AutoProcessing.cs:597) that followed the completed
            // ActivateMultipleSkills — never reached on the original unwound drain, replayed here.
            if (owningDrain != null)
            {
                await owningDrain.StackSkillInfos(null, EffectTiming.AfterEffectsActivate).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region List of effects already achieved

    // AS-IS AutoProcessing.cs:604-621 — the aggregate of every pool component's SkillInfos_used (the cut-in
    // skipCondition reads it via MultipleSkills' gate: `_autoProcessing.skillInfos_used`).
    public List<SkillInfo> skillInfos_used
    {
        get
        {
            List<SkillInfo> skillInfos_used = new List<SkillInfo>();

            foreach (MultipleSkills multipleSkills in multipleSkills)
            {
                foreach (SkillInfo skillInfo in multipleSkills.SkillInfos_used)
                {
                    skillInfos_used.Add(skillInfo);
                }
            }

            return skillInfos_used;
        }
    }

    #endregion

    #region Whether there is awaiting activate effects

    // AS-IS AutoProcessing.cs:750-766 — the play pipeline's cut-in gate (CardController reads
    // autoProcessing_CutIn.HasAwaitingActivateEffects). AS-IS :731-746 ShrinkSecurityDigimonDisplay is a
    // UI-only coroutine (moves the security Digimon's on-screen card + runs the brainstorm animation) and is
    // stripped throughout this half.
    public bool HasAwaitingActivateEffects()
    {
        if (StackedSkillInfos.Count >= 2)
        {
            return true;
        }

        else if (StackedSkillInfos.Count == 1)
        {
            if (StackedSkillInfos[0].CardEffect.CanActivate(StackedSkillInfos[0].Hashtable))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Automated processing checks

    // AS-IS AutoProcessing.cs:122-140, IEnumerator→Task. DoneStartGame gate, then RuleProcess → RulesTiming
    // stack → TriggeredSkillProcess(false, null). AS-IS :126 ShrinkSecurityDigimonDisplay is UI (stripped);
    // AS-IS :128/:139 `turnStateMachine.IsSelecting = true/false` is a UI/selection-guard flag with no mirror
    // (stripped). LIVE (since the R3-C2 flip, commit 8f155d02): GameFlowProcessor.AutoProcessAsync drives this per
    // batch-group (seam 7), and SecurityResolver.ResolveSecurityCheckWindowAsync drives it for the OnSecurityCheck
    // window; it drains StackedSkillInfos through the live MultipleSkills window loop. The old WindowResolver main
    // loop is DEAD (0 live callers — grep-verified at the flip).
    public async Task AutoProcessCheck(CancellationToken cancellationToken = default)
    {
        if (!GManager.instance.turnStateMachine.DoneStartGame) return;

        //Rule processing
        await RuleProcess(cancellationToken);

        //Rule Timing
        await StackSkillInfos(null, EffectTiming.RulesTiming);

        //Trigger effect processing
        await TriggeredSkillProcess(false, null);
    }

    #endregion

    #region The same effect has already been achieved

    // (P6C1) AS-IS AutoProcessing.cs:624-627 — verbatim (the play pipeline's cut-in skipCondition).
    public static bool HasExecutedSameEffect(List<SkillInfo> skillInfos, SkillInfo skillInfo)
    {
        return skillInfos.Some((skillInfo1) => skillInfo1.CardEffect.IsSameEffect(skillInfo.CardEffect));
    }

    #endregion

    #region Check end of turn

    // (R4 P2b) AS-IS AutoProcessing.cs:630-643, IEnumerator→Task. The turn-end trigger every phase body pumps
    // (TurnStateMachine :579/:641/:655/:696/:704/:831/:880/:950): outside the End phase, with no attack mid-flight,
    // the NON-turn player having reached the (effect-adjustable) turn-end memory threshold starts EndTurnProcess —
    // clearing `Passed` first so the threshold-triggered run skips the explicit-pass gauge jump (:681-694 arm).
    // DORMANT until the S3 driver flip (callers = the dormant phase bodies only); the live turn-end evaluation
    // stays HeadlessMainPhaseFlow.EvaluateMemoryPass / MetadataActionProcessor.EndTurnAsync until S3 retires it.
    public async Task EndTurnCheck(CancellationToken cancellationToken = default)
    {
        if (GManager.instance.turnStateMachine.gameContext.TurnPhase != GameContext.phase.End && !GManager.instance.attackProcess.ActiveAttack())
        {
            if (GManager.instance.turnStateMachine.gameContext.NonTurnPlayer!.MemoryForPlayer >= TurnEndMinMemory)
            {
                GManager.instance.turnStateMachine.Passed = false;
                await GManager.instance.autoProcessing.EndTurnProcess(cancellationToken);
            }
        }
    }

    #endregion

    #region Minimum memory to end turn

    // (R4 P2b) AS-IS AutoProcessing.cs:645-672 — verbatim fold (seed 1) over every usable
    // IChangeEndTurnMinMemoryEffect on (1) the players' own effect lists then (2) the players' field permanents'
    // effect lists, Players_ForTurnPlayer order (BOTH players, turn player first). Same scan the live
    // HeadlessMainPhaseFlow.ResolveTurnEndMinMemory carries today (rehoused there at W3c-4b B2); this is the
    // AS-IS-position owner — S3 re-points the flow's copy here when the invented evaluation retires.
    static int TurnEndMinMemory
    {
        get
        {
            int turnEndMinMemory = 1;

            #region the effects of players
            GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer
                    .Map(player => player.EffectList(EffectTiming.None))
                    .Flat()
                    .Filter(cardEffect => cardEffect is IChangeEndTurnMinMemoryEffect && cardEffect.CanUse(null))
                    .ForEach(cardEffect => turnEndMinMemory = ((IChangeEndTurnMinMemoryEffect)cardEffect).GetMinMemory(turnEndMinMemory));
            #endregion

            #region the effects of permanents
            GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer
                    .Map(player => player.GetFieldPermanents())
                    .Flat()
                    .Map(permanent => permanent.EffectList(EffectTiming.None))
                    .Flat()
                    .Filter(cardEffect => cardEffect is IChangeEndTurnMinMemoryEffect && cardEffect.CanUse(null))
                    .ForEach(cardEffect => turnEndMinMemory = ((IChangeEndTurnMinMemoryEffect)cardEffect).GetMinMemory(turnEndMinMemory));
            #endregion

            return turnEndMinMemory;
        }
    }

    #endregion

    #region Turn end processing

    // (R4 P2b) AS-IS AutoProcessing.cs:674-727, IEnumerator→Task, StartCoroutine(X)→await X. The turn-end body:
    // explicit-pass gauge jump → OnEndTurn window → auto-process → attack pump → threshold re-check deciding
    // End vs continue-in-Main. AS-IS :679 photonWaitController.StartWait is commented out in the ORIGINAL
    // (nothing to mirror); :707 Debug.Log is log-only (stripped). The OnEndTurn stack + AutoProcessCheck here is
    // the SAME window unit the live MetadataActionProcessor.EndTurnAsync drives via SkillWindowSupply (which
    // builds the AS-IS null payload for OnEndTurn — RDW-07) + DrainEndOfTurnWindowAsync → AutoProcessAsync →
    // this file's StackSkillInfos/AutoProcessCheck — so the S3 flip swaps the COLLECTION entry point, not the
    // window mechanism. AS-IS re-runs the window each EndTurnProcess (no once-per-turn drain marker; bounded by
    // per-effect once-per-turn caps) — at S3 that AS-IS semantic replaces the live EndOfTurnDrainedTurn marker.
    public async Task EndTurnProcess(CancellationToken cancellationToken = default)
    {
        if (GManager.instance.turnStateMachine.gameContext.TurnPhase != GameContext.phase.End)
        {
            // yield return GManager.instance.photonWaitController.StartWait("EndTurnProcess");

            if (GManager.instance.turnStateMachine.Passed && GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Main)
            {
                // AS-IS :683-693 seat-absolute gauge write (TurnPlayer.PlayerID==0 ⇒ Memory=3 / else Memory=-3):
                // both arms SET the shared gauge so the NON-turn player reads +3. The mirror gauge is
                // turn-player-relative (see Player.MemoryForPlayer / AddMemory), so both AS-IS arms reduce to the
                // single Set(-3) — the same value the live explicit-pass seam writes
                // (HeadlessMainPhaseFlow.PassTurn, -DefaultMemoryPassValue). Applied directly on the controller,
                // as the AS-IS body writes the gauge directly (Player.AddMemory precedent). ADAPTATION.
                _context.MemoryController.Set(-3);

                // AS-IS :694 memoryObject.SetMemory() — memory-tab UI animation (stripped; Player.cs:559 precedent).
            }

            GManager.instance.turnStateMachine.Passed = true;

            //Effect at end of turn
            await StackSkillInfos(null, EffectTiming.OnEndTurn);

            //Automatic processing check timing
            await GManager.instance.autoProcessing.AutoProcessCheck(cancellationToken);

            //Handle attack steps
            while (GManager.instance.attackProcess.ActiveAttack())
            {
                // AS-IS :707 Debug.Log($"Active Attack, {...} Step") — log only (stripped).
                await GManager.instance.attackProcess.ProcessNextState(cancellationToken);

                //自動処理チェックタイミング
                await GManager.instance.autoProcessing.AutoProcessCheck(cancellationToken);
            }

            if (GManager.instance.turnStateMachine.gameContext.NonTurnPlayer!.MemoryForPlayer >= TurnEndMinMemory)
            {
                GManager.instance.turnStateMachine.gameContext.TurnPhase = GameContext.phase.End;
            }

            else
            {
                if (GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Main)
                {
                    // AS-IS :722 StartCoroutine(turnStateMachine.SetMainPhase()) — SetMainPhase (:1354-2872) is
                    // the main-phase UI re-arm (non-scope, S1 decision 1). The RULE content of this arm is "the
                    // turn does NOT end; play continues in Main", carried by the phase staying Main — the
                    // MainPhase pump (TurnStateMachine.MainPhaseAsync, AS-IS :936 while) continues. ADAPTATION.
                }
            }
        }
    }

    #endregion

    #region Activate effect process

    public ICardEffect MainProcessingEffect { get; private set; } = null;
    List<ICardEffect> _usedCutinEffects = new List<ICardEffect>();

    // AS-IS AutoProcessing.cs:1063-1088 — the stacked-skill EXECUTION entry: per-pass `CanActivate(hashtable)
    // || IsDeclarative` gate, then Activate_Optional_Effect_Execute (the AS-IS Optional→Effect→Execute flow).
    public async Task ActivateEffectProcess(ICardEffect cardEffect, Hashtable hashtable, bool isCheckOptional = true)
    {
        if (cardEffect == null) return;
        if (!(cardEffect is ActivateICardEffect)) return;

        if (cardEffect.CanActivate(hashtable) || cardEffect.IsDeclarative)
        {
            bool isEffectProcessing = MainProcessingEffect != null;

            if (!isEffectProcessing)
            {
                MainProcessingEffect = cardEffect;

                _usedCutinEffects = new List<ICardEffect>();
            }

            await ((ActivateICardEffect)cardEffect).Activate_Optional_Effect_Execute(hashtable, isCheckOptional);

            if (!isEffectProcessing)
            {
                MainProcessingEffect = null;

                _usedCutinEffects = new List<ICardEffect>();
            }
        }
    }

    #endregion

    #region Cut-in effect accounting (AS-IS AutoProcessing.cs:1090-1106)

    // AS-IS AutoProcessing.cs:1090-1093 — VERBATIM: returns false. The real check
    // (`_usedCutinEffects.Some(cardEffect => cardEffect.IsSameEffect(cutinEffect))`) is commented out in the
    // original (a design-item, deliberately-disabled cut-in re-use guard); mirrored as-is.
    public bool IsCutInEffectHasUsed(ICardEffect cutinEffect)
    {
        return false;// AS-IS General line 17 — _usedCutinEffects.Some(cardEffect => cardEffect.IsSameEffect(cutinEffect));
    }

    // AS-IS AutoProcessing.cs:1095-1098.
    public bool IsCutInEffectUsedMaxCount(ICardEffect cutinEffect)
    {
        return _usedCutinEffects.Count(cardEffect => cardEffect.IsSameEffect(cutinEffect)) < cutinEffect.ChainActivations;
    }

    // AS-IS AutoProcessing.cs:1100-1106.
    public void AddCutinEffect(ICardEffect cardEffect)
    {
        if (GManager.instance.autoProcessing.MainProcessingEffect != null)
        {
            _usedCutinEffects.Add(cardEffect);
        }
    }

    #endregion
}
