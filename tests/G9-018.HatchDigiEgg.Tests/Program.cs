using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// BT-PRE-A4 (G9-018): HatchDigiEggEffect — the headless mirror of the original HatchDigiEggClass ("hatch 1
// digi-egg into the breeding area, if CanHatch"). Resolved through the activation flow. AS-IS CanHatch = an
// empty breeding area AND an available digi-egg; violating either is a no-op.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Hatch with an egg + empty breeding: top digi-egg enters the breeding area", HatchIntoEmptyBreeding),
    ("No hatch when the breeding area is already occupied (CanHatch guard)", NoHatchWhenBreedingOccupied),
    ("No hatch when the digitama library is empty", NoHatchWhenNoEgg),
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

async Task HatchIntoEmptyBreeding()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1);
    var egg = await PlaceZone(context, P1, "EGG", ChoiceZone.DigitamaLibrary);

    AssertEqual(0, Count(context, P1, ChoiceZone.BreedingArea), "breeding area starts empty");
    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.BreedingArea, egg), "the digi-egg hatched into the breeding area");
    AssertEqual(0, Count(context, P1, ChoiceZone.DigitamaLibrary), "the egg left the digitama library");
}

async Task NoHatchWhenBreedingOccupied()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1);
    var egg = await PlaceZone(context, P1, "EGG", ChoiceZone.DigitamaLibrary);
    await PlaceZone(context, P1, "OCCUPANT", ChoiceZone.BreedingArea);

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(!InZone(context, P1, ChoiceZone.BreedingArea, egg), "no hatch — the egg stayed in the digitama library");
    AssertEqual(1, Count(context, P1, ChoiceZone.DigitamaLibrary), "digitama library unchanged");
    AssertEqual(1, Count(context, P1, ChoiceZone.BreedingArea), "breeding area still holds only the original occupant");
}

async Task NoHatchWhenNoEgg()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1);
    // no digi-egg

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(0, Count(context, P1, ChoiceZone.BreedingArea), "no hatch — breeding area stays empty");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 918);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixture(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TfxHatch");
    cards.Upsert(new CardRecord(defId, "TfxHatch", "TfxHatch", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:TfxHatch");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceZone(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
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
