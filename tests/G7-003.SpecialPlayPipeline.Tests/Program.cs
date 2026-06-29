using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G7-003: SpecialPlay is now a first-class pipeline action — MetadataActionProcessor routes it (not only
// direct ProcessAsync), and SpecialPlayAction.GetLegalActions is wired into the dispatcher (recipe-gated).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MetadataActionProcessor routes a SpecialPlay (DigiXros) action", RoutedThroughProcessor),
    ("SpecialPlayAction.GetLegalActions is callable (recipe-gated -> empty)", GetLegalActionsCallable),
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

async Task RoutedThroughProcessor()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 703);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    HeadlessEntityId top = Hand(context, "XROSTOP");
    HeadlessEntityId m1 = Battle(context, "M1");
    HeadlessEntityId m2 = Battle(context, "M2");

    LegalAction action = SpecialPlayAction.Create(P1, top, new[] { m1, m2 }, memoryCost: 0, SpecialPlayKind.DigiXros);
    AssertEqual(HeadlessActionTypes.SpecialPlay, action.ActionType, "action type is SpecialPlay");

    // Route through the generic action processor (the agent pipeline), not SpecialPlayAction directly.
    ActionProcessResult result = await new MetadataActionProcessor().ProcessAsync(action, context);
    AssertTrue(result.IsSuccess, $"processor routed + executed SpecialPlay ({result.Message})");

    AssertTrue(InBattle(context, top), "fused top on battle area");
    DigivolutionStack stack = DigivolutionStackReader.Read(context.CardInstanceRepository, context.CardRepository, top);
    var sources = stack.UnderCards.Select(u => u.InstanceId).ToHashSet();
    AssertTrue(sources.Contains(m1) && sources.Contains(m2), "materials fused as sources");
}

void GetLegalActionsCallableSync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7031);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    IReadOnlyList<LegalAction> actions = new SpecialPlayAction().GetLegalActions(context, P1);
    AssertEqual(0, actions.Count, "recipe-gated: empty until recipe data exists");
}

Task GetLegalActionsCallable() { GetLegalActionsCallableSync(); return Task.CompletedTask; }

// --- Helpers -------------------------------------------------------------

HeadlessEntityId Hand(EngineContext context, string tag)
{
    var (id, def) = Def(context, tag);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Battle(EngineContext context, string tag)
{
    var (id, def) = Def(context, tag);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

(HeadlessEntityId id, HeadlessEntityId def) Def(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    return (new HeadlessEntityId($"p1:zone:{tag}"), def);
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
