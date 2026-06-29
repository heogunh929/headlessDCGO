using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Memory-cost affordability (AS-IS Player.MaxMemoryCost = |-10 - Memory|): a play is allowed only if it
// does not push memory below the -10 floor. At memory 1 the max payable cost is 11 (→ -10); a 12-cost
// play is blocked. Verifies CanPay + PlayCardAction validation + legal-action filtering.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CanPay: memory 1 allows cost 11 (→ -10), blocks cost 12 (→ -11)", () => Pure(CanPayBoundary)),
    ("PlayCard cost 12 at memory 1 is illegal", () => PlayBlocked(12)),
    ("PlayCard cost 11 at memory 1 is legal (memory → -10)", () => PlayAllowed(11)),
    ("Legal actions at memory 1 do not offer the 12-cost play", NotOfferedInLegalActions),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void CanPayBoundary()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1);
    context.MemoryController.Set(1);
    AssertTrue(context.MemoryController.CanPay(11), "cost 11 payable at memory 1");
    AssertFalse(context.MemoryController.CanPay(12), "cost 12 NOT payable at memory 1");
}

async Task PlayBlocked(int cost)
{
    (EngineContext context, HeadlessEntityId card) = await Setup(cost);
    context.MemoryController.Set(1);
    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, card, cost), context);
    AssertTrue(result.IsIllegal, $"cost {cost} at memory 1 is illegal");
    AssertFalse(InZone(context, ChoiceZone.BattleArea, card), "card not played");
}

async Task PlayAllowed(int cost)
{
    (EngineContext context, HeadlessEntityId card) = await Setup(cost);
    context.MemoryController.Set(1);
    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, card, cost), context);
    AssertTrue(result.IsSuccess, $"cost {cost} at memory 1 is legal ({result.Message})");
    AssertEqual(-10, context.MemoryController.Current.Current, "memory dropped to the -10 floor");
}

async Task NotOfferedInLegalActions()
{
    (EngineContext context, HeadlessEntityId card) = await Setup(12);
    context.MemoryController.Set(1);
    bool offered = new PlayCardAction()
        .GetLegalActions(context, P1)
        .Any(a => a.ActionType == HeadlessActionTypes.PlayCard
            && ReadString(a.Parameters, HeadlessActionParameterKeys.CardId) == card.Value);
    AssertFalse(offered, "12-cost play not offered as a legal action at memory 1");
}

// --- Helpers -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId)> Setup(int cost)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 2);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("BIG"), "BIG", "Expensive", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: cost));
    HeadlessEntityId card = new("p1:hand:BIG");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("BIG"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.Hand));
    return (context, card);
}

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(cardId);

static string? ReadString(IReadOnlyDictionary<string, object?> p, string key) =>
    p.TryGetValue(key, out object? raw) ? raw?.ToString() : null;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
