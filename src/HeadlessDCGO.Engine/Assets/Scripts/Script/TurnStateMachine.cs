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
    // AS-IS-region 1:1 mirror of the TurnStateMachine phase bodies. (4b B6: the OLD step drivers —
    // MetadataActionProcessor AdvancePhase/EndTurn bodies, HeadlessEarlyPhaseFlow, HeadlessMainPhaseFlow —
    // are physically retired; these bodies are the LIVE turn cadence under the TurnFlowPump.) Established
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

    /// <summary>(R4 S3b) The pump translation of the AS-IS IN-COROUTINE selection wait inside the attack
    /// machinery (block window / deletion-replacement / security-skill choices — the original suspends the
    /// attack-stage coroutine until the player answers). The attack stages open a plain ChoiceController
    /// request and park the attack phase (e.g. AttackPhase.Blocking); pump-driven, the body's attack pump must
    /// idle at the choice-pause seam until the agent's ResolveChoice applies the answer (BlockTiming/
    /// DeletionReplacement routing in MetadataActionProcessor), then advance the stage. Non-pump callers no-op
    /// (the OLD driver pauses stepping on a pending choice at the RunToStable boundary instead).</summary>
    internal static async Task WaitPendingChoiceUnderPump(EngineContext context)
    {
        if (Headless.Runtime.TurnFlowPumpHost.Find(context) is { } pumpHost)
        {
            await pumpHost.Gate.WaitUntilAsync(() => !context.ChoiceController.Current.IsPending)
                .ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TurnStateMachine.Init()</c> (:34-297) — the pre-game setup coroutine that runs BEFORE the
    /// game-state machine (:291 <c>StartCoroutine(GameStateMachine())</c> → :303 <c>StartGame()</c>). Nearly all of
    /// AS-IS Init is platform/UI: Photon room join + player-name plumbing, the loading screen, memory-gauge and
    /// brain-storm object Init, BGM. The two RULE-relevant steps are re-housed here 1:1 —
    /// 「デッキカード生成」 (:233) and 「先攻・後攻の決定」 (:255-283) — so the whole opening runs as ONE AS-IS path:
    /// Init → CreatePlayerDecks → (GameStateMachine) StartGame → deal → mulligan → security.
    /// AS-IS :221-228 「乱数列初期化」 (the master client RPCs a shared seed to both clients) is the substrate
    /// random-seed apply, owned by the match host before this point.</summary>
    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.TryGetService(out Headless.Runtime.MatchConfig? matchConfig)
            || matchConfig?.Setup is not { } setup)
        {
            // No platform deck data (a hand-seeded scenario / unit fixture placed its cards directly): AS-IS has
            // no such mode — the Photon path always carries a deck — so there is nothing to create here.
            return;
        }

        // AS-IS :232-234 「デッキカード生成」.
        await CardObjectController
            .CreatePlayerDecks(_context, setup, matchConfig.PlayerIds, cancellationToken)
            .ConfigureAwait(false);

        // AS-IS :253-285 「先攻・後攻の決定」. :256 picks the turn player at random over the seats
        //   (`gameContext.TurnPlayer = gameContext.PlayerFromID(GameRandom.Range(0, 2))`), then :262-283 OVERRIDE it
        //   from the room custom property `DataBase.FirstPlayerKey` when the platform named a first player — the
        //   override resolves to `PlayerFromID(playerID).Enemy`, i.e. the named player's OPPONENT becomes the
        //   pre-switch turn player, so that the :306 SwitchTurnPlayer hands the FIRST turn to the named player.
        //   Substrate coordinate: the mirror turn controller is initialized in POST-switch coordinates (the pump
        //   enters ActivePhase directly, with no AS-IS :306 pre-switch), so it is seeded with the FIRST player
        //   itself rather than the pre-switch opposite. GameRandom.Range ≡ the seeded RandomSource.NextInt.
        //   The COORDINATE TRANSLATION therefore has to be applied to the drawn VALUE, not only to where it is
        //   stored: the draw names the PRE-switch turn player, and AS-IS :306 `gameContext.SwitchTurnPlayer()`
        //   (GameContext.cs:166-181 — "the first seat in `Players` that is not the current TurnPlayer") turns it
        //   into the seat that actually takes turn 1. That AS-IS switch loop is replayed verbatim below over the
        //   seat order (`matchConfig.PlayerIds` ≡ AS-IS `gameContext.Players` = [PlayerFromID(0), PlayerFromID(1)],
        //   GameContext.cs:41-52 — the turn controller keeps this order as PlayerOrder). One NextInt draw, at the
        //   same point in the stream as AS-IS :256.
        Headless.Services.HeadlessPlayerId preSwitchTurnPlayerId = matchConfig.PlayerIds[
            _context.RandomSource.NextInt(0, matchConfig.PlayerIds.Count)];

        Headless.Services.HeadlessPlayerId firstPlayerId = preSwitchTurnPlayerId;
        foreach (Headless.Services.HeadlessPlayerId seat in matchConfig.PlayerIds)
        {
            if (seat != preSwitchTurnPlayerId)
            {
                firstPlayerId = seat;
                break;
            }
        }

        // AS-IS :273-283 the NAMED-first-player override assigns the pre-switch turn player as
        //   `PlayerFromID(playerID).Enemy`, so the :306 switch hands turn 1 to the named player itself — which is
        //   exactly what the mirror stores here (POST-switch coordinates). Already in agreement; left alone.
        if (setup.FirstPlayerId is { } namedFirstPlayer)
        {
            firstPlayerId = namedFirstPlayer;
        }

        _context.TurnController.Initialize(matchConfig.PlayerIds, firstPlayerId);
    }

    /// <summary>AS-IS <c>TurnStateMachine._isRedraw</c> (:340): the per-player mulligan decision the AS-IS
    /// <c>SetRedraw</c> PunRPC writes and the <c>StartGame</c> body reads back at :448/:455. The RPC round-trip
    /// collapses into the inline <c>ChooseAsync</c> below (same collapse as the breeding-decision seam), so the
    /// field never escapes a single <see cref="StartGameAsync"/> call — a plain instance field reproduces :340
    /// verbatim without needing the isExecuting-style cross-view box.</summary>
    private bool _isRedraw = false;

    /// <summary>AS-IS <c>StartGame()</c> (:341-504): initial hands, mulligan, security.</summary>
    public async Task StartGameAsync(CancellationToken cancellationToken = default)
    {
        // AS-IS :347-367 first/second determination + :358 `gameContext.FirstPlayer = gameContext.NonTurnPlayer`:
        //   commandText UI only; the first-player seat is turn-order state owned by the substrate turn-controller
        //   (mirror GameContext.FirstPlayer has no setter). P1/S2-junction: first-player seat on HeadlessTurnState.

        // ==== COORDINATE TRANSLATION: AS-IS `Players_ForNonTurnPlayer` here ≡ mirror `Players_ForTurnPlayer` ====
        //   AS-IS runs StartGame in PRE-SWITCH turn coordinates: Init (:262-283) seeds gameContext.TurnPlayer with
        //   the first player's OPPONENT, :358 therefore reads `FirstPlayer = NonTurnPlayer`, and GameStateMachine
        //   (:306) does SwitchTurnPlayer at the TOP of every loop iteration — including the first — so the FIRST
        //   player opens the game. AS-IS's `Players_ForNonTurnPlayer` at :369/:375/:497 thus enumerates
        //   [FIRST, SECOND]. The mirror pump has no :306 pre-switch (it enters ActivePhaseAsync directly and flips
        //   at the turn boundary instead), so InitAsync seeds the turn controller with the FIRST player itself —
        //   POST-switch coordinates — and the same [FIRST, SECOND] list is `Players_ForTurnPlayer`. Same order as
        //   AS-IS, expressed in the mirror's coordinate; the sibling translation the memory gauge documents at the
        //   pump's turn boundary. (Reading Players_ForNonTurnPlayer here would deal to the SECOND player first.)

        // AS-IS :369-372 draw 5, first player first.
        foreach (Player player in gameContext.Players_ForTurnPlayer)
        {
            await new DrawClass(_context, player.PlayerId, 5, null).Draw(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :374-494 マリガン — INLINE, per player, FIRST player first (:375 walks the same list as the deal
        //   above). The externalised MulliganCoordinator (Begin + per-decision ResolveAsync driven from the action
        //   processor, which ALSO owned the deferred security deal) is RETIRED: AS-IS runs the whole sequence on
        //   ONE coroutine stack, and the mirror does the same on the pump stack — each keep/redraw parks the pump
        //   in place and the body resumes where it stopped.
        foreach (Player player in gameContext.Players_ForTurnPlayer)
        {
            _isRedraw = false;   // AS-IS :377

            // AS-IS :379-444 the DECISION. Four AS-IS branches — the opponent's "is selecting" commandText
            //   (:381), the local auto/AI coin flip (:386-389 RandomUtility.IsSucceedProbability(0.5f)), the
            //   local human selectCardPanel (:393-425) and the remote AI's level-3 heuristic (:432-442
            //   `HandCards.Count(IsDigimon && Level == 3) == 0`) — are all seat-local UI/AI POLICY choosing the
            //   same boolean. Headless every seat is an agent, so they collapse to ONE inline choice, exactly as
            //   the breeding-decision and MultipleSkills order picks do: the panel is a pure yes/no (:412
            //   `_CanTargetCondition: (cardSource) => false` makes every hand card unselectable, so nothing is
            //   ever picked from RootCardSources), "Mulligan" (:410 EndSelect → SetRedraw true) = select the
            //   redraw candidate, "Keep Hand" (:409 NotSelect → SetRedraw false) = skip.
            string message = "Will you mulligan your hand?";   // AS-IS :393
            // AS-IS :395 `player == gameContext.NonTurnPlayer` ⇒ FIRST. Same COORDINATE TRANSLATION as the two
            //   Players_ForNonTurnPlayer→Players_ForTurnPlayer loops above: AS-IS StartGame runs PRE-switch, so its
            //   NonTurnPlayer IS the first player (:358 `gameContext.FirstPlayer = gameContext.NonTurnPlayer`);
            //   the mirror runs POST-switch, so the first player is `gameContext.TurnPlayer`. Same string for the
            //   same seat as AS-IS.
            message += player == gameContext.TurnPlayer
                ? "\n(You are FIRST)"     // AS-IS :397 (color markup = UI, stripped)
                : "\n(You are SECOND)";   // AS-IS :402

            Headless.Choices.ChoiceResult mulliganSelection = await _context.ChoiceProvider.ChooseAsync(
                new Headless.Choices.ChoiceRequest(
                    Headless.Choices.ChoiceType.Mulligan,
                    player.PlayerId,
                    message,
                    minCount: 0,
                    maxCount: 1,
                    canSkip: true,               // AS-IS :417 `_CanNoSelect: () => true`
                    Headless.Choices.ChoiceZone.Hand,
                    new[]
                    {
                        new Headless.Choices.ChoiceCandidate(
                            new Headless.Services.HeadlessEntityId("mulligan:redraw"),
                            "Mulligan (redraw opening hand)",
                            Headless.Choices.ChoiceZone.Hand,
                            IsSelectable: true,
                            ownerId: player.PlayerId),
                    }),
                cancellationToken).ConfigureAwait(false);

            // AS-IS :446-448 WaitUntil(HasPlayerSelection) + DequeuePlayerSelection<ValueSelection>().ValueAsBool()
            //   (null selection → false). Same read as the breeding seam's :789-790 translation.
            _isRedraw = !mulliganSelection.IsSkipped && mulliganSelection.SelectedIds.Count > 0;

            // AS-IS :450-453 CloseSelectCardPanel / CloseCommandText = UI (stripped).

            if (_isRedraw)   // AS-IS :455
            {
                // AS-IS :459-471 destroy the hand HandCard GameObjects = UI (stripped).

                // AS-IS :474 手札のカードを山札の下に加える — the hand goes to the deck BOTTOM. Taken over a
                //   CLONE (AS-IS `player.HandCards.Clone()`) because the move mutates the hand zone underneath.
                //   notAddLog:true is AS-IS's own argument (:474); the :476-479 PlayLog append = UI (stripped).
                await CardObjectController
                    .AddLibraryBottomCards(player.HandCards.Clone(), true, cancellationToken)
                    .ConfigureAwait(false);

                // AS-IS :484 シャッフル.
                await CardObjectController.Shuffle(player, cancellationToken).ConfigureAwait(false);

                // AS-IS :488 5枚引き直す.
                await new DrawClass(_context, player.PlayerId, 5, null).Draw(cancellationToken).ConfigureAwait(false);
            }
        }

        // AS-IS :496-501 set up security — 5 each, AFTER every player's mulligan decision, from the
        //   post-mulligan deck, in the same first-player-first order.
        foreach (Player player in gameContext.Players_ForTurnPlayer)
        {
            await new IAddSecurityFromLibrary(_context, player.PlayerId, 5)
                .AddSecurity(cancellationToken)
                .ConfigureAwait(false);
        }

        // AS-IS :503 `DoneStartGame = true`. Mirror DoneStartGame is a computed getter (phase past None), so the
        //   AS-IS mutable set-point needs no write here: by the time this returns, the whole opening sequence
        //   has run on this stack and the pump proceeds straight into the turn loop (AS-IS :303-306).
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
            await WaitPendingChoiceUnderPump(_context).ConfigureAwait(false);
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
            await WaitPendingChoiceUnderPump(_context).ConfigureAwait(false);
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
            await WaitPendingChoiceUnderPump(_context).ConfigureAwait(false);
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
            await WaitPendingChoiceUnderPump(_context).ConfigureAwait(false);
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

        // AS-IS :910-933 CanSelect() — complete (R4 S3b: the three P1-junction card predicates landed).
        bool CanSelect()
        {
            // You can play cards from your hand. (:913)
            if (gameContext.TurnPlayer!.HandCards.Some((_card) => _card.CanPlayFromHandDuringMainPhase))
                return true;

            // There is a permanent in play that can use the effect. (:917)
            if (gameContext.TurnPlayer!.GetFieldPermanents().Count(permanent => permanent.CanDeclareSkill()) > 0)
                return true;

            // There is a permanent in play that can attack. (:921)
            if (gameContext.TurnPlayer!.GetFieldPermanents().Count(permanent => permanent.CanAttack(null)) > 0)
                return true;

            // I have a card in my hand that can use an effect. (:925)
            if (gameContext.TurnPlayer!.HandCards.Count((_card) => _card.CanDeclareSkill) > 0)
                return true;

            // I have a card in my trash that can use an effect. (:929)
            if (gameContext.TurnPlayer!.TrashCards.Count(_card => _card.CanDeclareSkill) > 0)
                return true;

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
                await WaitPendingChoiceUnderPump(_context).ConfigureAwait(false);
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

            // AS-IS :971-1170 selection wait (R4 S3b live seam). The `yield return null` idle frame is the pump
            //   park (TurnFlowGate); the AS-IS producer (UI click / network packet → Player.QueueMainPhaseAction)
            //   is the TurnFlowDriver's LegalAction conversion. The AS-IS AI auto-play branch (:989-1160,
            //   `IsAI && !isYou` RandomUtility bot) and the auto-pilot arm (:1155-1160 `isYou && isAuto && IsAI`
            //   → EndTurnProcess) are the ORIGINAL's agent substitutes — the headless AGENT replaces them
            //   (stripped; the agent passes explicitly via the mirror PassAction → PassTurn → EndTurnProcess,
            //   the same :1149/:1158 EndTurnProcess destination). A dormant direct call (no pump host) keeps the
            //   P2a break contract.
            while (PlayCard == null && UseCardEffect == null && AttackingPermanent == null)
            {
                if (Headless.Runtime.TurnFlowPumpHost.Find(_context) is not { } mainPump)
                {
                    goto EndMainPhase;   // dormant direct call: no selection source (P2a contract).
                }

                // AS-IS :973 yield return null — park until the driver queues an action.
                await mainPump.Gate.WaitUntilAsync(() => gameContext.TurnPlayer!.HasMainPhaseAction() || endGame)
                    .ConfigureAwait(false);

                if (endGame)
                {
                    return;   // AS-IS :976-979 yield break
                }

                // AS-IS :983-986 (the non-AI arm — the only agent-facing path).
                if (gameContext.TurnPlayer!.HasMainPhaseAction())
                {
                    MainPhaseAction? queued = gameContext.TurnPlayer!.DequeueMainPhaseAction();
                    if (queued != null)
                    {
                        await queued.Execute(this).ConfigureAwait(false);
                    }
                }

                // AS-IS :1164-1168 (a Pass ran EndTurnProcess inside Execute and drove the phase off Main).
                if (gameContext.TurnPhase != GameContext.phase.Main)
                {
                    goto EndMainPhase;
                }
            }

            // AS-IS :1172 ResetUI — stripped.

            // AS-IS :1176-1196 use activation effect.
            if (UseCardEffect != null)
            {
                if (UseCardEffect is ActivateICardEffect)
                {
                    if (UseCardEffect.CanUse(null))
                    {
                        UseCardEffect.SetIsDeclarative(true);

                        // Count up the number of uses (:1184-1187).
                        if (UseCardEffect.MaxCountPerTurn < 100)
                        {
                            UseCardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(UseCardEffect);
                        }

                        await autoProcessing.ActivateEffectProcess(UseCardEffect, null).ConfigureAwait(false);
                    }
                }
            }

            // AS-IS :1198-1244 play cards.
            else if (PlayCard != null)
            {
                // AS-IS :1200-1202 DeleteHandCardEffectCoroutine / ShowUseHandCardEffect_PlayCard — UI stripped.

                Permanent? targetPermanent = null;

                // AS-IS :1206-1209 fieldCardFrames[TargetFrameID].GetFramePermanent() — FRAME ADAPTATION (no
                // frame/slot model, RD-P6C1-2 family): TargetFrameID indexes the turn player's field-permanent
                // LIST (the driver produces it from the target-permanent action param); -1 = empty-frame play.
                if (0 <= TargetFrameID && TargetFrameID <= gameContext.TurnPlayer!.GetFieldPermanents().Count - 1)
                {
                    targetPermanent = gameContext.TurnPlayer!.GetFieldPermanents()[TargetFrameID];
                }

                // AS-IS :1212 PlayLog — stripped.

                PlayCardClass playCard = new PlayCardClass(
                    cardSources: new List<CardSource>() { PlayCard },
                    hashtable: null,
                    payCost: true,
                    targetPermanent: targetPermanent,
                    isTapped: false,
                    root: SelectCardEffect.Root.Hand,
                    activateETB: true);

                if (JogressEvoRootsFrameIDs != null)
                {
                    if (JogressEvoRootsFrameIDs.Length == 2)
                    {
                        playCard.SetJogress(JogressEvoRootsFrameIDs);
                    }
                }

                // AS-IS :1234-1237 burst frame guard (fieldCardFrames bound) — frame-less: non-negative id only
                // (SetBurst itself STOPs on the frame model, RD-P6C1-1).
                if (BurstTamerFrameID >= 0)
                {
                    playCard.SetBurst(BurstTamerFrameID, PlayCard);
                }

                if (AppFusionFrameIDs != null && AppFusionFrameIDs.Length == 2)
                {
                    playCard.SetAppFusion(AppFusionFrameIDs);
                }

                await playCard.PlayCard().ConfigureAwait(false);
            }

            // AS-IS :1246-1250 attack.
            else if (AttackingPermanent != null)
            {
                // AS-IS :1248 attackProcess.Attack(AttackingPermanent, DefendingPermanent, null) — the mirror
                // AS-IS-shaped overload (controller DeclareAttack + the Attack :73-253 sequence). (DECLARATION
                // re-migration) the substrate AttackDeclarationCommons.Declare wrapper is retired.
                await AttackProcess.For(_context).Attack(
                    turnPlayer.PlayerId,
                    AttackingPermanent.InstanceId,
                    gameContext.NonTurnPlayer!.PlayerId,
                    DefendingPermanent?.InstanceId,
                    isDirectAttack: DefendingPermanent == null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

    // AS-IS EndMainPhase label (:1256; the :1258-1284 command-panel / timer UI under it is stripped).
    EndMainPhase:
        ResetMainPhaseParameter();   // AS-IS :1283
        // AS-IS :1287 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>EndPhase()</c> (:3151-3210): the end-of-turn duration-bucket reset, inline (the
    /// externalised cleanup flow that used to own :3170-3201 is retired).</summary>
    public async Task EndPhaseAsync(CancellationToken cancellationToken = default)
    {
        AutoProcessing autoProcessing = AutoProcessing.For(_context);

        // AS-IS :3154 log / :3158-3159 OffCardTarget UI — stripped.

        // AS-IS :3162 gameContext.TurnPhase = End (real write — idempotent when EndTurnProcess already set it).
        gameContext.TurnPhase = GameContext.phase.End;

        isFirstPlayerFirstTurn = false;   // AS-IS :3165

        // AS-IS :3168 auto-processing check.
        await autoProcessing.AutoProcessCheck(cancellationToken).ConfigureAwait(false);

        // === AS-IS :3170-3201 "Reset status until end of turn" — INLINE, in AS-IS statement order.
        //     (HeadlessEndTurnCleanupFlow retirement) The externalised cleanup flow that used to own this block is
        //     RETIRED and its AS-IS-backed body is re-migrated here, its AS-IS home. The flow's two SUBSTRATE-ONLY
        //     halves are deliberately NOT carried over:
        //       (a) the card-metadata turn-end key clears ("untilEachTurnEndEffects" / "untilEndTurnEffects" /
        //           "temporaryPower" / "useCountThisTurn" / … ) — the OLD metadata duration model, whose writers are
        //           0 since the Player/Permanent bucket stores (PlayerEffectListStore / PermanentEffectListStore)
        //           became the carriers; the bucket resets below ARE the AS-IS expiry.
        //       (b) the AddSelfDeleteEffect / burst turn-end marker PROMOTION (…AtTurnEnd -> …AtTurnEndDue), whose
        //           consumer — the substrate GameFlowProcessor turn-end deletion sweep — no longer exists. AS-IS
        //           EndPhase has no such promotion. RESOLVED (design item RD-EOT-SELFDELETE, GAMEFLOW re-migration):
        //           CardEffectCommons.AddSelfDeleteEffect no longer writes a marker at all — it now carries the AS-IS
        //           body (two permanent.PermanentEffects producers: OnEndTurn ⇒ PermanentEffectFactory.DeleteSelfEffect,
        //           None ⇒ AddDetailClass), so the self-delete fires through the AS-IS OnEndTurn window.
        //           The BURST turn-end marker half remains an open design item (RD-EOT-BURSTTRASH) — its writer is
        //           the burst digivolution path, whose AS-IS carrier is a permanent.UntilEachTurnEndEffects effect
        //           (AS-IS SelectBurstDigivolutionEffect.cs:249-344).

        // AS-IS :3171 `GManager.instance.attackProcess.AttackCount = 0`. The mirror AttackProcess.AttackCount is a
        //   read-through of the substrate counter (AttackProcess.cs:100), so the write is the controller reset.
        _context.AttackController.ResetTurnAttackState();

        // AS-IS :3173 CardEffectCommons.CardPermanenceMap reset — no mirror analog (see ResetMainPhaseParameter).
        //   ADAPTATION.

        foreach (Player player in gameContext.Players)   // AS-IS :3175
        {
            player.UntilEachTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();         // AS-IS :3177

            player.UntilCalculateFixedCostEffect = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3179

            // AS-IS :3181 `player.DigivolveCount_ThisTurn = 0` — junction: mirror Player has no
            //   DigivolveCount_ThisTurn member; the substrate PlayerTurnCounterController owns this per-turn counter
            //   (the live driver resets it at the turn boundary). P1/P3-junction (dormant: no double-reset).

            foreach (Permanent permanent in player.GetFieldPermanents())   // AS-IS :3183
            {
                permanent.UntilEachTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3185
            }
        }

        foreach (Permanent permanent in gameContext.TurnPlayer!.GetFieldPermanents())   // AS-IS :3189
        {
            permanent.UntilOwnerTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3191
        }

        gameContext.NonTurnPlayer!.UntilOpponentTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3194

        gameContext.TurnPlayer!.UntilOwnerTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3196

        foreach (Permanent permanent in gameContext.NonTurnPlayer!.GetFieldPermanents())   // AS-IS :3198
        {
            permanent.UntilOpponentTurnEndEffects = new List<Func<EffectTiming, ICardEffect>>();   // AS-IS :3200
        }

        // AS-IS :3204-3208 reset per-card use counts. Junction: NOT owned by Cleanup (Cleanup clears the OLD
        //   metadata-key use-count model; the live driver resets the NEW-model per-instance caps at the turn boundary
        //   via CEntity_EffectControllerStore.ResetUseCountsForTurn). Kept direct as the AS-IS-position 1:1 mirror of
        //   :3204-3208 so the EndPhase region stays faithful (dormant: no double-reset, 0 live callers).
        foreach (CardSource cardSource in gameContext.ActiveCardList)
        {
            cardSource.cEntity_EffectController.InitUseCountThisTurn();
        }
    }

    // ==== (R4 S3b) AS-IS main-phase intent setters (:3050-3148) + PassTurn (:3364-3372) — the MainPhaseAction
    // Execute targets. Index currencies are the AS-IS list indexes (GetFieldPermanents / ActiveCardList); the
    // one FRAME currency (SetPlayCard's TargetFrameID) is adapted to the field-permanent list index (no
    // frame/slot model — RD-P6C1-2 family, see the play-dispatch arm).

    /// <summary>AS-IS <c>SetActSkill(permanentIndex, skillIndex)</c> (:3050-3065).</summary>
    public void SetActSkill(int permanentIndex, int skillIndex)
    {
        List<Permanent> feild = gameContext.TurnPlayer!.GetFieldPermanents();   // AS-IS variable-name typo kept

        if (permanentIndex < 0 || permanentIndex >= feild.Count)
        {
            return;
        }

        Permanent UseSkillPermanent = feild[permanentIndex];

        if (0 <= skillIndex && skillIndex < UseSkillPermanent.EffectList(EffectTiming.OnDeclaration).Count)
        {
            this.UseCardEffect = UseSkillPermanent.EffectList(EffectTiming.OnDeclaration)[skillIndex];
        }
    }

    /// <summary>AS-IS <c>SetActCardSkill(cardIndex, skillIndex)</c> (:3069-3082).</summary>
    public void SetActCardSkill(int cardIndex, int skillIndex)
    {
        if (cardIndex < 0 || cardIndex >= gameContext.ActiveCardList.Count)
        {
            return;
        }

        CardSource UseSkillCard = gameContext.ActiveCardList[cardIndex];

        if (0 <= skillIndex && skillIndex < UseSkillCard.CanDeclareSkillList.Count)
        {
            this.UseCardEffect = UseSkillCard.CanDeclareSkillList[skillIndex];
        }
    }

    /// <summary>AS-IS <c>SetPlayCard(cardIndex, TargetFrameID, JogressEvoRootsFrameIDs, BurstTamerFrameID,
    /// AppFusionFrameIDs)</c> (:3086-3124).</summary>
    public void SetPlayCard(int cardIndex, int TargetFrameID, int[]? JogressEvoRootsFrameIDs, int BurstTamerFrameID, int[]? AppFusionFrameIDs)
    {
        if (cardIndex < 0 || cardIndex >= gameContext.ActiveCardList.Count)
        {
            return;
        }

        PlayCard = gameContext.ActiveCardList[cardIndex];
        this.TargetFrameID = TargetFrameID;

        if (JogressEvoRootsFrameIDs != null)
        {
            if (JogressEvoRootsFrameIDs.Length == 2)
            {
                this.JogressEvoRootsFrameIDs = new int[JogressEvoRootsFrameIDs.Length];

                for (int i = 0; i < JogressEvoRootsFrameIDs.Length; i++)
                {
                    this.JogressEvoRootsFrameIDs[i] = JogressEvoRootsFrameIDs[i];
                }
            }
        }

        this.BurstTamerFrameID = BurstTamerFrameID;

        if (AppFusionFrameIDs != null)
        {
            if (AppFusionFrameIDs.Length == 2)
            {
                this.AppFusionFrameIDs = new int[AppFusionFrameIDs.Length];

                for (int i = 0; i < AppFusionFrameIDs.Length; i++)
                {
                    this.AppFusionFrameIDs[i] = AppFusionFrameIDs[i];
                }
            }
        }
    }

    /// <summary>AS-IS <c>SetAttackingPermaent(permanentIndex, attackTargetPermanentIndex)</c> (:3127-3145) —
    /// method-name typo preserved (mechanical mirror).</summary>
    public void SetAttackingPermaent(int permanentIndex, int attackTargetPermanentIndex)
    {
        List<Permanent> turnPlayerField = gameContext.TurnPlayer!.GetFieldPermanents();
        List<Permanent> nonTurnPlayerFeid = gameContext.NonTurnPlayer!.GetFieldPermanents();   // AS-IS typo kept

        AttackingPermanent = null;
        DefendingPermanent = null;

        if (permanentIndex >= 0 && permanentIndex < turnPlayerField.Count)
        {
            AttackingPermanent = turnPlayerField[permanentIndex];
        }

        if (attackTargetPermanentIndex >= 0 && attackTargetPermanentIndex < nonTurnPlayerFeid.Count)
        {
            DefendingPermanent = nonTurnPlayerFeid[attackTargetPermanentIndex];
        }
    }

    /// <summary>AS-IS <c>PassTurn()</c> (:3364-3372): the explicit pass — in Main, run EndTurnProcess with
    /// <see cref="Passed"/> still true (the memory-jump arm). ADAPTATION: AS-IS ResetUI is stripped and the
    /// fire-and-forget <c>StartCoroutine(EndTurnProcess())</c> is awaited inline (single-threaded coroutine
    /// interleaving reaches the same completion point before the wait loop re-checks the phase).</summary>
    public async Task PassTurn(CancellationToken cancellationToken = default)
    {
        if (gameContext.TurnPhase == GameContext.phase.Main)
        {
            await AutoProcessing.For(_context).EndTurnProcess(cancellationToken).ConfigureAwait(false);
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
