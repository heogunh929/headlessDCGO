using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-002: the breeding area may move to the battle area ONLY once its top card is a Digimon — a freshly
// hatched Digi-Egg (level 2, DP 0) cannot walk into the battle area. Mirrors AS-IS Permanent.CanMove
// (`if (!IsDigimon) return false;` + `if (TopCard.IsDigiEgg && DP <= 0) return false;`). Previously the
// engine offered MoveBreedingToBattle whenever the area was occupied, so a level-2 egg illegally entered
// the battle area.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A freshly-hatched Digi-Egg in breeding CANNOT move to battle", DigiEggCannotMove),
    ("A Digimon in breeding CAN move to battle (and lands there)", DigimonCanMove),
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

async Task DigiEggCannotMove()
{
    var match = await BreedingMatchAsync();
    // A Digi-Egg (the only thing a hatch produces) sitting in the breeding area.
    await PlaceInBreedingAsync(match, P1, "EGG", CardType: "DigiEgg", level: 2, dp: 0);

    bool moveOffered = match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
    AssertTrue(!moveOffered, "a level-2 Digi-Egg is NOT a legal MoveBreedingToBattle");

    // Hatch is still available only because the area is NOT empty here it must NOT be (occupied) — sanity:
    bool hatchOffered = match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.HatchDigitama);
    AssertTrue(!hatchOffered, "hatch is not offered while the breeding area is occupied");
}

async Task DigimonCanMove()
{
    var match = await BreedingMatchAsync();
    await PlaceInBreedingAsync(match, P1, "ROOKIE", CardType: "Digimon", level: 3, dp: 3000);

    bool moveOffered = match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
    AssertTrue(moveOffered, "a level-3 Digimon IS a legal MoveBreedingToBattle");

    LegalAction move = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
    await match.ApplyActionAsync(move);
    await match.StepAsync();

    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BreedingArea).Count, "breeding area is now empty");
    AssertEqual(1, zones.GetCards(P1, ChoiceZone.BattleArea).Count, "the Digimon moved to the battle area");
    // And the battle area holds a Digimon, never a Digi-Egg.
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).All(id => DefType(match, id) == "Digimon"),
        "no Digi-Egg ever lands in the battle area");
}

// --- Helpers -------------------------------------------------------------

async Task<DcgoMatch> BreedingMatchAsync()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    // Drive the turn player (P1) into the Breeding phase, where hatch/move actions are offered.
    match.Context.TurnController.SetPhase(HeadlessPhase.Breeding);
    return match;
}

static async Task PlaceInBreedingAsync(DcgoMatch match, HeadlessPlayerId owner, string tag, string CardType, int level, int dp)
{
    var cards = (CardDatabase)match.Context.CardRepository;
    var def = new HeadlessEntityId($"{tag}-def");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: CardType));
    var inst = new HeadlessEntityId(tag);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(inst, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, inst, ChoiceZone.None, ChoiceZone.BreedingArea));
}

static string DefType(DcgoMatch match, HeadlessEntityId instanceId) =>
    match.Context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? inst) && inst is not null
        && match.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
        ? def.CardType ?? "?" : "?";

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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
