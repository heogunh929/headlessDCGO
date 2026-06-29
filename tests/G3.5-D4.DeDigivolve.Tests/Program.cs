using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-4 De-Digivolve: remove the top N cards of a Digimon's digivolution stack (AS-IS IDegeneration).
// Each step trashes the current top and promotes the immediate under-source to the new top. Stops at
// the rookie level floor (3), when sources run out, or count is reached; static immunity is honoured.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Top = new("p1:main:TOP");   // level 5
HeadlessEntityId Mid = new("p1:src:MID");    // level 4
HeadlessEntityId Egg = new("p1:src:EGG");    // level varies per test

var tests = new (string Name, Func<Task> Body)[]
{
    ("De-Digivolve 1 trashes the top and promotes the under-source", DeDigivolveOne),
    ("De-Digivolve 2 regresses two forms", DeDigivolveTwo),
    ("Rookie level floor stops further regression", LevelFloorStops),
    ("Immune card is not de-digivolved", ImmuneSkips),
    ("De-Digivolve opens WhenTopCardTrashed", EmitsTiming),
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

async Task DeDigivolveOne()
{
    EngineContext context = await Stack(eggLevel: 2);
    int removed = await DeDigivolveHelpers.DeDigivolveAsync(context.CardInstanceRepository, context.ZoneMover, Top, count: 1, context.GameEventQueue);

    AssertEqual(1, removed, "one card removed");
    AssertTrue(InZone(context, ChoiceZone.Trash, Top), "old top trashed");
    AssertTrue(InZone(context, ChoiceZone.BattleArea, Mid), "under-source promoted to top");
    AssertSources(context, Mid, Egg);
}

async Task DeDigivolveTwo()
{
    EngineContext context = await Stack(eggLevel: 2);
    int removed = await DeDigivolveHelpers.DeDigivolveAsync(context.CardInstanceRepository, context.ZoneMover, Top, count: 2, context.GameEventQueue);

    AssertEqual(2, removed, "two cards removed");
    AssertTrue(InZone(context, ChoiceZone.Trash, Top), "top trashed");
    AssertTrue(InZone(context, ChoiceZone.Trash, Mid), "mid trashed");
    AssertTrue(InZone(context, ChoiceZone.BattleArea, Egg), "egg is the new top");
    AssertNoSources(context, Egg);
}

async Task LevelFloorStops()
{
    EngineContext context = await Stack(eggLevel: 3); // egg is a rookie (level 3)
    int removed = await DeDigivolveHelpers.DeDigivolveAsync(context.CardInstanceRepository, context.ZoneMover, Top, count: 5, context.GameEventQueue);

    AssertEqual(2, removed, "stops at the rookie floor after removing TOP and MID");
    AssertTrue(InZone(context, ChoiceZone.BattleArea, Egg), "rookie egg survives as top");
    AssertFalse(InZone(context, ChoiceZone.Trash, Egg), "rookie not trashed");
}

async Task ImmuneSkips()
{
    EngineContext context = await Stack(eggLevel: 2, immuneTop: true);
    int removed = await DeDigivolveHelpers.DeDigivolveAsync(context.CardInstanceRepository, context.ZoneMover, Top, count: 2, context.GameEventQueue);

    AssertEqual(0, removed, "immune card not de-digivolved");
    AssertTrue(InZone(context, ChoiceZone.BattleArea, Top), "top untouched");
}

async Task EmitsTiming()
{
    EngineContext context = await Stack(eggLevel: 2);
    context.GameEventQueue.DrainPending();
    await DeDigivolveHelpers.DeDigivolveAsync(context.CardInstanceRepository, context.ZoneMover, Top, count: 1, context.GameEventQueue);
    AssertTrue(QueueOpens(context, TriggerTimings.WhenTopCardTrashed), "WhenTopCardTrashed opened");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Stack(int eggLevel, bool immuneTop = false)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 13);
    var topMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["sourceIds"] = new[] { Mid.Value, Egg.Value },
        ["level"] = 5,
    };
    if (immuneTop)
    {
        topMeta[DeDigivolveHelpers.CannotBeDeDigivolvedKey] = true;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Top, new HeadlessEntityId("TOP"), P1, Metadata: topMeta));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Mid, new HeadlessEntityId("MID"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Egg, new HeadlessEntityId("EGG"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = eggLevel }));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Top, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending();
    return context;
}

void AssertSources(EngineContext context, HeadlessEntityId host, params HeadlessEntityId[] expected)
{
    CardInstanceRecord r = Instance(context, host);
    var actual = r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s ? s.ToArray() : Array.Empty<string>();
    AssertEqual(expected.Length, actual.Length, "source count");
    for (int i = 0; i < expected.Length; i++)
    {
        AssertEqual(expected[i].Value, actual[i], $"source[{i}]");
    }
}

void AssertNoSources(EngineContext context, HeadlessEntityId host)
{
    CardInstanceRecord r = Instance(context, host);
    AssertFalse(r.Metadata.ContainsKey("sourceIds"), "no sources remain");
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(cardId);

bool QueueOpens(EngineContext context, string timing) =>
    context.GameEventQueue.DrainPending().Any(e => TriggerTimingMap.Derive(e).Contains(timing));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
