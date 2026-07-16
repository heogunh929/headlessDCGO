using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R4 batch S3a, decision 3 = B — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3 실행 설계")
// TurnFlowPump witnesses: an INSTALLED pump drives the mirror TurnStateMachine bodies continuously through the
// AS-IS pre-main trajectory, pausing only at the interactive stops:
//   StartGame (hands 5/5, mulligan choice) -> [keep/keep -> security 5/5] -> Active (start-of-turn window,
//   unsuspend) -> Draw (turn-1 skip) -> Breeding (BreedingDecision choice: hatch vs decline) -> Main entry
//   (OnStartMainPhase) -> parks at the S3b dispatch seam.
// A match WITHOUT Install must be byte-for-byte on the OLD driver (the S3a injectability contract).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("pump StartGame draws 5/5 and opens the mulligan choice", PumpStartGame),
    ("mulligan keep/keep deals security 5/5 and the pump advances to the BreedingDecision stop", PumpToBreeding),
    ("breeding ACT hatches; the empty main auto-passes (AS-IS :958-960) and the pump stops at turn 2's breeding decision", PumpBreedingHatch),
    ("breeding DECLINE leaves the breeding area empty; the turn still auto-passes to turn 2", PumpBreedingDecline),
    ("a full pump-driven game runs to the deck-out terminal with a marked winner", PumpFullGame),
    ("two same-seed pump matches stay digest-identical through the whole game", PumpDeterminism),
    ("a match without Install keeps the OLD driver behavior (no pump task, setup-owned mulligan)", OldDriverUnaffected),
};

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

async Task PumpStartGame()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 11);
    await StepAsync(match);   // task-runner steps the pump: StartGame runs to the mulligan park.

    AssertEqual(5, Count(match, P1, ChoiceZone.Hand), "P1 opening hand");
    AssertEqual(5, Count(match, P2, ChoiceZone.Hand), "P2 opening hand");
    AssertEqual(0, Count(match, P1, ChoiceZone.Security), "security is NOT dealt before the mulligan resolves");
    AssertTrue(match.HasPendingChoice(), "a mulligan choice is pending");
    AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
}

async Task PumpToBreeding()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 11);
    await StepAsync(match);
    await KeepBothMulligansAsync(match);

    AssertEqual(5, Count(match, P1, ChoiceZone.Security), "P1 security dealt after both keeps");
    AssertEqual(5, Count(match, P2, ChoiceZone.Security), "P2 security dealt after both keeps");

    await PumpUntilAsync(match, m => m.HasPendingChoice());
    AssertEqual(ChoiceType.BreedingDecision, match.Context.ChoiceController.PendingRequest!.Type,
        "the pump ran Active -> Draw and parked on the breeding decision");
    AssertEqual(HeadlessPhase.Breeding, match.Context.TurnController.Current.Phase, "phase is Breeding at the stop");
    AssertEqual(1, match.Context.TurnController.Current.TurnNumber, "turn 1");
}

async Task PumpBreedingHatch()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 11);
    await StepAsync(match);
    await KeepBothMulligansAsync(match);
    await PumpUntilAsync(match, m => m.HasPendingChoice());
    HeadlessPlayerId firstTurnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;

    // ACT -> hatch (CanHatch wins per AS-IS :804). With no playable/attackable/declarable option the main
    // phase auto-passes (AS-IS :958 CanSelect() false -> :960 EndTurnProcess: pass jump Set(-3), threshold
    // met, phase End), the turn flips, and the pump stops at TURN 2's breeding decision.
    await ResolvePendingAsync(match, skip: false);
    await PumpUntilAsync(match, m => m.HasPendingChoice() || m.IsTerminal());

    AssertEqual(1, Count(match, firstTurnPlayer, ChoiceZone.BreedingArea), "digitama hatched into the breeding area");
    AssertEqual(2, match.Context.TurnController.Current.TurnNumber, "the empty main auto-passed into turn 2");
    AssertEqual(ChoiceType.BreedingDecision, match.Context.ChoiceController.PendingRequest!.Type,
        "turn 2 opens the second player's breeding decision");
    AssertTrue(match.Context.TurnController.Current.TurnPlayerId!.Value != firstTurnPlayer, "turn flipped");
}

async Task PumpBreedingDecline()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 11);
    await StepAsync(match);
    await KeepBothMulligansAsync(match);
    await PumpUntilAsync(match, m => m.HasPendingChoice());
    HeadlessPlayerId firstTurnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;

    await ResolvePendingAsync(match, skip: true);    // DECLINE
    await PumpUntilAsync(match, m => m.HasPendingChoice() || m.IsTerminal());

    AssertEqual(0, Count(match, firstTurnPlayer, ChoiceZone.BreedingArea), "declined: nothing hatched");
    AssertEqual(2, match.Context.TurnController.Current.TurnNumber, "the turn still auto-passed into turn 2");
}

async Task PumpFullGame()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 11);
    await StepAsync(match);
    await KeepBothMulligansAsync(match);
    int turns = await DriveToTerminalAsync(match);

    AssertTrue(match.IsTerminal(), "the pump-driven game reached a terminal state");
    MatchResult result = match.GetResult();
    AssertTrue(result.WinnerId is not null && !result.IsDraw, $"deck-out marks a winner (reason: {result.Reason})");
    AssertTrue(turns > 10, $"the game ran many auto-passed turns (saw {turns})");
}

async Task PumpDeterminism()
{
    DcgoMatch a = await NewPumpMatchAsync(seed: 23);
    DcgoMatch b = await NewPumpMatchAsync(seed: 23);

    foreach (DcgoMatch m in new[] { a, b })
    {
        await StepAsync(m);
        await KeepBothMulligansAsync(m);
        await DriveToTerminalAsync(m);
    }

    AssertTrue(a.IsTerminal() && b.IsTerminal(), "both games finished");
    AssertEqual(Digest(a), Digest(b), "terminal digest");
    AssertEqual(a.GetResult().WinnerId, b.GetResult().WinnerId, "same winner");
}

async Task OldDriverUnaffected()
{
    // Standard OLD-driver match (no Install): setup owns hands+mulligan; no pump task exists.
    EngineContext context = NewContext(seed: 7);
    MatchConfig config = OldConfig(seed: 7);
    DcgoMatch match = new(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    await match.InitializeAsync(config);

    AssertEqual(0, context.TaskRunner.PendingTaskCount, "no pump task on an OLD match");
    AssertEqual(5, Count(match, P1, ChoiceZone.Hand), "setup-owned opening hand");
    AssertTrue(match.HasPendingChoice(), "setup-owned mulligan pending");
    await StepAsync(match);   // stepping without actions must not fault
    AssertTrue(!match.IsTerminal(), "match alive");
}

// --- Drivers -------------------------------------------------------------

async Task<DcgoMatch> NewPumpMatchAsync(int seed)
{
    EngineContext context = NewContext(seed);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    // Deck seed + shuffle ONLY — the pump's StartGameAsync owns hands, mulligan and security (AS-IS :341-504).
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

    DcgoMatch match = new(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    await match.InitializeAsync(config);
    TurnFlowPumpHost.Install(context);
    return match;
}

MatchConfig OldConfig(int seed)
{
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        },
        firstPlayerId: P1,
        enableMulligan: true);
    return MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);
}

static EngineContext NewContext(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    return context;
}

async Task KeepBothMulligansAsync(DcgoMatch match)
{
    for (int i = 0; i < 2; i++)
    {
        AssertTrue(match.HasPendingChoice(), $"mulligan choice {i + 1} pending");
        AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "mulligan type");
        await ResolvePendingAsync(match, skip: true);   // keep
    }
}

// Resolve the pending choice via the dispatcher-built ResolveChoice legal action (skip = keep/decline,
// non-skip = the first selectable candidate), then step once so the action processes.
async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action = Legal(match, chooser)
        .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice
            && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    StepResult applied = await match.ApplyActionAsync(action);
    StepResult stepped = await match.StepAsync();
    if (Environment.GetEnvironmentVariable("S3A_DEBUG") == "1")
    {
        foreach (GameEvent e in applied.Events.Concat(stepped.Events))
        {
            Console.Error.WriteLine($"  [ev] {e.Type}: {e.Message}");
        }
    }
}

// Drive a pump game to terminal: resolve every pending choice with ACT (non-skip), stepping between.
// Returns the final turn number. Bounded so a wedged pump fails the witness instead of hanging it.
async Task<int> DriveToTerminalAsync(DcgoMatch match)
{
    for (int i = 0; i < 400 && !match.IsTerminal(); i++)
    {
        if (match.HasPendingChoice())
        {
            await ResolvePendingAsync(match, skip: false);
        }
        else
        {
            await StepAsync(match);
        }
    }

    return match.Context.TurnController.Current.TurnNumber;
}

// Step the match until the condition holds (the pump advances one park per step) — bounded so a wedged pump
// fails the witness instead of hanging it.
async Task PumpUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 32 && !condition(match); i++)
    {
        await StepAsync(match);
    }

    if (!condition(match))
    {
        TurnFlowPumpHost? host = TurnFlowPumpHost.Find(match.Context);
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            "pump did not reach the expected stop within 32 steps — " +
            $"phase:{t.Phase}/{t.StepCursor} pendingChoice:{match.HasPendingChoice()} " +
            $"choiceType:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} " +
            $"pumpChoice:{host?.HasPendingPumpChoice} deposited:{host?.HasDepositedAnswer} " +
            $"parked:{host?.Gate.IsParked} tasks:{match.Context.TaskRunner.PendingTaskCount} terminal:{match.IsTerminal()}");
    }
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

static int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}

static string Digest(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessTurnState t = match.Context.TurnController.Current;
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"turn:{t.TurnNumber} player:{t.TurnPlayerId?.Value} phase:{t.Phase} cursor:{t.StepCursor}");
    sb.AppendLine($"memory:{match.Context.MemoryController.Current.Current}");
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
