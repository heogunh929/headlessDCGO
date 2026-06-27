using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("Resolver runs the bound effect body and reports mutation count", ResolverRunsBoundEffectBody),
    ("Resolver treats an unbound request as a drained no-op", ResolverSkipsUnboundRequest),
    ("Scheduler drains queued effects through their bound bodies", SchedulerDrainsThroughBoundBodies),
    ("KeywordBaseBatch1 ToBinding attaches the effect body", KeywordBindingAttachesEffectBody),
    ("EngineContext.CreateDefault wires the card effect resolver", DefaultEngineContextWiresResolver),
    ("EffectBinding rejects an effect body with a mismatched id", EffectBindingRejectsMismatchedEffect),
    ("Resolver source has no placeholder or Unity dependency", ResolverSourceHasNoPlaceholderOrUnityDependency),
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

async Task ResolverRunsBoundEffectBody()
{
    var registry = new InMemoryEffectRegistry();
    var effect = new RecordingFakeEffect("fx-1", "src-1", "Main", mutationCount: 2);
    EffectRequest request = CreateRequest("fx-1", "src-1");
    registry.Register(new EffectBinding(request, effect: effect));

    var resolver = CardEffectSchedulerResolver.Create(registry);
    EffectResult result = await resolver(request, CancellationToken.None);

    AssertTrue(result.Resolved, "resolved");
    AssertEqual("fake resolved", result.Message, "message");
    AssertEqual(1, effect.ResolveCalls, "resolve calls");
    AssertEqual(2, ReadValue<int>(result, "mutationCount"), "mutation count");
}

async Task ResolverSkipsUnboundRequest()
{
    var registry = new InMemoryEffectRegistry();
    EffectRequest request = CreateRequest("missing", "src-1");

    var resolver = CardEffectSchedulerResolver.Create(registry);
    EffectResult result = await resolver(request, CancellationToken.None);

    AssertTrue(result.Resolved, "unbound resolved (drained no-op)");
    AssertEqual(true, ReadValue<bool>(result, "unresolved"), "unresolved flag");
}

async Task SchedulerDrainsThroughBoundBodies()
{
    var registry = new InMemoryEffectRegistry();
    var first = new RecordingFakeEffect("fx-a", "src-a", "Main", mutationCount: 1);
    var second = new RecordingFakeEffect("fx-b", "src-b", "Main", mutationCount: 1);
    EffectRequest firstRequest = CreateRequest("fx-a", "src-a");
    EffectRequest secondRequest = CreateRequest("fx-b", "src-b");
    registry.Register(new EffectBinding(firstRequest, effect: first));
    registry.Register(new EffectBinding(secondRequest, effect: second));

    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        CardEffectSchedulerResolver.Create(registry));
    scheduler.Enqueue(firstRequest, EffectResolutionMode.MainStack);
    scheduler.Enqueue(secondRequest, EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertEqual(2, results.Count, "result count");
    AssertTrue(results.All(r => r.Resolved), "all resolved");
    AssertEqual(1, first.ResolveCalls, "first resolve calls");
    AssertEqual(1, second.ResolveCalls, "second resolve calls");
    AssertEqual(0, scheduler.PendingCount, "queue drained");
}

Task KeywordBindingAttachesEffectBody()
{
    var controller = new HeadlessPlayerId(1);
    var source = new HeadlessEntityId("kw-source");
    KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(
        KeywordBaseBatch1Kind.Blocker,
        source,
        targetEntityId: null);

    EffectBinding binding = effect.ToBinding(controller, new EffectContext(controller, source));

    AssertTrue(binding.Effect is not null, "binding effect attached");
    AssertTrue(ReferenceEquals(binding.Effect, effect), "binding effect identity");
    AssertEqual(effect.Definition.EffectId.Value, binding.Effect!.Definition.EffectId.Value, "binding effect id");
    return Task.CompletedTask;
}

async Task DefaultEngineContextWiresResolver()
{
    EngineContext context = EngineContext.CreateDefault();
    var effect = new RecordingFakeEffect("fx-default", "src-default", "Main", mutationCount: 3);
    EffectRequest request = CreateRequest("fx-default", "src-default");
    context.EffectRegistry.Register(new EffectBinding(request, effect: effect));

    context.EffectScheduler.Enqueue(request, EffectResolutionMode.MainStack);
    EffectResult result = await context.EffectScheduler.ResolveNextAsync();

    AssertTrue(result.Resolved, "default resolved");
    AssertEqual(1, effect.ResolveCalls, "default invoked body");
    // CreateDefault wires the production MatchStateMutationSink; the fake effect's
    // "fake" mutations are unsupported there, which still proves the sink was invoked.
    AssertEqual(3, ReadValue<int>(result, "unsupportedMutationCount"), "default production sink processed mutations");

    EffectRequest unbound = CreateRequest("fx-unbound", "src-unbound");
    context.EffectScheduler.Enqueue(unbound, EffectResolutionMode.MainStack);
    EffectResult unboundResult = await context.EffectScheduler.ResolveNextAsync();
    AssertEqual(true, ReadValue<bool>(unboundResult, "unresolved"), "default unbound flag");
}

Task EffectBindingRejectsMismatchedEffect()
{
    EffectRequest request = CreateRequest("fx-x", "src-x");
    var mismatched = new RecordingFakeEffect("fx-other", "src-x", "Main", mutationCount: 0);

    ExpectThrows<ArgumentException>(() => new EffectBinding(request, effect: mismatched));
    return Task.CompletedTask;
}

Task ResolverSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(
        root,
        "src",
        "HeadlessDCGO.Engine",
        "Headless",
        "Effects",
        "CardEffectSchedulerResolver.cs");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Resolver source was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(!text.Contains("TODO", StringComparison.Ordinal), "no TODO marker");
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

static T ReadValue<T>(EffectResult result, string key)
{
    if (!result.Values.TryGetValue(key, out object? value) || value is not T typedValue)
    {
        throw new InvalidOperationException($"Expected value '{key}' with type {typeof(T).Name}.");
    }

    return typedValue;
}

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} to be thrown.");
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
    private readonly int _mutationCount;

    public RecordingFakeEffect(string effectId, string sourceId, string timing, int mutationCount)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId),
            new HeadlessEntityId(sourceId),
            name: effectId,
            timing: timing);
        _mutationCount = mutationCount;
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
        for (int i = 0; i < _mutationCount; i++)
        {
            mutations.Apply(new EffectMutation("fake", Definition.SourceEntityId));
        }

        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
