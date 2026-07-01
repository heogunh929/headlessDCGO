using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// BT-PRE-A5 (G9-019): PlayCardEffect — the headless mirror of the original PlayCardClass simple case
// (payCost:false, root:Library): play a pre-selected card onto the battle area at no cost. Resolved through
// the activation flow (staged PlayCard sink mutation). TfxPlayCard plays the top library card cost-free.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Play the top library card cost-free: it enters the battle area, memory unchanged", PlayFromLibraryFree),
    ("No target (empty library) is a no-op", EmptyLibraryNoOp),
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

async Task PlayFromLibraryFree()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    var src = await PlaceFixture(context, P1);
    var target = await PlaceZone(context, P1, "TARGET", ChoiceZone.Library, playCost: 5);

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, target), "the library card was played onto the battle area");
    AssertTrue(!InZone(context, P1, ChoiceZone.Library, target), "the card left the library");
    AssertEqual(0, context.MemoryController.Current.Current, "no cost paid (play cost 5 ignored — payCost:false)");
}

async Task EmptyLibraryNoOp()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1);
    // no library cards

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(0, Count(context, P1, ChoiceZone.BattleArea) - 1, "battle area holds only the source fixture (nothing played)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 919);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixture(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TfxPlayCard");
    cards.Upsert(new CardRecord(defId, "TfxPlayCard", "TfxPlayCard", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:TfxPlayCard");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceZone(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

int Count(EngineContext context, HeadlessPlayerId p, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
