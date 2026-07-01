using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-034): Tamer memory-gain effects via the new TriggeredGainMemoryEffect (AddMemory + owner-turn +
// optional condition). Gain1 gains 1 only if the opponent has a Digimon; Gain2 gains 2.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gain1: opponent has a Digimon -> +1 (0 -> 1)", () => Gain1(opponentDigimon: true, expected: 1)),
    ("Gain1: opponent has NO Digimon -> no change (0)", () => Gain1(opponentDigimon: false, expected: 0)),
    ("Gain2: always +2 (0 -> 2)", Gain2),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Gain1(bool opponentDigimon, int expected)
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    var tamer = await Place(context, P1, "TAMER", "Tamer", ChoiceZone.BattleArea);
    if (opponentDigimon)
    {
        await Place(context, P2, "FOE", "Digimon", ChoiceZone.BattleArea);
    }

    await Resolve(context, CardEffectFactory.Gain1MemoryTamerOpponentDigimonEffect(new CardSource(context, tamer, P1)));
    AssertEqual(expected, context.MemoryController.Current.Current, $"opponentDigimon={opponentDigimon} -> {expected}");
}

async Task Gain2()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    var tamer = await Place(context, P1, "TAMER", "Tamer", ChoiceZone.BattleArea);
    await Resolve(context, CardEffectFactory.Gain2MemoryOptionDelayEffect(new CardSource(context, tamer, P1)));
    AssertEqual(2, context.MemoryController.Current.Current, "Gain2 -> +2");
}

// --- Helpers -------------------------------------------------------------

async Task Resolve(EngineContext context, ICardEffect effect)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    await ((IHeadlessCardEffect)effect).ResolveAsync(new CardEffectResolveContext(effect.ToBinding("gain").Request), sink);
    await sink.FlushAsync();
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 934);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, string cardType, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
