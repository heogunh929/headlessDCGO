using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
// (R4 S3b) the mirror Script/MainPhaseAction classes share the AS-IS names — pin the Runtime one.
using PlayCardAction = HeadlessDCGO.Engine.Headless.Runtime.PlayCardAction;

// F-1.7: a one-shot "until cost is calculated" cost modifier applies to the next play's cost, then expires
// once a card is played (cost locked). AS-IS 1:1: the effect is a ChangeCostClass placed on the owner's
// Player.UntilCalculateFixedCostEffect BUCKET (EX4_062 / BT18_057 precedent), read by
// CardSource.GetPayingCostWithBaseCost's CanUse-gated GetChangedCostItselef/GetChangedPayingCost fold
// (Player.EffectList folds the bucket into the player-scope scan). The one-shot semantics come from the
// AS-IS clear of Player.UntilCalculateFixedCostEffect on each play (CardController.cs:961/3496), mirrored by
// EffectDurationExpiry.ExpireFixedCostCalc(context, payer) which every pay choke calls.
// (W3c-final) Retargeted off the retired invented EffectRegistry UntilCalculateFixedCost NumericModifier
// fold (ContinuousModifierGate.FoldLegacyPlayCostModifiers) onto this AS-IS bucket path.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Future = new("p1:hand:FUTURE"); // the card whose cost the temp effect will reduce

var tests = new (string Name, Func<Task> Body)[]
{
    ("Temp cost modifier reduces cost, then expires after a card is played", ExpiresOnPlay),
    ("ExpireFixedCostCalc(context, payer) clears the bucket directly (one-shot)", () => Pure(DirectExpire)),
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

async Task ExpiresOnPlay()
{
    EngineContext context = Board(randomSeed: 31);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("PL"), "PL", "Playable", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 0));
    cards.Upsert(new CardRecord(new HeadlessEntityId("FUTURE"), "FUTURE", "Future", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 3));

    HeadlessEntityId playNow = new("p1:hand:PL");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(playNow, new HeadlessEntityId("PL"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Future, new HeadlessEntityId("FUTURE"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, playNow, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Future, ChoiceZone.None, ChoiceZone.Hand));

    // "Until cost is calculated: the next card you play costs 2 less" — targets FUTURE, on the owner's bucket.
    RegisterTempCostReducer(context, P1, Future, reduce: 2);
    AssertEqual(1, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "temp reducer applies before any play");

    // Play a (different) card — the fixed cost is now calculated, so the one-shot bucket clears (PlayCardAction
    // -> ExpireFixedCostCalc(context, payer), AS-IS CardController.cs:961).
    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, playNow, memoryCost: 0), context);
    AssertTrue(result.IsSuccess, $"play succeeded ({result.Message})");

    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "temp reducer expired after the play");
}

void DirectExpire()
{
    EngineContext context = Board(randomSeed: 32);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Future, new HeadlessEntityId("FUTURE"), P1));
    RegisterTempCostReducer(context, P1, Future, reduce: 2);

    AssertEqual(1, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "applies before expiry");
    // AS-IS CardController.cs:961/3496 mirror reset — clearing the owner's bucket gives the one-shot semantic.
    EffectDurationExpiry.ExpireFixedCostCalc(context, P1);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "back to base after the bucket is cleared");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board(int randomSeed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: randomSeed);
    // Past None/Setup so the AS-IS ChangeCostClass fold's CanUse -> CanTrigger -> DoneStartGame gate passes.
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

// AS-IS one-shot [BeforePayCost]-style bucket registration: a self-targeted ChangeCostClass into the owner's
// UntilCalculateFixedCostEffect bucket (cleared once a play's cost is locked). Body identical to a
// "cost -N" reduction.
void RegisterTempCostReducer(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId target, int reduce)
{
    HeadlessEntityId sourceId = new("temp-cost");
    var carrier = new CardSource(context, sourceId, owner);
    ChangeCostClass changeCostClass = new ChangeCostClass();
    changeCostClass.SetUpICardEffect($"Cost -{reduce}", _ => true, carrier);
    changeCostClass.SetUpChangeCostClass(
        changeCostFunc: (cs, cost, root, targetPermanents) => cost - reduce,
        cardSourceCondition: cs => cs is not null && cs.InstanceId == target,
        rootCondition: root => true,
        isUpDown: () => true,
        isCheckAvailability: () => false,
        isChangePayingCost: () => true);
    new Player(context, owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
