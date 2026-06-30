using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-011 (LA-1): [When Digivolving] activated effects now fire LIVE through DigivolveAction (not just via a
// direct ActivatedEffectResolver call in tests). Digivolving onto EX8_074 resolves its [When Digivolving]
// suspend+delete through the real action flow. A control digivolve onto a card WITHOUT a When Digivolving
// effect must not disturb anything.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Digivolving onto EX8_074 fires its [When Digivolving] suspend+delete live", DigivolveFiresWhenDigivolving),
    ("Digivolving onto a plain Digimon resolves with no spurious effect", DigivolvePlainNoOp),
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

async Task DigivolveFiresWhenDigivolving()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var baseDigimon = await Place(context, P1, "BASE", "BASE", ChoiceZone.BattleArea, dp: 3000, level: 4);
    var evolve = await PlaceEvolve(context, P1, "EX8_074", digivolveCost: 2);
    var ally = await Place(context, P1, "ALLY", "ALLY", ChoiceZone.BattleArea, dp: 4000, level: 4);
    var foe = await Place(context, P2, "FOE", "FOE", ChoiceZone.BattleArea, dp: 7000, level: 4);

    // The [When Digivolving] flow asks for: suspend 1 (ALLY), then delete 1 opponent (FOE).
    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally));
    provider.Enqueue(ChoiceResult.Select(foe));

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evolve, baseDigimon, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"digivolve succeeded ({result.Message})");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evolve), "EX8_074 is the new top in the battle area");
    AssertTrue(IsSuspended(context, ally), "[When Digivolving] suspended the ally (fired live via DigivolveAction)");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe), "[When Digivolving] deleted the opponent");
}

async Task DigivolvePlainNoOp()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var baseDigimon = await Place(context, P1, "BASE", "BASE", ChoiceZone.BattleArea, dp: 3000, level: 4);
    var evolve = await PlaceEvolve(context, P1, "PLAIN", digivolveCost: 2); // no ported When Digivolving effect
    var foe = await Place(context, P2, "FOE", "FOE", ChoiceZone.BattleArea, dp: 7000, level: 4);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evolve, baseDigimon, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"digivolve succeeded ({result.Message})");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evolve), "the plain Digimon is the new top");
    AssertTrue(!InZone(context, P2, ChoiceZone.Trash, foe), "no spurious deletion (no When Digivolving effect)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string cardNumber, string tag, ChoiceZone zone, int dp, int level)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// Evolve card in hand: EvolutionCondition=null matches any target; fixedDigivolutionCost sets the cost.
async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, HeadlessPlayerId owner, string cardNumber, int digivolveCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = digivolveCost },
        CardType: "Digimon", EvolutionCondition: null));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = digivolveCost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
