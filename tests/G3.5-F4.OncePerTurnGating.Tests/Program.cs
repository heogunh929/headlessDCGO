using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-4: once-per-turn / max-count-per-turn auto-gating. OnceFlagController tracks per-turn use counts;
// the GameFlowProcessor trigger loop consults it so an effect bound with a MaxCountPerTurn cap does not
// activate beyond its limit in a turn, and the count resets at the start of each turn.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string Timing = "OnTestTiming";

var tests = new (string Name, Func<Task> Body)[]
{
    ("Uncapped effect always activates", () => Pure(UncappedAlwaysActivates)),
    ("Cap of 1 allows one activation per turn", () => Pure(CapOfOne)),
    ("Cap of 2 allows two activations per turn", () => Pure(CapOfTwo)),
    ("ResetForTurn clears the per-turn counts", () => Pure(ResetClearsCounts)),
    ("Triggered effect with cap 1 fires once per turn and again next turn", CappedTriggerGatedInLoop),
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

// --- Controller unit tests -----------------------------------------------

void UncappedAlwaysActivates()
{
    var controller = new OnceFlagController();
    EffectRequest req = Request("fx");
    for (int i = 0; i < 5; i++)
    {
        AssertTrue(controller.TryActivate(req, maxCountPerTurn: null), $"uncapped activation {i}");
    }
}

void CapOfOne()
{
    var controller = new OnceFlagController();
    EffectRequest req = Request("fx");
    AssertTrue(controller.TryActivate(req, 1), "first activation allowed");
    AssertFalse(controller.TryActivate(req, 1), "second activation blocked");
}

void CapOfTwo()
{
    var controller = new OnceFlagController();
    EffectRequest req = Request("fx");
    AssertTrue(controller.TryActivate(req, 2), "first allowed");
    AssertTrue(controller.TryActivate(req, 2), "second allowed");
    AssertFalse(controller.TryActivate(req, 2), "third blocked");
}

void ResetClearsCounts()
{
    var controller = new OnceFlagController();
    EffectRequest req = Request("fx");
    AssertTrue(controller.TryActivate(req, 1), "first allowed");
    AssertFalse(controller.TryActivate(req, 1), "blocked before reset");
    controller.ResetForTurn(turnSequence: 2, turnPlayerId: P1);
    AssertTrue(controller.TryActivate(req, 1), "allowed again after reset");
}

// --- End-to-end loop gating ----------------------------------------------

async Task CappedTriggerGatedInLoop()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(Timing, maxCountPerTurn: 1);

    await FireTimingAsync(match);
    AssertEqual(1, effect.ResolveCalls, "fired on first trigger this turn");
    await FireTimingAsync(match);
    AssertEqual(1, effect.ResolveCalls, "second trigger this turn is gated by the per-turn cap");

    await EndTurnAsync(match);
    await FireTimingAsync(match);
    AssertEqual(2, effect.ResolveCalls, "fires again after the turn reset");
}

async Task FireTimingAsync(DcgoMatch match)
{
    TriggerEventEmitter.Emit(match.Context.GameEventQueue, Timing);
    await match.StepAsync();
}

async Task EndTurnAsync(DcgoMatch match)
{
    match.Context.TurnController.SetPhase(HeadlessPhase.End);
    await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(P1));
    await match.StepAsync();
}

// --- Harness -------------------------------------------------------------

async Task<(DcgoMatch Match, RecordingFakeEffect Effect)> CreateMatchAsync(string timing, int maxCountPerTurn)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var effect = new RecordingFakeEffect("fx", "src", timing, maxCountPerTurn);
    context.EffectRegistry.Register(new EffectBinding(Request("fx", timing), effect: effect));
    return (match, effect);
}

EffectRequest Request(string effectId, string timing = Timing)
{
    return new EffectRequest(
        new HeadlessEntityId(effectId), P1, timing,
        new EffectContext(P1, P1, new HeadlessEntityId("src"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing, int? maxCountPerTurn)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing,
            maxCountPerTurn: maxCountPerTurn);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
