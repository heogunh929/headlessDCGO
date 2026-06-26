using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W1 (part 2): the engine now EMITS timing-window events at action points that produce no zone
// move — turn boundaries, digivolution, draw, security checks — so effects bound to OnStartTurn /
// OnEndTurn / WhenDigivolving / OnDraw / OnSecurityCheck fire through the common loop.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Emitter publishes an event that derives exactly the given timing", () => Pure(EmitterPublishesTiming)),
    ("Canonical timing constants are defined", () => Pure(ConstantsDefined)),
    ("OnEndTurn effect fires when a turn ends", EndTurnFiresOnEndTurn),
    ("OnStartTurn effect fires on the next turn", EndTurnFiresOnStartTurn),
    ("A turn-boundary effect bound to an unrelated timing does NOT fire", UnrelatedTimingDoesNotFire),
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

// --- Emitter -------------------------------------------------------------

void EmitterPublishesTiming()
{
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(queue, TriggerTimings.WhenDigivolving, actor: P1, subject: new HeadlessEntityId("c1"));

    IReadOnlyList<GameEvent> drained = queue.DrainPending();
    AssertEqual(1, drained.Count, "one event published");

    GameEvent e = drained[0];
    AssertEqual(P1, e.Actor, "structured actor");
    AssertEqual(TriggerTimings.WhenDigivolving, e.Cause, "cause = timing");

    var timings = TriggerTimingMap.Derive(e);
    AssertEqual(1, timings.Count, "derives exactly the emitted timing");
    AssertEqual(TriggerTimings.WhenDigivolving, timings[0], "timing value");
}

void ConstantsDefined()
{
    foreach (string t in new[]
    {
        TriggerTimings.OnStartTurn, TriggerTimings.OnEndTurn, TriggerTimings.WhenDigivolving,
        TriggerTimings.OnDraw, TriggerTimings.OnSecurityCheck,
    })
    {
        AssertTrue(!string.IsNullOrWhiteSpace(t), "timing constant non-empty");
    }
}

// --- E2E -----------------------------------------------------------------

async Task EndTurnFiresOnEndTurn()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndTurn);
    await EndTurnAsync(match);
    AssertEqual(1, effect.ResolveCalls, "OnEndTurn effect fired exactly once when the turn ended");
}

async Task EndTurnFiresOnStartTurn()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnStartTurn);
    await EndTurnAsync(match);
    AssertEqual(1, effect.ResolveCalls, "OnStartTurn effect fired when the next turn started");
}

async Task UnrelatedTimingDoesNotFire()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync("OnNeverHappens");
    await EndTurnAsync(match);
    AssertEqual(0, effect.ResolveCalls, "an effect bound to an unrelated timing does not fire on end turn");
}

async Task EndTurnAsync(DcgoMatch match)
{
    match.Context.TurnController.SetPhase(HeadlessPhase.End);
    await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(P1));
    await match.StepAsync();
}

// --- Harness -------------------------------------------------------------

async Task<(DcgoMatch Match, RecordingFakeEffect Effect)> CreateMatchAsync(string timing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var effect = new RecordingFakeEffect("fx", "src", timing);
    context.EffectRegistry.Register(new EffectBinding(CreateRequest("fx", "src", timing), effect: effect));
    return (match, effect);
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
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
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
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
