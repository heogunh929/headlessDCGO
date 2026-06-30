using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-006 (EX8_074 Stage 3, brick 2): the PlayCardAction pre-payment BeforePayCost window, E2E through the
// REAL play action. The TfxBeforePayCost fixture's [BeforePayCost] returns a SuspendCostReductionEffect
// ("suspend 2 Digimon to get Play Cost -4"). Played with a synchronous (scripted) resolver, the play
// resolves that effect BEFORE locking the cost: it suspends the 2 chosen Digimon and pays the REDUCED cost.
// When fewer than 2 suspendable Digimon exist the gate is closed and the full cost is paid (no reduction).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Playing the card suspends 2 chosen Digimon and pays the reduced cost (6 -> 2)", PaysReducedAndSuspends),
    ("With only 1 suspendable Digimon the gate is closed: full cost paid, nothing suspended", GateClosedFullCost),
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

async Task PaysReducedAndSuspends()
{
    EngineContext context = Context();
    context.MemoryController.Set(8); // affords the FULL cost (6) -> the play is legal; brick 2 then pays less.
    var hand = await PlaceCard(context, P1, "TfxBeforePayCost", "TfxBeforePayCost", playCost: 6, CardType: "Digimon", zone: ChoiceZone.Hand);
    var d1 = await PlaceDigimon(context, P1, "U1", suspended: false);
    var d2 = await PlaceDigimon(context, P1, "U2", suspended: false);

    // Synchronous resolver: seed the suspend selection the BeforePayCost effect will ask for.
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(d1, d2));

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(
        HeadlessActionFactory.PlayCard(P1, hand, 6), context);

    AssertTrue(result.IsSuccess, $"the play succeeded ({result.Message})");
    AssertTrue(IsSuspended(context, d1) && IsSuspended(context, d2), "both chosen Digimon were suspended as the cost");
    // Started at 8, full cost would leave 2; the -4 reduction means only 2 is paid -> 6 remains.
    AssertEqual(6, context.MemoryController.Current.Current, "paid the REDUCED cost (2), not the full 6");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, hand), "the card entered the battle area");
}

async Task GateClosedFullCost()
{
    EngineContext context = Context();
    context.MemoryController.Set(8);
    var hand = await PlaceCard(context, P1, "TfxBeforePayCost", "TfxBeforePayCost", playCost: 6, CardType: "Digimon", zone: ChoiceZone.Hand);
    await PlaceDigimon(context, P1, "U1", suspended: false); // only ONE suspendable -> gate (>= 2) closed.

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(
        HeadlessActionFactory.PlayCard(P1, hand, 6), context);

    AssertTrue(result.IsSuccess, $"the play succeeded ({result.Message})");
    // Full cost 6 paid: 8 -> 2 (no reduction because the BeforePayCost effect was not offered).
    AssertEqual(2, context.MemoryController.Current.Current, "full cost (6) paid when the gate is closed");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceCard(EngineContext context, HeadlessPlayerId owner, string cardNumber, string tag, int playCost, string CardType, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }, CardType: CardType, PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
