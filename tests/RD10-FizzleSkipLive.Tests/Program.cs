using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// (P0-1 / RD-10) LIVE-PATH through-test. The RD10-FizzleSkip unit test injects a custom resolver lambda straight
// into the scheduler, bypassing CardEffectSchedulerResolver.WithSinkMetadata — the exact layer that clobbered the
// Skipped status back to Failed and re-wedged the queue. This test drives the REAL production chain
// (EngineContext.EffectScheduler = CardEffectSchedulerResolver.Create + MatchStateMutationSink + HeadlessCardEffect
// gate) so a resolution-time fizzle actually skips and the effects behind it still resolve.

HeadlessPlayerId P1 = new(1);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

EffectRequest Request(string id) => new(
    new HeadlessEntityId(id), P1, "Main",
    new EffectContext(P1, P1, new HeadlessEntityId($"src-{id}"), triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>()));

void Register(EngineContext ctx, StubEffect effect) =>
    ctx.EffectRegistry.Register(new EffectBinding(Request(effect.Definition.EffectId.Value), effect: effect));

// --- 1. A fizzle (gate fails) at resolution comes out SKIPPED, not Failed, and does NOT wedge the queue:
//        the effect enqueued behind it still resolves. Pre-fix, WithSinkMetadata reclassified Skipped→Failed,
//        the scheduler parked the head, and "after" never ran. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 10);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);

    var fizzle = new StubEffect("fizzle", canResolve: false);
    var after = new StubEffect("after", canResolve: true);
    Register(ctx, fizzle);
    Register(ctx, after);

    ctx.EffectScheduler.Enqueue(Request("fizzle"), EffectResolutionMode.MainStack);
    ctx.EffectScheduler.Enqueue(Request("after"), EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await ctx.EffectScheduler.ResolveAllAsync();

    EffectResult? fizzleResult = results.FirstOrDefault(r => r.IsSkipped);
    Check(fizzleResult is not null,
        "a resolution-time fizzle surfaces as Skipped through the live resolver+sink chain (not Failed)");
    Check(!results.Any(r => r.Status == EffectResolutionStatus.Failed),
        "the fizzle is NOT clobbered back to Failed by sink-metadata merging (P0-1)");
    Check(after.Resolved == 1,
        "the effect queued BEHIND the fizzle still resolves — the queue is not wedged");
    Check(ctx.EffectScheduler.PendingCount == 0,
        "the scheduler drains completely (fizzle dequeued, no parked head)");
}

// --- 2. Control: a genuine BODY Failure (not a gate fizzle) still parks the head and wedges — proving Skipped
//        and Failed stay behaviorally distinct after the metadata merge (the fix preserves ALL statuses). ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);

    var boom = new StubEffect("boom", canResolve: true, bodyOverride: EffectResult.Failure("body failed hard"));
    var behind = new StubEffect("behind", canResolve: true);
    Register(ctx, boom);
    Register(ctx, behind);

    ctx.EffectScheduler.Enqueue(Request("boom"), EffectResolutionMode.MainStack);
    ctx.EffectScheduler.Enqueue(Request("behind"), EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await ctx.EffectScheduler.ResolveAllAsync();

    Check(results.Any(r => r.Status == EffectResolutionStatus.Failed),
        "a genuine body Failure keeps its Failed status through the merge (not silently Skipped)");
    Check(behind.Resolved == 0 && ctx.EffectScheduler.PendingCount > 0,
        "a real Failure still parks the head and stops the drain (Skipped≠Failed preserved)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-10 live-path fizzle-skip checks passed.");

// A real IHeadlessCardEffect whose resolution-time gate can be made to fail (fizzle) or pass, and whose body
// can be forced to return a genuine Failure (distinct from a gate fizzle).
sealed class StubEffect : IHeadlessCardEffect
{
    private readonly bool _canResolve;
    private readonly EffectResult? _bodyOverride;
    public int Resolved { get; private set; }
    public CardEffectDefinition Definition { get; }

    public StubEffect(string id, bool canResolve, EffectResult? bodyOverride = null)
    {
        _canResolve = canResolve;
        _bodyOverride = bodyOverride;
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(id), new HeadlessEntityId($"src-{id}"), id, "Main");
    }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) =>
        _canResolve
            ? CardEffectCanResolveResult.Success()
            : CardEffectCanResolveResult.Failure("gate fails: fizzle at resolution time");

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        Resolved++;
        return ValueTask.FromResult(_bodyOverride ?? EffectResult.Success($"resolved {Definition.EffectId.Value}"));
    }
}
