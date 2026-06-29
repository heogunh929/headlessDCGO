using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-6 Blast / Arts Digivolve: a costless single-target digivolve (AS-IS payCost: false). The card moves
// onto the target as a digivolve (target + its sources become the new stack), NO memory is paid, and the
// result inherits the target's summoning-sickness state (a normal digivolve keeps the permanent's field
// time, unlike a Jogress). Opens WhenDigivolving.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Card = new("p1:hand:BLAST");
HeadlessEntityId Target = new("p1:main:T");
HeadlessEntityId EggT = new("egg:T");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Free digivolve attaches the card on top, target becomes a source, no cost paid", FreeDigivolve),
    ("Result inherits the target's summoning sickness (true)", InheritsSickTrue),
    ("Result inherits the target's summoning sickness (false)", InheritsSickFalse),
    ("Free digivolve opens WhenDigivolving", EmitsTiming),
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

async Task FreeDigivolve()
{
    EngineContext context = await Board(targetSick: false);
    context.MemoryController.Set(5);

    bool ok = await FreeDigivolveHelpers.DigivolveFreeAsync(context.CardInstanceRepository, context.ZoneMover, Card, Target, ChoiceZone.Hand, context.GameEventQueue);
    AssertTrue(ok, "free digivolve performed");

    AssertTrue(InZone(context, ChoiceZone.BattleArea, Card), "Blast card is the new top");
    AssertFalse(InZone(context, ChoiceZone.BattleArea, Target), "target left the battle area (now a source)");
    AssertFalse(InZone(context, ChoiceZone.Hand, Card), "card left the hand");
    AssertSources(context, Card, "p1:main:T", "egg:T");
    AssertEqual(5, context.MemoryController.Current.Current, "no memory paid (free)");
}

async Task InheritsSickTrue()
{
    EngineContext context = await Board(targetSick: true);
    await FreeDigivolveHelpers.DigivolveFreeAsync(context.CardInstanceRepository, context.ZoneMover, Card, Target, ChoiceZone.Hand, context.GameEventQueue);
    AssertTrue(Sick(context, Card), "result is sick because the target was");
}

async Task InheritsSickFalse()
{
    EngineContext context = await Board(targetSick: false);
    await FreeDigivolveHelpers.DigivolveFreeAsync(context.CardInstanceRepository, context.ZoneMover, Card, Target, ChoiceZone.Hand, context.GameEventQueue);
    AssertFalse(Sick(context, Card), "result is not sick because the target was established");
}

async Task EmitsTiming()
{
    EngineContext context = await Board(targetSick: false);
    context.GameEventQueue.DrainPending();
    await FreeDigivolveHelpers.DigivolveFreeAsync(context.CardInstanceRepository, context.ZoneMover, Card, Target, ChoiceZone.Hand, context.GameEventQueue);
    AssertTrue(QueueOpens(context, TriggerTimings.WhenDigivolving), "WhenDigivolving opened");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Board(bool targetSick)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 15);
    await Place(context, Card, ChoiceZone.Hand, sources: null, sick: false);
    await Place(context, Target, ChoiceZone.BattleArea, sources: new[] { EggT.Value }, sick: targetSick);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(EggT, new HeadlessEntityId("EGGT"), P1));
    context.GameEventQueue.DrainPending();
    return context;
}

async Task Place(EngineContext context, HeadlessEntityId id, ChoiceZone zone, string[]? sources, bool sick)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["enteredThisTurn"] = sick };
    if (sources is not null)
    {
        meta["sourceIds"] = sources;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(id.Value), P1, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

void AssertSources(EngineContext context, HeadlessEntityId host, params string[] expected)
{
    CardInstanceRecord r = Instance(context, host);
    var actual = r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s ? s.ToArray() : Array.Empty<string>();
    AssertEqual(expected.Length, actual.Length, "source count");
    for (int i = 0; i < expected.Length; i++)
    {
        AssertEqual(expected[i], actual[i], $"source[{i}]");
    }
}

bool Sick(EngineContext context, HeadlessEntityId id) =>
    Instance(context, id).Metadata.TryGetValue("enteredThisTurn", out object? raw) && raw is bool b && b;

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
