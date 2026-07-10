using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 2) WindowResolver PRODUCTION wiring test — drives RunWindowAsync with the real
// scheduler-path deps (WindowResolverWiring.BuildSchedulerDeps) over the actual EngineContext scheduler +
// registry + OnceFlags, proving a registered IHeadlessCardEffect resolves end-to-end, the once-cap is consumed
// at COMMIT (before the body), and the gate blocks a non-resolvable / capped-out effect.

HeadlessPlayerId P1 = new(1);

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

TimingWindowTrigger Trigger(EffectRequest req) =>
    new(req, EffectResolutionMode.MainStack, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0);

void Register(EngineContext ctx, StubEffect effect) =>
    ctx.EffectRegistry.Register(new EffectBinding(Request(effect.Definition.EffectId.Value), effect: effect));

WindowResolverDeps Deps(EngineContext ctx) =>
    WindowResolverWiring.BuildSchedulerDeps(ctx, new ThrowPort(), () => Array.Empty<TimingWindowTrigger>());

// --- 1. A registered effect resolves end-to-end through the real scheduler. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    var e = new StubEffect("E1", maxCountPerTurn: null);
    Register(ctx, e);

    var r = await new WindowResolver().RunWindowAsync(new[] { Trigger(Request("E1")) }, Deps(ctx));
    Check(r == WindowRunResult.Completed && e.Resolved == 1,
        $"a registered effect resolves once through the production scheduler path (resolved={e.Resolved})");
}

// --- 2. The once-per-turn cap is consumed at COMMIT — a capped effect cannot re-fire in the same turn. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 6);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    var e = new StubEffect("E2", maxCountPerTurn: 1);
    Register(ctx, e);

    await new WindowResolver().RunWindowAsync(new[] { Trigger(Request("E2")) }, Deps(ctx));
    Check(e.Resolved == 1, "precondition: capped effect fired once");
    Check(!ctx.OnceFlags.CanActivate(Request("E2"), 1), "the once-per-turn use was consumed at commit");

    // A second window for the same capped effect: the gate blocks it (already consumed) — it does not re-fire.
    await new WindowResolver().RunWindowAsync(new[] { Trigger(Request("E2")) }, Deps(ctx));
    Check(e.Resolved == 1, "a once-per-turn effect does not fire a second time the same turn (gate blocks on consumed cap)");
}

// --- 3. The gate blocks an effect whose CanResolve is false — no resolution, no consume. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    var e = new StubEffect("E3", maxCountPerTurn: null, canResolve: false);
    Register(ctx, e);

    var r = await new WindowResolver().RunWindowAsync(new[] { Trigger(Request("E3")) }, Deps(ctx));
    Check(r == WindowRunResult.Completed && e.Resolved == 0,
        "a gate-blocked (CanResolve=false) effect does not resolve");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall Stage-5 window-wiring checks passed.");

// A real IHeadlessCardEffect: records resolutions; configurable cap + CanResolve.
sealed class StubEffect : IHeadlessCardEffect
{
    private readonly bool _canResolve;
    public int Resolved { get; private set; }
    public CardEffectDefinition Definition { get; }

    public StubEffect(string id, int? maxCountPerTurn, bool canResolve = true)
    {
        _canResolve = canResolve;
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(id), new HeadlessEntityId($"src:{id}"), id, "OnTest", maxCountPerTurn: maxCountPerTurn);
    }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) =>
        _canResolve ? CardEffectCanResolveResult.Success() : CardEffectCanResolveResult.Failure("blocked");

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        Resolved++;
        return ValueTask.FromResult(EffectResult.Success($"resolved {Definition.EffectId.Value}"));
    }
}

// A choice port that must never be invoked (single-mandatory windows do not choose).
sealed class ThrowPort : IWindowChoicePort
{
    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct) =>
        throw new InvalidOperationException("ChooseOrderAsync should not be called for a single-mandatory window.");
    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct) =>
        throw new InvalidOperationException("ConfirmOptionalAsync should not be called for a mandatory window.");
}
