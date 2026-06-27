using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-S1: MEMORY MODEL EQUIVALENCE.
//
// The divergence audit (S-1) questioned whether the port's single, turn-player-relative memory gauge
// (Pay = Current - cost; pass when Current <= -1; handoff = Math.Abs(Current); empty-pass = set -3)
// is functionally equivalent to the original's two-sided SIGNED shared gauge.
//
// ORIGINAL RULE (read from DCGO/Assets/Scripts/Script/):
//   - gameContext.Memory is a single SIGNED value; P0's memory = -Memory, P1's memory = +Memory
//     (Player.cs MemoryForPlayer). The signed value is NEVER reset on turn change (GameContext.SwitchTurnPlayer).
//   - Turn player paying cost c moves Memory toward the opponent (P0: +c, P1: -c).
//   - Turn ends when the NON-turn player's memory >= 1 (AutoProcessing EndTurnCheck), i.e. the gauge
//     crossed onto the opponent's side by >= 1. The opponent then starts with that overshoot amount.
//   - Voluntary pass sets the opponent to exactly 3 (Memory = +-3).
//
// Therefore, for a turn player who starts a turn with memory m and pays cost c:
//   * the turn ends iff c >= m + 1, and
//   * the opponent's starting memory = the OVERSHOOT = c - m.
//   * spending to exactly 0 (c == m) does NOT end the turn.
//   * a voluntary pass gives the opponent exactly 3.
// These outcomes are PLAYER-SYMMETRIC. Below we drive the REAL port end-to-end and assert it produces
// the same outcomes for BOTH players (the audit's concern was that the 2nd player would diverge).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("P1 overspends by K -> P2 starts with K", P1OverspendHandsOvershoot),
    ("P2 overspends by K -> P1 starts with K (2nd-player symmetry)", P2OverspendHandsOvershoot),
    ("Voluntary pass by P1 -> P2 starts with 3", P1VoluntaryPassGives3),
    ("Voluntary pass by P2 -> P1 starts with 3 (2nd-player symmetry)", P2VoluntaryPassGives3),
    ("Spending to exactly 0 keeps the turn (no pass)", SpendToZeroKeepsTurn),
    ("Partial spend stays in Main", PartialSpendStaysMain),
    ("Multi-turn chain carries the correct overshoot each handoff", MultiTurnChain),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task P1OverspendHandsOvershoot()
{
    const int K = 2; // P1 starts main with 0; paying K overshoots by K.
    DcgoMatch match = await NewMatchInMainAsync(P1);

    await Apply(match, HeadlessActionFactory.PayMemory(P1, K));
    AssertEqual(HeadlessPhase.MemoryPass, Phase(match), "pay overshoot -> memory pass");

    StepResult end = await Apply(match, HeadlessActionFactory.EndTurn(P1));
    AssertEqual(P2, end.Observation.Turn.TurnPlayerId, "turn handed to P2");
    AssertEqual(K, end.Observation.Memory.Current, "P2 starts with the overshoot K");
}

async Task P2OverspendHandsOvershoot()
{
    // This is the audit's key concern: does the SECOND player's turn handoff also work?
    const int K = 2;
    DcgoMatch match = await NewMatchInMainAsync(P1);

    // P1 voluntarily passes -> P2 starts its turn with 3.
    await Apply(match, HeadlessActionFactory.Pass(P1));
    await Apply(match, HeadlessActionFactory.EndTurn(P1));
    await AdvanceToMainAsync(match, P2);
    AssertEqual(3, Memory(match), "P2 starts its turn with 3");

    // P2 (memory 3) pays 3 + K -> overshoots by K -> P1 should start with K.
    await Apply(match, HeadlessActionFactory.PayMemory(P2, 3 + K));
    AssertEqual(HeadlessPhase.MemoryPass, Phase(match), "P2 overshoot -> memory pass");

    StepResult end = await Apply(match, HeadlessActionFactory.EndTurn(P2));
    AssertEqual(P1, end.Observation.Turn.TurnPlayerId, "turn handed back to P1");
    AssertEqual(K, end.Observation.Memory.Current, "P1 starts with the overshoot K (2nd-player symmetric)");
}

async Task P1VoluntaryPassGives3()
{
    DcgoMatch match = await NewMatchInMainAsync(P1);

    await Apply(match, HeadlessActionFactory.Pass(P1));
    StepResult end = await Apply(match, HeadlessActionFactory.EndTurn(P1));

    AssertEqual(P2, end.Observation.Turn.TurnPlayerId, "turn handed to P2");
    AssertEqual(3, end.Observation.Memory.Current, "voluntary pass gives the opponent 3");
}

async Task P2VoluntaryPassGives3()
{
    DcgoMatch match = await NewMatchInMainAsync(P1);
    await Apply(match, HeadlessActionFactory.Pass(P1));
    await Apply(match, HeadlessActionFactory.EndTurn(P1));
    await AdvanceToMainAsync(match, P2);

    await Apply(match, HeadlessActionFactory.Pass(P2));
    StepResult end = await Apply(match, HeadlessActionFactory.EndTurn(P2));

    AssertEqual(P1, end.Observation.Turn.TurnPlayerId, "turn handed back to P1");
    AssertEqual(3, end.Observation.Memory.Current, "P2 voluntary pass gives P1 3 (2nd-player symmetric)");
}

async Task SpendToZeroKeepsTurn()
{
    DcgoMatch match = await NewMatchInMainAsync(P1);

    await Apply(match, HeadlessActionFactory.AddMemory(P1, 3)); // memory 3
    await Apply(match, HeadlessActionFactory.PayMemory(P1, 3)); // exactly to 0

    AssertEqual(0, Memory(match), "memory is exactly 0");
    AssertEqual(HeadlessPhase.Main, Phase(match), "spending to 0 does NOT pass the turn");
}

async Task PartialSpendStaysMain()
{
    DcgoMatch match = await NewMatchInMainAsync(P1);

    await Apply(match, HeadlessActionFactory.AddMemory(P1, 5)); // memory 5
    await Apply(match, HeadlessActionFactory.PayMemory(P1, 2)); // -> 3

    AssertEqual(3, Memory(match), "partial spend leaves 3");
    AssertEqual(HeadlessPhase.Main, Phase(match), "partial spend stays in Main");
}

async Task MultiTurnChain()
{
    DcgoMatch match = await NewMatchInMainAsync(P1);

    // P1 (0) pays 2 -> overshoot 2 -> P2 gets 2.
    await Apply(match, HeadlessActionFactory.PayMemory(P1, 2));
    await Apply(match, HeadlessActionFactory.EndTurn(P1));
    await AdvanceToMainAsync(match, P2);
    AssertEqual(2, Memory(match), "P2 received 2");

    // P2 (2) pays 3 -> overshoot 1 -> P1 gets 1.
    await Apply(match, HeadlessActionFactory.PayMemory(P2, 3));
    StepResult end = await Apply(match, HeadlessActionFactory.EndTurn(P2));
    AssertEqual(P1, end.Observation.Turn.TurnPlayerId, "turn back to P1");
    AssertEqual(1, end.Observation.Memory.Current, "P1 received overshoot 1");
}

// --- Harness (from G2A-004) ----------------------------------------------

async Task<DcgoMatch> NewMatchInMainAsync(HeadlessPlayerId first)
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AdvanceToMainAsync(match, first);
    return match;
}

int Memory(DcgoMatch match) => match.GetObservation().Memory.Current;

HeadlessPhase Phase(DcgoMatch match) => match.GetObservation().Turn.Phase;

static async Task<StepResult> Apply(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

static async Task<DcgoMatch> CreateInitializedMatchAsync(int mainDeckCount = 12)
{
    DcgoMatch match = new();
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1", mainDeckCount), BuildDeck(new HeadlessPlayerId(2), "P2", mainDeckCount) },
        firstPlayerId: new HeadlessPlayerId(1));
    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 17, setup: setup));
    return match;
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3) =>
    new(playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        await Apply(match, HeadlessActionFactory.AdvancePhase(playerId));
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to Main");
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
