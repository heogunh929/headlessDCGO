using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W1-1 (G9-020): the original AddSelfDigivolutionRequirementStaticEffect declares a card's printed
// digivolution requirement (from Color@Level, cost) in EFFECT code. In headless this is SUBSUMED by the
// data-driven path: the card-data loader encodes evolutionConditions into CardRecord.EvolutionCondition
// ("Color@Level(:Cost)"), and DigivolveAction.MatchesEvolutionCondition gates on it. So no card-facing
// primitive is needed — a ported Digimon's requirement lives in its JSON. This test LOCKS that subsumption:
// digivolving is legal only from a target matching the printed Color@Level requirement.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Digivolve from a matching Red Lv4 target is legal (requirement satisfied)", MatchingTargetLegal),
    ("Digivolve from a wrong-color (Blue) target is illegal", WrongColorIllegal),
    ("Digivolve from a wrong-level (Red Lv3) target is illegal", WrongLevelIllegal),
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

async Task MatchingTargetLegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE", color: "Red", level: 4);
    var evo = await PlaceEvolve(context, "EVO", requirement: "Red@4", cost: 2);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"digivolve legal from matching Red Lv4 ({result.Message})");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evo), "the evolving card became the new top");
}

async Task WrongColorIllegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "BLUEBASE", color: "Blue", level: 4);
    var evo = await PlaceEvolve(context, "EVO", requirement: "Red@4", cost: 2);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "digivolve illegal from a Blue target (color requirement not met)");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, evo), "the evolving card did NOT enter play");
}

async Task WrongLevelIllegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "RED3", color: "Red", level: 3);
    var evo = await PlaceEvolve(context, "EVO", requirement: "Red@4", cost: 2);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "digivolve illegal from a Red Lv3 target (level requirement not met)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 920);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, string tag, string color, int level)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color }, ["level"] = level, ["dp"] = 3000 },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// Evolving card in hand: EvolutionCondition = printed "Color@Level" requirement (as the data loader encodes
// evolutionConditions), fixedDigivolutionCost = the cost.
async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, string tag, string requirement, int cost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = cost },
        CardType: "Digimon", EvolutionCondition: requirement));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = cost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
