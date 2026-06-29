using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// Phase 1 — ST1 Red trigger wave: triggered memory effects (the ActivateClass "gain/lose N memory" form).
//   ST1_06: <Blocker> + [When Attacking] lose 2 memory
//   ST1_09: [Your Turn] when blocked, gain 3 memory   (inherited, owner-turn gated)
// The trigger body is resolved through the real MatchStateMutationSink so the AddMemory mutation reaches
// the memory controller — validating the trigger recipe (effect body -> sink -> memory) and condition.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_06: <Blocker> is registered", () => Pure(ST1_06_Blocker)),
    ("ST1_06: [When Attacking] resolves to -2 memory", ST1_06_LoseMemory),
    ("ST1_09: gains 3 memory on the owner's turn", () => ST1_09_Memory(ownerTurn: true, expected: 3)),
    ("ST1_09: no memory gain on the opponent's turn", () => ST1_09_Memory(ownerTurn: false, expected: 0)),
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

void ST1_06_Blocker()
{
    (EngineContext context, _) = Card("ST1_06");
    CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, new HeadlessEntityId("p1:battle:ST1_06"), P1));
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "Blocker registered");
}

async Task ST1_06_LoseMemory()
{
    (EngineContext context, HeadlessEntityId card) = Card("ST1_06");
    IReadOnlyList<EffectBinding> bindings =
        CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, card, P1));
    context.MemoryController.Set(3);
    await ResolveTrigger(context, bindings, "OnAllyAttack");
    AssertEqual(1, context.MemoryController.Current.Current, "3 - 2 = 1 memory");
}

async Task ST1_09_Memory(bool ownerTurn, int expected)
{
    (EngineContext context, HeadlessEntityId card) = Card("ST1_09");
    IReadOnlyList<EffectBinding> bindings =
        CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_09(), "ST1_09", new CardSource(context, card, P1));
    context.TurnController.Initialize(new[] { P1, P2 }, ownerTurn ? P1 : P2);
    context.MemoryController.Set(0);
    await ResolveTrigger(context, bindings, "OnBlockAnyone");
    AssertEqual(expected, context.MemoryController.Current.Current, ownerTurn ? "+3 on owner turn" : "no change on opponent turn");
}

// --- Helpers -------------------------------------------------------------

(EngineContext, HeadlessEntityId) Card(string number)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId(number), number, number, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{number}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    return (context, id);
}

// Resolves the trigger effect bound to `timing` through the real match-state sink (the production path
// a fired trigger would take via the scheduler).
async Task ResolveTrigger(EngineContext context, IReadOnlyList<EffectBinding> bindings, string timing)
{
    EffectBinding binding = bindings.Single(b => string.Equals(b.Request.Timing, timing, StringComparison.Ordinal));
    AssertTrue(binding.Effect is not null, $"trigger binding for {timing} carries an effect body");

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, memory: context.MemoryController);
    var resolveContext = new CardEffectResolveContext(binding.Request);
    await binding.Effect!.ResolveAsync(resolveContext, sink);
    await sink.FlushAsync();
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
