using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GPT-#4 + 신1: (a) the DcgoMatch legality mode is explicit — the default ctor is unguarded, while
// DcgoMatch.CreateValidated enforces the agent legality boundary; (b) the strict effect-gate profile
// is reachable through EngineContext.CreateDefault(strictUnbound: true).

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Default ctor is unguarded (EnforcesActionLegality == false)", DefaultCtorUnguarded),
    ("CreateValidated enforces the legality boundary", CreateValidatedEnforces),
    ("CreateValidated rejects an out-of-set action without mutating state", CreateValidatedRejects),
    ("CreateDefault(strictUnbound:true) fails an unbound effect", StrictProfileFailsUnbound),
    ("CreateDefault() default keeps an unbound effect draining", LenientProfileDrainsUnbound),
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

// --- #4: match legality mode --------------------------------------------

Task DefaultCtorUnguarded()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace());
    AssertFalse(match.EnforcesActionLegality, "default ctor does not enforce the legality boundary");
    return Task.CompletedTask;
}

Task CreateValidatedEnforces()
{
    DcgoMatch match = DcgoMatch.CreateValidated(EngineContext.CreateDefault(), new EngineTrace());
    AssertTrue(match.EnforcesActionLegality, "CreateValidated enforces the legality boundary");
    return Task.CompletedTask;
}

async Task CreateValidatedRejects()
{
    DcgoMatch match = DcgoMatch.CreateValidated(EngineContext.CreateDefault(), new EngineTrace());
    await match.InitializeAsync(BuildConfig());

    // No choice is pending, so ResolveChoice is not in the legal set -> the boundary rejects it.
    StepResult result = await match.ApplyActionAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("ghost"))));

    AssertTrue(result.Events.Any(e => e.Type == GameEventType.InvalidAction), "out-of-set action rejected");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no state mutation from the rejected action");
}

// --- 신1: strict effect-gate profile -------------------------------------

async Task StrictProfileFailsUnbound()
{
    EngineContext context = EngineContext.CreateDefault(strictUnbound: true);
    EffectResult result = await ResolveUnboundAsync(context);

    AssertFalse(result.Resolved, "strict profile: unbound effect is a failure");
    AssertTrue(result.Values.TryGetValue("strictUnbound", out object? marker) && marker is true, "strictUnbound marker set");
}

async Task LenientProfileDrainsUnbound()
{
    EngineContext context = EngineContext.CreateDefault(); // default lenient
    EffectResult result = await ResolveUnboundAsync(context);

    AssertTrue(result.Resolved, "lenient profile: unbound effect still drains");
    AssertTrue(result.IsUnbound, "reported as Unbound");
}

// --- Helpers -------------------------------------------------------------

static async Task<EffectResult> ResolveUnboundAsync(EngineContext context)
{
    var request = new EffectRequest(
        new HeadlessEntityId("missing-fx"), new HeadlessPlayerId(1), "OnPlay",
        new EffectContext(new HeadlessPlayerId(1), new HeadlessPlayerId(1), new HeadlessEntityId("src"),
            triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
    context.EffectScheduler.Enqueue(request, EffectResolutionMode.MainStack);
    IReadOnlyList<EffectResult> results = await context.EffectScheduler.ResolveAllAsync();
    return results[0];
}

static MatchConfig BuildConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
