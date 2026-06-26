using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W4: security effect wiring. A revealed [Security] card's own effect (bound to OnSecurityCheck)
// fires through the common loop, scoped to that card — effects bound to OnSecurityCheck on OTHER cards
// stay dormant. SecurityResolver emits the scoped timing window (W1-2 + W4 scoping); the collector
// resolves only the subject's effect.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string OnSecurityCheck = TriggerTimings.OnSecurityCheck;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId SecurityThreeId = new("p2:main:008:P2-M08");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Scoped OnSecurityCheck fires only the subject card's effect", ScopedFiresSubjectOnly),
    ("A different card's OnSecurityCheck effect stays dormant", OtherCardStaysDormant),
    ("Unscoped timing window (no subject) still fires all bound effects", UnscopedFiresAll),
    ("SecurityResolver emits an OnSecurityCheck window scoped to the revealed card", ResolverEmitsScopedWindow),
    ("End to end: revealed security card's effect fires, the next one does not", EndToEndSecurityEffectFires),
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

// --- Collector-level scoping --------------------------------------------

async Task ScopedFiresSubjectOnly()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
    RecordingFakeEffect onCardA = Register(context, "fxA", "cardA", OnSecurityCheck);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", OnSecurityCheck);

    TriggerEventEmitter.Emit(context.GameEventQueue, OnSecurityCheck, actor: P2, subject: new HeadlessEntityId("cardA"));
    await DrainCollectResolveAsync(context);

    AssertEqual(1, onCardA.ResolveCalls, "subject card's effect fired");
    AssertEqual(0, onCardB.ResolveCalls, "other card's effect did NOT fire (scoped)");
}

async Task OtherCardStaysDormant()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 92);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", OnSecurityCheck);

    // Reveal cardA — there is no effect bound to cardA, and cardB's effect must not fire either.
    TriggerEventEmitter.Emit(context.GameEventQueue, OnSecurityCheck, actor: P2, subject: new HeadlessEntityId("cardA"));
    await DrainCollectResolveAsync(context);

    AssertEqual(0, onCardB.ResolveCalls, "unrelated security card's effect stayed dormant");
}

async Task UnscopedFiresAll()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 93);
    RecordingFakeEffect onCardA = Register(context, "fxA", "cardA", TriggerTimings.OnEndTurn);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", TriggerTimings.OnEndTurn);

    // No subject -> global timing window: every effect bound to the timing fires (turn boundaries).
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1);
    await DrainCollectResolveAsync(context);

    AssertEqual(1, onCardA.ResolveCalls, "card A end-turn effect fired");
    AssertEqual(1, onCardB.ResolveCalls, "card B end-turn effect fired");
}

// --- SecurityResolver integration ---------------------------------------

async Task ResolverEmitsScopedWindow()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 3);
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);
    AssertTrue(result.IsSuccess, "security resolved");

    GameEvent window = match.Context.GameEventQueue.DrainPending()
        .Single(e => string.Equals(e.Cause, OnSecurityCheck, StringComparison.Ordinal));

    AssertEqual(SecurityOneId, window.Subject, "window subject is the revealed card");
    AssertTrue(
        window.Metadata.TryGetValue(AutoProcessingTriggerCollector.SourceEntityIdKey, out object? scoped)
            && scoped is HeadlessEntityId id && id == SecurityOneId,
        "window carries a SourceEntityId filter scoped to the revealed card");
}

async Task EndToEndSecurityEffectFires()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 2);
    EngineContext context = match.Context;

    // The top security card carries a [Security] effect; the next one carries one too but must not fire
    // because only one card is revealed (strike 1).
    RecordingFakeEffect revealed = Register(context, "sec-fx-1", SecurityOneId.Value, OnSecurityCheck);
    RecordingFakeEffect unrevealed = Register(context, "sec-fx-2", SecurityTwoId.Value, OnSecurityCheck);

    DeclareDirectAttack(match);
    await new SecurityResolver().ResolveAsync(context);
    await DrainCollectResolveAsync(context);

    AssertEqual(1, revealed.ResolveCalls, "revealed security card's effect fired once");
    AssertEqual(0, unrevealed.ResolveCalls, "the un-revealed security card's effect did not fire");
}

// --- Common-loop emulation (mirrors GameFlowProcessor.AutoProcessAsync) --

async Task DrainCollectResolveAsync(EngineContext context)
{
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
    foreach (GameEvent gameEvent in context.GameEventQueue.DrainPending())
    {
        if (gameEvent.Type == GameEventType.Unknown)
        {
            continue;
        }

        collector.CollectAndEnqueueAll(gameEvent, context.EffectScheduler);
    }

    await context.EffectScheduler.ResolveAllAsync();
}

RecordingFakeEffect Register(EngineContext context, string effectId, string sourceId, string timing)
{
    var effect = new RecordingFakeEffect(effectId, sourceId, timing);
    context.EffectRegistry.Register(new EffectBinding(CreateRequest(effectId, sourceId, timing), effect: effect));
    return effect;
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(2);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

// --- Match harness (trimmed from G2G-004) --------------------------------

void DeclareDirectAttack(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(int strike, int securityCount)
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
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    HeadlessEntityId[] securityCards = { SecurityOneId, SecurityTwoId, SecurityThreeId };
    for (int index = 0; index < securityCount; index++)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, securityCards[index], ChoiceZone.None, ChoiceZone.Security));
    }

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, [SecurityResolver.StrikeKey] = strike });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });

    // Drain the events produced by setup so the test only observes events from the security check.
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    context.GameEventQueue.DrainPending();
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
        LegalAction[] actions = match.GetLegalActions(playerId)
            .Where(action => action.ActionType == HeadlessActionTypes.AdvancePhase)
            .ToArray();
        AssertEqual(1, actions.Length, "advance phase count");
        await match.ApplyActionAsync(actions[0]);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
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
