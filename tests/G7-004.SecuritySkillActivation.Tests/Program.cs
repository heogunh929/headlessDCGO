using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G7-004: a revealed security card's [Security] activated effect fires during the security check loop.
// ST1_13's [Security] gives all of the owner's Digimon Security Attack +1 — verified after the card is
// checked from security.

HeadlessPlayerId P1 = new(1); // attacker
HeadlessPlayerId P2 = new(2); // defender (security owner)

var tests = new (string Name, Func<Task> Body)[]
{
    ("Checking ST1_13 from security grants the owner's Digimon SA +1", SecuritySkillFires),
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

async Task SecuritySkillFires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 704);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var zones = (IZoneStateReader)context.ZoneMover;
    CardDatabase cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_13"), "ST1_13", "ST1_13", new Dictionary<string, object?>(), CardType: "Option"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("MYDIGI"), "MYDIGI", "MyDigi", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("ATK"), "ATK", "Atk", new Dictionary<string, object?>(), CardType: "Digimon"));

    var securityCard = new HeadlessEntityId("p2:sec:ST1_13");
    var defenderDigimon = new HeadlessEntityId("p2:battle:D");
    var attacker = new HeadlessEntityId("p1:battle:A");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(securityCard, new HeadlessEntityId("ST1_13"), P2));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(defenderDigimon, new HeadlessEntityId("MYDIGI"), P2));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(attacker, new HeadlessEntityId("ATK"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, securityCard, ChoiceZone.None, ChoiceZone.Security));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defenderDigimon, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.None, ChoiceZone.BattleArea));

    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, defenderDigimon, baseSecurityAttack: 1), "no buff before the check");

    await new SecurityResolver().RunSecurityCheckLoopAsync(context, zones, P1, attacker, P2, strike: 1);

    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, defenderDigimon, baseSecurityAttack: 1), "owner's Digimon got SA +1 from the security skill");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
