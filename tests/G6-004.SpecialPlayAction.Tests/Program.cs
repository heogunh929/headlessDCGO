using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G6-004: special plays (DigiXros / DNA / Blast) connect the D-5/D-6 fusion helpers to an executable
// action: materials become digivolution sources under the played top, the cost is paid, and the new top's
// ported effects auto-register (G6-001).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("DigiXros: materials fuse under the played top; cost paid; effects registered", DigiXros),
    ("Blast: single target becomes a source under the played top", Blast),
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

async Task DigiXros()
{
    EngineContext context = Board();
    HeadlessEntityId top = HandCard(context, "ST7_10");          // ported effect (SA +1 / Pierce)
    HeadlessEntityId m1 = BattleCard(context, P1, "M1");
    HeadlessEntityId m2 = BattleCard(context, P1, "M2");
    context.MemoryController.Set(5);

    LegalAction action = SpecialPlayAction.Create(P1, top, new[] { m1, m2 }, memoryCost: 2, SpecialPlayKind.DigiXros);
    ActionProcessResult result = await new SpecialPlayAction().ProcessAsync(action, context);
    AssertTrue(result.IsSuccess, $"DigiXros resolved ({result.Message})");

    AssertTrue(InBattle(context, P1, top), "top is on the battle area");
    AssertTrue(!InBattle(context, P1, m1) && !InBattle(context, P1, m2), "materials left the battle area (now sources)");

    DigivolutionStack stack = DigivolutionStackReader.Read(context.CardInstanceRepository, context.CardRepository, top);
    var sources = stack.UnderCards.Select(u => u.InstanceId).ToHashSet();
    AssertTrue(sources.Contains(m1) && sources.Contains(m2), "both materials are digivolution sources of the top");

    AssertEqual(3, context.MemoryController.Current.Current, "memory 5 - 2 = 3");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Piercing").Count >= 1, "top's effects auto-registered (Piercing)");
}

async Task Blast()
{
    EngineContext context = Board();
    HeadlessEntityId top = HandCard(context, "ST1_03");
    HeadlessEntityId target = BattleCard(context, P1, "T1");
    context.MemoryController.Set(5);

    LegalAction action = SpecialPlayAction.Create(P1, top, new[] { target }, memoryCost: 0, SpecialPlayKind.Blast);
    ActionProcessResult result = await new SpecialPlayAction().ProcessAsync(action, context);
    AssertTrue(result.IsSuccess, $"Blast resolved ({result.Message})");

    AssertTrue(InBattle(context, P1, top), "top is on the battle area");
    DigivolutionStack stack = DigivolutionStackReader.Read(context.CardInstanceRepository, context.CardRepository, top);
    AssertTrue(stack.UnderCards.Any(u => u.InstanceId == target), "target became a source under the top");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 604);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

HeadlessEntityId HandCard(EngineContext context, string cardNumber)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId(cardNumber), cardNumber, cardNumber, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId BattleCard(EngineContext context, HeadlessPlayerId owner, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

bool InBattle(EngineContext context, HeadlessPlayerId player, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.BattleArea).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
