using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-029): the fuller SelectCardConditionClass descriptor drives the same reveal-select mechanism
// (SimplifiedRevealAndSelectEffect via ToSimplified). TfxSelectCardCond reveals 3, adds 1 Tamer to hand,
// rest to deck bottom — proving the descriptor is wired to the working reveal-select.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("SelectCardConditionClass reveal-select: Tamer -> hand, others -> deck bottom", SelectViaFullDescriptor),
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

async Task SelectViaFullDescriptor()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxSelectCardCond");
    var tamer = await PlaceLibrary(context, P1, "TAMER", "Tamer");
    await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(tamer));
    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    var zones = (IZoneStateReader)context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.Hand).Contains(tamer), "selected Tamer went to hand");
    AssertEqual(2, zones.GetCards(P1, ChoiceZone.Library).Count, "the 2 others returned to the deck bottom");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 929);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

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

async Task<HeadlessEntityId> PlaceLibrary(EngineContext context, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:lib:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
