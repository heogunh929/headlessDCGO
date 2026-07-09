using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-10) AS-IS MultipleSkills.cs:122-126: an effect whose resolution-time gate fails is SKIPPED and the
// window CONTINUES. The headless scheduler previously left any !Resolved effect at the queue head and BROKE
// ResolveAll — so a fizzle (e.g. a preceding effect removed this one's target) wedged every effect behind it.
// A real resolver error still parks the head; only a gate-fizzle (EffectResult.Skipped) is dequeued+skipped.

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

static EffectRequest Req(string id)
{
    var p = new HeadlessPlayerId(1);
    return new EffectRequest(new HeadlessEntityId(id), p, "Main",
        new EffectContext(p, p, new HeadlessEntityId($"src-{id}"), triggerEntityId: null,
            targetEntityIds: null, values: new Dictionary<string, object?>(StringComparer.Ordinal)));
}

// --- 1. A FIZZLE (Skipped) is dequeued and the window keeps draining to the next effect. ---
{
    var resolved = new List<string>();
    var scheduler = new EffectScheduler(new EffectResolutionQueue(), resolver: (request, _) =>
        Task.FromResult(request.EffectId.Value == "A"
            ? EffectResult.Skipped("A fizzled (gate failed at resolution).")
            : Recorded(request, resolved)));

    scheduler.Enqueue(Req("A"));   // fizzles
    scheduler.Enqueue(Req("B"));   // must still resolve

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    Check(results.Count == 2, $"both effects were processed (got {results.Count})");
    Check(results[0].IsSkipped, "the fizzled effect A is reported Skipped");
    Check(resolved.SequenceEqual(new[] { "B" }), $"B resolved despite A fizzling (resolved: [{string.Join(",", resolved)}])");
    Check(!scheduler.HasPendingEffects, "the queue is fully drained (A skipped, B resolved) — no wedge");
}

// --- 2. A real Failure (error) still parks the queue head and stops the drain (unchanged behavior). ---
{
    var resolved = new List<string>();
    var scheduler = new EffectScheduler(new EffectResolutionQueue(), resolver: (request, _) =>
        Task.FromResult(request.EffectId.Value == "A"
            ? EffectResult.Failure("A errored.")
            : Recorded(request, resolved)));

    scheduler.Enqueue(Req("A"));
    scheduler.Enqueue(Req("B"));

    await scheduler.ResolveAllAsync();

    Check(scheduler.HasPendingEffects, "a real Failure keeps the head parked (queue not empty)");
    Check(resolved.Count == 0, "B is NOT reached past a parked error head");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-10 fizzle-skip checks passed.");

static EffectResult Recorded(EffectRequest request, List<string> sink)
{
    sink.Add(request.EffectId.Value);
    return EffectResult.Success("resolved");
}
