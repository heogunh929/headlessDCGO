// PRIM-P0 B.O.6: CardEffectFactory.CanNotReduceCostStaticEffect — the card-facing "cost cannot be reduced"
// grant. Registers a continuous CostReduction/Immune replacement; ContinuousModifierGate honours it so a
// cost-reduction modifier is blocked while an increase still applies (AS-IS CannotReduceCostClass).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("1:card:C");

var tests = new (string Name, Func<Task> Body)[]
{
    ("without the grant a -2 modifier reduces play cost (5 -> 3)", ReductionApplies),
    ("with CanNotReduceCostStaticEffect the -2 reduction is blocked (stays 5)", ReductionBlocked),
    ("the grant does NOT block a cost INCREASE (+1 still applies -> 6)", IncreaseStillApplies),
    ("the grant also blocks a digivolution-cost reduction (stays 4)", DigivolutionReductionBlocked),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task ReductionApplies()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.PlayCostDeltaKey, -2);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "-2 applies");
    await Task.CompletedTask;
}

async Task ReductionBlocked()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.PlayCostDeltaKey, -2);
    GrantCannotReduceCost(context);
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "reduction blocked by the grant");
    await Task.CompletedTask;
}

async Task IncreaseStillApplies()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.PlayCostDeltaKey, 1);
    GrantCannotReduceCost(context);
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "increase still applies");
    await Task.CompletedTask;
}

async Task DigivolutionReductionBlocked()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.DigivolutionCostDeltaKey, -1);
    GrantCannotReduceCost(context);
    AssertEqual(4, ContinuousModifierGate.ResolveDigivolutionCost(context, Card, baseDigivolutionCost: 4), "digivolution reduction blocked");
    await Task.CompletedTask;
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

void RegisterCostModifier(EngineContext context, string deltaKey, int delta)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [deltaKey] = delta };
    var effectContext = new EffectContext(P1, P1, new HeadlessEntityId($"src:mod:{deltaKey}:{delta}"),
        triggerEntityId: null, targetEntityIds: new[] { Card }, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"mod:{deltaKey}:{delta}"), P1, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }));
}

// Grant via the REAL card-facing factory (its ToBinding is what a ported card registers at enter-play).
void GrantCannotReduceCost(EngineContext context)
{
    var card = new CardSource(context, Card, P1, P1);
    ICardEffect effect = CardEffectFactory.CanNotReduceCostStaticEffect(permanentCondition: null, isInheritedEffect: false, card, condition: null);
    context.EffectRegistry.Register(effect.ToBinding($"{Card.Value}:cannotReduceCost"));
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
