using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-001: a play that takes the turn player's memory negative must END the turn (DCGO memory rule). The
// play/digivolve/option/special actions pay memory directly; HeadlessGameLoop now evaluates the memory
// turn-end after the action + its effects settle. This asserts: after a costed play crosses < 0, the phase
// becomes MemoryPass, the turn player can NO LONGER take a costed play, and on EndTurn the opponent starts
// with the mirrored (+|m|) memory.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A costed play that crosses memory < 0 ends the turn (MemoryPass, no more plays, opponent gains +|m|)", MemoryNegativeEndsTurn),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task MemoryNegativeEndsTurn()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // A plain level-3 Digimon that costs 3, playable from hand.
    const int cost = 3;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TGR-D"), "TGR-D", "TurnEnder",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId("p1:hand:TGR");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("TGR-D"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "starts in main phase");

    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);

    // The play took memory 0 -> -3, so the turn must have ended (MemoryPass), not continue in Main.
    AssertEqual(-cost, context.MemoryController.Current.Current, "memory went negative by the play cost");
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "memory crossing < 0 transitions to MemoryPass (turn ending)");

    // P1 can no longer take a costed play — only the turn handover (EndTurn) remains.
    bool anyPlay = match.GetLegalActions(P1).Any(a =>
        a.ActionType is HeadlessActionTypes.PlayCard or HeadlessActionTypes.Digivolve
            or HeadlessActionTypes.ActivateOption or HeadlessActionTypes.SpecialPlay);
    AssertTrue(!anyPlay, "no costed play is legal for P1 after the turn has ended");

    // Hand over the turn — the opponent starts with the mirrored (+|m|) memory.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await env.StepAsync(endTurn);

    AssertEqual(P2.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "turn passed to the opponent");
    AssertEqual(cost, context.MemoryController.Current.Current, "opponent starts with the mirrored +|m| memory");
}

// --- Helpers -------------------------------------------------------------

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).First(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
