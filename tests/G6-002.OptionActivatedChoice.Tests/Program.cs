using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G6-002: Option [Main] activated effects resolve their choice through the engine's IChoiceProvider via
// OptionActivateAction (no manual BuildRequest in the test). Driver answers are supplied by the context's
// ScriptedChoiceProvider — the same seam an RL driver uses.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_16: activating the Option deletes the chosen opponent Digimon", ST1_16_DeleteViaActivation),
    ("ST1_13: activating the Option gives the chosen own Digimon +3000 DP", ST1_13_BuffViaActivation),
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

async Task ST1_16_DeleteViaActivation()
{
    EngineContext context = Board();
    var b1 = new HeadlessEntityId("p2:battle:B1");
    var b2 = new HeadlessEntityId("p2:battle:B2");
    await Place(context, P2, b1, "Digimon", dp: 3000);
    await Place(context, P2, b2, "Digimon", dp: 3000);
    HeadlessEntityId option = await Hand(context, P1, "ST1_16");

    Scripted(context).Enqueue(ChoiceResult.Select(b1));
    await Activate(context, P1, option);

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, b1), "chosen B1 trashed");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, b2), "unchosen B2 untouched");
}

async Task ST1_13_BuffViaActivation()
{
    EngineContext context = Board();
    var mine = new HeadlessEntityId("p1:battle:D");
    await Place(context, P1, mine, "Digimon", dp: 2000);
    HeadlessEntityId option = await Hand(context, P1, "ST1_13");

    Scripted(context).Enqueue(ChoiceResult.Select(mine));
    await Activate(context, P1, option);

    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "chosen Digimon +3000 DP");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 602);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    return context;
}

ScriptedChoiceProvider Scripted(EngineContext context) => (ScriptedChoiceProvider)context.ChoiceProvider;

async Task<HeadlessEntityId> Hand(EngineContext context, HeadlessPlayerId owner, string cardNumber)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId(cardNumber), cardNumber, cardNumber, new Dictionary<string, object?>(), CardType: "Option", PlayCost: 0));
    var id = new HeadlessEntityId($"p1:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task Place(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, string type, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: type));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

async Task Activate(EngineContext context, HeadlessPlayerId player, HeadlessEntityId option)
{
    var optionAction = new OptionActivateAction();
    LegalAction action = optionAction.GetLegalActions(context, player)
        .Single(a => ReadString(a.Parameters, HeadlessActionParameterKeys.CardId) == option.Value);
    ActionProcessResult result = await optionAction.ProcessAsync(action, context);
    AssertTrue(result.IsSuccess, $"option activated ({result.Message})");
}

static string? ReadString(IReadOnlyDictionary<string, object?> p, string key) =>
    p.TryGetValue(key, out object? raw) ? raw?.ToString() : null;

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
