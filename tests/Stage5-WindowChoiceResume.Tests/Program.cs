using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 3b-i) LIVE window-choice resume — drives a window through the REAL agent-driven
// AgentWindowChoicePort + choice controller, suspends at the order / optional decision, then resolves it through
// the ACTUAL MetadataActionProcessor.ResolveChoiceAsync WindowChoice branch (not a direct call), proving the
// end-to-end round-trip: open choice -> pause -> ResolveChoice action -> record + re-drive -> complete. The bodies
// resolve synchronously through the production scheduler (no body-suspend — that integration is Phase 3b-iii).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

EffectRequest Request(string id) => new(
    new HeadlessEntityId(id), P1, "OnTest",
    new EffectContext(P1, P1, new HeadlessEntityId($"src:{id}"), triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>()));

TimingWindowTrigger Trigger(string id, TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory) =>
    new(Request(id), EffectResolutionMode.MainStack, kind, priority: 0, sequence: 0);

EngineContext Context()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3131);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

void Register(EngineContext ctx, StubEffect e) =>
    ctx.EffectRegistry.Register(new EffectBinding(Request(e.Definition.EffectId.Value), effect: e));

// Mimic the (Phase 3b-iii) main-loop caller: drive the window; on a suspend, PARK the continuation so the next
// ResolveChoice resumes it.
async Task<WindowRunResult> DriveMain(EngineContext ctx, WindowContinuation cont)
{
    WindowResolverDeps deps = WindowResolverWiring.BuildMainLoopDeps(ctx);
    WindowRunResult r = await new WindowResolver().DriveAsync(cont, deps);
    if (r == WindowRunResult.Suspended) ctx.WindowResolution.Suspend(cont);
    else ctx.WindowResolution.Clear();
    return r;
}

var processor = new MetadataActionProcessor();

// --- 1. Order choice: two simultaneous MANDATORY triggers open a WindowChoice; the agent picks E1 first through
//        the real ResolveChoice action; the window resumes and resolves BOTH (E1 then E2 alone). ---
{
    EngineContext ctx = Context();
    var e1 = new StubEffect("E1"); var e2 = new StubEffect("E2");
    Register(ctx, e1); Register(ctx, e2);

    WindowRunResult r = await DriveMain(ctx, WindowResolver.CreateContinuation(new[] { Trigger("E1"), Trigger("E2") }));
    Check(r == WindowRunResult.Suspended, "order: window suspends at the order choice");
    Check(ctx.ChoiceController.Current.IsPending && ctx.ChoiceController.Current.Type == ChoiceType.WindowChoice,
        "order: a WindowChoice is pending on the choice controller");
    Check(ctx.WindowResolution.HasPending, "order: the window is parked in WindowResolution");
    Check(e1.Resolved == 0 && e2.Resolved == 0, "order: nothing resolved before the agent chooses");

    ActionProcessResult res = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("E1"))), ctx);
    Check(res.IsSuccess, $"order: ResolveChoice action succeeded ({res.Message})");
    Check(!ctx.WindowResolution.HasPending && !ctx.ChoiceController.Current.IsPending,
        "order: after the pick the window ran to exhaustion (nothing parked / pending)");
    Check(e1.Resolved == 1 && e2.Resolved == 1, $"order: both effects resolved once (E1={e1.Resolved}, E2={e2.Resolved})");
}

// --- 2. Optional confirm = YES: a lone optional trigger opens a yes/no WindowChoice; selecting the effect id
//        (yes) through the real action resumes the window and resolves the body. ---
{
    EngineContext ctx = Context();
    var e = new StubEffect("E3");
    Register(ctx, e);

    WindowRunResult r = await DriveMain(ctx, WindowResolver.CreateContinuation(new[] { Trigger("E3", TimingWindowTriggerKind.Optional) }));
    Check(r == WindowRunResult.Suspended && ctx.ChoiceController.Current.Type == ChoiceType.WindowChoice,
        "optional-yes: window suspends at the optional confirm");
    Check(e.Resolved == 0, "optional-yes: not resolved before the confirm");

    ActionProcessResult res = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("E3"))), ctx);
    Check(res.IsSuccess, $"optional-yes: ResolveChoice succeeded ({res.Message})");
    Check(!ctx.WindowResolution.HasPending, "optional-yes: window completed");
    Check(e.Resolved == 1, $"optional-yes: the accepted optional resolved (resolved={e.Resolved})");
}

// --- 3. Optional confirm = NO: skipping the yes/no WindowChoice declines the optional — the window completes and
//        the body never resolves (and nothing was consumed). ---
{
    EngineContext ctx = Context();
    var e = new StubEffect("E4", maxCountPerTurn: 1);
    Register(ctx, e);

    await DriveMain(ctx, WindowResolver.CreateContinuation(new[] { Trigger("E4", TimingWindowTriggerKind.Optional) }));
    ActionProcessResult res = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Skip()), ctx);
    Check(res.IsSuccess, $"optional-no: ResolveChoice(skip) succeeded ({res.Message})");
    Check(!ctx.WindowResolution.HasPending, "optional-no: window completed");
    Check(e.Resolved == 0, "optional-no: the declined optional did not resolve");
    Check(ctx.OnceFlags.CanActivate(Request("E4"), 1), "optional-no: a declined optional consumed nothing (cap still available)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall Stage-5 window-choice-resume checks passed.");

// A real IHeadlessCardEffect that resolves through the production scheduler and records how many times it ran.
sealed class StubEffect : IHeadlessCardEffect
{
    public int Resolved { get; private set; }
    public CardEffectDefinition Definition { get; }

    public StubEffect(string id, int? maxCountPerTurn = null)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(id), new HeadlessEntityId($"src:{id}"), id, "OnTest", maxCountPerTurn: maxCountPerTurn);
    }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        Resolved++;
        return ValueTask.FromResult(EffectResult.Success($"resolved {Definition.EffectId.Value}"));
    }
}
