using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-5 DNA Digivolve (Jogress) / DigiXros: fuse several materials into one permanent. The new top is
// placed on the battle area; every material (and its existing sources) merges into the new top's stack;
// materials leave the field. Fused Digimon is not summoning sick. Opens WhenDigivolving.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("DNA Digivolve fuses two battle-area Digimon (with their stacks) under the new top", DnaFusion),
    ("Materials leave the battle area (become off-field sources)", MaterialsLeaveField),
    ("Fused Digimon is not summoning sick", NotSick),
    ("DigiXros fuses material cards from hand", DigiXros),
    ("Fusion opens WhenDigivolving", EmitsTiming),
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

async Task DnaFusion()
{
    EngineContext context = await DnaBoard();
    HeadlessEntityId dna = new("p1:hand:DNA");
    IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
        context.CardInstanceRepository, context.ZoneMover, dna, ChoiceZone.Hand,
        new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") }, ChoiceZone.BattleArea, context.GameEventQueue);

    AssertTrue(InZone(context, ChoiceZone.BattleArea, dna), "DNA card is the new top on the battle area");
    // A had source eggA, so merged order = A, eggA, B.
    AssertSequence(merged, new("p1:main:A"), new("egg:A"), new("p1:main:B"));
    AssertSources(context, dna, "p1:main:A", "egg:A", "p1:main:B");
}

async Task MaterialsLeaveField()
{
    EngineContext context = await DnaBoard();
    HeadlessEntityId dna = new("p1:hand:DNA");
    await FusionDigivolveHelpers.FuseAsync(context.CardInstanceRepository, context.ZoneMover, dna, ChoiceZone.Hand,
        new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") }, ChoiceZone.BattleArea, context.GameEventQueue);

    AssertFalse(InZone(context, ChoiceZone.BattleArea, new("p1:main:A")), "material A left the battle area");
    AssertFalse(InZone(context, ChoiceZone.BattleArea, new("p1:main:B")), "material B left the battle area");
}

async Task NotSick()
{
    EngineContext context = await DnaBoard();
    HeadlessEntityId dna = new("p1:hand:DNA");
    await FusionDigivolveHelpers.FuseAsync(context.CardInstanceRepository, context.ZoneMover, dna, ChoiceZone.Hand,
        new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") }, ChoiceZone.BattleArea, context.GameEventQueue);

    CardInstanceRecord top = Instance(context, dna);
    bool sick = top.Metadata.TryGetValue("enteredThisTurn", out object? raw) && raw is bool b && b;
    AssertFalse(sick, "fused Digimon is not summoning sick");
}

async Task DigiXros()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 14);
    HeadlessEntityId xros = new("p1:hand:XROS");
    HeadlessEntityId matX = new("p1:hand:MX");
    HeadlessEntityId matY = new("p1:hand:MY");
    await Place(context, xros, ChoiceZone.Hand, null);
    await Place(context, matX, ChoiceZone.Hand, null);
    await Place(context, matY, ChoiceZone.Hand, null);
    context.GameEventQueue.DrainPending();

    IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
        context.CardInstanceRepository, context.ZoneMover, xros, ChoiceZone.Hand,
        new[] { matX, matY }, ChoiceZone.Hand, context.GameEventQueue);

    AssertTrue(InZone(context, ChoiceZone.BattleArea, xros), "Xros Digimon on the battle area");
    AssertSequence(merged, matX, matY);
    AssertFalse(InZone(context, ChoiceZone.Hand, matX), "material X left the hand");
}

async Task EmitsTiming()
{
    EngineContext context = await DnaBoard();
    context.GameEventQueue.DrainPending();
    await FusionDigivolveHelpers.FuseAsync(context.CardInstanceRepository, context.ZoneMover, new("p1:hand:DNA"), ChoiceZone.Hand,
        new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") }, ChoiceZone.BattleArea, context.GameEventQueue);
    AssertTrue(QueueOpens(context, TriggerTimings.WhenDigivolving), "WhenDigivolving opened");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> DnaBoard()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 14);
    await Place(context, new("p1:hand:DNA"), ChoiceZone.Hand, null);
    // Material A carries an existing digivolution source (egg:A) so we can verify stack merging.
    await Place(context, new("p1:main:A"), ChoiceZone.BattleArea, new[] { "egg:A" });
    await Place(context, new("p1:main:B"), ChoiceZone.BattleArea, null);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new("egg:A"), new HeadlessEntityId("EGGA"), P1));
    context.GameEventQueue.DrainPending();
    return context;
}

async Task Place(EngineContext context, HeadlessEntityId id, ChoiceZone zone, string[]? sources)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
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

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(cardId);

bool QueueOpens(EngineContext context, string timing) =>
    context.GameEventQueue.DrainPending().Any(e => TriggerTimingMap.Derive(e).Contains(timing));

static void AssertSequence(IReadOnlyList<HeadlessEntityId> actual, params HeadlessEntityId[] expected)
{
    AssertEqual(expected.Length, actual.Count, "merged length");
    for (int i = 0; i < expected.Length; i++)
    {
        AssertEqual(expected[i], actual[i], $"merged[{i}]");
    }
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
