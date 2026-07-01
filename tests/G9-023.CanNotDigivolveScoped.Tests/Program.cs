using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W1-8 (G9-023): CardEffectFactory.CanNotDigivolveStaticEffect — a continuous PLAYER-SCOPE "the scoped
// player's Digimon cannot digivolve" restriction (structured scope). "Your opponent's Digimon cannot
// digivolve" => scopePlayerId = opponent. Consulted by DigivolveAction via the player-scope continuous path.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent (P2) scoped: P2 cannot digivolve", ScopedOpponentBlocked),
    ("The scoping player's own (P1) digivolve is NOT affected", OwnUnaffected),
    ("A false condition lifts the restriction -> P2 digivolve legal", ConditionLifts),
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

async Task ScopedOpponentBlocked()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var source = await PlaceSource(context, P1);
    RegisterOpponentCannotDigivolve(context, source, scope: P2, condition: null);

    var target = await PlaceBase(context, P2, "REDBASE");
    var evo = await PlaceEvolve(context, P2, "EVO");
    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P2, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "P2 (scoped) cannot digivolve");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, evo), "the P2 evolving card did NOT enter play");
}

async Task OwnUnaffected()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var source = await PlaceSource(context, P1);
    RegisterOpponentCannotDigivolve(context, source, scope: P2, condition: null);

    var target = await PlaceBase(context, P1, "REDBASE");
    var evo = await PlaceEvolve(context, P1, "EVO");
    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"P1's own digivolve is legal ({result.Message})");
}

async Task ConditionLifts()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var source = await PlaceSource(context, P1);
    RegisterOpponentCannotDigivolve(context, source, scope: P2, condition: () => false);

    var target = await PlaceBase(context, P2, "REDBASE");
    var evo = await PlaceEvolve(context, P2, "EVO");
    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P2, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, "false condition -> no restriction -> P2 digivolve legal");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 923);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

void RegisterOpponentCannotDigivolve(EngineContext context, HeadlessEntityId source, HeadlessPlayerId scope, Func<bool>? condition)
{
    var sourceCard = new CardSource(context, source, P1);
    context.EffectRegistry.Register(
        CardEffectFactory.CanNotDigivolveStaticEffect(scope, scopeCardType: null, isInheritedEffect: false, sourceCard, condition).ToBinding($"cnd:scope:{scope.Value}"));
}

async Task<HeadlessEntityId> PlaceSource(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("SRC");
    cards.Upsert(new CardRecord(defId, "SRC", "SRC", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:SRC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"{owner.Value}:{tag}:def");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 4, ["dp"] = 3000 },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"{owner.Value}:{tag}:def");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = 2 },
        CardType: "Digimon", EvolutionCondition: "Red@4"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 2 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
