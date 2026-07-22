// PRIM-P0 B.O.6 / R2-C ③: CardEffectFactory.CanNotReduceCostStaticEffect — the card-facing "cost cannot be
// reduced" grant, FLIPPED to the AS-IS new-model kind-class CannotReduceCostClass (an ICannotReduceCostEffect,
// no ToBinding) consulted SOLELY by the live scan Player.CanReduceCost, which the cost pipeline
// CardSource.GetPayingCostWithBaseCost re-derives. A cost-reduction modifier is blocked while an increase still
// applies. The registry-key CostReduction/Immune representation is retired; the grant is now placed on a FIELD
// permanent (the live scan walks Players_ForTurnPlayer's field permanents' EffectList(None)), exactly as a real
// card (BT5_021) grants it.
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
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
    GrantCannotReduceCost(context, CostReductionScope.Both);
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "reduction blocked by the grant");
    await Task.CompletedTask;
}

async Task IncreaseStillApplies()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.PlayCostDeltaKey, 1);
    GrantCannotReduceCost(context, CostReductionScope.Both);
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "increase still applies");
    await Task.CompletedTask;
}

async Task DigivolutionReductionBlocked()
{
    EngineContext context = Context();
    RegisterCostModifier(context, ModifierHelpers.DigivolutionCostDeltaKey, -1);
    GrantCannotReduceCost(context, CostReductionScope.Both);
    AssertEqual(4, ContinuousModifierGate.ResolveDigivolutionCost(context, Card, baseDigivolutionCost: 4), "digivolution reduction blocked");
    await Task.CompletedTask;
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // past None/Setup so CanTrigger's DoneStartGame gate passes.
    // The played card must exist so the pipeline can resolve its owner (P1).
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C"), P1));
    return context;
}

// (W3c-final) Retargeted off the retired invented EffectRegistry NumericModifier cost fold onto the AS-IS
// ChangeCostClass path: a self-scoped ±cost ChangeCostClass on the owner's UntilCalculateFixedCostEffect bucket,
// read by the CanUse-gated GetChangedCostItselef/GetChangedPayingCost fold. `deltaKey` documents which resolve
// path the caller exercises (play vs digivolution); the ChangeCostClass applies to the resolved cost either way.
// isUpDown:true so the reduction respects the CannotReduceCost grant (Player.CanReduceCost), while +N increases.
void RegisterCostModifier(EngineContext context, string deltaKey, int delta)
{
    _ = deltaKey;
    var carrier = new CardSource(context, new HeadlessEntityId($"cost-src:{delta}"), P1);
    var changeCostClass = new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ChangeCostClass();
    changeCostClass.SetUpICardEffect($"Cost {delta:+#;-#;0}", _ => true, carrier);
    changeCostClass.SetUpChangeCostClass(
        changeCostFunc: (cs, cost, root, targetPermanents) => cost + delta,
        cardSourceCondition: cs => cs is not null && cs.InstanceId == Card,
        rootCondition: root => true,
        isUpDown: () => true,
        isCheckAvailability: () => false,
        isChangePayingCost: () => true);
    new Player(context, P1).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);
}

// (R2-C ③) Grant the AS-IS kind-class CannotReduceCostClass on a P1 FIELD permanent (the live scan walks
// Players_ForTurnPlayer's field permanents' EffectList(None)); dispatched via the TfxCannotReduceCost fixture,
// scoped to the played card (cardCondition) and the given cost path (costKind), any payer (playerCondition).
void GrantCannotReduceCost(EngineContext context, CostReductionScope costKind)
{
    TfxCannotReduceCost.PlayerCondition = _ => true;
    TfxCannotReduceCost.CardCondition = cs => cs is not null && cs.InstanceId == Card;
    TfxCannotReduceCost.CostKind = costKind;

    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId("DEF:GRANT");
    cards.Upsert(new CardRecord(def, "TfxCannotReduceCost", "GRANT", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var srcId = new HeadlessEntityId("1:battle:GRANT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, def, P1, Metadata: new Dictionary<string, object?>()));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, srcId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
