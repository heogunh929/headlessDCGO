using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// B-5 / F-2.2 / F-2.4: SelectCardEffect (the card-zone selection sibling of SelectPermanentEffect). It
// enumerates a player's cards in a Root zone (Hand/Library/Trash/...) and maps the Mode to a mutation:
// Discard = trash (hand discard, deck mill), AddHand = return to hand (trash recovery).

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("RootZone maps the Root enum to the matching ChoiceZone", RootMapping),
    ("BuildRequest enumerates the Root zone filtered by the predicate", BuildRequestFromHand),
    ("Discard mode trashes selected hand cards (hand discard)", DiscardFromHand),
    ("Discard mode over Library trashes a deck card (mill)", MillFromLibrary),
    ("AddHand mode returns a selected trash card to hand (recovery)", RecoverFromTrash),
    ("PlayForFree mode plays a hand card to the battle area (F-3.7)", PlayForFreePlaysToField),
    ("PlayForCost mode is not yet supported", PlayForCostUnsupported),
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

Task RootMapping()
{
    AssertEqual(ChoiceZone.Hand, RootZoneOf(SelectCardEffect.Root.Hand), "Hand");
    AssertEqual(ChoiceZone.Library, RootZoneOf(SelectCardEffect.Root.Library), "Library");
    AssertEqual(ChoiceZone.Trash, RootZoneOf(SelectCardEffect.Root.Trash), "Trash");
    AssertEqual(ChoiceZone.Security, RootZoneOf(SelectCardEffect.Root.Security), "Security");
    return Task.CompletedTask;

    ChoiceZone RootZoneOf(SelectCardEffect.Root root)
    {
        var sel = new SelectCardEffect();
        sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.Custom, root, new HeadlessEntityId("src"));
        return sel.RootZone;
    }
}

async Task BuildRequestFromHand()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 3);
    await Place(context, new HeadlessEntityId("h1"), ChoiceZone.Hand);
    await Place(context, new HeadlessEntityId("h2"), ChoiceZone.Hand);
    await Place(context, new HeadlessEntityId("keep"), ChoiceZone.Hand);

    var sel = new SelectCardEffect();
    sel.SetUp(P1, id => id.Value.StartsWith("h", StringComparison.Ordinal), maxCount: 2,
        canNoSelect: false, canEndNotMax: false, SelectCardEffect.Mode.Discard, SelectCardEffect.Root.Hand, new HeadlessEntityId("src"));

    ChoiceRequest request = sel.BuildRequest(Zones(context));
    AssertEqual(2, request.Candidates.Count, "only h1/h2 match the predicate");
    AssertEqual(ChoiceZone.Hand, request.SourceZone, "source zone is Hand");
    AssertEqual(2, request.MinCount, "exact pick of 2");
    AssertFalse(request.CanSkip, "not skippable");
}

async Task DiscardFromHand()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 4);
    HeadlessEntityId card = new("h1");
    await Place(context, card, ChoiceZone.Hand);

    var sel = new SelectCardEffect();
    sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.Discard, SelectCardEffect.Root.Hand, new HeadlessEntityId("src"));
    ChoiceRequest request = sel.BuildRequest(Zones(context));
    var provider = new ScriptedChoiceProvider();
    provider.Enqueue(ChoiceResult.Select(card));
    ChoiceResult result = await provider.ChooseAsync(request);

    MatchStateMutationSink sink = Sink(context);
    sel.Apply(sink, result.SelectedIds);
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Trash, card), "discarded card moved to trash");
    AssertFalse(InZone(context, ChoiceZone.Hand, card), "card left the hand");
}

async Task MillFromLibrary()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 6);
    HeadlessEntityId top = new("d1");
    await Place(context, top, ChoiceZone.Library);

    var sel = new SelectCardEffect();
    sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.Discard, SelectCardEffect.Root.Library, new HeadlessEntityId("src"));
    MatchStateMutationSink sink = Sink(context);
    sel.Apply(sink, new[] { top });
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Trash, top), "milled card moved to trash");
}

async Task RecoverFromTrash()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    HeadlessEntityId card = new("t1");
    await Place(context, card, ChoiceZone.Trash);

    var sel = new SelectCardEffect();
    sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.AddHand, SelectCardEffect.Root.Trash, new HeadlessEntityId("src"));
    MatchStateMutationSink sink = Sink(context);
    sel.Apply(sink, new[] { card });
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.Hand, card), "recovered card moved to hand");
    AssertFalse(InZone(context, ChoiceZone.Trash, card), "card left the trash");
}

async Task PlayForFreePlaysToField()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 8);
    HeadlessEntityId card = new("h1");
    await Place(context, card, ChoiceZone.Hand);

    var sel = new SelectCardEffect();
    sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.PlayForFree, SelectCardEffect.Root.Hand, new HeadlessEntityId("src"));
    MatchStateMutationSink sink = Sink(context);
    sel.Apply(sink, new[] { card });
    await sink.FlushAsync();

    AssertTrue(InZone(context, ChoiceZone.BattleArea, card), "played card moved to battle area");
    AssertFalse(InZone(context, ChoiceZone.Hand, card), "card left the hand");
    AssertTrue(ReadFlag(context, card, MatchStateMutationSink.EnteredThisTurnKey), "marked entered this turn (summoning sickness)");
}

Task PlayForCostUnsupported()
{
    var sel = new SelectCardEffect();
    sel.SetUp(P1, _ => true, 1, false, false, SelectCardEffect.Mode.PlayForCost, SelectCardEffect.Root.Hand, new HeadlessEntityId("src"));
    bool threw = false;
    try { sel.BuildMutations(new[] { new HeadlessEntityId("h1") }); }
    catch (NotSupportedException) { threw = true; }
    AssertTrue(threw, "PlayForCost throws NotSupported (D-8 cost pipeline pending)");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

IZoneStateReader Zones(EngineContext context) => (IZoneStateReader)context.ZoneMover;

async Task Place(EngineContext context, HeadlessEntityId id, ChoiceZone zone)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(id.Value), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
}

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
