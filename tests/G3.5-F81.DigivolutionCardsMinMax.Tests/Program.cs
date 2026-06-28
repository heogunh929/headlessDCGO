using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// F-8.1 (remaining): IsMin/MaxDigivolutionCards — the digivolution-source-count metric for the
// MinMax requirement helper (DP/Cost/Level already covered). "This Digimon has the most/fewest
// digivolution cards among your Digimon."

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Deep = new("p1:deep");    // 2 sources
HeadlessEntityId Shallow = new("p1:shallow"); // 0 sources

var definitions = new Dictionary<HeadlessEntityId, CardRecord>
{
    [new("D-DEEP")] = Digimon("D-DEEP"),
    [new("D-SHAL")] = Digimon("D-SHAL"),
};

var tests = new (string Name, Action Body)[]
{
    ("Deep stack matches MAX digivolution cards", () =>
        AssertTrue(MinMaxRequirementHelpers.IsMaxDigivolutionCards(State(), P1, Deep, definitions).IsMatch, "deep is max")),
    ("Shallow stack does NOT match MAX", () =>
        AssertFalse(MinMaxRequirementHelpers.IsMaxDigivolutionCards(State(), P1, Shallow, definitions).IsMatch, "shallow not max")),
    ("Shallow stack matches MIN digivolution cards", () =>
        AssertTrue(MinMaxRequirementHelpers.IsMinDigivolutionCards(State(), P1, Shallow, definitions).IsMatch, "shallow is min")),
    ("Deep stack does NOT match MIN", () =>
        AssertFalse(MinMaxRequirementHelpers.IsMinDigivolutionCards(State(), P1, Deep, definitions).IsMatch, "deep not min")),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

MatchState State()
{
    var p1 = new PlayerState(P1).WithZone(ChoiceZone.BattleArea, new[] { Deep, Shallow });
    var p2 = new PlayerState(P2);
    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [Deep] = new CardInstanceState(Deep, new HeadlessEntityId("D-DEEP"), P1,
            SourceIds: new[] { new HeadlessEntityId("s1"), new HeadlessEntityId("s2") }),
        [Shallow] = new CardInstanceState(Shallow, new HeadlessEntityId("D-SHAL"), P1),
    };
    return new MatchState(new[] { p1, p2 }, cards);
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
