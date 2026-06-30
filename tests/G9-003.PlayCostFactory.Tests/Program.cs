using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-003: the headless mirror of the original CardEffectFactory play-cost factories
// (ChangePlayCostStaticEffect / MandatorySelfPlayCostReduction). The headless play-cost engine already
// pulls continuous cost modifiers from the EffectRegistry at play time (PlayCardAction ->
// ContinuousModifierGate.ResolvePlayCost); these factories were the missing card-facing entry points.
// This test drives the REAL resolution path: Factory.X(card,...).ToBinding() -> register -> the same
// ResolvePlayCost call PlayCardAction makes returns the reduced cost, and the CanReduceCost guard holds.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MandatorySelfPlayCostReduction(4) reduces a 6-cost card to 2 via ResolvePlayCost", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, null), baseCost: 6, expected: 2)),
    ("ChangePlayCostStaticEffect(-3) reduces a 5-cost card to 2", () => CostResolvesTo(
        (card) => CardEffectFactory.ChangePlayCostStaticEffect(-3, null, false, card, null, false), baseCost: 5, expected: 2)),
    ("ChangePlayCostStaticEffect(+2) increases a 3-cost card to 5", () => CostResolvesTo(
        (card) => CardEffectFactory.ChangePlayCostStaticEffect(2, null, false, card, null, false), baseCost: 3, expected: 5)),
    ("Reduction never drops below 0 (a 2-cost card minus 4 = 0)", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, null), baseCost: 2, expected: 0)),
    ("Dynamic MandatorySelfPlayCostReduction(()=>2) reduces 5 to 3", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(() => 2, card, null), baseCost: 5, expected: 3)),
    ("condition:false makes the reduction inert (cost unchanged)", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, () => false), baseCost: 6, expected: 6)),
    ("CanReduceCost:false blocks the reduction (the original CanReduceCost guard)", CanReduceCostGuardHolds),
    ("setFixedCost:true throws rather than silently treating set as add", FixedCostThrows),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CostResolvesTo(Func<CardSource, ICardEffect> build, int baseCost, int expected)
{
    EngineContext context = Context();
    var id = await PlaceInHand(context, P1, "CARD", playCost: baseCost);
    var source = new CardSource(context, id, P1);
    context.EffectRegistry.Register(build(source).ToBinding($"effect:cost:{id.Value}"));

    int resolved = ContinuousModifierGate.ResolvePlayCost(context, id, baseCost);
    AssertEqual(expected, resolved, $"resolved play cost (base {baseCost})");
}

async Task CanReduceCostGuardHolds()
{
    EngineContext context = Context();
    var id = await PlaceInHand(context, P1, "CARD", playCost: 6);
    var source = new CardSource(context, id, P1);
    context.EffectRegistry.Register(CardEffectFactory.MandatorySelfPlayCostReduction(4, source, null).ToBinding($"effect:cost:{id.Value}"));

    int withReduction = ContinuousModifierGate.ResolvePlayCost(context, id, basePlayCost: 6, canReduceCost: true);
    int noReduction = ContinuousModifierGate.ResolvePlayCost(context, id, basePlayCost: 6, canReduceCost: false);
    AssertEqual(2, withReduction, "reduction applies when CanReduceCost");
    AssertEqual(6, noReduction, "reduction is blocked when CanReduceCost is false");
}

Task FixedCostThrows()
{
    EngineContext context = Context();
    var source = new CardSource(context, new HeadlessEntityId("1:hand:X"), P1);
    bool threw = false;
    try { CardEffectFactory.ChangePlayCostStaticEffect(1, null, false, source, null, setFixedCost: true); }
    catch (NotSupportedException) { threw = true; }
    AssertTrue(threw, "setFixedCost:true throws NotSupportedException (no silent set-as-add)");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceInHand(EngineContext context, HeadlessPlayerId owner, string tag, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
