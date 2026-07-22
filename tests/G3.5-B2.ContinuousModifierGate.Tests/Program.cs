using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-2: continuous ±Security Attack / ±cost modifiers (sibling of ContinuousDpGate, which covers ±DP).
// Sourced from continuous registry bindings, so an EffectDuration tag (F-1) makes them expire; and they
// honour player-scope (F-5) via the shared evaluation. DP+duration is already covered by CV-A1.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:main:C1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Continuous +Security Attack raises the value", () => Pure(SecurityAttackBoost)),
    ("Continuous -PlayCost reduces, floored at zero", () => Pure(PlayCostReduce)),
    ("Continuous -DigivolutionCost reduces", () => Pure(DigivolutionCostReduce)),
    ("Security Attack modifier with a duration expires at turn end", () => Pure(SecurityAttackDurationExpires)),
    ("Player-scope +Security Attack applies to the owner's cards", () => Pure(PlayerScopeSecurityAttack)),
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

void SecurityAttackBoost()
{
    EngineContext context = Board();
    RegisterModifier(context, Card, ModifierHelpers.SecurityAttackDeltaKey, 1, duration: null);
    AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Card).Strike, "+1 security attack");
}

// (W3c-final) Cost cases retargeted off the retired invented EffectRegistry NumericModifier cost fold onto the
// AS-IS ChangeCostClass bucket path (CostBoard/AddBucketCostModifier). The floor (Math.Max(0)) lives in AS-IS
// GetChangedCostItselef/GetChangedPayingCost 1:1. (The ±Security-Attack cases below keep the registry modifier
// path — that is a separate, non-cost gate, not part of this retirement.)
void PlayCostReduce()
{
    EngineContext context = CostBoard();
    AddBucketCostModifier(context, Card, -2);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "-2 play cost");

    EngineContext floored = CostBoard();
    AddBucketCostModifier(floored, Card, -5);
    AssertEqual(0, ContinuousModifierGate.ResolvePlayCost(floored, Card, basePlayCost: 1), "play cost floored at 0");
}

void DigivolutionCostReduce()
{
    EngineContext context = CostBoard();
    AddBucketCostModifier(context, Card, -1);
    AssertEqual(3, ContinuousModifierGate.ResolveDigivolutionCost(context, Card, baseDigivolutionCost: 4), "-1 digivolution cost");
}

void SecurityAttackDurationExpires()
{
    // (③-B) The registry duration-modifier + sweep surface (RegisterModifier(EffectDuration) + the retired
    // EffectDurationExpiry.ExpireTurnEnd) is gone (continuous-binding producer 0). Security-attack duration expiry
    // is now the AS-IS bucket reset at the turn-end choke — witness that the REAL HeadlessEndTurnCleanupFlow runs
    // its reset pass (the replacement for the registry sweep). Live per-duration expiry coverage: G3.5-CVA1 /
    // G9-073 (turn-end bucket resets), BT1.StopRemainder (UntilEachTurnEnd player bucket).
    EngineContext context = CostBoard();   // initialises the TurnController so Cleanup has a turn state
    EndTurnCleanupResult result = new HeadlessEndTurnCleanupFlow().Cleanup(context, context.TurnController.Current);
    AssertEqual(true, result.Applied, "the AS-IS turn-end cleanup (bucket reset) runs — the retired registry sweep's replacement");
}

void PlayerScopeSecurityAttack()
{
    EngineContext context = Board();
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = P1.Value,
        [ModifierHelpers.SecurityAttackDeltaKey] = 1,
    };
    Register(context, "pscope:sa:p1", P1, Array.Empty<HeadlessEntityId>(), values, duration: null);

    AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Card).Strike, "P1 card boosted by player-scope");
    AssertEqual(1, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, new HeadlessEntityId("p2:main:O1")).Strike, "P2 card unaffected");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 5);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("C1"), "C1", "Card", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("O1"), "O1", "Opp", new Dictionary<string, object?>(), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new HeadlessEntityId("p2:main:O1"), new HeadlessEntityId("O1"), P2));
    return context;
}

// A live match past None/Setup so the AS-IS ChangeCostClass fold's CanUse -> CanTrigger -> DoneStartGame gate
// (and the Players_ForTurnPlayer player-scope scan that folds the bucket) run.
EngineContext CostBoard()
{
    EngineContext context = Board();
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

// AS-IS ChangeCostClass on the owner's UntilCalculateFixedCostEffect bucket (self-scoped ±cost). isUpDown:true so
// a reduction respects Player.CanReduceCost; here no immunity is granted so it always applies (floored at 0).
void AddBucketCostModifier(EngineContext context, HeadlessEntityId cardId, int delta)
{
    var carrier = new CardSource(context, new HeadlessEntityId($"cost-src:{delta}"), P1);
    var changeCostClass = new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ChangeCostClass();
    changeCostClass.SetUpICardEffect($"Cost {delta:+#;-#;0}", _ => true, carrier);
    changeCostClass.SetUpChangeCostClass(
        changeCostFunc: (cs, cost, root, targetPermanents) => cost + delta,
        cardSourceCondition: cs => cs is not null && cs.InstanceId == cardId,
        rootCondition: root => true,
        isUpDown: () => true,
        isCheckAvailability: () => false,
        isChangePayingCost: () => true);
    new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Player(context, P1).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);
}

void RegisterModifier(EngineContext context, HeadlessEntityId cardId, string deltaKey, int delta, EffectDuration? duration)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [deltaKey] = delta };
    Register(context, $"mod:{cardId.Value}:{deltaKey}:{delta}", P1, new[] { cardId }, values, duration);
}

void Register(EngineContext context, string effectId, HeadlessPlayerId owner, HeadlessEntityId[] targets, Dictionary<string, object?> values, EffectDuration? duration)
{
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{effectId}"),
        triggerEntityId: null, targetEntityIds: targets, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId(effectId), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope },
        effect: null, duration: duration));
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
