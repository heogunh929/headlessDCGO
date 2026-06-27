using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GPT-#3: GameFlowProcessor.RunToStableAsync must distinguish a genuine stable fixpoint from
// exhausting the iteration budget while still making progress (a runaway trigger loop). Before the
// fix it always returned Stable; now a cap-hit returns FlowProcessStatus.MaxIterationsExceeded.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A non-converging loop returns MaxIterationsExceeded at the cap", RunawayReturnsMaxIterationsExceeded),
    ("A genuinely stable run returns Stable", StableRunReturnsStable),
    ("MaxIterationsExceeded is distinct from Stable / Paused", () => Pure(StatusesAreDistinct)),
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

async Task RunawayReturnsMaxIterationsExceeded()
{
    EngineContext context = EngineContext.CreateDefault();
    // An active attack makes the flow call the (injected) pipeline every iteration; the looping
    // pipeline always reports progress without ever resolving the attack -> never converges.
    context.AttackController.DeclareAttack(P1, new HeadlessEntityId("atk"), P2, targetId: null, isDirectAttack: true);

    var processor = new GameFlowProcessor(new LoopingAttackPipeline());
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsMaxIterationsExceeded, "runaway loop returns MaxIterationsExceeded");
    AssertEqual(false, result.IsStable, "not reported as Stable");
    AssertEqual(false, result.PausedForChoice, "not reported as Paused");
    AssertEqual(GameFlowProcessor.MaxIterations, result.Iterations, "stopped exactly at the iteration cap");
}

async Task StableRunReturnsStable()
{
    EngineContext context = EngineContext.CreateDefault();
    // No active attack, no pending events/effects -> the first iteration makes no progress -> stable.
    var processor = new GameFlowProcessor(new LoopingAttackPipeline());
    FlowProcessResult result = await processor.RunToStableAsync(context);

    AssertTrue(result.IsStable, "a no-progress run is Stable");
    AssertEqual(false, result.IsMaxIterationsExceeded, "not flagged as exceeded");
    AssertTrue(result.Iterations < GameFlowProcessor.MaxIterations, "converged before the cap");
}

void StatusesAreDistinct()
{
    AssertTrue(FlowProcessStatus.Stable != FlowProcessStatus.MaxIterationsExceeded, "Stable != MaxIterationsExceeded");
    AssertTrue(FlowProcessStatus.PausedForChoice != FlowProcessStatus.MaxIterationsExceeded, "Paused != MaxIterationsExceeded");

    FlowProcessResult exceeded = FlowProcessResult.MaxIterationsExceeded(progressedAny: true, resolvedEffectCount: 0, iterations: 256);
    AssertTrue(exceeded.IsMaxIterationsExceeded, "factory sets the status");
    AssertEqual(false, exceeded.IsStable, "exceeded is not stable");
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static Task Pure(Action body) { body(); return Task.CompletedTask; }

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

// --- Fake pipeline -------------------------------------------------------

// Always reports progress and never resolves the attack, so the flow loop never reaches a fixpoint.
internal sealed class LoopingAttackPipeline : AttackPipeline
{
    public override Task<AttackAdvanceResult> AdvanceAsync(EngineContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared));
    }
}
