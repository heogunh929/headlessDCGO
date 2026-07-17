using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-8 / R2-C ③: cost reduction pipeline. Continuous ±play/digivolution cost modifiers fold into the resolved
// cost (CardSource.GetPayingCostWithBaseCost), and a continuous "cost cannot be reduced" restriction
// (AS-IS ICannotReduceCostEffect / CannotReduceCostClass, consulted by the live Player.CanReduceCost scan)
// blocks reductions while still allowing increases. R2-C ③ retired the parallel registry-key immunity; the
// immunity is now the kind-class placed on a field permanent (as a real card grants it).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:hand:C1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Continuous -play cost reduces the resolved cost", () => Pure(PlayCostReduced)),
    ("Cost-reduction immunity blocks the reduction", () => Pure(ImmunityBlocksReduction)),
    ("Cost-reduction immunity still allows increases", () => Pure(ImmunityAllowsIncrease)),
    ("Continuous -digivolution cost reduces; immunity blocks it", () => Pure(DigivolutionCostReduceAndImmunity)),
    ("Player-scope cost reduction applies to the owner's card", () => Pure(PlayerScopeCostReduction)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void PlayCostReduced()
{
    EngineContext context = Board();
    RegisterCostModifier(context, Card, ModifierHelpers.PlayCostDeltaKey, -2);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "-2 play cost");
}

void ImmunityBlocksReduction()
{
    EngineContext context = Board();
    RegisterCostModifier(context, Card, ModifierHelpers.PlayCostDeltaKey, -2);
    RegisterCostReductionImmunity(context, Card);
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "reduction blocked by immunity");
}

void ImmunityAllowsIncrease()
{
    EngineContext context = Board();
    RegisterCostModifier(context, Card, ModifierHelpers.PlayCostDeltaKey, 1);
    RegisterCostReductionImmunity(context, Card);
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "increase still applies under immunity");
}

void DigivolutionCostReduceAndImmunity()
{
    EngineContext reduced = Board();
    RegisterCostModifier(reduced, Card, ModifierHelpers.DigivolutionCostDeltaKey, -1);
    AssertEqual(3, ContinuousModifierGate.ResolveDigivolutionCost(reduced, Card, baseDigivolutionCost: 4), "-1 digivolution cost");

    EngineContext immune = Board();
    RegisterCostModifier(immune, Card, ModifierHelpers.DigivolutionCostDeltaKey, -1);
    RegisterCostReductionImmunity(immune, Card);
    AssertEqual(4, ContinuousModifierGate.ResolveDigivolutionCost(immune, Card, baseDigivolutionCost: 4), "digivolution reduction blocked");
}

void PlayerScopeCostReduction()
{
    EngineContext context = Board();
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = P1.Value,
        [ModifierHelpers.PlayCostDeltaKey] = -1,
    };
    Register(context, "pscope:cost:p1", P1, Array.Empty<HeadlessEntityId>(), values);

    AssertEqual(4, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "player-scope -1 applies to P1 card");
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, new HeadlessEntityId("p2:hand:O1"), basePlayCost: 5), "P2 card unaffected");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 8);
    // (R2-C ③) a live match past None/Setup so the CannotReduceCost kind-class scan (CanUse -> DoneStartGame) runs.
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new HeadlessEntityId("p2:hand:O1"), new HeadlessEntityId("O1"), P2));
    return context;
}

void RegisterCostModifier(EngineContext context, HeadlessEntityId cardId, string deltaKey, int delta)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [deltaKey] = delta };
    Register(context, $"cost:{cardId.Value}:{deltaKey}:{delta}", P1, new[] { cardId }, values);
}

// (R2-C ③) Grant the AS-IS CannotReduceCostClass (Both scope) on a P1 FIELD permanent scoped to `cardId`,
// dispatched via the TfxCannotReduceCost fixture (the live Player.CanReduceCost scan walks field permanents).
void RegisterCostReductionImmunity(EngineContext context, HeadlessEntityId cardId)
{
    TfxCannotReduceCost.PlayerCondition = _ => true;
    TfxCannotReduceCost.CardCondition = cs => cs is not null && cs.InstanceId == cardId;
    TfxCannotReduceCost.CostKind = CostReductionScope.Both;

    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId("DEF:GRANT");
    cards.Upsert(new CardRecord(def, "TfxCannotReduceCost", "GRANT", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var srcId = new HeadlessEntityId("1:battle:GRANT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, def, P1, Metadata: new Dictionary<string, object?>()));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, srcId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

void Register(EngineContext context, string effectId, HeadlessPlayerId owner, HeadlessEntityId[] targets, Dictionary<string, object?> values)
{
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{effectId}"),
        triggerEntityId: null, targetEntityIds: targets, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId(effectId), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }));
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
