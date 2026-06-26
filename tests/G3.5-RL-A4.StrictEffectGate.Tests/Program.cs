using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A4: a strict effect gate. In test/dev a request with no bound effect body is a hard FAILURE
// so missing coverage is caught immediately during Phase 4 porting, instead of silently draining as
// Unbound. Production keeps the lenient (countable) Unbound behaviour.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Strict gate turns an unbound effect into a failure", StrictUnboundFails),
    ("Strict failure carries the gate marker and effect id", StrictFailureCarriesMarker),
    ("Lenient mode keeps the unbound effect draining (default)", LenientUnboundDrains),
    ("Strict gate does not affect a bound effect", BoundEffectUnaffected),
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

async Task StrictUnboundFails()
{
    EffectScheduler scheduler = BuildScheduler(strictUnbound: true, out _);
    scheduler.Enqueue(Request("missing-fx", "OnPlay"), EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertEqual(1, results.Count, "one resolution");
    AssertFalse(results[0].Resolved, "strict unbound is not resolved");
    AssertEqual(EffectResolutionStatus.Failed, results[0].Status, "status is Failed");
    AssertFalse(results[0].IsUnbound, "not reported as Unbound under strict gate");
    AssertEqual(0, scheduler.TotalResolvedCount, "nothing drained");
}

async Task StrictFailureCarriesMarker()
{
    EffectScheduler scheduler = BuildScheduler(strictUnbound: true, out _);
    scheduler.Enqueue(Request("missing-fx", "OnDeletion"), EffectResolutionMode.MainStack);

    EffectResult result = (await scheduler.ResolveAllAsync())[0];

    AssertTrue(result.Values.TryGetValue("strictUnbound", out object? marker) && marker is true, "strictUnbound marker set");
    AssertEqual("missing-fx", result.Values["effectId"], "effect id surfaced");
    AssertEqual("OnDeletion", result.Values["timing"], "timing surfaced");
    AssertTrue(result.Message!.Contains("Strict effect gate", StringComparison.Ordinal), "message names the strict gate");
}

async Task LenientUnboundDrains()
{
    EffectScheduler scheduler = BuildScheduler(strictUnbound: false, out _);
    scheduler.Enqueue(Request("missing-fx", "OnPlay"), EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertTrue(results[0].Resolved, "lenient unbound still resolves (drains)");
    AssertTrue(results[0].IsUnbound, "reported as Unbound");
    AssertEqual(1, scheduler.TotalUnboundCount, "counted as an unbound coverage gap");
}

async Task BoundEffectUnaffected()
{
    EffectScheduler scheduler = BuildScheduler(strictUnbound: true, out InMemoryEffectRegistry registry);
    var effect = new NoOpEffect("bound-fx", "src", "OnPlay");
    registry.Register(new EffectBinding(Request("bound-fx", "OnPlay"), effect: effect));
    scheduler.Enqueue(Request("bound-fx", "OnPlay"), EffectResolutionMode.MainStack);

    EffectResult result = (await scheduler.ResolveAllAsync())[0];

    AssertTrue(result.Resolved, "bound effect resolves under strict gate");
    AssertEqual(1, effect.ResolveCalls, "bound effect body ran");
}

// --- Harness -------------------------------------------------------------

EffectScheduler BuildScheduler(bool strictUnbound, out InMemoryEffectRegistry registry)
{
    registry = new InMemoryEffectRegistry();
    return new EffectScheduler(
        new EffectResolutionQueue(),
        CardEffectSchedulerResolver.Create(registry, sinkFactory: _ => new RecordingEffectMutationSink(), strictUnbound: strictUnbound));
}

EffectRequest Request(string effectId, string timing) =>
    new(new HeadlessEntityId(effectId), P1, timing,
        new EffectContext(P1, P1, new HeadlessEntityId("src"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

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

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

internal sealed class NoOpEffect : IHeadlessCardEffect
{
    public NoOpEffect(string effectId, string sourceId, string timing)
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
        return ValueTask.FromResult(EffectResult.Success("ok"));
    }
}
