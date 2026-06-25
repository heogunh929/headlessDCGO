using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("Empty context reaches stable without progress", EmptyContextReachesStable),
    ("Loop resolves queued effects until stable", LoopResolvesQueuedEffects),
    ("Loop pauses when a choice is pending and resolves nothing", LoopPausesForPendingChoice),
    ("HeadlessGameLoop drains effects through the flow processor", GameLoopDrainsThroughFlowProcessor),
    ("FlowProcessResult factories report status correctly", FlowProcessResultFactories),
    ("Flow processor source has no placeholder or Unity dependency", FlowProcessorSourceIsClean),
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

async Task EmptyContextReachesStable()
{
    EngineContext context = EngineContext.CreateDefault();
    var processor = new GameFlowProcessor();

    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsStable, "stable");
    AssertTrue(!result.ProgressedAny, "no progress");
    AssertEqual(0, result.ResolvedEffectCount, "resolved count");
    AssertTrue(result.Iterations >= 1, "iterations");
}

async Task LoopResolvesQueuedEffects()
{
    EngineContext context = EngineContext.CreateDefault();
    var effect = new RecordingFakeEffect("fx-loop", "src-loop");
    EffectRequest request = CreateRequest("fx-loop", "src-loop");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));
    context.EffectScheduler.Enqueue(request, EffectResolutionMode.MainStack);

    var processor = new GameFlowProcessor();
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsStable, "stable");
    AssertTrue(result.ProgressedAny, "progressed");
    AssertEqual(1, result.ResolvedEffectCount, "resolved count");
    AssertEqual(1, effect.ResolveCalls, "effect invoked");
    AssertEqual(0, context.EffectScheduler.PendingCount, "queue drained");
}

async Task LoopPausesForPendingChoice()
{
    EngineContext context = EngineContext.CreateDefault();
    var effect = new RecordingFakeEffect("fx-paused", "src-paused");
    EffectRequest request = CreateRequest("fx-paused", "src-paused");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));
    context.EffectScheduler.Enqueue(request, EffectResolutionMode.MainStack);

    context.ChoiceController.RequestChoice(CardRequest(new HeadlessPlayerId(1)));

    var processor = new GameFlowProcessor();
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.PausedForChoice, "paused for choice");
    AssertEqual(0, result.ResolvedEffectCount, "nothing resolved while paused");
    AssertEqual(0, effect.ResolveCalls, "effect not invoked while paused");
    AssertEqual(1, context.EffectScheduler.PendingCount, "effect stays pending");
}

async Task GameLoopDrainsThroughFlowProcessor()
{
    EngineContext context = EngineContext.CreateDefault();
    var gameLoop = new HeadlessGameLoop(context);
    var effect = new RecordingFakeEffect("fx-step", "src-step");
    EffectRequest request = CreateRequest("fx-step", "src-step");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));
    context.EffectScheduler.Enqueue(request, EffectResolutionMode.MainStack);

    HeadlessGameLoopStep step = await gameLoop.StepAsync();

    AssertTrue(step.HadPendingEffects, "had pending effects");
    AssertEqual(1, step.ResolvedEffectCount, "resolved through flow processor");
    AssertEqual(1, effect.ResolveCalls, "effect invoked via game loop");
}

Task FlowProcessResultFactories()
{
    FlowProcessResult stable = FlowProcessResult.Stable(progressedAny: true, resolvedEffectCount: 2, iterations: 3);
    AssertTrue(stable.IsStable, "stable status");
    AssertTrue(!stable.PausedForChoice, "stable not paused");
    AssertEqual(2, stable.ResolvedEffectCount, "stable resolved");

    FlowProcessResult paused = FlowProcessResult.Paused(progressedAny: false, resolvedEffectCount: 0, iterations: 0);
    AssertTrue(paused.PausedForChoice, "paused status");
    AssertTrue(!paused.IsStable, "paused not stable");
    return Task.CompletedTask;
}

Task FlowProcessorSourceIsClean()
{
    string path = Path.Combine(
        root,
        "src",
        "HeadlessDCGO.Engine",
        "Headless",
        "Runtime",
        "GameFlowProcessor.cs");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Flow processor source was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(!text.Contains("NotImplementedException", StringComparison.Ordinal), "no NotImplementedException");
    AssertTrue(!text.Contains("UnityEngine", StringComparison.Ordinal), "no Unity dependency");
    return Task.CompletedTask;
}

static EffectRequest CreateRequest(string effectId, string sourceId)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        "Main",
        new EffectContext(
            player,
            player,
            new HeadlessEntityId(sourceId),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static ChoiceRequest CardRequest(HeadlessPlayerId player)
{
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        "Pick one",
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        ChoiceZone.Hand,
        new[]
        {
            new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Hand, IsSelectable: true, player),
        });
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {label}");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {label}. Expected '{expected}', got '{actual}'.");
    }
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

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId),
            new HeadlessEntityId(sourceId),
            name: effectId,
            timing: "Main");
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
