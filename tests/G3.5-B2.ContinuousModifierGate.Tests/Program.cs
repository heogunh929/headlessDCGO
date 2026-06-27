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
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 1), "+1 security attack");
}

void PlayCostReduce()
{
    EngineContext context = Board();
    RegisterModifier(context, Card, ModifierHelpers.PlayCostDeltaKey, -2, duration: null);
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Card, basePlayCost: 5), "-2 play cost");

    EngineContext floored = Board();
    RegisterModifier(floored, Card, ModifierHelpers.PlayCostDeltaKey, -5, duration: null);
    AssertEqual(0, ContinuousModifierGate.ResolvePlayCost(floored, Card, basePlayCost: 1), "play cost floored at 0");
}

void DigivolutionCostReduce()
{
    EngineContext context = Board();
    RegisterModifier(context, Card, ModifierHelpers.DigivolutionCostDeltaKey, -1, duration: null);
    AssertEqual(3, ContinuousModifierGate.ResolveDigivolutionCost(context, Card, baseDigivolutionCost: 4), "-1 digivolution cost");
}

void SecurityAttackDurationExpires()
{
    EngineContext context = Board();
    RegisterModifier(context, Card, ModifierHelpers.SecurityAttackDeltaKey, 2, EffectDuration.UntilEachTurnEnd);

    AssertEqual(3, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 1), "boost before expiry");
    EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1);
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 1), "back to base after expiry");
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

    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 1), "P1 card boosted by player-scope");
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, new HeadlessEntityId("p2:main:O1"), baseSecurityAttack: 1), "P2 card unaffected");
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
