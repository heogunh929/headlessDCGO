using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-004: in-breeding digivolution (the digi-egg ramp). A Lv.2 Digi-Egg in the breeding area can be
// digivolved into a Lv.3 Digimon IN PLACE (the result stays in breeding, the egg becomes a digivolution
// source), after which it is a movable Digimon (GR-002 gate). Mirrors AS-IS: the breeding digi-egg frame
// is a valid digivolution target. Uses real cards so the printed evolution condition (Red@2) matches.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Hatch -> digivolve egg into a Lv.3 in breeding -> move the Lv.3 to battle (full ramp)", BreedingRamp),
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

async Task BreedingRamp()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    EngineContext context = match.Context;
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    var zones = (IZoneStateReader)context.ZoneMover;

    // A real Red Lv.2 Digi-Egg (Koromon) hatched into the breeding area, and a real Red Lv.3 (Biyomon,
    // evolution condition Red@2, cost 0) in hand.
    var egg = new HeadlessEntityId("p1:breed:ST1_01");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(egg, new HeadlessEntityId("ST1_01"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, egg, ChoiceZone.None, ChoiceZone.BreedingArea));
    var rookie = new HeadlessEntityId("p1:hand:ST1_02");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(rookie, new HeadlessEntityId("ST1_02"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, rookie, ChoiceZone.None, ChoiceZone.Hand));

    // 1) In the MAIN phase, digivolving the breeding egg into the Lv.3 is offered.
    await AdvanceToMainAsync(match, P1);
    context.MemoryController.Set(0); // evolution cost is 0
    LegalAction digi = match.GetLegalActions(P1).Single(a =>
        a.ActionType == HeadlessActionTypes.Digivolve && a.Id.Value.Contains("ST1_02", StringComparison.Ordinal)
        && a.Id.Value.Contains(egg.Value, StringComparison.Ordinal));

    await match.ApplyActionAsync(digi);
    await match.StepAsync();

    // The Lv.3 now occupies the breeding area (the egg is a digivolution source, not a standalone card).
    AssertTrue(zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(rookie), "the Lv.3 is the breeding permanent's top card");
    AssertTrue(!zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(egg), "the egg is no longer a standalone breeding card (it is a source)");
    AssertEqual("Digimon", DefType(context, zones.GetCards(P1, ChoiceZone.BreedingArea)[0]), "breeding top is a Digimon now");
    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BattleArea).Count, "nothing entered the battle area from the in-breeding digivolution");

    // 2) Next breeding step: the Lv.3 Digimon is now a legal MoveBreedingToBattle (GR-002 gate).
    context.TurnController.SetPhase(HeadlessPhase.Breeding);
    LegalAction move = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
    await match.ApplyActionAsync(move);
    await match.StepAsync();

    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BreedingArea).Count, "breeding area emptied after the move");
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(rookie), "the Lv.3 reached the battle area via the breeding ramp");
}

// --- Helpers -------------------------------------------------------------

static string DefType(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
        && ctx.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null ? def.CardType ?? "?" : "?";

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
