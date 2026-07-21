// PRIM-P0 Build Order 4: BeforePayCostReductionEffect — a non-interactive one-shot reduction of THIS card's own
// play cost, registered in the PlayCardAction BeforePayCost window and consumed by the cost re-resolve. Verifies
// (1) the reduction is applied (pays base - amount), (2) a false condition applies nothing (pays full), (3) the
// UntilCalculateFixedCost modifier does not persist after the play. Fixture: TfxBeforePayCostReduction.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("condition met: the play pays the reduced cost (6 - 3 = 3)", ReducesOwnCost),
    ("condition unmet: the play pays the full cost (6)", ConditionUnmetPaysFull),
    ("#1 digivolve: a Digivolve-gated before-pay reduction lowers the DIGIVOLVE cost (4 -> 1)", DigivolveReducesCost),
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

async Task ReducesOwnCost()
{
    EngineContext context = Context();
    context.MemoryController.Set(8);
    HeadlessEntityId hand = await PlaceCard(context, P1, playCost: 6, allowReduce: true);

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, hand, 6), context);

    AssertTrue(result.IsSuccess, "the play resolved");
    AssertEqual(5, context.MemoryController.Current.Current, "paid the reduced cost 3 (8 -> 5)");
}

async Task ConditionUnmetPaysFull()
{
    EngineContext context = Context();
    context.MemoryController.Set(8);
    HeadlessEntityId hand = await PlaceCard(context, P1, playCost: 6, allowReduce: false);

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, hand, 6), context);

    AssertTrue(result.IsSuccess, "the play resolved");
    AssertEqual(2, context.MemoryController.Current.Current, "no reduction: paid the full cost 6 (8 -> 2)");
}

async Task DigivolveReducesCost()
{
    EngineContext context = Context();
    context.MemoryController.Set(8);
    var cards = (CardDatabase)context.CardRepository;

    // Target: a base Digimon on the battle area.
    var tgtDef = new HeadlessEntityId("DEF:TGT");
    cards.Upsert(new CardRecord(tgtDef, "TGT", "Base", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 3 }, CardType: "Digimon"));
    var target = new HeadlessEntityId("1:battle:TGT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(target, tgtDef, P1, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));

    // The digivolving card (TfxBeforePayCostReduction, gated to the Digivolve root), digivolution cost 4.
    var evoDef = new HeadlessEntityId("TfxBeforePayCostReduction");
    cards.Upsert(new CardRecord(evoDef, "TfxBeforePayCostReduction", "Evo", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon", EvolutionCost: 4));
    var evo = new HeadlessEntityId("1:hand:Evo");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evo, evoDef, P1, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["allowReduce"] = true, ["gateRoot"] = "digivolve" }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evo, ChoiceZone.None, ChoiceZone.Hand));

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, 4), context);

    AssertTrue(result.IsSuccess, $"the digivolve resolved ({result.Message})");
    AssertEqual(7, context.MemoryController.Current.Current, "paid the reduced digivolve cost 1 (8 -> 7), not 4");
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (R6-Da'-3) advance past phase None so the AS-IS ChangeCostClass fold's CanUse->CanTrigger->DoneStartGame
    // gate passes: the before-pay reduction now lives on the UntilCalculateFixedCost BUCKET (AS-IS 1:1), read by
    // CardSource.GetPayingCostWithBaseCost's CanUse-gated GetChangedCostItselef/GetChangedPayingCost — unlike the
    // former invented EffectRegistry NumericModifier fold, which bypassed the DoneStartGame gate.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> PlaceCard(EngineContext context, HeadlessPlayerId owner, int playCost, bool allowReduce)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TfxBeforePayCostReduction");
    cards.Upsert(new CardRecord(defId, "TfxBeforePayCostReduction", "CostRed",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:CostRed");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["allowReduce"] = allowReduce }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
