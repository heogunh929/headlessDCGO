using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (R4 batch S3b — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3 실행 설계")
// Main-phase DISPATCH witnesses: the agent's PlayCard / DeclareAttack / Pass actions flow through
// TurnFlowDriver -> mirror MainPhaseAction packets -> the TurnStateMachine selection wait (:971-1170) ->
// the AS-IS dispatch arms (:1176-1252) — PlayCardClass (cost pipeline + [On Play] windows), the RD-9 attack
// declaration + the full attack pipeline ON the pump stack, and PassTurn -> EndTurnProcess.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(S3b-2 몸통 flip) PlayCard dispatch runs the REAL executor: cost paid, permanent enters, bookkeeping stamped, exhausted memory auto-ends the turn", PlayCardDispatch),
    ("(S3b-2 flip) DeclareAttack dispatch: a staged attacker's security attack resolves the full pipeline on the pump stack and consumes a security card", AttackDispatch),
    ("the pass/breeding surface stays fully live alongside the registered STOPs (S3a suite parity on this build)", PassSurfaceStillLive),
    ("(S3b-2①) CreateNewPermanent: zone entry + suspended/entered metadata + effect registration + view identity", CreateNewPermanentOp),
    ("(S3b-2①) AddCardSource: the new card becomes the zone-resident top, the old top threads into sourceIds, sickness inherits", AddCardSourceOp),
    ("(S3b-2②) just-after bookkeeping: view-stable writes, PERSISTS across the top swap (re-key), DIES on field leave (reset)", BookkeepingLifetime),
    ("(S3b-2 몸통) a PLAY-agent full game: real ST1/ST2 plays + [On Play] windows + attacks on the pump stack, to a terminal, deterministically", PlayAgentFullGame),
    ("(S3c-b) the pump legal-action table: main wait exposes Pass+plays (no AdvancePhase/EndTurn), auto-flow phases expose nothing, choices expose ResolveChoice only", PumpLegalTable),
    ("(S3c-c repro) an EVOLVED permanent's death trashes its WHOLE stack (top + digivolution sources)", EvolvedDeathTrashesStack),
    ("(S3c-d1) CreatePumpDriven promotion: pump installed at init (setup normalized), first step opens the mulligan, no step-cadence actions", PumpFactoryPromotion),
    ("(리뷰3 P2-3) pump-match blocker path: block choice opens, blocker substitutes as the battle target (suspend + DP deletion, no security check), the game continues", PumpBlockerBattle),
    ("(리뷰3 P2-⑤) a second same-mask packet cannot cross the turn boundary: the driver refuses production outside the armed main wait", PumpPacketTurnBoundaryGate),
    ("(리뷰3 P2-②) DISPATCH-REMAP double-key guard: a card registering the same effect under OnEnterFieldAnyone AND WhenDigivolving surfaces as a STOP on digivolve", WhenDigivolvingDoubleKeyGuard),
};

async Task PumpFactoryPromotion()
{
    // The promoted factory: a DEFAULT setup (hand 5 / security 5) must be normalized to pump ownership
    // (deck seeding + shuffle kept), the pump task must be installed by InitializeAsync itself, and the
    // OLD step-cadence actions must be absent from every legal table.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 89);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        },
        firstPlayerId: P1);   // deliberately DEFAULT hand/security/mulligan — the factory must normalize
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: 89, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);

    // (a) pump installed at initialize + agent profile + setup normalization (pump owns the deal).
    AssertTrue(match.IsPumpDriven, "the factory marks the match pump-driven");
    AssertTrue(TurnFlowPumpHost.Find(context) is not null, "the pump host service is registered at init");
    AssertTrue(context.TaskRunner.PendingTaskCount >= 1, "the pump task is enqueued on the TaskRunner at init");
    AssertTrue(match.EnforcesActionLegality, "the agent legality boundary is on by default");
    AssertEqual(0, Count(match, P1, ChoiceZone.Hand), "setup dealt NO hand (normalized: StartGame owns the deal)");
    AssertEqual(0, Count(match, P1, ChoiceZone.Security), "setup dealt NO security (normalized)");
    AssertTrue(Count(match, P1, ChoiceZone.Library) > 0, "deck seeding kept (library populated)");

    // (b) the first step runs the pump into StartGame: hands dealt, mulligan choice open.
    await StepAsync(match);
    AssertTrue(match.HasPendingChoice(), "first step opens a pending choice");
    AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "the choice is the mulligan");
    AssertEqual(5, Count(match, P1, ChoiceZone.Hand), "the pump dealt the AS-IS opening hand");

    // (c) NO step-cadence actions on any legal table: during the mulligan, then at the main wait.
    foreach (HeadlessPlayerId p in new[] { P1, P2 })
    {
        AssertTrue(Legal(match, p).All(a =>
            HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedAdvancePhase
            && HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedEndTurn),
            $"no AdvancePhase/EndTurn for player {p.Value} during the mulligan");
    }

    for (int i = 0; i < 2; i++)
    {
        await ResolvePendingAsync(match, skip: true);
    }

    await DriveUntilAsync(match, AtMainWait);
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    IReadOnlyList<LegalAction> table = Legal(match, turnPlayer);
    AssertTrue(table.Any(a => HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPass),
        "the main wait offers Pass");
    AssertTrue(table.All(a =>
        HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedAdvancePhase
        && HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedEndTurn),
        "no AdvancePhase/EndTurn at the main wait on a factory pump match");
}

async Task EvolvedDeathTrashesStack()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 83);
    await StepAsync(match);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    List<Cec.CardSource> digimons = player.HandCards.Where(c => c.IsDigimon).Take(2).ToList();

    // Build an evolved stack via the S3b-2① ops (the same seam the pump digivolve uses).
    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(digimons[0], isSuspended: false);
    HeadlessEntityId sourceId = permanent.TopCard.InstanceId;
    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveFromAllArea(digimons[1]);
    permanent = await permanent.AddCardSource(digimons[1]);
    HeadlessEntityId topId = permanent.TopCard.InstanceId;
    AssertEqual(1, permanent.DigivolutionCards.Count, "stack has one source under the top");

    // Kill it through the shared deletion pipeline (the AS-IS destroy: DiscardEvoRoots + field removal + trash).
    await new Cec.DestroyPermanentsClass(
        new List<Cec.Permanent> { permanent }, null).Destroy();

    var trash = (match.Context.ZoneMover as IZoneStateReader)!.GetCards(P1, ChoiceZone.Trash);
    AssertTrue(trash.Contains(topId), "the evolved TOP went to the trash");
    AssertTrue(trash.Contains(sourceId), $"the digivolution SOURCE went to the trash (got trash: {string.Join(",", trash.Select(c => c.Value))})");
    AssertEqual(0, Count(match, P1, ChoiceZone.BattleArea), "the battle area is empty");
}

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PlayCardDispatch()
{
    // (S3b-2 몸통 flip) RD-P6C1-4 resolved: the PlayCardClass hand-off now drives the REAL
    // PlayPermanentClass executor on the pump stack — pay pipeline → zone entry (S3b-2① op) →
    // bookkeeping (S3b-2② store) → inline OnEnterFieldAnyone window.
    DcgoMatch match = await NewPumpMatchAsync(seed: 31);
    await ReachMainWaitAsync(match);
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;

    (HeadlessEntityId cardId, int cost) = FirstPlayableDigimon(match, turnPlayer);
    int handBefore = Count(match, turnPlayer, ChoiceZone.Hand);
    int fieldBefore = Count(match, turnPlayer, ChoiceZone.BattleArea);

    await SendAsync(match, turnPlayer, HeadlessActionTypes.PlayCard,
        new Dictionary<string, object?> { [HeadlessActionParameterKeys.CardId] = cardId.Value });
    await DriveUntilAsync(match, m => Count(m, turnPlayer, ChoiceZone.BattleArea) > fieldBefore || m.IsTerminal());

    AssertEqual(fieldBefore + 1, Count(match, turnPlayer, ChoiceZone.BattleArea), "the digimon entered the battle area");
    AssertEqual(handBefore - 1, Count(match, turnPlayer, ChoiceZone.Hand), "the card left the hand");

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        var played = new Cec.Permanent(match.Context, cardId, turnPlayer);
        AssertTrue(played.LevelJustAfterPlayed >= 3, $"LevelJustAfterPlayed stamped (got {played.LevelJustAfterPlayed})");
        AssertEqual(cost, played.PlayCostJustAfterPlayed, "PlayCostJustAfterPlayed stamped with the printed cost");
    }

    // Memory 0 − cost crosses the threshold: the pump-loop EndTurnCheck (AS-IS :950) auto-ends the turn and
    // the gauge re-signs to +cost for the new turn player.
    await DriveUntilAsync(match, m =>
        m.Context.TurnController.Current.TurnPlayerId!.Value != turnPlayer || m.IsTerminal());
    AssertEqual(2, match.Context.TurnController.Current.TurnNumber, "the exhausted memory ended turn 1");
    AssertEqual(cost, match.Context.MemoryController.Current.Current, "gauge re-signed to +cost for the new turn player");
}

async Task AttackDispatch()
{
    // (S3b-2 flip) The CanEvolve engine landed (RD-P6C1-2 read side), so a staged battle digimon no longer
    // gates CanSelect — the attack ARM is now exercisable end-to-end on the pump stack. STAGED board (fixture,
    // R4P2a Place pattern): the play EXECUTOR is still the RD-P6C1-4 STOP, so the attacker is placed directly.
    DcgoMatch match = await NewPumpMatchAsync(seed: 31);
    HeadlessEntityId attackerId = StageBattleDigimon(match, P1, dp: 3000);

    await ReachMainWaitAsync(match);
    HeadlessPlayerId attackerOwner = match.Context.TurnController.Current.TurnPlayerId!.Value;
    HeadlessPlayerId defenderOwner = attackerOwner == P1 ? P2 : P1;
    AssertEqual(P1, attackerOwner, "P1 is the staged attacker's owner and the first turn player");

    // Turn 1: pass (summoning-sickness parity; the staged permanent attacks on P1's next turn).
    await SendAsync(match, attackerOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 2 && AtMainWait(m));
    await SendAsync(match, defenderOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 3 && AtMainWait(m));

    int securityBefore = Count(match, defenderOwner, ChoiceZone.Security);
    await SendAsync(match, attackerOwner, HeadlessActionTypes.DeclareAttack,
        new Dictionary<string, object?> { [HeadlessActionParameterKeys.AttackerId] = attackerId.Value });
    await DriveUntilAsync(match, m =>
        Count(m, defenderOwner, ChoiceZone.Security) < securityBefore || m.IsTerminal());

    AssertEqual(securityBefore - 1, Count(match, defenderOwner, ChoiceZone.Security),
        "the security attack consumed one security card");
    AssertTrue(!match.IsTerminal(), "the match continues after the first security check");
}

async Task PumpBlockerBattle()
{
    // (리뷰3 P2-3) A pump match hosts TWO attack drivers: the body's attack pump loops (TurnStateMachine
    // :941-948 etc., parked at WaitPendingChoiceUnderPump while a choice is pending) AND GameFlowProcessor.
    // RunToStableAsync step 3, which stays resident in every DcgoMatch step. The shadow policy never blocks,
    // so this interleaving was uncovered: resolving an IN-ATTACK choice lets the FLOW complete the residual
    // attack stages within the same step, and the pump's attack loop wakes to an ActiveAttack()==false idle
    // no-op. This witness pins the AS-IS outcome across that seam: declare -> Blocker choice (defender-owned,
    // attack parked at Blocking) -> resolve WITH the blocker -> SwitchDefender substitutes the blocker as the
    // battle target (suspend + DP compare deletes the weaker attacker, NO security check) -> the pump returns
    // to the main wait and the turn still flips.
    DcgoMatch match = await NewPumpMatchAsync(seed: 31);
    HeadlessEntityId attackerId = StageBattleDigimon(match, P1, dp: 3000);
    HeadlessEntityId blockerId = StageBattleDigimon(match, P2, dp: 5000, hasBlocker: true);

    await ReachMainWaitAsync(match);
    HeadlessPlayerId attackerOwner = match.Context.TurnController.Current.TurnPlayerId!.Value;
    HeadlessPlayerId defenderOwner = attackerOwner == P1 ? P2 : P1;
    AssertEqual(P1, attackerOwner, "P1 is the staged attacker's owner and the first turn player");

    // Turns 1-2: pass (summoning-sickness parity — the AttackDispatch pattern).
    await SendAsync(match, attackerOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 2 && AtMainWait(m));
    await SendAsync(match, defenderOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 3 && AtMainWait(m));

    int securityBefore = Count(match, defenderOwner, ChoiceZone.Security);
    await SendAsync(match, attackerOwner, HeadlessActionTypes.DeclareAttack,
        new Dictionary<string, object?> { [HeadlessActionParameterKeys.AttackerId] = attackerId.Value });
    await DriveUntilAsync(match, m => m.HasPendingChoice());

    // The block stage opened the choice and BOTH drivers idle on it: the pump parks at the choice-pause seam,
    // the flow's RunToStableAsync pauses on the pending choice — the attack stays parked at Blocking.
    AssertEqual(ChoiceType.Blocker, match.Context.ChoiceController.PendingRequest!.Type, "the block window opened as a pending choice");
    AssertEqual(defenderOwner, match.Context.ChoiceController.PendingRequest!.PlayerId, "the DEFENDER owns the block choice");
    AssertEqual(AttackPhase.Blocking, match.Context.AttackController.Current.Phase, "the attack parked at the Blocking stage");
    AssertTrue(match.Context.AttackController.Current.IsDirectAttack, "the declared attack was the direct (security) attack");
    AssertTrue(!IsSuspendedMeta(match, blockerId), "the blocker is unsuspended while the choice is open");

    // Resolve WITH the blocker (the only candidate). The residual stages (Blocking -> Combat -> battle ->
    // Resolved) complete flow-side within this resolution step; the pump attack loop then wakes as the idle
    // no-op. Any crash/divergence here is the review-3 P2-3 escalation case.
    await ResolvePendingAsync(match, skip: false);

    AssertTrue(IsSuspendedMeta(match, blockerId), "blocking SUSPENDED the blocker (AS-IS SwitchDefender Tap)");
    AssertTrue(ZoneCards(match, defenderOwner, ChoiceZone.BattleArea).Contains(blockerId),
        "the higher-DP blocker survived the battle on the field");
    AssertTrue(ZoneCards(match, attackerOwner, ChoiceZone.Trash).Contains(attackerId),
        "the attacker LOST the DP compare against the substituted blocker and was deleted to the trash");
    AssertEqual(securityBefore, Count(match, defenderOwner, ChoiceZone.Security),
        "the blocked attack never checked security (target substituted to the blocker)");
    AssertTrue(!match.Context.AttackController.Current.IsPending, "the attack fully resolved");
    AssertTrue(!match.IsTerminal(), "the game continues after the blocked battle");

    // Liveness across the seam: the pump (woken from its idle no-op) re-parks at the main wait, and the turn
    // still flips on an explicit pass — the double-driver interleaving did not wedge the match.
    await DriveUntilAsync(match, AtMainWait);
    AssertEqual(3, match.Context.TurnController.Current.TurnNumber, "still the attacker's turn at the post-battle main wait");
    await SendAsync(match, attackerOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 4 && AtMainWait(m));
}

async Task PumpPacketTurnBoundaryGate()
{
    // (리뷰3 P2-⑤) AS-IS gates packet PRODUCTION at the UI (NextPhaseButton.OnClick `!IsSelecting &&
    // !isExecuting && ... && TurnPhase == Main`; card clicks are armed only during the selection wait, one
    // click per arm) and never drains Player.mainPhaseActions at a turn boundary. An agent that queues TWO
    // packets against the SAME mask (both pass apply-time legality — queueing does not change the observable
    // state) must therefore have the SECOND refused at the driver's producer seat once the first ends the
    // turn; before the gate the stale packet survived into the NEXT turn's main wait and auto-consumed it.
    DcgoMatch match = await NewPumpMatchAsync(seed: 59);
    await ReachMainWaitAsync(match);
    HeadlessPlayerId first = match.Context.TurnController.Current.TurnPlayerId!.Value;

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var pass1 = new LegalAction(new HeadlessEntityId("witness:pass:1"), first, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    var pass2 = new LegalAction(new HeadlessEntityId("witness:pass:2"), first, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    StepResult apply1 = await match.ApplyActionAsync(pass1);
    StepResult apply2 = await match.ApplyActionAsync(pass2);
    AssertTrue(apply1.Events.All(e => e.Type != GameEventType.InvalidAction), "the first pass is accepted at apply");
    AssertTrue(apply2.Events.All(e => e.Type != GameEventType.InvalidAction),
        "the second pass ALSO passes the apply-time mask (the stale-mask window under test)");

    // Step 1 processes pass1 (packet queued); step 2's task-runner phase consumes it (the turn ends), then
    // pass2 is processed — the producer gate must refuse it (the main wait is no longer armed).
    await match.StepAsync();
    StepResult step2 = await match.StepAsync();
    GameEvent processed = step2.Events.Single(e => e.Type == GameEventType.ActionProcessed);
    AssertTrue(processed.Metadata.TryGetValue("success", out object? ok) && ok is bool okFlag && !okFlag,
        "the stale second packet was REFUSED (Failure), not queued");
    AssertEqual(2, match.Context.TurnController.Current.TurnNumber, "the first pass ended turn 1");

    // The refusal left the queue empty, so the first player's NEXT main wait waits for the agent instead of
    // consuming a stale cross-turn pass (the pre-gate behavior auto-passed turn 3).
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 2 && AtMainWait(m));
    HeadlessPlayerId second = match.Context.TurnController.Current.TurnPlayerId!.Value;
    await SendAsync(match, second, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 3 && AtMainWait(m));
    AssertEqual(first, match.Context.TurnController.Current.TurnPlayerId!.Value, "turn 3 belongs to the first player again");
    AssertTrue(Legal(match, first).Any(a => HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPass),
        "the turn-3 main wait is ARMED for the agent (no stale packet consumed it)");
}

async Task WhenDigivolvingDoubleKeyGuard()
{
    // (리뷰3 P2-②) The executor's WhenDigivolving bridge opens BOTH windows on an evolution play
    // (OnEnterFieldAnyone = the literal AS-IS key; WhenDigivolving = the corpus DISPATCH-REMAP dialect key).
    // A card registering the SAME effect under both keys would fire it twice per digivolve — a state AS-IS
    // (single key) cannot express, hence a corpus authoring defect. The guard at the bridge seat must
    // SURFACE it as a STOP (NotSupportedException), not silently dedup. Fixture: TfxDoubleKeyWhenDigivolving
    // (the deliberate double-key registration; live corpus double-key registrations: 0).
    DcgoMatch match = await NewPumpMatchAsync(seed: 61);
    EngineContext context = match.Context;
    var db = (CardDatabase)context.CardRepository;

    // A RED level-3 evolution target on P1's board (the Tfx staging convention: definition + instance + move).
    var targetDef = new HeadlessEntityId("DEF:tfx-dk-target");
    db.Upsert(new CardRecord(targetDef, "TfxDkTarget", "TfxDkTarget",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        { ["color"] = "Red", ["colors"] = new[] { "Red" }, ["level"] = 3, ["dp"] = 3000 },
        CardType: "Digimon"));
    var targetId = new HeadlessEntityId("card:tfx-dk-target");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(targetId, targetDef, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, targetId, ChoiceZone.None, ChoiceZone.BattleArea));

    // The double-keyed fixture in P1's hand (dispatch resolves the card number to the Tfx class).
    var evoDef = new HeadlessEntityId("TfxDoubleKeyWhenDigivolving");
    db.Upsert(new CardRecord(evoDef, "TfxDoubleKeyWhenDigivolving", "TfxDoubleKeyWhenDigivolving",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        { ["color"] = "Red", ["colors"] = new[] { "Red" }, ["level"] = 4, ["dp"] = 5000, ["evolutionConditions"] = new[] { "Red@3:2" } },
        CardType: "Digimon", EvolutionCost: 2, EvolutionCondition: "Red@3:2"));
    var evoId = new HeadlessEntityId("card:tfx-dk-evo");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evoId, evoDef, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evoId, ChoiceZone.None, ChoiceZone.Hand));

    await ReachMainWaitAsync(match);
    AssertEqual(P1, match.Context.TurnController.Current.TurnPlayerId!.Value, "P1 owns the first main wait");

    // Digivolve the fixture onto the target: the evolution arm reaches the bridge, the guard must throw.
    Exception? surfaced = null;
    try
    {
        await SendAsync(match, P1, HeadlessActionTypes.PlayCard,
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.CardId] = evoId.Value,
                [HeadlessActionParameterKeys.TargetCardId] = targetId.Value,
            });
        for (int i = 0; i < 32; i++)
        {
            await StepAsync(match);
        }
    }
    catch (Exception ex)
    {
        surfaced = ex;
    }

    AssertTrue(surfaced is NotSupportedException,
        $"the double-key registration surfaced as a STOP (got {surfaced?.GetType().Name ?? "<no exception>"}: {surfaced?.Message ?? "-"})");
    AssertTrue(surfaced!.Message.Contains("BOTH OnEnterFieldAnyone and WhenDigivolving", StringComparison.Ordinal),
        $"the STOP names the double-key defect (got: {surfaced.Message})");
}

async Task PassSurfaceStillLive()
{
    // Alongside the two registered STOP gates, the whole pass/breeding surface must stay live on THIS build —
    // a compact re-run of the S3a full-game drive (pass-only agent to the deck-out terminal).
    DcgoMatch match = await NewPumpMatchAsync(seed: 47);
    await StepAsync(match);
    for (int i = 0; i < 2; i++)
    {
        await ResolvePendingAsync(match, skip: true);
    }

    for (int i = 0; i < 800 && !match.IsTerminal(); i++)
    {
        if (match.HasPendingChoice())
        {
            await ResolvePendingAsync(match, skip: true);   // decline breeding: board stays digimon-free
        }
        else if (AtMainWait(match))
        {
            await SendAsync(match, match.Context.TurnController.Current.TurnPlayerId!.Value,
                HeadlessActionTypes.Pass, new Dictionary<string, object?>());
        }
        else
        {
            await StepAsync(match);
        }
    }

    AssertTrue(match.IsTerminal(), "the pass-only game reached the deck-out terminal");
    AssertTrue(match.GetResult().WinnerId is not null, "a winner was marked");
}

async Task CreateNewPermanentOp()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 53);
    await StepAsync(match);   // StartGame: hands drawn (mulligan pending is fine — the op is zone-level)
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    Cec.CardSource card = player.HandCards.First(c => c.IsDigimon);

    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(card, isSuspended: true);

    AssertEqual(1, Count(match, P1, ChoiceZone.BattleArea), "the card entered the battle area");
    AssertEqual(card.InstanceId, permanent.TopCard.InstanceId, "the view keys on the entered card");
    AssertTrue(permanent.IsSuspended, "the AS-IS initializer state (IsSuspended) landed in metadata");
    AssertEqual(match.Context.TurnController.Current.TurnNumber, permanent.EnterFieldTurnCount,
        "entered-this-turn stamped (AS-IS EnterFieldTurnCount == TurnCount)");

    // AS-IS jogress writes EnterFieldTurnCount = -1 — the setter maps it onto the sickness carrier.
    permanent.EnterFieldTurnCount = -1;
    AssertEqual(-1, permanent.EnterFieldTurnCount, "the jogress -1 write clears the sickness flag");
}

async Task AddCardSourceOp()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 53);
    await StepAsync(match);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    List<Cec.CardSource> digimons = player.HandCards.Where(c => c.IsDigimon).Take(2).ToList();
    AssertTrue(digimons.Count == 2, "two hand digimons available");

    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(digimons[0], isSuspended: false);
    HeadlessEntityId oldTopId = permanent.TopCard.InstanceId;

    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveFromAllArea(digimons[1]);
    permanent = await permanent.AddCardSource(digimons[1]);

    AssertEqual(digimons[1].InstanceId, permanent.TopCard.InstanceId, "the new card is the top");
    AssertEqual(1, Count(match, P1, ChoiceZone.BattleArea), "still ONE battle-area permanent (top swapped)");
    AssertTrue(permanent.DigivolutionCards.Any(c => c.InstanceId == oldTopId),
        "the old top threaded into the digivolution sources");
    AssertEqual(match.Context.TurnController.Current.TurnNumber, permanent.EnterFieldTurnCount,
        "sickness INHERITED from the under-card (N-1: both entered this turn)");
}

async Task BookkeepingLifetime()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 59);
    await StepAsync(match);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    List<Cec.CardSource> digimons = player.HandCards.Where(c => c.IsDigimon).Take(2).ToList();

    // CREATE: the executor-shaped writes land on the store and read back through a FRESH view.
    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(digimons[0], isSuspended: false);
    permanent.LevelJustAfterPlayed = 3;
    permanent.PlayCostJustAfterPlayed = 4;
    permanent.TraitsJustAfterPlayed = new List<string> { "D-Brigade" };
    Cec.Permanent freshView = new(match.Context, permanent.TopCard.InstanceId, P1);
    AssertEqual(3, freshView.LevelJustAfterPlayed, "a fresh view reads the same entry (match-scoped store)");
    AssertEqual(4, freshView.PlayCostJustAfterPlayed, "play cost survives the view boundary");

    // PERSIST: the top swap (AddCardSource → AttachTargetAsSource) re-keys the entry to the new top.
    HeadlessEntityId oldTop = permanent.TopCard.InstanceId;
    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveFromAllArea(digimons[1]);
    permanent = await permanent.AddCardSource(digimons[1]);
    AssertEqual(3, permanent.LevelJustAfterPlayed, "the AS-IS object persists: played-level survives digivolving");
    AssertTrue(permanent.TraitsJustAfterPlayed.Contains("D-Brigade"), "played-traits survive digivolving");

    // DIE: leaving the field drops the entry — a fresh life reads AS-IS defaults.
    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveField(permanent, ignoreOverflow: true);
    Cec.Permanent rePlayed = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(permanent.TopCard, isSuspended: false);
    AssertEqual(-1, rePlayed.LevelJustAfterPlayed, "a re-played card starts a FRESH bookkeeping life (default -1)");
    AssertEqual(0, rePlayed.TraitsJustAfterPlayed.Count, "fresh life: empty played-traits");
}

async Task PlayAgentFullGame()
{
    // The scripted deterministic PLAY agent: at every main wait, play the cheapest playable hand digimon; if
    // none, attack security with the first ready attacker; else pass. Choices: breeding = act, others = act.
    // Runs REAL card effects ([On Play] draws / memory effects of ST1/ST2) through the executor on the pump
    // stack. Two same-seed games must stay digest-identical to the terminal.
    DcgoMatch a = await DriveFullPlayGameAsync(seed: 67);
    DcgoMatch b = await DriveFullPlayGameAsync(seed: 67);

    AssertTrue(a.IsTerminal() && b.IsTerminal(), "both play-agent games reached a terminal");
    AssertTrue(a.GetResult().WinnerId is not null || a.GetResult().IsDraw, "a verdict was recorded");
    AssertEqual(Digest(a), Digest(b), "same-seed play-agent games are digest-identical at the terminal");
}

async Task<DcgoMatch> DriveFullPlayGameAsync(int seed)
{
    DcgoMatch match = await NewPumpMatchAsync(seed);
    await StepAsync(match);
    for (int i = 0; i < 2; i++)
    {
        await ResolvePendingAsync(match, skip: true);   // keep both opening hands
    }

    int lastTurn = 0;
    for (int i = 0; i < 1200 && !match.IsTerminal(); i++)
    {
        if (Environment.GetEnvironmentVariable("S3B2_DEBUG") == "1" && (i % 25 == 0 || match.Context.TurnController.Current.TurnNumber != lastTurn))
        {
            lastTurn = match.Context.TurnController.Current.TurnNumber;
            HeadlessTurnState t = match.Context.TurnController.Current;
            Console.Error.WriteLine($"  [drive {i}] turn:{t.TurnNumber} phase:{t.Phase} choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "-"} mem:{match.Context.MemoryController.Current.Current} p1F:{Count(match, P1, ChoiceZone.BattleArea)} p2F:{Count(match, P2, ChoiceZone.BattleArea)}");
            Console.Error.Flush();
        }

        if (match.HasPendingChoice())
        {
            await ResolvePendingAsync(match, skip: false);
            continue;
        }

        if (!AtMainWait(match))
        {
            await StepAsync(match);
            continue;
        }

        HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
        var player = new Cec.Player(match.Context, turnPlayer);

        // ATTACK-first agent (games end by security depletion, not deck-out) and a small board cap (the
        // frame-less battle area is uncapped — RD-P6C1-2 — so an unbounded play-first agent grows O(board²)
        // scans and drags the game past the deck).
        Cec.Permanent? attacker = player.GetFieldPermanents()
            .FirstOrDefault(p => p.CanAttack(null));

        Cec.CardSource? playable = player.GetFieldPermanents().Count >= 4
            ? null
            : player.HandCards
                .Where(c => c.IsDigimon && c.CanPlayFromHandDuringMainPhase
                    // keep the drive to NEW plays (the digivolve intent rides TargetCardId — out of this witness's scope)
                    && new Cec.Player(match.Context, turnPlayer).MaxMemoryCost >= c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
                .OrderBy(c => c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
                .FirstOrDefault();

        if (attacker is not null)
        {
            await SendAsync(match, turnPlayer, HeadlessActionTypes.DeclareAttack,
                new Dictionary<string, object?> { [HeadlessActionParameterKeys.AttackerId] = attacker.InstanceId.Value });
        }
        else if (playable is not null)
        {
            await SendAsync(match, turnPlayer, HeadlessActionTypes.PlayCard,
                new Dictionary<string, object?> { [HeadlessActionParameterKeys.CardId] = playable.InstanceId.Value });
        }
        else
        {
            await SendAsync(match, turnPlayer, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
        }
    }

    AssertTrue(match.IsTerminal(), "the play-agent game reached a terminal within the drive bound");
    return match;
}

static string Digest(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessTurnState t = match.Context.TurnController.Current;
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"turn:{t.TurnNumber} player:{t.TurnPlayerId?.Value} phase:{t.Phase} cursor:{t.StepCursor}");
    sb.AppendLine($"memory:{match.Context.MemoryController.Current.Current}");
    sb.AppendLine($"winner:{match.GetResult().WinnerId?.Value} draw:{match.GetResult().IsDraw}");
    foreach (HeadlessPlayerId p in new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) })
    {
        foreach (ChoiceZone z in new[] { ChoiceZone.Hand, ChoiceZone.Library, ChoiceZone.Security, ChoiceZone.BreedingArea, ChoiceZone.BattleArea, ChoiceZone.Trash })
        {
            if (match.Context.ZoneMover is IZoneStateReader zones)
            {
                sb.AppendLine($"p{p.Value}:{z}:{string.Join(",", zones.GetCards(p, z).Select(c => c.Value))}");
            }
        }
    }

    return sb.ToString();
}

async Task PumpLegalTable()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 71);
    await StepAsync(match);

    // During the mulligan choice: chooser sees ResolveChoice only; the other player sees nothing.
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    HeadlessPlayerId other = chooser == P1 ? P2 : P1;
    AssertTrue(Legal(match, chooser).All(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "pending choice: chooser sees ResolveChoice only");
    AssertEqual(0, Legal(match, other).Count, "pending choice: the other player sees nothing");

    for (int i = 0; i < 2; i++)
    {
        await ResolvePendingAsync(match, skip: true);
    }

    // Drive to the breeding decision: still a CHOICE (no breeding phase actions on a pump match).
    await DriveUntilAsync(match, m => m.HasPendingChoice());
    AssertEqual(ChoiceType.BreedingDecision, match.Context.ChoiceController.PendingRequest!.Type, "breeding stop is a choice");
    HeadlessPlayerId breeder = match.Context.ChoiceController.PendingRequest!.PlayerId;
    AssertTrue(Legal(match, breeder).All(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "breeding stop exposes ResolveChoice only (no HatchDigitama/AdvancePhase actions)");
    await ResolvePendingAsync(match, skip: true);

    // At the main wait: Pass + plays/attacks — and NO step-cadence actions.
    await DriveUntilAsync(match, AtMainWait);
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    IReadOnlyList<LegalAction> table = Legal(match, turnPlayer);
    AssertTrue(table.Any(a => HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPass), "main wait offers Pass");
    AssertTrue(table.Any(a => HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPlayCard), "main wait offers plays");
    AssertTrue(table.All(a => HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedAdvancePhase), "NO AdvancePhase on a pump match");
    AssertTrue(table.All(a => HeadlessActionTypes.Normalize(a.ActionType) != HeadlessActionTypes.NormalizedEndTurn), "NO EndTurn on a pump match");
    AssertEqual(0, Legal(match, turnPlayer == P1 ? P2 : P1).Count, "the non-turn player has no main actions");
}

// --- Drivers -------------------------------------------------------------

async Task<DcgoMatch> NewPumpMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        },
        firstPlayerId: P1,
        initialHandSize: 0,
        initialSecuritySize: 0,
        enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = new(context, new EngineTrace(), actionProcessor: new TurnFlowDriver());
    await match.InitializeAsync(config);
    TurnFlowPumpHost.Install(context);
    return match;
}

// Start the pump, keep both mulligans, decline the breeding decision, land at the main selection wait.
async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepAsync(match);
    for (int i = 0; i < 2; i++)
    {
        AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "mulligan pending");
        await ResolvePendingAsync(match, skip: true);
    }

    await DriveUntilAsync(match, AtMainWait);
}

static bool AtMainWait(DcgoMatch match) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && !match.HasPendingChoice()
    && !match.IsTerminal();

// Generic driver: resolve breeding decisions with DECLINE (keep the board minimal), resolve any other
// pending choice with ACT, otherwise step — until the condition holds. Bounded.
async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 64 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} terminal:{match.IsTerminal()} " +
            $"memory:{match.Context.MemoryController.Current.Current}");
    }
}

async Task SendAsync(DcgoMatch match, HeadlessPlayerId player, string actionType, Dictionary<string, object?> parameters)
{
    var action = new LegalAction(
        new HeadlessEntityId($"witness:{actionType}:{Guid.NewGuid():N}"),
        player,
        actionType,
        parameters);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip);
    }

    using AmbientMatchContext.Scope __ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

// Stage a synthetic battle-area digimon (R4P2a Place fixture pattern) — the RD-P6C1-4 STOP blocks the real
// play path, so the attack witness constructs its board directly.
static HeadlessEntityId StageBattleDigimon(DcgoMatch match, HeadlessPlayerId owner, int dp, bool hasBlocker = false)
{
    EngineContext context = match.Context;
    var defId = new HeadlessEntityId($"DEF:staged:{owner.Value}");
    var definitionMetadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 3 };
    if (hasBlocker)
    {
        definitionMetadata[BlockTiming.HasBlockerKey] = true;   // the G3.5-A3 blocker fixture convention
    }

    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(defId, "Staged", "Staged",
        definitionMetadata, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:staged:{owner.Value}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// The cheapest playable digimon in hand by the MIRROR predicate (the same term CanSelect() reads).
static (HeadlessEntityId Id, int Cost) FirstPlayableDigimon(DcgoMatch match, HeadlessPlayerId owner)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var player = new Cec.Player(match.Context, owner);
    Cec.CardSource card = player.HandCards
        .Where(c => c.IsDigimon && c.CanPlayFromHandDuringMainPhase)
        .OrderBy(c => c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
        .First();
    int cost = card.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true);
    return (card.InstanceId, cost);
}


static int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones
        ? zones.GetCards(player, zone)
        : Array.Empty<HeadlessEntityId>();
}

// The G3.5-A3 suspend probe: the isSuspended instance-metadata flag (the BlockTiming.SuspendBlocker write).
static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    }

    return record.Metadata.TryGetValue(BlockTiming.IsSuspendedKey, out object? raw) && raw is bool flag && flag;
}


// --- Asserts -------------------------------------------------------------

static void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"expected TRUE: {message}");
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}
