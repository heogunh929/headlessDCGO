using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-026): CardEffectFactory.SetMemoryTo3TamerEffect — "[Start of Your Turn] If you have 2 or less
// memory, set your memory to 3." (Tamer memory-setter, triggered on OnStartTurn). Resolves only on the
// owner's turn and only when memory <= 2. Verified by driving the effect's ResolveAsync + sink flush.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Owner's turn, memory 1 (<=2) -> set to 3", () => Run(turnPlayer: 1, memory: 1, expected: 3)),
    ("Owner's turn, memory 2 (boundary) -> set to 3", () => Run(turnPlayer: 1, memory: 2, expected: 3)),
    ("Owner's turn, memory 5 (>2) -> unchanged", () => Run(turnPlayer: 1, memory: 5, expected: 5)),
    ("Opponent's turn -> unchanged (IsOwnerTurn guard)", () => Run(turnPlayer: 2, memory: 1, expected: 1)),
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

async Task Run(int turnPlayer, int memory, int expected)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 926);
    context.TurnController.Initialize(new[] { P1, P2 }, new HeadlessPlayerId(turnPlayer));
    context.MemoryController.Set(memory);

    var tamer = await PlaceTamer(context, P1);
    ICardEffect effect = CardEffectFactory.SetMemoryTo3TamerEffect(new CardSource(context, tamer, P1));

    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    await ((IHeadlessCardEffect)effect).ResolveAsync(new CardEffectResolveContext(effect.ToBinding("setmem").Request), sink);
    await sink.FlushAsync();

    AssertEqual(expected, context.MemoryController.Current.Current,
        $"turnPlayer={turnPlayer}, memory {memory} -> {expected}");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceTamer(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TAMER");
    cards.Upsert(new CardRecord(defId, "TAMER", "Tamer", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:TAMER");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
