using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-044): AddSelfDigivolutionRequirementStaticEffect — a predicate-based alternative digivolution
// source. The printed requirement (Red@4) fails against a Blue Lv5 target, but the added predicate
// (p => p.Level == 5, honored by the new view layer) makes the digivolve legal. Controls: no effect / a
// non-matching predicate stay illegal.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Added predicate (Level==5) makes an off-color target legal", AddedPredicateLegal),
    ("Without the added source, the off-color target is illegal (control)", NoEffectIllegal),
    ("A non-matching predicate (Level==6) stays illegal (control)", NonMatchingIllegal),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task AddedPredicateLegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    Register(ctx, evo, p => p.Level == 5);

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(result.IsSuccess, $"legal via added predicate ({result.Message})");
    AssertTrue(InZone(ctx, P1, evo), "evolving card became the new top");
}

async Task NoEffectIllegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(!result.IsSuccess, "illegal without the added source");
}

async Task NonMatchingIllegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    Register(ctx, evo, p => p.Level == 6); // does not match a Lv5 target

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(!result.IsSuccess, "illegal when the predicate does not match");
}

// --- Helpers -------------------------------------------------------------

void Register(EngineContext ctx, HeadlessEntityId evo, Func<Permanent, bool> predicate) =>
    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: predicate, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
        card: new CardSource(ctx, evo, P1), condition: null).ToBinding($"asdr:{evo.Value}"));

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 944);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext ctx, string tag, string color, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color }, ["level"] = level, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext ctx, string tag, string requirement, int cost)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 6, ["fixedDigivolutionCost"] = cost },
        CardType: "Digimon", EvolutionCondition: requirement));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = cost }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext ctx, HeadlessPlayerId p, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
