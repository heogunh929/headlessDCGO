using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predecessor goal G3.5-005 is complete", PredecessorGoalsComplete),
    ("Auto-process collects and resolves a triggered effect from a published event", PublishedEventCollectsAndResolvesTrigger),
    ("Zone move bridges into event-driven trigger collection", ZoneMoveBridgesIntoTriggerCollection),
    ("Empty registry keeps the loop stable without progress", EmptyRegistryKeepsLoopStable),
    ("Sync cursor prevents the same event from collecting twice", SyncCursorPreventsDoubleCollection),
    ("GameEventQueue cursor and reset behave correctly", QueueCursorAndResetBehaveCorrectly),
    ("EngineContext registers the queue and reset clears it", EngineContextRegistersQueueAndResetClearsIt),
    ("Auto-processing wiring source is clean", AutoProcessingWiringSourceIsClean),
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
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

Task PredecessorGoalsComplete()
{
    AssertComplete("G3.5-005_attack_pipeline_integration_unit_test_results.md");
    return Task.CompletedTask;
}

async Task PublishedEventCollectsAndResolvesTrigger()
{
    EngineContext context = EngineContext.CreateDefault();
    var effect = new RecordingFakeEffect("fx-pub", "src-pub", "OnTestTrigger");
    EffectRequest request = CreateRequest("fx-pub", "src-pub", "OnTestTrigger");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));

    context.GameEventQueue.Publish(new GameEvent(
        1,
        GameEventType.StateChanged,
        "test trigger window",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = "OnTestTrigger",
        }));

    var processor = new GameFlowProcessor();
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsStable, "stable");
    AssertTrue(result.ProgressedAny, "progressed from collected trigger");
    AssertEqual(1, effect.ResolveCalls, "collected effect resolved exactly once");
    AssertEqual(0, context.EffectScheduler.PendingCount, "scheduler drained");
    AssertEqual(0, context.GameEventQueue.PendingCount, "event queue drained");
}

async Task ZoneMoveBridgesIntoTriggerCollection()
{
    EngineContext context = EngineContext.CreateDefault();
    var cardId = new HeadlessEntityId("p1:main:001:X");
    var effect = new RecordingFakeEffect("fx-move", cardId.Value, "CardMoved");
    EffectRequest request = CreateRequest("fx-move", cardId.Value, "CardMoved");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, cardId, ChoiceZone.None, ChoiceZone.BattleArea));

    var processor = new GameFlowProcessor();
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.ProgressedAny, "zone move bridged into a trigger");
    AssertEqual(1, effect.ResolveCalls, "bridged CardMoved event resolved the bound effect");
}

async Task EmptyRegistryKeepsLoopStable()
{
    EngineContext context = EngineContext.CreateDefault();
    var cardId = new HeadlessEntityId("p1:main:002:Y");

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, cardId, ChoiceZone.None, ChoiceZone.BattleArea));

    var processor = new GameFlowProcessor();
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsStable, "stable with no registered triggers");
    AssertTrue(!result.ProgressedAny, "no progress when nothing matches");
    AssertEqual(0, result.ResolvedEffectCount, "nothing resolved");
    AssertTrue(result.Iterations <= 2, "loop terminates immediately without spinning");
    AssertEqual(0, context.GameEventQueue.PendingCount, "events still drained even when unmatched");
}

async Task SyncCursorPreventsDoubleCollection()
{
    EngineContext context = EngineContext.CreateDefault();
    var cardId = new HeadlessEntityId("p1:main:003:Z");
    var effect = new RecordingFakeEffect("fx-once", cardId.Value, "CardMoved");
    EffectRequest request = CreateRequest("fx-once", cardId.Value, "CardMoved");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, cardId, ChoiceZone.None, ChoiceZone.BattleArea));

    var processor = new GameFlowProcessor();
    await processor.RunToStableAsync(context);
    await processor.RunToStableAsync(context);

    AssertEqual(1, effect.ResolveCalls, "the same zone event is only collected once across passes");
}

Task QueueCursorAndResetBehaveCorrectly()
{
    var queue = new GameEventQueue();
    GameEvent moved = new(
        1,
        GameEventType.CardMoved,
        "card moved",
        new Dictionary<string, object?>(StringComparer.Ordinal));
    var source = new List<GameEvent> { moved };

    AssertEqual(1, queue.SyncFrom(source), "first sync appends the new event");
    AssertEqual(1, queue.PendingCount, "event pending");
    AssertEqual(1, queue.DrainPending().Count, "drain returns the event");
    AssertEqual(0, queue.PendingCount, "drain clears pending");

    AssertEqual(0, queue.SyncFrom(source), "second sync over an unchanged source appends nothing");
    AssertEqual(0, queue.PendingCount, "no re-collection");

    queue.ResetMatchState();
    AssertEqual(1, queue.SyncFrom(source), "after reset the cursor restarts");
    AssertEqual(1, queue.PendingCount, "event re-enqueued after reset");
    return Task.CompletedTask;
}

Task EngineContextRegistersQueueAndResetClearsIt()
{
    EngineContext context = EngineContext.CreateDefault();
    AssertTrue(context.TryGetService<GameEventQueue>(out GameEventQueue? service) && service is not null, "queue registered as a service");
    AssertTrue(ReferenceEquals(service, context.GameEventQueue), "registered queue is the context queue");

    context.GameEventQueue.Publish(new GameEvent(1, GameEventType.StateChanged, "a", new Dictionary<string, object?>()));
    context.GameEventQueue.Publish(new GameEvent(2, GameEventType.StateChanged, "b", new Dictionary<string, object?>()));
    AssertEqual(2, context.GameEventQueue.PendingCount, "two events pending");

    context.ResetMatchState();
    AssertEqual(0, context.GameEventQueue.PendingCount, "ResetMatchState clears the queue");
    return Task.CompletedTask;
}

Task AutoProcessingWiringSourceIsClean()
{
    string flowPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameFlowProcessor.cs");
    string flow = File.ReadAllText(flowPath);
    AssertContains(flow, "AutoProcessingTriggerCollector", "flow processor drives the trigger collector");
    AssertContains(flow, "GameEventQueue", "flow processor drains the game event queue");

    string queuePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameEventQueue.cs");
    if (!File.Exists(queuePath))
    {
        throw new FileNotFoundException($"Game event queue source was not found: {queuePath}");
    }

    string queue = File.ReadAllText(queuePath);
    AssertFalse(queue.Contains("TODO", StringComparison.OrdinalIgnoreCase), "GameEventQueue must not contain TODO");
    AssertFalse(queue.Contains("NotImplementedException", StringComparison.Ordinal), "GameEventQueue must not throw NotImplementedException");
    AssertFalse(queue.Contains("UnityEngine", StringComparison.Ordinal), "GameEventQueue must not reference UnityEngine");
    return Task.CompletedTask;
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        timing,
        new EffectContext(
            player,
            player,
            new HeadlessEntityId(sourceId),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src"))
            && Directory.Exists(Path.Combine(directory.FullName, "docs")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root with 'src' and 'docs' was not found.");
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId),
            new HeadlessEntityId(sourceId),
            name: effectId,
            timing: timing);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
