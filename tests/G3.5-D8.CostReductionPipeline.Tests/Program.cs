using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-8 / R2-C ③: cost reduction pipeline. Continuous ±play/digivolution cost modifiers fold into the resolved
// cost (CardSource.GetPayingCostWithBaseCost / GetChangedCostItselef / GetChangedPayingCost), and a continuous
// "cost cannot be reduced" restriction (AS-IS ICannotReduceCostEffect / CannotReduceCostClass, consulted by the
// live Player.CanReduceCost scan AND ChangeCostClass's own IsUpDown veto) blocks reductions while still allowing
// increases.
// (W3c-final) Retargeted off the retired invented EffectRegistry NumericModifier cost fold
// (ContinuousModifierGate.FoldLegacy*CostModifiers) onto the AS-IS ChangeCostClass path: a self- (or owner-)
// scoped ChangeCostClass placed on the owner's Player.UntilCalculateFixedCostEffect bucket, read by the
// CanUse-gated GetChangedCostItselef/GetChangedPayingCost fold. The cost-reduction immunity is the kind-class on
// a field permanent (as a real card grants it).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:hand:C1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Continuous -play cost reduces the resolved cost", () => Pure(PlayCostReduced)),
    ("Cost-reduction immunity blocks the reduction", () => Pure(ImmunityBlocksReduction)),
    ("Cost-reduction immunity still allows increases", () => Pure(ImmunityAllowsIncrease)),
    ("Continuous -digivolution cost reduces; immunity blocks it", () => Pure(DigivolutionCostReduceAndImmunity)),
    ("Player-scope (owner) cost reduction applies to the owner's card", () => Pure(PlayerScopeCostReduction)),
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
    AddBucketCostModifier(context, P1, cs => cs is not null && cs.InstanceId == Card, delta: -2);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "-2 play cost");
}

void ImmunityBlocksReduction()
{
    EngineContext context = Board();
    AddBucketCostModifier(context, P1, cs => cs is not null && cs.InstanceId == Card, delta: -2);
    RegisterCostReductionImmunity(context, Card);
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "reduction blocked by immunity");
}

void ImmunityAllowsIncrease()
{
    EngineContext context = Board();
    AddBucketCostModifier(context, P1, cs => cs is not null && cs.InstanceId == Card, delta: 1);
    RegisterCostReductionImmunity(context, Card);
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "increase still applies under immunity");
}

void DigivolutionCostReduceAndImmunity()
{
    EngineContext reduced = Board();
    AddBucketCostModifier(reduced, P1, cs => cs is not null && cs.InstanceId == Card, delta: -1);
    AssertEqual(3, ContinuousModifierGate.ResolveDigivolutionCost(reduced, Card, baseDigivolutionCost: 4), "-1 digivolution cost");

    EngineContext immune = Board();
    AddBucketCostModifier(immune, P1, cs => cs is not null && cs.InstanceId == Card, delta: -1);
    RegisterCostReductionImmunity(immune, Card);
    AssertEqual(4, ContinuousModifierGate.ResolveDigivolutionCost(immune, Card, baseDigivolutionCost: 4), "digivolution reduction blocked");
}

// (BLK-2 / W3c-final) "your cards cost 1 less" — a REAL AS-IS rule (230 cards use ChangeCostClass; BT2_045 /
// BT2_047 / BT2_088 / BT8_097 gate cardSourceCondition by cs.Owner). Its canonical form is a ChangeCostClass
// whose cardSourceCondition matches by OWNER — NOT the retired invented player-scope registry binding
// (PlayerScopeModifierEffect / ScopePlayerIdKey). P1's card is reduced; P2's is untouched.
void PlayerScopeCostReduction()
{
    EngineContext context = Board();
    AddBucketCostModifier(context, P1, cs => cs is not null && cs.Owner == P1, delta: -1);

    AssertEqual(4, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "owner-scope -1 applies to P1 card");
    AssertEqual(5, ContinuousModifierGate.ResolvePlayCost(context, new HeadlessEntityId("p2:hand:O1"), basePlayCost: 5), "P2 card unaffected");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 8);
    // (R2-C ③) a live match past None/Setup so the CannotReduceCost kind-class scan AND the ChangeCostClass fold
    // (CanUse -> CanTrigger -> DoneStartGame) run.
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new HeadlessEntityId("p2:hand:O1"), new HeadlessEntityId("O1"), P2));
    return context;
}

// AS-IS ChangeCostClass on the owner's UntilCalculateFixedCostEffect bucket: a continuous ±cost modifier gated by
// `cardCond`. isUpDown:true so the reduction respects Player.CanReduceCost (immunity), while increases always apply.
void AddBucketCostModifier(EngineContext context, HeadlessPlayerId owner, Func<CardSource, bool> cardCond, int delta)
{
    var carrier = new CardSource(context, new HeadlessEntityId($"cost-src:{delta}"), owner);
    ChangeCostClass changeCostClass = new ChangeCostClass();
    changeCostClass.SetUpICardEffect($"Cost {delta:+#;-#;0}", _ => true, carrier);
    changeCostClass.SetUpChangeCostClass(
        changeCostFunc: (cs, cost, root, targetPermanents) => cost + delta,
        cardSourceCondition: cardCond,
        rootCondition: root => true,
        isUpDown: () => true,
        isCheckAvailability: () => false,
        isChangePayingCost: () => true);
    new Player(context, owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);
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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
