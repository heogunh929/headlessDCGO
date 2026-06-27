using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W6: the counter timing window opens once per attack, before block timing (AS-IS AttackProcess:
// State=Counter -> CounterTiming -> Block). It is a GLOBAL window (no subject filter), so any card's
// OnCounterTiming / [Counter] effect is collected and self-gates, mirroring StackSkillInfos.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Advancing a declared attack opens an OnCounter timing window", AdvanceOpensCounterWindow),
    ("The counter window is global (every OnCounter effect fires)", CounterWindowIsGlobal),
    ("The counter window opens before block timing", CounterBeforeBlock),
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

async Task AdvanceOpensCounterWindow()
{
    DcgoMatch match = await CreateMatchAsync();
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    DrainEvents(match.Context);

    await new AttackPipeline().AdvanceAsync(match.Context);

    GameEvent window = match.Context.GameEventQueue.DrainPending()
        .Single(e => string.Equals(e.Cause, TriggerTimings.OnCounter, StringComparison.Ordinal));
    AssertEqual(Player, window.Actor, "counter window actor is the attacking player");
}

async Task CounterWindowIsGlobal()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;

    // Two different cards each carry an OnCounter effect; both must fire (no subject scoping).
    var onCounterA = Register(context, "ctr-a", "src-a", TriggerTimings.OnCounter);
    var onCounterB = Register(context, "ctr-b", "src-b", TriggerTimings.OnCounter);

    context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    DrainEvents(context);

    await new AttackPipeline().AdvanceAsync(context);

    // Drain the counter window through the common-loop collector/scheduler.
    var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
    foreach (GameEvent gameEvent in context.GameEventQueue.DrainPending())
    {
        if (gameEvent.Type != GameEventType.Unknown)
        {
            collector.CollectAndEnqueueAll(gameEvent, context.EffectScheduler);
        }
    }

    await context.EffectScheduler.ResolveAllAsync();

    AssertEqual(1, onCounterA.ResolveCalls, "card A's OnCounter effect fired");
    AssertEqual(1, onCounterB.ResolveCalls, "card B's OnCounter effect fired (global window)");
}

async Task CounterBeforeBlock()
{
    // With no blockers the attack auto-advances Declared -> Combat, but the counter window must still
    // have been emitted during that step.
    DcgoMatch match = await CreateMatchAsync();
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    DrainEvents(match.Context);

    await new AttackPipeline().AdvanceAsync(match.Context);

    bool hasCounter = match.Context.GameEventQueue.DrainPending()
        .Any(e => string.Equals(e.Cause, TriggerTimings.OnCounter, StringComparison.Ordinal));
    AssertTrue(hasCounter, "counter window emitted while advancing out of the declared phase");
}

// --- Harness -------------------------------------------------------------

RecordingFakeEffect Register(EngineContext context, string effectId, string sourceId, string timing)
{
    var effect = new RecordingFakeEffect(effectId, sourceId, timing);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId(effectId), Player, timing,
            new EffectContext(Player, Player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())),
        effect: effect));
    return effect;
}

void DrainEvents(EngineContext context)
{
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    context.GameEventQueue.DrainPending();
}

async Task<DcgoMatch> CreateMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") }, firstPlayerId: Player, initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction[] advance = match.GetLegalActions(playerId)
            .Where(a => a.ActionType == HeadlessActionTypes.AdvancePhase).ToArray();
        AssertEqual(1, advance.Length, "advance phase count");
        await match.ApplyActionAsync(advance[0]);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

// --- Assertions ----------------------------------------------------------

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
