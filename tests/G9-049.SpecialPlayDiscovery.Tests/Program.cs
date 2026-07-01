using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-049): end-to-end special-play DISCOVERY. A ported DigiXros card declares its recipe inside its
// effect code (which only runs on-play), yet SpecialPlayAction.GetLegalActions must offer the play while the
// card is still in HAND. This verifies the on-demand recipe registration fix: the DigiXros is enumerated as a
// legal action from hand, WITHOUT anyone pre-registering the recipe.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Hand DigiXros card with materials present -> offered as a legal special play", Discovered),
    ("No materials -> not offered (control)", NoMaterials),
    ("Arbitrary material predicate (Level==3, not a name) is evaluated 1:1", ArbitraryPredicate),
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

async Task Discovered()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var xros = await Hand(ctx, "XROS", "TfxDigiXros");   // card number = fixture class name
    await Battle(ctx, "M1", "MatA");
    await Battle(ctx, "M2", "MatB");

    var actions = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(actions.Count >= 1, $"a special play is offered from hand (got {actions.Count})");
    _ = xros;
}

async Task NoMaterials()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    await Hand(ctx, "XROS", "TfxDigiXros");
    await Battle(ctx, "M1", "WrongName");

    var actions = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(actions.Count == 0, $"no special play without matching materials (got {actions.Count})");
}

async Task ArbitraryPredicate()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var xros = await Hand(ctx, "XROS", "SomeXrosCard");
    // Register a recipe with an ARBITRARY predicate (any Lv3 material — NOT a card name). This mirrors the
    // original CanSelectCardCondition. Direct registration exercises the predicate path in TryMatchMaterials.
    SpecialPlayRecipeRegistry.Register("SomeXrosCard", new SpecialPlayRecipe(
        SpecialPlayKind.DigiXros,
        new[] { new SpecialPlayMaterial(cs => cs.Level == 3, "any Lv3") },
        MemoryCost: 0));

    await BattleLevel(ctx, "M1", "IrrelevantName", level: 3);   // matches by predicate (Lv3), wrong name
    var offered = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(offered.Count >= 1, "Lv3 material matches the arbitrary predicate (name irrelevant)");

    // A Lv4 material must NOT satisfy the same predicate.
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx2 = Ctx();
    ctx2.MemoryController.Set(5);
    await Place2(ctx2, "XROS", "SomeXrosCard", ChoiceZone.Hand, 5);
    SpecialPlayRecipeRegistry.Register("SomeXrosCard", new SpecialPlayRecipe(
        SpecialPlayKind.DigiXros, new[] { new SpecialPlayMaterial(cs => cs.Level == 3, "any Lv3") }, MemoryCost: 0));
    await Place2(ctx2, "M1", "IrrelevantName", ChoiceZone.BattleArea, 4);   // Lv4 -> predicate false
    var none = new SpecialPlayAction().GetLegalActions(ctx2, P1);
    AssertTrue(none.Count == 0, "Lv4 material does not satisfy the Lv3 predicate");
    _ = xros;
}

async Task<HeadlessEntityId> BattleLevel(EngineContext ctx, string tag, string name, int level) => await Place2(ctx, tag, name, ChoiceZone.BattleArea, level);

async Task<HeadlessEntityId> Place2(EngineContext ctx, string tag, string nameOrNumber, ChoiceZone zone, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, nameOrNumber, nameOrNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
    return id;
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 949);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Hand(EngineContext ctx, string tag, string cardNumber) => await Place(ctx, tag, cardNumber, ChoiceZone.Hand);
async Task<HeadlessEntityId> Battle(EngineContext ctx, string tag, string name) => await Place(ctx, tag, name, ChoiceZone.BattleArea);

async Task<HeadlessEntityId> Place(EngineContext ctx, string tag, string nameOrNumber, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    // For the DigiXros card, nameOrNumber is the card NUMBER (= fixture class); for materials it is the NAME.
    cards.Upsert(new CardRecord(defId, nameOrNumber, nameOrNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
