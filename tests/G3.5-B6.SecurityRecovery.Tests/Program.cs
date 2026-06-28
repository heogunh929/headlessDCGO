// B-6 effect-driven security operations: Recovery (top N library -> security; AS-IS IRecovery /
// IAddSecurityFromLibrary) and Trash Security (N security -> trash; AS-IS IDestroySecurity), exposed as
// player-scoped MatchStateMutationSink mutation kinds (Recover / TrashSecurity) so EFFECTS can drive them.
// The batch zone primitives already existed (IZoneMover.AddSecurityFromLibraryAsync / TrashSecurityAsync);
// B-6 wires the effect vocabulary + the OnDiscardSecurity timing for the trash.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Recover moves top library cards into security", RecoverMovesLibraryToSecurity),
    ("Trash Security moves security cards to the trash", TrashSecurityToTrash),
    ("Recover is capped by the library size", RecoverCappedByLibrary),
    ("A Recover mutation without a player is unsupported", RecoverMissingPlayerUnsupported),
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

async Task RecoverMovesLibraryToSecurity()
{
    EngineContext context = await NewMatch();
    int secBefore = Count(context, P1, ChoiceZone.Security);
    int libBefore = Count(context, P1, ChoiceZone.Library);

    await ApplyAsync(context, Mutation(MatchStateMutationSink.RecoverKind, P1, count: 2));

    AssertEqual(secBefore + 2, Count(context, P1, ChoiceZone.Security), "security grew by 2");
    AssertEqual(libBefore - 2, Count(context, P1, ChoiceZone.Library), "library shrank by 2");
}

async Task TrashSecurityToTrash()
{
    EngineContext context = await NewMatch();
    int secBefore = Count(context, P1, ChoiceZone.Security);
    int trashBefore = Count(context, P1, ChoiceZone.Trash);

    await ApplyAsync(context, Mutation(MatchStateMutationSink.TrashSecurityKind, P1, count: 1));

    AssertEqual(secBefore - 1, Count(context, P1, ChoiceZone.Security), "security shrank by 1");
    AssertEqual(trashBefore + 1, Count(context, P1, ChoiceZone.Trash), "trash grew by 1");
}

async Task RecoverCappedByLibrary()
{
    EngineContext context = await NewMatch();
    int libBefore = Count(context, P1, ChoiceZone.Library);
    int secBefore = Count(context, P1, ChoiceZone.Security);

    await ApplyAsync(context, Mutation(MatchStateMutationSink.RecoverKind, P1, count: libBefore + 5));

    AssertEqual(0, Count(context, P1, ChoiceZone.Library), "library emptied (recover capped by available)");
    AssertEqual(secBefore + libBefore, Count(context, P1, ChoiceZone.Security), "only available library cards recovered");
}

async Task RecoverMissingPlayerUnsupported()
{
    EngineContext context = await NewMatch();
    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.RecoverKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.CountKey] = 1 }));
    await sink.FlushAsync();

    AssertTrue(sink.Unsupported.Any(m => m.Kind == MatchStateMutationSink.RecoverKind), "missing player -> unsupported");
}

// --- Harness -------------------------------------------------------------

async Task<EngineContext> NewMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    return context;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);

async Task ApplyAsync(EngineContext context, EffectMutation mutation)
{
    var sink = Sink(context);
    sink.Apply(mutation);
    await sink.FlushAsync();
}

EffectMutation Mutation(string kind, HeadlessPlayerId player, int count) =>
    new(kind, new HeadlessEntityId("src"), new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.PlayerIdKey] = player,
        [MatchStateMutationSink.CountKey] = count,
    });

int Count(EngineContext context, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Count;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
