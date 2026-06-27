using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// R2-3: the flow processor's iteration-cap stop (MaxIterationsExceeded) is now surfaced as a TYPED flag
// on StepResult.FlowExceededIterationCap and RlStepResult.FlowExceededIterationCap — previously a
// runaway loop was only a log message, invisible to an RL trainer / agent caller.
// R2-4: DcgoMatch.CreateStrictValidated builds the strict + validated profile (strict-unbound effect
// gate + agent legality boundary) in one call, and HeadlessRlEnvironmentOptions.StrictUnbound reaches
// the same strict gate for the default RL match.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Runaway flow step surfaces FlowExceededIterationCap on StepResult", RunawayStepSetsFlag),
    ("A normal step leaves FlowExceededIterationCap false", NormalStepLeavesFlagFalse),
    ("RL env propagates the cap flag from StepResult to RlStepResult", RlEnvPropagatesCapFlag),
    ("CreateStrictValidated is both validated and strict-unbound", StrictValidatedProfile),
    ("RL env StrictUnbound option makes the default match strict", RlEnvStrictOption),
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

// --- R2-3: typed cap flag -----------------------------------------------

async Task RunawayStepSetsFlag()
{
    // Inject a non-converging flow processor; an active attack makes it loop every iteration until the
    // cap, so the step's flow stops at MaxIterations without stabilizing.
    DcgoMatch match = new(
        EngineContext.CreateDefault(randomSeed: 31),
        new EngineTrace(),
        actionProcessor: null,
        actionLegality: null,
        gameFlowProcessor: new GameFlowProcessor(new LoopingAttackPipeline()));
    await match.InitializeAsync(BuildConfig(31));
    match.Context.AttackController.DeclareAttack(P1, new HeadlessEntityId("atk"), P2, targetId: null, isDirectAttack: true);

    StepResult step = await match.StepAsync();

    AssertTrue(step.FlowExceededIterationCap, "runaway step sets the typed cap flag on StepResult");
}

async Task NormalStepLeavesFlagFalse()
{
    DcgoMatch match = new(EngineContext.CreateDefault(randomSeed: 31), new EngineTrace());
    await match.InitializeAsync(BuildConfig(31));

    StepResult step = await match.StepAsync();

    AssertFalse(step.FlowExceededIterationCap, "a converging step does not set the cap flag");
}

async Task RlEnvPropagatesCapFlag()
{
    var env = new HeadlessRlEnvironment();
    await env.InitializeAsync(BuildConfig(42));

    StepResult flagged = new(
        IsTerminal: false,
        HasPendingChoice: false,
        Events: Array.Empty<GameEvent>(),
        Observation: env.Match.GetObservation(),
        ActionMask: env.Match.GetActionMask(),
        FlowExceededIterationCap: true);
    RlStepResult flaggedRl = env.Encode(flagged);
    AssertTrue(flaggedRl.FlowExceededIterationCap, "RL result carries the cap flag when set");

    StepResult clean = flagged with { FlowExceededIterationCap = false };
    RlStepResult cleanRl = env.Encode(clean);
    AssertFalse(cleanRl.FlowExceededIterationCap, "RL result leaves the cap flag false otherwise");
}

// --- R2-4: strict + validated profile -----------------------------------

async Task StrictValidatedProfile()
{
    DcgoMatch match = DcgoMatch.CreateStrictValidated(randomSeed: 7);

    AssertTrue(match.EnforcesActionLegality, "strict-validated match enforces the legality boundary");
    EffectResult result = await ResolveUnboundAsync(match.Context);
    AssertFalse(result.Resolved, "strict-validated context fails an unbound effect");
    AssertTrue(
        result.Values.TryGetValue("strictUnbound", out object? marker) && marker is true,
        "strictUnbound marker present on the failure");
}

async Task RlEnvStrictOption()
{
    var strict = new HeadlessRlEnvironment(options: new HeadlessRlEnvironmentOptions { StrictUnbound = true });
    EffectResult strictResult = await ResolveUnboundAsync(strict.Match.Context);
    AssertFalse(strictResult.Resolved, "StrictUnbound option makes the default match strict");

    var lenient = new HeadlessRlEnvironment();
    EffectResult lenientResult = await ResolveUnboundAsync(lenient.Match.Context);
    AssertTrue(lenientResult.Resolved, "default RL match stays lenient (unbound effect drains)");
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

static MatchConfig BuildConfig(int seed)
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: seed, setup: setup);
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

// --- Fake pipeline (from GPT-#3) ----------------------------------------

// Always reports progress and never resolves the attack, so the flow loop never reaches a fixpoint.
internal sealed class LoopingAttackPipeline : AttackPipeline
{
    public override Task<AttackAdvanceResult> AdvanceAsync(EngineContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared));
    }
}
