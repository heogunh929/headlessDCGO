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

    // (R4 P2b) AS-IS TurnStateMachine.Passed (TurnStateMachine.cs:3150, `public bool Passed { get; set; } = true;`):
    // the explicit-pass marker the turn-end seam reads — EndTurnCheck (AutoProcessing.cs:637) CLEARS it before a
    // memory-threshold-triggered EndTurnProcess, so the "both passed in Main → gauge jumps to the opponent's 3"
    // arm (:681-694) fires only for an explicit pass (PassTurn :3364 calls EndTurnProcess with Passed still true);
    // EndTurnProcess re-arms it (:696). Match-scoped box for the same reason as `isExecuting` (the mirror
    // TurnStateMachine is a per-access view; a per-instance field would not survive two `GManager.instance` reads).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EngineContext, System.Runtime.CompilerServices.StrongBox<bool>> _passedStore = new();

    public bool Passed
    {
        get => _passedStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(true)).Value;
        set => _passedStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(true)).Value = value;
    }

    #region (R4 P2a→S3a) Turn-flow phase bodies — AS-IS-region mirror (pump-driven when TurnFlowPump is installed)

    // (R4 batch P2a, design docs/audit/r4_tsm_s1_design_2026-07-16.md + r4_tsm_investigation_2026-07-16.md)
    // AS-IS-region 1:1 mirror of the TurnStateMachine phase bodies, assembled DORMANT: the live turn drivers
    // (MetadataActionProcessor / HeadlessGameLoop / HeadlessEarlyPhaseFlow / HeadlessMainPhaseFlow) are UNTOUCHED
    // and NOTHING calls these six methods (grep-verified 0 callers) — the driver flip is S3. Established
    // adaptations applied throughout: coroutine → async Task, `GManager.instance.X` → EngineContext services /
    // `.For(_context)` singletons, Unity/Photon/UI (commandText / selectCardPanel / ShowPhase / outlines / SE /
    // FirstObject / draggables) stripped, `WaitUntil`/`WaitWhile` interactive stops delegated to the established
    // choice-pause + action-queue seams.
    //
    // The two P2a deferred seams are now RESOLVED (both were call-surface-only scaffolds):
    //   * S2-cursor (decision 2 = option A) — RESOLVED at P2b: S2 landed the 6-value HeadlessPhase + TurnStepCursor
    //     substrate, and GameContext.TurnPhase now has the AS-IS mutable-field SETTER (delegating to
    //     TurnController.SetPhase), so the bodies read/write `gameContext.TurnPhase` directly, exactly as AS-IS —
    //     the per-method `currentPhase` locals are gone. Cross-body phase state is the real substrate turn state.
    //   * P2b turn-end seam — RESOLVED at P2b: `AutoProcessing.EndTurnCheck / TurnEndMinMemory / EndTurnProcess`
    //     are mirrored (AutoProcessing.cs, AS-IS :630-727) and the phase bodies call them at the AS-IS positions.
    //
    // (R4 S3a, decision 3 = B) The bodies are now driven by the INJECTABLE TurnFlowPump (Headless/Runtime/
    // TurnFlowPump.cs — the AS-IS continuous driver chain: StartGame → {Active→Draw→Breeding→Main→End→flip}),
    // installed per match via TurnFlowPumpHost.Install. The DEFAULT (OLD) driver is untouched until the S3c
    // cutover approval — a match without Install never reaches these bodies.

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

        // AS-IS :554 gameContext.TurnPhase = Active (real write via the P2b TurnPhase setter).
        gameContext.TurnPhase = GameContext.phase.Active;

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

        // AS-IS :579 turn-end check (P2b live seam — may drive TurnPhase to End).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
        if (gameContext.TurnPhase == GameContext.phase.End)
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
        // AS-IS :641 turn-end check (no guard after — AS-IS falls through to the resets regardless).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);

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

        // AS-IS :655 turn-end check (P2b live seam).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
        if (gameContext.TurnPhase == GameContext.phase.End)
        {
            return;   // AS-IS :657-660
        }

        gameContext.TurnPhase = GameContext.phase.Draw;   // AS-IS :666 (real write)

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
        // AS-IS :696 turn-end check (no guard after — AS-IS ends the phase body regardless).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>BreedingPhase()</c> (:701-837): hatch / move (dispatch-action seam), attack pump.</summary>
    public async Task BreedingPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);
        AttackProcess attackProcess = AttackProcess.For(_context);

        // AS-IS :704 turn-end check (P2b live seam).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
        if (gameContext.TurnPhase == GameContext.phase.End)
        {
            return;   // AS-IS :706-709
        }

        gameContext.TurnPhase = GameContext.phase.Breeding;   // AS-IS :715 (real write)
        IsSelecting = false;                                   // AS-IS :717

        // AS-IS :719-816 breeding decision block (R4 S3a live seam). UI stripped: :721-723 ShowPhase wait,
        //   :727-767 hatch-object/outline/commandText/autoHatch/AI-probability branches. The DECISION is the
        //   AS-IS bool ValueSelection (SendShouldHatch semantics) — externalized as a
        //   ChoiceType.BreedingDecision choice-pause replacing the :788 WaitWhile(HasPlayerSelection) poll,
        //   parked on the TurnFlowPump gate and answered via the agent's ResolveChoice action (deposit seam).
        //   Only reachable pump-driven: a dormant direct call sees no pump host and skips the block (the P2a
        //   dormant contract), and AS-IS gates the whole block on CanHatch||CanMove anyway.
        Player breedingPlayer = gameContext.TurnPlayer!;
        if ((breedingPlayer.CanHatch || breedingPlayer.CanMove)
            && Headless.Runtime.TurnFlowPumpHost.Find(_context) is { } breedingPump)
        {
            _context.ChoiceController.RequestChoice(
                new Headless.Choices.ChoiceRequest(
                    Headless.Choices.ChoiceType.BreedingDecision,
                    breedingPlayer.PlayerId,
                    breedingPlayer.CanHatch
                        ? "BreedingPhase : Will you hatch Digiegg?"
                        : "BreedingPhase : Will you move your Digimon to Battle Area?",
                    minCount: 0,
                    maxCount: 1,
                    canSkip: true,
                    Headless.Choices.ChoiceZone.Custom,
                    new[]
                    {
                        new Headless.Choices.ChoiceCandidate(
                            new Headless.Services.HeadlessEntityId("breeding:act"),
                            breedingPlayer.CanHatch ? "hatch" : "move",
                            Headless.Choices.ChoiceZone.Custom,
                            IsSelectable: true,
                            ownerId: breedingPlayer.PlayerId),
                    }),
                new Headless.Services.HeadlessEntityId("breeding:decision"));
            breedingPump.MarkPumpChoice();
            await breedingPump.Gate.WaitUntilAsync(() => breedingPump.HasDepositedAnswer).ConfigureAwait(false);
            Headless.Choices.ChoiceResult breedSelection = breedingPump.TakeDepositedAnswer();
            // AS-IS :789-790 DequeuePlayerSelection<ValueSelection>().ValueAsBool().
            bool doAction_BreedingPhase = !breedSelection.IsSkipped && breedSelection.SelectedIds.Count > 0;

            // AS-IS :792-794 hideCannotSelectObject/OffHatchObject/OffFieldCardTarget — UI stripped.
            IsSelecting = true;   // AS-IS :797

            if (gameContext.TurnPhase == GameContext.phase.Breeding)   // AS-IS :799
            {
                if (doAction_BreedingPhase)
                {
                    // AS-IS :802-812 — hatch WINS when both are possible (:804 `CanHatch || !CanMove`, quirk
                    // preserved). :804 HatchDigiEggClass.Hatch() → ZoneMover.HatchDigitamaAsync and :810
                    // CardObjectController.MovePermanent(breeding[0]) → ZoneMover.MoveBreedingToBattleAsync are
                    // the established substrate seats (P2a mapping).
                    if (breedingPlayer.CanHatch || !breedingPlayer.CanMove)
                    {
                        await _context.ZoneMover
                            .HatchDigitamaAsync(breedingPlayer.PlayerId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else if (!breedingPlayer.CanHatch || breedingPlayer.CanMove)
                    {
                        await _context.ZoneMover
                            .MoveBreedingToBattleAsync(breedingPlayer.PlayerId, count: 1, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            gameContext.TurnPhase = GameContext.phase.Breeding;   // AS-IS :816
        }

        // AS-IS :818 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        // AS-IS :821-828 pump attacks.
        while (attackProcess.ActiveAttack())
        {
            await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
            await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
        }
        // AS-IS :831 turn-end check (P2b live seam).
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
        if (gameContext.TurnPhase == GameContext.phase.End)
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

        // (P2-1, review-1) AS-IS :880 ENTRY turn-end check + :882-885 goto guard: the breeding-phase body may have
        // driven memory past the threshold, so MainPhase re-checks BEFORE opening the OnStartMainPhase window (:905)
        // — without this guard a turn that ended during Breeding would still fire the main-phase window.
        await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);
        if (gameContext.TurnPhase == GameContext.phase.End)
        {
            goto EndMainPhase;   // AS-IS :882-885
        }

        // AS-IS :888-895 log + hand/field selection-status reset (OffHandCardTarget/OffFieldCardTarget) — UI stripped.

        gameContext.TurnPhase = GameContext.phase.Main;   // AS-IS :897 (real write)

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
            // AS-IS :950 turn-end check (P2b live seam — may drive TurnPhase to End).
            await autoProcessing.EndTurnCheck(cancellationToken).ConfigureAwait(false);

            ResetMainPhaseParameter();   // AS-IS :953

            if (gameContext.TurnPhase == GameContext.phase.Main)   // AS-IS :956
            {
                if (!CanSelect())                                   // AS-IS :958
                {
                    // AS-IS :960 no-selectable-action auto turn-end (P2b live seam).
                    await autoProcessing.EndTurnProcess(cancellationToken).ConfigureAwait(false);
                }
            }

            if (gameContext.TurnPhase != GameContext.phase.Main)   // AS-IS :964
            {
                goto EndMainPhase;   // AS-IS :966
            }

            // AS-IS :969 StartCoroutine(SetMainPhase()) — SetMainPhase (:1354-2872) is pure UI (spot-audit: no
            //   rule mutation, no selection-intent field write) — non-scope.
            // AS-IS :971-1253 selection-wait + AI auto-play + play/attack/effect dispatch = ACTION-QUEUE seam
            //   (DequeueMainPhaseAction().Execute ≈ ProcessAsync; the intent fields PlayCard/UseCardEffect/
            //   AttackingPermanent are set by the pushed HeadlessAction, not polled here). The dispatch region's
            //   OWN EndTurnProcess call sites (:1149 pass-command / :1158 auto-pass) ride the same seam — the S3
            //   driver routes the externalized Pass action to AutoProcessing.EndTurnProcess.
            // (R4 S3a) Pump-driven, the :971 selection WAIT parks the pump here; the action DISPATCH body is the
            //   S3b batch (design item RD-S3B-01) — until it lands the park condition is never satisfied, so an
            //   S3a pump match intentionally rests at main entry. A dormant direct call (no pump host) keeps the
            //   P2a break contract.
            if (Headless.Runtime.TurnFlowPumpHost.Find(_context) is { } mainPump)
            {
                await mainPump.Gate.WaitUntilAsync(static () => false).ConfigureAwait(false);
            }

            break;
        }

    // AS-IS EndMainPhase label (:1256; the :1258-1284 command-panel / timer UI under it is stripped).
    EndMainPhase:
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

        // AS-IS :3162 gameContext.TurnPhase = End (real write — idempotent when EndTurnProcess already set it).
        gameContext.TurnPhase = GameContext.phase.End;

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
