using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-10: effect-driven trash / return of a Digimon's digivolution SOURCES, and trash of its LINK cards.
// New sink mutation kinds delegate to DigivolutionStackHelpers / LinkHelpers.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Host = new("p1:main:HOST");
HeadlessEntityId Mid = new("src:MID");
HeadlessEntityId Egg = new("src:EGG");
HeadlessEntityId Link1 = new("link:L1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("TrashDigivolutionCards trashes the bottom source and drops it from the stack", TrashSource),
    ("ReturnDigivolutionCards returns the bottom source to hand", ReturnSource),
    ("TrashLinkCards trashes the host's link cards", TrashLink),
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

async Task TrashSource()
{
    EngineContext context = await Stack();
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Mut(MatchStateMutationSink.TrashDigivolutionCardsKind, Host, count: 1));
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Trash, Egg), "deepest source (egg) trashed");
    AssertSources(context, Host, "src:MID");
}

async Task ReturnSource()
{
    EngineContext context = await Stack();
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Mut(MatchStateMutationSink.ReturnDigivolutionCardsKind, Host, count: 1));
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Hand, Egg), "deepest source returned to hand");
    AssertSources(context, Host, "src:MID");
}

async Task TrashLink()
{
    EngineContext context = await Stack(withLink: true);
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Mut(MatchStateMutationSink.TrashLinkCardsKind, Host, count: 0)); // count omitted => all
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Trash, Link1), "link card trashed");
    CardInstanceRecord host = Instance(context, Host);
    AssertEqual(0, LinkHelpers.ReadLinkedCardIds(host.Metadata).Count, "host has no link cards left");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);

EffectMutation Mut(string kind, HeadlessEntityId target, int count)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value };
    if (count > 0)
    {
        values[MatchStateMutationSink.CountKey] = count;
    }

    return new EffectMutation(kind, new HeadlessEntityId("src"), values);
}

async Task<EngineContext> Stack(bool withLink = false)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 17);
    var hostMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["sourceIds"] = new[] { Mid.Value, Egg.Value },
    };
    if (withLink)
    {
        hostMeta[LinkHelpers.LinkedCardIdsKey] = new[] { Link1.Value };
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Host, new HeadlessEntityId("HOST"), P1, Metadata: hostMeta));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Mid, new HeadlessEntityId("MID"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Egg, new HeadlessEntityId("EGG"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Link1, new HeadlessEntityId("L1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Host, ChoiceZone.None, ChoiceZone.BattleArea));
    return context;
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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
