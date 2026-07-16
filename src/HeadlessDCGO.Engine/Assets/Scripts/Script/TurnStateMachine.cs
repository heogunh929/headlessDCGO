// Source: DCGO/Assets/Scripts/Script/TurnStateMachine.cs
// (EFFECT-MODEL REBUILD) File at the AS-IS path Script/TurnStateMachine.cs; namespace kept ...CardEffectCommons
// so existing references are unaffected — namespace normalisation is a later, separate pass.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>(MIG6 goal-6 surface) Thin mirror of the original <c>TurnStateMachine</c> — the ONLY member card
/// effects reach on it is <c>.gameContext</c> (no non-gameContext <c>turnStateMachine.X</c> card call site
/// exists). The turn-flow logic itself (StartGame/SetMainPhase/EndTurn/EndGame) lives in the verified
/// substrate <c>GameFlowProcessor</c> / <c>HeadlessGameLoop</c> "temporary home" — re-housing it here is
/// high-risk / zero behavioral value (goal-3 lesson) and is deferred. This wrapper only reproduces the AS-IS
/// <c>GManager.instance.turnStateMachine.gameContext</c> access path so card ports mirror it mechanically.</summary>
public sealed class TurnStateMachine
{
    private readonly EngineContext _context;

    private TurnStateMachine(EngineContext context)
    {
        _context = context;
        gameContext = new GameContext(context);
    }

    /// <summary>The per-match <see cref="GameContext"/> (AS-IS <c>turnStateMachine.gameContext</c>).</summary>
    public GameContext gameContext { get; }

    /// <summary>(MIG6/rebuild; R4 S2) AS-IS <c>TurnStateMachine.DoneStartGame</c>: the initial setup sequence
    /// (mulligan + security deal) has completed, so triggered effects may fire (ICardEffect.CanTrigger gates on
    /// this). Headless proxy: a match is active and past phase None — effect resolution only runs during live play,
    /// so this reads true throughout normal effect processing. (R4 S2 folded the former HeadlessPhase.Setup into the
    /// (None, Starting) step, so the pre-game guard is now simply "phase is not None".)</summary>
    public bool DoneStartGame =>
        _context.TurnController.Current.Phase is not HeadlessPhase.None;

    // (EFFECT-MODEL REBUILD / P2, design item P2-ISEXECUTING) AS-IS TurnStateMachine.isExecuting
    // (TurnStateMachine.cs:23, a plain mutable public bool). The foundation `ActivateICardEffectExtensionClass`
    // saves/restores it around an effect execution (read old -> set true -> ... -> restore). The mirror
    // TurnStateMachine is a per-access VIEW (a fresh instance on every `GManager.instance`), so a per-instance
    // field would not survive that save/restore across two `GManager.instance` reads. Backed by a match-scoped
    // box (keyed by EngineContext, same idea as CEntity_EffectControllerStore) so the flag is stable per match.
    // The mirror's async execution model does not depend on this re-entrancy guard (no live coroutine frame); it
    // exists only to reproduce the AS-IS save/restore verbatim.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EngineContext, System.Runtime.CompilerServices.StrongBox<bool>> _isExecutingStore = new();

    public bool isExecuting
    {
        get => _isExecutingStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value;
        set => _isExecutingStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value = value;
    }

    #region (R4 P2a) Turn-flow phase bodies — DORMANT AS-IS-region mirror (0 live callers)

    // (R4 batch P2a, design docs/audit/r4_tsm_s1_design_2026-07-16.md + r4_tsm_investigation_2026-07-16.md)
    // AS-IS-region 1:1 mirror of the TurnStateMachine phase bodies, assembled DORMANT: the live turn drivers
    // (MetadataActionProcessor / HeadlessGameLoop / HeadlessEarlyPhaseFlow / HeadlessMainPhaseFlow) are UNTOUCHED
    // and NOTHING calls these six methods (grep-verified 0 callers) — the driver flip is S3. Established
    // adaptations applied throughout: coroutine → async Task, `GManager.instance.X` → EngineContext services /
    // `.For(_context)` singletons, Unity/Photon/UI (commandText / selectCardPanel / ShowPhase / outlines / SE /
    // FirstObject / draggables) stripped, `WaitUntil`/`WaitWhile` interactive stops delegated to the established
    // choice-pause + action-queue seams.
    //
    // TWO deferred seams are represented as CALL-SURFACE ONLY (no body mirror), by design:
    //   * S2-cursor (decision 2 = option A): the AS-IS 6-value `gameContext.TurnPhase` write/read is represented
    //     by a local `GameContext.phase currentPhase` cursor. The substrate step-position sub-cursor
    //     (HeadlessTurnState) that S2 introduces does not exist yet, so cross-body phase state is a per-method
    //     local; the mirror `GameContext.TurnPhase` is a computed getter (no setter).
    //   * P2b turn-end seam: `AutoProcessing.EndTurnCheck / TurnEndMinMemory / EndTurnProcess` are NOT yet
    //     mirrored (MultipleSkills-window-inseparable, P2b). Their call sites are left as commented call
    //     surfaces; the `if (phase == End) return;` guards that read their result stay, reading the local cursor.

    /// <summary>AS-IS <c>TurnStateMachine.isFirstPlayerFirstTurn</c> (:26).</summary>
    public bool isFirstPlayerFirstTurn { get; set; } = true;

    /// <summary>AS-IS <c>TurnStateMachine.TurnCount</c> (:29, read by cards via
    /// <c>turnStateMachine.TurnCount</c> — e.g. the EnterFieldTurnCount comparisons in OptionEffect /
    /// Permanent / FieldPermanentCard). (R4 P3 / P1 wrong-host resolution) A read-only view over the substrate
    /// turn counter <see cref="HeadlessTurnState.TurnNumber"/> (1-indexed, same as AS-IS), sibling to
    /// <see cref="DoneStartGame"/> — so a live card read sees the real turn number the turn-controller advances,
    /// not a dormant-body local that stays 0 in live play. The AS-IS :550 in-body increment is the substrate
    /// turn-advance (owned by the turn-controller today; S3 relocates the advance point into ActivePhaseAsync).</summary>
    public int TurnCount => _context.TurnController.Current.TurnNumber;

    /// <summary>AS-IS <c>TurnStateMachine.IsSelecting</c> (:20).</summary>
    public bool IsSelecting { get; set; } = false;

    /// <summary>AS-IS <c>TurnStateMachine.endGame</c> (:3245).</summary>
    public bool endGame { get; set; } = false;

    // AS-IS main-phase selection-intent fields (:861-868): set by the action-queue seam (a pushed HeadlessAction
    // supplies the play/attack/effect intent), reset by ResetMainPhaseParameter.
    private CardSource? PlayCard { get; set; }
    private ICardEffect? UseCardEffect { get; set; }
    private Permanent? AttackingPermanent { get; set; }
    private Permanent? DefendingPermanent { get; set; }
    private int TargetFrameID { get; set; }
    private int[] JogressEvoRootsFrameIDs { get; set; } = new int[0];
    private int BurstTamerFrameID { get; set; }
    private int[] AppFusionFrameIDs { get; set; } = new int[0];

    /// <summary>AS-IS <c>StartGame()</c> (:341-504): initial hands, mulligan, security.</summary>
    public async Task StartGameAsync(CancellationToken cancellationToken = default)
    {
        // AS-IS :347-367 first/second determination + :358 `gameContext.FirstPlayer = gameContext.NonTurnPlayer`:
        //   commandText UI only; the first-player seat is turn-order state owned by the substrate turn-controller
        //   (mirror GameContext.FirstPlayer has no setter). P1/S2-junction: first-player seat on HeadlessTurnState.

        // AS-IS :369-372 draw 5, non-turn-player first.
        foreach (Player player in gameContext.Players_ForNonTurnPlayer)
        {
            await new DrawClass(_context, player.PlayerId, 5, null).Draw(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :374-494 mulligan + :496-501 security 5. The interactive per-player keep/redraw stop is
        //   externalized to the established choice-pause (design decision-1, "신설 금지"):
        //   MulliganCoordinator.Begin opens the ChoiceType.Mulligan decision (first player first); the driver
        //   step-loop pumps MulliganCoordinator.ResolveAsync per player, which applies each redraw (hand → deck
        //   bottom, shuffle, draw 5) AND deals security (DealSecurityAsync, 5 each) once all have decided — so
        //   AS-IS :496-501 security is NOT a separate step in the externalized model (coordinator-owned).
        _context.MulliganCoordinator.Begin(
            _context.ChoiceController,
            gameContext.Players_ForNonTurnPlayer.Select(player => player.PlayerId).ToList(),
            handSize: 5,
            securitySize: 5);

        // AS-IS :503 `DoneStartGame = true`. Mirror DoneStartGame is a computed getter (phase past None/Setup);
        //   the AS-IS mutable set-point maps to the controller leaving Setup after the mulligan choice-pause
        //   resolves, driven by the driver. S2-junction (risk ②): exact set-point = post-security phase transition.
    }

    /// <summary>AS-IS <c>ActivePhase()</c> (:530-648): start-of-turn window, attack pump, unsuspend, bucket resets.</summary>
    public async Task ActivePhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);
        AttackProcess attackProcess = AttackProcess.For(_context);
        Player turnPlayer = gameContext.TurnPlayer!;

        // AS-IS :532 turnPlayer.SetTurnStartTime() — turn-clock UI; no headless analog. ADAPTATION: dropped.

        // AS-IS :534-537 reset each turn-player permanent's OnOwnerTurnStart list.
        foreach (Permanent permanent in turnPlayer.GetFieldPermanents())
        {
            permanent.UntilOwnerTurnStartEffects = new List<Func<EffectTiming, ICardEffect>>();
        }

        // AS-IS :539-548 SE / FirstObject / log — UI stripped.

        // AS-IS :550 TurnCount++ — the mirror turn counter (HeadlessTurnState.TurnNumber) is advanced by the
        //   substrate turn-controller at the turn boundary; TurnCount is now a read-only view over it (R4 P3 /
        //   P1 resolution), so the increment is not a dormant-body mutation. S3 relocates the advance point here.
        // AS-IS :552 turnPlayer.TurnCount++ — mirror Player has no per-player TurnCount member
        //   (substrate PlayerTurnCounterController owns it). P1-junction: per-player turn count.

        // AS-IS :554 phase = Active (S2-cursor).
        GameContext.phase currentPhase = GameContext.phase.Active;

        // AS-IS :557-561 showTurnPlayer / nextPhaseButton — UI stripped.

        // AS-IS :564 start-of-turn (OnStartTurn) window.
        await autoProcessing.StackSkillInfos(null, EffectTiming.OnStartTurn).ConfigureAwait(false);
        // AS-IS :567 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);

        // AS-IS :570-576 pump attacks caused this phase.
        while (attackProcess.ActiveAttack())
        {
            await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :579 turn-end check — P2b seam (body-mirror forbidden): may set currentPhase = End.
        //   await autoProcessing.EndTurnCheck();   // P2b
        if (currentPhase == GameContext.phase.End)
        {
            return;   // AS-IS :581-584
        }

        // AS-IS :586-624 Unsuspend.
        List<Permanent> unsuspendPermanents = new List<Permanent>();
        foreach (Permanent permanent in gameContext.PermanentsForTurnPlayer)
        {
            if (permanent.IsSuspended && permanent.CanUnsuspend)
            {
                // AS-IS :597 `permanent.TopCard.Owner == gameContext.TurnPlayer` — mirror CardSource.Owner is a
                //   HeadlessPlayerId (not a Player), so compare to turnPlayer.PlayerId. ADAPTATION.
                if (permanent.TopCard.Owner == turnPlayer.PlayerId || permanent.HasReboot)
                {
                    unsuspendPermanents.Add(permanent);
                }
            }
        }

        foreach (Permanent permanent in turnPlayer.GetBreedingAreaPermanents())
        {
            if (permanent.IsSuspended)
            {
                permanent.IsSuspended = false;   // AS-IS :609 (ShowPermanentData UI :611-614 stripped)
            }
        }

        await new IUnsuspendPermanents(unsuspendPermanents, null).Unsuspend(cancellationToken).ConfigureAwait(false);

        // AS-IS :629 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        // AS-IS :632-638 pump attacks.
        while (attackProcess.ActiveAttack())
        {
            await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        }
        // AS-IS :641 turn-end check — P2b seam.
        //   await autoProcessing.EndTurnCheck();   // P2b

        // AS-IS :643-647 reset active-phase-end lists.
        turnPlayer.UntilOwnerActivePhaseEffects = new List<Func<EffectTiming, ICardEffect>>();
        foreach (Permanent permanent in turnPlayer.GetBattleAreaDigimons())
        {
            permanent.UntilNextUntapEffects = new List<Func<EffectTiming, ICardEffect>>();
        }
    }

    /// <summary>AS-IS <c>DrawPhase()</c> (:652-697): turn-1 skip, deck-out loss, draw 1.</summary>
    public async Task DrawPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);
        AttackProcess attackProcess = AttackProcess.For(_context);
        Player turnPlayer = gameContext.TurnPlayer!;
        GameContext.phase currentPhase = gameContext.TurnPhase;

        // AS-IS :655 turn-end check — P2b seam.
        //   await autoProcessing.EndTurnCheck();   // P2b
        if (currentPhase == GameContext.phase.End)
        {
            return;   // AS-IS :657-660
        }

        currentPhase = GameContext.phase.Draw;   // AS-IS :666 (S2-cursor)

        // AS-IS :669-682 draw (skipped on turn 1).
        if (TurnCount != 1)
        {
            // AS-IS :672-677 deck-out loss: the turn player must draw from an empty library → the non-turn
            //   player wins (EndGame(NonTurnPlayer, false)).
            if (turnPlayer.LibraryCards.Count == 0)
            {
                EndGame(gameContext.NonTurnPlayer, false);
                return;
            }

            await new DrawClass(_context, turnPlayer.PlayerId, 1, null).Draw(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :685 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        // AS-IS :688-694 pump attacks.
        while (attackProcess.ActiveAttack())
        {
            await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        }
        // AS-IS :696 turn-end check — P2b seam.
        //   await autoProcessing.EndTurnCheck();   // P2b
    }

    /// <summary>AS-IS <c>BreedingPhase()</c> (:701-837): hatch / move (dispatch-action seam), attack pump.</summary>
    public async Task BreedingPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);
        AttackProcess attackProcess = AttackProcess.For(_context);
        GameContext.phase currentPhase = gameContext.TurnPhase;

        // AS-IS :704 turn-end check — P2b seam.
        //   await autoProcessing.EndTurnCheck();   // P2b
        if (currentPhase == GameContext.phase.End)
        {
            return;   // AS-IS :706-709
        }

        currentPhase = GameContext.phase.Breeding;   // AS-IS :715 (S2-cursor)
        IsSelecting = false;                          // AS-IS :717

        // AS-IS :719-816 breeding decision block. `Player.CanHatch` / `Player.CanMove` are not on the mirror
        //   Player yet (P1-junction: breeding-eligibility predicates). The interactive hatch/move itself is
        //   externalized to the established DISPATCH-ACTION seam (design "기존 디스패치 액션 seam 위임"): AS-IS
        //   :804 `HatchDigiEggClass.Hatch()` → ZoneMover.HatchDigitamaAsync, AS-IS :810
        //   `CardObjectController.MovePermanent(...)` → ZoneMover.MoveBreedingToBattleAsync, dispatched as
        //   HatchDigitama / MoveBreedingToBattle HeadlessActions (choice-pause replaces the AS-IS
        //   HasPlayerSelection poll at :788). ShowPhase (:721) is UI — stripped.

        // AS-IS :818 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        // AS-IS :821-828 pump attacks.
        while (attackProcess.ActiveAttack())
        {
            await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        }
        // AS-IS :831 turn-end check — P2b seam.
        //   await autoProcessing.EndTurnCheck();   // P2b
        if (currentPhase == GameContext.phase.End)
        {
            return;   // AS-IS :833-836
        }
    }

    /// <summary>AS-IS <c>MainPhase()</c> PUMP ONLY (:935-1351). The card-play / attack / effect DISPATCH
    /// (:969-1253) is the mirror action-queue seam; SetMainPhase UI (:1354-2872) is non-scope.</summary>
    public async Task MainPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);
        AttackProcess attackProcess = AttackProcess.For(_context);
        Player turnPlayer = gameContext.TurnPlayer!;
        GameContext.phase currentPhase = GameContext.phase.Main;   // AS-IS :897

        // AS-IS :905 OnStartMainPhase window + :908 auto-processing check (pre-pump).
        await autoProcessing.StackSkillInfos(null, EffectTiming.OnStartMainPhase).ConfigureAwait(false);
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);

        // AS-IS :910-933 CanSelect().
        bool CanSelect()
        {
            // AS-IS :917 permanent can use an effect / :921 permanent can attack — mirror seams live.
            if (turnPlayer.GetFieldPermanents().Count(permanent => permanent.CanDeclareSkill()) > 0)
                return true;
            if (turnPlayer.GetFieldPermanents().Count(permanent => permanent.CanAttack(null)) > 0)
                return true;
            // AS-IS :913 HandCards.Some(CanPlayFromHandDuringMainPhase) / :925 HandCards.Count(CanDeclareSkill) /
            //   :929 TrashCards.Count(CanDeclareSkill): CardSource.CanPlayFromHandDuringMainPhase and
            //   CardSource.CanDeclareSkill are not yet on the mirror CardSource — P1-junction (play-from-hand /
            //   hand+trash declarable predicates land with the play-cost / declare-skill card surface).
            return false;
        }

        // AS-IS :1292-1349 ResetMainPhaseParameter() (local function, hoisted).
        void ResetMainPhaseParameter()
        {
            // AS-IS :1294-1336 UI (arrows / frames / card outlines / draggables) stripped.
            IsSelecting = false;                        // AS-IS :1338
            PlayCard = null;                            // AS-IS :1340
            TargetFrameID = -1;                         // AS-IS :1341
            JogressEvoRootsFrameIDs = new int[0];       // AS-IS :1342
            BurstTamerFrameID = -1;                     // AS-IS :1343
            AppFusionFrameIDs = new int[0];             // AS-IS :1344
            UseCardEffect = null;                       // AS-IS :1345
            AttackingPermanent = null;                  // AS-IS :1346
            DefendingPermanent = null;                  // AS-IS :1347
            // AS-IS :1348 CardEffectCommons.CardPermanenceMap = new Dictionary<ICardEffect, Permanent>() — the
            //   AS-IS effect→permanent binding map has no mirror analog (the mirror binds effect source via
            //   CardSource / CEntity_EffectController); nothing to reset here. ADAPTATION.
        }

        // AS-IS :936 while (!endGame) pump.
        while (!endGame)
        {
            // AS-IS :939 auto-processing check.
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
            // AS-IS :941-948 pump attacks.
            while (attackProcess.ActiveAttack())
            {
                await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
                await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
            }
            // AS-IS :950 turn-end check — P2b seam: may set currentPhase = End.
            //   await autoProcessing.EndTurnCheck();   // P2b

            ResetMainPhaseParameter();   // AS-IS :953

            if (currentPhase == GameContext.phase.Main)   // AS-IS :956
            {
                if (!CanSelect())                          // AS-IS :958
                {
                    // AS-IS :960 EndTurnProcess — P2b seam (body-mirror forbidden): drives currentPhase off Main.
                    //   await autoProcessing.EndTurnProcess();   // P2b
                }
            }

            if (currentPhase != GameContext.phase.Main)   // AS-IS :964
            {
                break;   // AS-IS :966 goto EndMainPhase
            }

            // AS-IS :969 StartCoroutine(SetMainPhase()) — SetMainPhase (:1354-2872) is pure UI (spot-audit: no
            //   rule mutation, no selection-intent field write) — non-scope.
            // AS-IS :971-1253 selection-wait + AI auto-play + play/attack/effect dispatch = ACTION-QUEUE seam
            //   (DequeueMainPhaseAction().Execute ≈ ProcessAsync; the intent fields PlayCard/UseCardEffect/
            //   AttackingPermanent are set by the pushed HeadlessAction, not polled here). The driver flip (S3)
            //   supplies the action-queue drive; DORMANT here → break rather than spin without a selection source.
            break;
        }

        // AS-IS EndMainPhase: (:1256-1284) command-panel / timer UI stripped.
        ResetMainPhaseParameter();   // AS-IS :1283
        // AS-IS :1287 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>EndPhase()</c> (:3151-3210): end-of-turn bucket reset (P3 re-points to
    /// <see cref="HeadlessEndTurnCleanupFlow"/>).</summary>
    public async Task EndPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);

        // AS-IS :3154 log / :3158-3159 OffCardTarget UI — stripped.

        // AS-IS :3162 phase = End (S2-cursor).
        GameContext.phase currentPhase = GameContext.phase.End;
        _ = currentPhase;

        isFirstPlayerFirstTurn = false;   // AS-IS :3165

        // AS-IS :3168 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);

        // === AS-IS :3170-3201 end-of-turn bucket reset. (P3 RE-POINT — single owner) HeadlessEndTurnCleanupFlow.Cleanup
        //     is the established mirror of this whole reset block, and it is the SAME flow the live turn driver calls at
        //     the turn boundary (MetadataActionProcessor). Delegating here deletes the duplicate bucket-reset body so
        //     there is one owner. Cleanup owns:
        //       * AS-IS :3171 `attackProcess.AttackCount = 0`   -> AttackController.ResetTurnAttackState()
        //       * AS-IS :3177 player.UntilEachTurnEndEffects, :3179 player.UntilCalculateFixedCostEffect,
        //         :3185 permanent.UntilEachTurnEndEffects, :3191 permanent.UntilOwnerTurnEndEffects,
        //         :3194 player.UntilOpponentTurnEndEffects, :3196 player.UntilOwnerTurnEndEffects,
        //         :3200 permanent.UntilOpponentTurnEndEffects   (the ten Until* duration buckets)
        //       * plus the turn-end continuous-effect duration expiry and the card-metadata turn-end key clears.
        //     The ending turn = the live turn-controller state (turnPlayer = the player whose turn is ending).
        new HeadlessEndTurnCleanupFlow().Cleanup(_context, _context.TurnController.Current);

        // AS-IS :3173 CardEffectCommons.CardPermanenceMap reset — no mirror analog (see ResetMainPhaseParameter);
        //   absent in Cleanup too. ADAPTATION.
        // AS-IS :3181 `player.DigivolveCount_ThisTurn = 0` — junction: mirror Player has no DigivolveCount_ThisTurn
        //   member; the substrate PlayerTurnCounterController owns this per-turn counter (the live driver resets it at
        //   the turn boundary), so it is NOT part of Cleanup's bucket reset. P1/P3-junction (dormant: no double-reset).

        // AS-IS :3204-3208 reset per-card use counts. Junction: NOT owned by Cleanup (Cleanup clears the OLD
        //   metadata-key use-count model; the live driver resets the NEW-model per-instance caps at the turn boundary
        //   via CEntity_EffectControllerStore.ResetUseCountsForTurn). Kept direct as the AS-IS-position 1:1 mirror of
        //   :3204-3208 so the EndPhase region stays faithful (dormant: no double-reset, 0 live callers).
        foreach (CardSource cardSource in gameContext.ActiveCardList)
        {
            cardSource.cEntity_EffectController.InitUseCountThisTurn();
        }
    }

    /// <summary>AS-IS <c>EndGame(Player Winner, bool Surrendered, string effectName)</c> (:3302-3360): UI /
    /// Photon / scene / BGM stripped. Core rule state = the <c>endGame</c> flag + the terminal verdict. AS-IS
    /// marks the WINNER (resultObject.ShowResult(Winner)); the mirror terminal model marks the LOSER
    /// (PlayerStatusController.MarkLose), so the loser = <c>winner.Enemy</c> (ADAPTATION).</summary>
    public void EndGame(Player? winner, bool surrendered, string effectName = "")
    {
        _ = surrendered;
        endGame = true;   // AS-IS :3325
        if (winner?.Enemy is Player loser)
        {
            _context.PlayerStatusController.MarkLose(
                loser.PlayerId,
                effectName.Length > 0 ? effectName : "Game over.");
        }
    }

    #endregion

    /// <summary>The per-context instance (AS-IS <c>GManager.instance.turnStateMachine</c>).</summary>
    public static TurnStateMachine For(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new TurnStateMachine(context);
    }
}
