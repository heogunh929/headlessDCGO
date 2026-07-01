using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// PRIM-W2 (G9-030): App Fusion is now a SpecialPlayKind — mechanically the "named materials fuse under the
// new top" fusion (routed through the DigiXros fusion path). The recipe (material names + cost) is
// data-driven via SpecialPlayRecipeRegistry, like DigiXros. This drives the fusion directly.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("App Fusion: named materials fuse under the new top, cost paid", AppFusionFuses),
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

async Task AppFusionFuses()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var top = await Hand(context, "APPTOP");
    var m1 = await Battle(context, P1, "M1");
    var m2 = await Battle(context, P1, "M2");

    LegalAction action = SpecialPlayAction.Create(P1, top, new[] { m1, m2 }, memoryCost: 2, SpecialPlayKind.AppFusion);
    ActionProcessResult result = await new SpecialPlayAction().ProcessAsync(action, context);

    AssertTrue(result.IsSuccess, $"App Fusion resolved ({result.Message})");
    AssertTrue(InBattle(context, top), "the App Fusion top is on the battle area");
    AssertTrue(!InBattle(context, m1) && !InBattle(context, m2), "materials left the battle area (now sources)");

    DigivolutionStack stack = DigivolutionStackReader.Read(context.CardInstanceRepository, context.CardRepository, top);
    var sources = stack.UnderCards.Select(u => u.InstanceId).ToHashSet();
    AssertTrue(sources.Contains(m1) && sources.Contains(m2), "both materials are digivolution sources of the top");
    AssertEqual(3, context.MemoryController.Current.Current, "memory 5 - 2 = 3");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 930);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Hand(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task<HeadlessEntityId> Battle(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
