using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-S1: MEMORY MODEL EQUIVALENCE.
//
// The divergence audit (S-1) questioned whether the port's single, turn-player-relative memory gauge
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
//
// (4b B6 re-pin, GR-001 P-D pattern) The OLD step driver's currency (AdvancePhase preamble + explicit
// MemoryPass phase + EndTurn action two-step) is RETIRED onto DcgoMatch.CreatePumpDriven: memory is spent
// by REAL costed plays (staged hand cards with the exact PlayCost), the voluntary pass is the pump's legal
// Pass lane (AS-IS PassTurn -> EndTurnProcess), and the turn handover is the pump's auto EndTurnCheck flip
// (no explicit EndTurn action; GR-001 already pins the P1 overshoot case — this suite keeps the SYMMETRY,
// spend-to-zero, voluntary-pass=3 and multi-turn-chain rules that GR-001 does not cover).

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
    const int K = 2; // P1 starts main with 0; a cost-K play overshoots by K.
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 411);
    match.Context.MemoryController.Set(0);
    await PlayStagedAsync(match, P1, cost: K, tag: "P1K");
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));

    AssertEqual(P2, ctx.TurnController.Current.TurnPlayerId, "turn handed to P2");
    AssertEqual(K, Memory(match), "P2 starts with the overshoot K");
}

async Task P2OverspendHandsOvershoot()
{
    // The audit's key concern: does the SECOND player's turn handoff also work?
    const int K = 2;
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 412);

    // P1 voluntarily passes -> P2 starts its turn with 3.
    await PassAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    AssertEqual(3, Memory(match), "P2 starts its turn with 3");

    // P2 (memory 3) pays 3 + K -> overshoots by K -> P1 should start with K.
    await PlayStagedAsync(match, P2, cost: 3 + K, tag: "P2K");
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(P1, ctx.TurnController.Current.TurnPlayerId, "turn handed back to P1");
    AssertEqual(K, Memory(match), "P1 starts with the overshoot K (2nd-player symmetric)");
}

async Task P1VoluntaryPassGives3()
{
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 413);

    await PassAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));

    AssertEqual(P2, ctx.TurnController.Current.TurnPlayerId, "turn handed to P2");
    AssertEqual(3, Memory(match), "voluntary pass gives the opponent 3");
}

async Task P2VoluntaryPassGives3()
{
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 414);
    await PassAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));

    await PassAsync(match, P2);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(P1, ctx.TurnController.Current.TurnPlayerId, "turn handed back to P1");
    AssertEqual(3, Memory(match), "P2 voluntary pass gives P1 3 (2nd-player symmetric)");
}

async Task SpendToZeroKeepsTurn()
{
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 415);

    match.Context.MemoryController.Set(3);
    await PlayStagedAsync(match, P1, cost: 3, tag: "ZERO"); // exactly to 0
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(0, Memory(match), "memory is exactly 0");
    AssertEqual(P1, ctx.TurnController.Current.TurnPlayerId, "spending to 0 does NOT pass the turn");
}

async Task PartialSpendStaysMain()
{
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 416);

    match.Context.MemoryController.Set(5);
    await PlayStagedAsync(match, P1, cost: 2, tag: "PART"); // -> 3
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(3, Memory(match), "partial spend leaves 3");
    AssertEqual(P1, ctx.TurnController.Current.TurnPlayerId, "partial spend stays in P1's Main");
}

async Task MultiTurnChain()
{
    (DcgoMatch match, EngineContext ctx) = await NewPumpMatchAsync(seed: 417);

    // P1 (0) pays 2 -> overshoot 2 -> P2 gets 2.
    match.Context.MemoryController.Set(0);
    await PlayStagedAsync(match, P1, cost: 2, tag: "CH1");
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    AssertEqual(2, Memory(match), "P2 received 2");

    // P2 (2) pays 3 -> overshoot 1 -> P1 gets 1.
    await PlayStagedAsync(match, P2, cost: 3, tag: "CH2");
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    AssertEqual(P1, ctx.TurnController.Current.TurnPlayerId, "turn back to P1");
    AssertEqual(1, Memory(match), "P1 received overshoot 1");
}

// --- Harness (pump P-D retarget scaffold; GR-001 precedent) ---------------

int Memory(DcgoMatch match) => match.Context.MemoryController.Current.Current;

async Task<(DcgoMatch Match, EngineContext Context)> NewPumpMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, context);
}

// Stage a fresh plain Digimon with the EXACT play cost into the player's hand and play it through the
// pump's legal PlayCard lane (the real memory-paying currency; RD-R3-02 guard: the lane must exist and
// the play must land on the battle area).
async Task PlayStagedAsync(DcgoMatch match, HeadlessPlayerId player, int cost, string tag)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"SEQ-{tag}");
    cards.Upsert(new CardRecord(def, def.Value, $"Cost{cost}",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId($"{player.Value}:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, def, player));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(player, hand, ChoiceZone.None, ChoiceZone.Hand));

    LegalAction play;
    using (AmbientMatchContext.Enter(ctx))
    {
        play = match.GetLegalActions(player)
            .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    }
    await ApplyAsync(match, play);

    AssertTrue(!ZoneCards(match, player, ChoiceZone.Hand).Contains(hand), $"staged {tag} left the hand (the play resolved)");
    AssertTrue(ZoneCards(match, player, ChoiceZone.BattleArea).Contains(hand), $"staged {tag} entered the battle area");
}

async Task PassAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass;
    using (AmbientMatchContext.Enter(match.Context))
    {
        pass = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.Pass);
    }
    await ApplyAsync(match, pass);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} " +
            $"terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    await ApplyAsync(match, action);
}

async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).ToArray();

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

// --- Helpers -------------------------------------------------------------

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
