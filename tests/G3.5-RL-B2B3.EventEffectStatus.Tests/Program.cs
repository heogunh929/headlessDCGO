using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-B2: structured GameEvent schema (actor/subject/zoneFrom/zoneTo/cause).
// G3.5-RL-B3: effect resolution status — unbound (skeleton) effects are countable, not silent.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Mover = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("CardMoved event carries structured actor/subject/zone fields (B2)", CardMovedIsStructured),
    ("CardMoved event keeps legacy metadata for back-compat (B2)", CardMovedKeepsLegacyMetadata),
    ("Plain events leave structured fields null (B2 additive)", () => Pure(PlainEventsAreNull)),
    ("EffectResult.Unbound is resolved but flagged unbound (B3)", () => Pure(UnboundResultStatus)),
    ("Success and Failure map to their statuses (B3)", () => Pure(SuccessFailureStatus)),
    ("Scheduler counts unbound resolutions while draining (B3)", SchedulerCountsUnbound),
    ("Real resolver reports unbound for missing bindings (B3)", RealResolverReportsUnbound),
    ("Unbound count surfaces in the observation (B3)", UnboundSurfacesInObservation),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
}

// --- B2 ------------------------------------------------------------------

async Task CardMovedIsStructured()
{
    DcgoMatch match = await CreateMatchAsync();
    ZoneMoveResult result = await match.Context.ZoneMover.MoveAsync(
        new ZoneMoveRequest(P1, Mover, ChoiceZone.Hand, ChoiceZone.BattleArea));

    GameEvent moved = result.Event;
    AssertEqual(GameEventType.CardMoved, moved.Type, "event type");
    AssertEqual(P1, moved.Actor, "actor = mover's player");
    AssertEqual(Mover, moved.Subject, "subject = moved card");
    AssertEqual(ChoiceZone.Hand, moved.ZoneFrom, "zone from");
    AssertEqual(ChoiceZone.BattleArea, moved.ZoneTo, "zone to");
    AssertEqual("Move", moved.Cause, "cause = operation");
}

async Task CardMovedKeepsLegacyMetadata()
{
    DcgoMatch match = await CreateMatchAsync();
    ZoneMoveResult result = await match.Context.ZoneMover.MoveAsync(
        new ZoneMoveRequest(P1, Mover, ChoiceZone.Hand, ChoiceZone.BattleArea));

    AssertTrue(result.Event.Metadata.ContainsKey("cardId"), "legacy cardId metadata present");
    AssertTrue(result.Event.Metadata.ContainsKey("fromZone"), "legacy fromZone metadata present");
}

void PlainEventsAreNull()
{
    GameEvent plain = new(1, GameEventType.StateChanged, "x", new Dictionary<string, object?>());
    AssertTrue(plain.Actor is null, "actor null");
    AssertTrue(plain.Subject is null, "subject null");
    AssertTrue(plain.ZoneFrom is null, "zoneFrom null");
    AssertTrue(plain.Cause is null, "cause null");
}

// --- B3 ------------------------------------------------------------------

void UnboundResultStatus()
{
    EffectResult unbound = EffectResult.Unbound("no body");
    AssertTrue(unbound.Resolved, "unbound still resolves (queue drains)");
    AssertTrue(unbound.IsUnbound, "IsUnbound true");
    AssertEqual(EffectResolutionStatus.Unbound, unbound.Status, "status unbound");
}

void SuccessFailureStatus()
{
    AssertEqual(EffectResolutionStatus.Resolved, EffectResult.Success().Status, "success -> resolved");
    AssertEqual(EffectResolutionStatus.Failed, EffectResult.Failure("x").Status, "failure -> failed");
}

async Task SchedulerCountsUnbound()
{
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (_, _) => Task.FromResult(EffectResult.Unbound("stub")));
    scheduler.Enqueue(CreateRequest("e1"));
    scheduler.Enqueue(CreateRequest("e2"));

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertEqual(2, scheduler.TotalUnboundCount, "two unbound resolutions counted");
    AssertEqual(0, scheduler.PendingCount, "queue still drains on unbound");
    AssertTrue(results.All(r => r.IsUnbound), "all results unbound");
}

async Task RealResolverReportsUnbound()
{
    var resolver = CardEffectSchedulerResolver.Create(new InMemoryEffectRegistry());
    var scheduler = new EffectScheduler(new EffectResolutionQueue(), resolver);
    scheduler.Enqueue(CreateRequest("unbound-effect"));

    await scheduler.ResolveAllAsync();

    AssertEqual(1, scheduler.TotalUnboundCount, "real resolver reports unbound for missing binding");
}

async Task UnboundSurfacesInObservation()
{
    DcgoMatch match = await CreateMatchAsync();
    match.Context.EffectScheduler.Enqueue(CreateRequest("unbound-effect"));
    await match.Context.EffectScheduler.ResolveAllAsync();

    EncodedObservation encoded = new ObservationEncoder().Encode(match.GetObservation());
    ObservationFeature unbound = encoded.Features.First(f => f.Name == "effects.totalUnbound");
    AssertEqual(1d, unbound.Value, "observation surfaces the unbound count");
}

// --- Harness -------------------------------------------------------------

static EffectRequest CreateRequest(string effectId)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        "Main",
        new EffectContext(
            player,
            player,
            new HeadlessEntityId($"source-{effectId}"),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

async Task<DcgoMatch> CreateMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDigimon($"P1-M{index:D2}"));
        cards.Upsert(CreateDigimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1"), BuildDeck(P2, "P2") },
        firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);
    return match;
}

static CardRecord CreateDigimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}
