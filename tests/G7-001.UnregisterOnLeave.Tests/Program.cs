using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G7-001: a card's auto-registered effects are removed when it LEAVES PLAY by any path, not just deletion.
// Especially player-scope effects (matched by owner only) must stop applying once the source bounces /
// returns to deck / goes to security / is trashed.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Bouncing a player-scope Tamer (ST1_12) removes its +1000 DP ghost buff", BouncePlayerScope),
    ("Trashing a Digimon (ST7_10) removes its keyword/SA effects", TrashSelfEffect),
    ("Returning a Digimon to deck removes its effects", ReturnToDeck),
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

async Task BouncePlayerScope()
{
    EngineContext context = Board();
    HeadlessEntityId tamer = await Place(context, P1, "ST1_12", "Tamer");
    HeadlessEntityId mine = await Place(context, P1, "MYDIGI", "Digimon");
    AssertTrue(CardEffectRegistrar.RegisterCard(context, tamer, P1), "ST1_12 registered");

    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "player-scope +1000 while Tamer in play");

    await ApplyLeave(context, MatchStateMutationSink.ReturnToHandKind, tamer);
    AssertEqual(2000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "buff gone after the Tamer bounced");
}

async Task TrashSelfEffect()
{
    EngineContext context = Board();
    HeadlessEntityId card = await Place(context, P1, "ST7_10", "Digimon");
    AssertTrue(CardEffectRegistrar.RegisterCard(context, card, P1), "ST7_10 registered");

    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, card, baseSecurityAttack: 1), "SA +1 while in play");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Piercing").Count >= 1, "Piercing while in play");

    await ApplyLeave(context, MatchStateMutationSink.TrashCardKind, card);
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, card, baseSecurityAttack: 1), "SA gone after trash");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Piercing").Count == 0, "Piercing gone after trash");
}

async Task ReturnToDeck()
{
    EngineContext context = Board();
    HeadlessEntityId card = await Place(context, P1, "ST7_10", "Digimon");
    AssertTrue(CardEffectRegistrar.RegisterCard(context, card, P1), "ST7_10 registered");
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, card, baseSecurityAttack: 1), "SA +1 while in play");

    await ApplyLeave(context, MatchStateMutationSink.ReturnToDeckTopKind, card);
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, card, baseSecurityAttack: 1), "SA gone after return-to-deck");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 701);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string cardNumber, string type)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId(cardNumber), cardNumber, cardNumber, new Dictionary<string, object?>(), CardType: type));
    var id = new HeadlessEntityId($"p1:battle:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task ApplyLeave(EngineContext context, string kind, HeadlessEntityId target)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(kind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    await sink.FlushAsync();
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
