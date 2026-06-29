using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-004: AddActivateMainOptionSecurityEffect ("[Security] activate this card's [Main] effect") is no
// longer a stub — resolving the SecuritySkill re-runs the card's Option [Main] effect. For ST1_16 that
// deletes a chosen opponent Digimon, just like its Main skill.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_16 [Security] re-runs the Main effect: deletes the chosen opponent Digimon", SecurityReusesMain),
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

async Task SecurityReusesMain()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 804);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_16"), "ST1_16", "Gaia Force", new Dictionary<string, object?>(), CardType: "Option"));
    var option = new HeadlessEntityId("p1:trash:ST1_16");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(option, new HeadlessEntityId("ST1_16"), P1));

    var b1 = await Opponent(context, "B1");
    var b2 = await Opponent(context, "B2");

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(b1));

    int resolved = await ActivatedEffectResolver.ResolveAsync(context, option, P1, EffectTiming.SecuritySkill);
    AssertTrue(resolved >= 1, "the [Security] effect resolved the reused [Main] effect");

    AssertTrue(InZone(context, b1, ChoiceZone.Trash), "chosen B1 deleted by the reused Main effect");
    AssertTrue(InBattle(context, b2), "unchosen B2 untouched");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> Opponent(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p2:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, ChoiceZone.BattleArea).Contains(id);

bool InZone(EngineContext context, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
