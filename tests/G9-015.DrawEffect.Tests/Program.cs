using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// BT-PRE-A1 (G9-015): DrawEffect — the headless mirror of the original DrawClass ("draw N"). Resolved through
// the activation flow (ActivatedEffectResolver), it stages a DrawCards sink mutation that moves the top N of
// the controller's library to their hand. AS-IS guards: non-positive count is a no-op, and an empty / short
// library draws only what is available (min). The TfxDraw fixture's [Main] returns DrawEffect(2).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Draw 2 from a 5-card library: hand +2, library -2", DrawTwoFromFive),
    ("Draw 2 from a 1-card library: only 1 drawn (min guard)", DrawShortLibrary),
    ("Draw 2 from an empty library: no-op", DrawEmptyLibrary),
    ("DrawEffect(0) is a no-op (non-positive count guard)", DrawZeroNoOp),
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

async Task DrawTwoFromFive()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxDraw");
    await FillLibrary(context, P1, 5);

    AssertEqual(0, HandCount(context, P1), "hand starts empty");
    int resolved = await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(resolved > 0, "DrawEffect resolved");
    AssertEqual(2, HandCount(context, P1), "hand +2 after drawing 2");
    AssertEqual(3, LibraryCount(context, P1), "library -2 (5 -> 3)");
}

async Task DrawShortLibrary()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxDraw");
    await FillLibrary(context, P1, 1);

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(1, HandCount(context, P1), "only 1 drawn from a 1-card library (min guard)");
    AssertEqual(0, LibraryCount(context, P1), "library emptied");
}

async Task DrawEmptyLibrary()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxDraw");
    // no library cards

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(0, HandCount(context, P1), "nothing drawn from an empty library (no-op)");
}

async Task DrawZeroNoOp()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "ZERO");
    await FillLibrary(context, P1, 3);

    // DrawEffect(0).Apply must emit no mutation — assert via a direct sink flush.
    var card = new CardSource(context, src, P1);
    var sink = new HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    new DrawEffect(card, drawCount: 0, "Draw 0.").Apply(sink);
    await sink.FlushAsync();

    AssertEqual(0, HandCount(context, P1), "DrawEffect(0) drew nothing");
    AssertEqual(3, LibraryCount(context, P1), "library untouched by DrawEffect(0)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 915);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

// Place the source card (effectClass = card number, dispatch-discoverable) on the battle area so it does not
// inflate the hand count.
async Task<HeadlessEntityId> PlaceFixture(EngineContext context, HeadlessPlayerId owner, string cardNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task FillLibrary(EngineContext context, HeadlessPlayerId owner, int n)
{
    var cards = (CardDatabase)context.CardRepository;
    for (int i = 0; i < n; i++)
    {
        var defId = new HeadlessEntityId($"LIB:{owner.Value}:{i}");
        cards.Upsert(new CardRecord(defId, defId.Value, $"lib{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{owner.Value}:lib:{i}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    }
}

int HandCount(EngineContext context, HeadlessPlayerId p) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, ChoiceZone.Hand).Count;

int LibraryCount(EngineContext context, HeadlessPlayerId p) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, ChoiceZone.Library).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
