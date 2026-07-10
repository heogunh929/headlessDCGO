using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 3a-ii) WindowResolver RESUME-model unit tests. Validate that a suspended window persists its
// whole cut-in frame stack + in-flight pick in a WindowContinuation and resumes correctly on the next DriveAsync,
// WITHOUT re-picking or re-committing. Expected orderings are derived from the AS-IS MultipleSkills re-entry
// semantics (a suspended coroutine resumes IN PLACE; the once-use was consumed at commit, before the body).

HeadlessPlayerId P1 = new(1);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

TimingWindowTrigger Trigger(string id, TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory)
{
    var ctx = new EffectContext(P1, P1, new HeadlessEntityId($"src:{id}"), triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>());
    var request = new EffectRequest(new HeadlessEntityId(id), P1, "OnTest", ctx);
    return new TimingWindowTrigger(request, EffectResolutionMode.MainStack, kind, priority: 0, sequence: 0);
}

string Id(TimingWindowTrigger t) => t.Request.EffectId.Value;
IReadOnlyList<TimingWindowTrigger> None() => Array.Empty<TimingWindowTrigger>();

// --- 1. Body-suspend resume (single frame): A's body suspends, then resolves on replay; B resolves after. The
//        remaining stack (B) survives the suspend, and A is committed EXACTLY ONCE (at the original commit, before
//        the body — never re-committed on replay). AS-IS: the suspended skill resumes in place, use already spent. ---
{
    var committed = new List<string>();
    var resolved = new List<string>();
    var bodies = new ScriptedBodies(suspendsBefore: new() { ["A"] = 1 }, resolved);
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)), bodies.Resolve, new FifoPort(), None);

    var cont = WindowResolver.CreateContinuation(new[] { Trigger("A"), Trigger("B") });
    var resolver = new WindowResolver();

    WindowRunResult r1 = await resolver.DriveAsync(cont, deps);
    Check(r1 == WindowRunResult.Suspended, "body-suspend: first drive returns Suspended");
    Check(committed.SequenceEqual(new[] { "A" }), $"body-suspend: A committed at commit before its body; got [{string.Join(",", committed)}]");
    Check(resolved.Count == 0, "body-suspend: nothing has RESOLVED yet (A's body is parked)");

    WindowRunResult r2 = await resolver.DriveAsync(cont, deps);
    Check(r2 == WindowRunResult.Completed, "body-suspend: resume drive completes");
    Check(resolved.SequenceEqual(new[] { "A", "B" }), $"body-suspend: A resolves on replay, then B; got [{string.Join(",", resolved)}]");
    Check(committed.SequenceEqual(new[] { "A", "B" }), $"body-suspend: A committed once across suspend+resume (no re-commit); got [{string.Join(",", committed)}]");
    Check(cont.IsExhausted, "body-suspend: continuation exhausted after resume");
}

// --- 2. Multi-suspend body: A's body suspends TWICE before resolving. Each replay must NOT re-commit; the use is
//        spent once. Proves DriveAsync's resume path re-invokes only the body, never the pick/commit. ---
{
    var committed = new List<string>();
    var resolved = new List<string>();
    var bodies = new ScriptedBodies(suspendsBefore: new() { ["A"] = 2 }, resolved);
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)), bodies.Resolve, new FifoPort(), None);

    var cont = WindowResolver.CreateContinuation(new[] { Trigger("A") });
    var resolver = new WindowResolver();

    Check(await resolver.DriveAsync(cont, deps) == WindowRunResult.Suspended, "multi-suspend: 1st drive Suspended");
    Check(await resolver.DriveAsync(cont, deps) == WindowRunResult.Suspended, "multi-suspend: 2nd drive still Suspended (body re-parked)");
    Check(await resolver.DriveAsync(cont, deps) == WindowRunResult.Completed, "multi-suspend: 3rd drive completes");
    Check(committed.SequenceEqual(new[] { "A" }), $"multi-suspend: A committed exactly once across two replays; got [{string.Join(",", committed)}]");
    Check(resolved.SequenceEqual(new[] { "A" }), "multi-suspend: A resolves once");
}

// --- 3. Choice-suspend resume: a mandatory order choice between A,B suspends (the port opens an agent choice and
//        throws WindowChoicePendingException). Nothing is picked/committed. On resume the port answers (A first);
//        B is then alone and auto-resolves. No double-mutation. AS-IS: the panel is re-offered, no skill removed
//        until actually activated. ---
{
    var committed = new List<string>();
    var resolved = new List<string>();
    var bodies = new ScriptedBodies(suspendsBefore: new(), resolved);
    // Port: throw on the FIRST ChooseOrder call (suspend), then answer index-of-A on the next.
    var port = new SuspendOncePort(answerId: "A");
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)), bodies.Resolve, port, None);

    var cont = WindowResolver.CreateContinuation(new[] { Trigger("A"), Trigger("B") });
    var resolver = new WindowResolver();

    WindowRunResult r1 = await resolver.DriveAsync(cont, deps);
    Check(r1 == WindowRunResult.Suspended, "choice-suspend: first drive returns Suspended (port threw)");
    Check(cont.InFlightPick is null, "choice-suspend: NO in-flight pick (suspend was pre-commit)");
    Check(committed.Count == 0 && resolved.Count == 0, "choice-suspend: nothing committed or resolved before the choice");

    WindowRunResult r2 = await resolver.DriveAsync(cont, deps);
    Check(r2 == WindowRunResult.Completed, "choice-suspend: resume completes");
    Check(resolved.SequenceEqual(new[] { "A", "B" }), $"choice-suspend: chosen order A then B; got [{string.Join(",", resolved)}]");
    Check(committed.SequenceEqual(new[] { "A", "B" }), $"choice-suspend: each committed once (no double); got [{string.Join(",", committed)}]");
    Check(port.OrderCalls == 2, $"choice-suspend: port asked twice (suspend + replayed answer); got {port.OrderCalls}");
}

// --- 4. Nested cut-in body-suspend resume: resolving A emits cut-in C; C's body suspends; on resume C resolves,
//        its (now-exhausted) cut-in frame pops, and the PARENT stack (B) continues. Proves the whole nested frame
//        stack persists across the suspend and cut-in-first ordering (RD-17) survives. ---
{
    var committed = new List<string>();
    var resolved = new List<string>();
    var bodies = new ScriptedBodies(
        suspendsBefore: new() { ["C"] = 1 },
        resolved,
        emitOnResolve: new() { ["A"] = new[] { "C" } },
        triggerFactory: Trigger);
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)), bodies.Resolve, new FifoPort(), bodies.DrainEmitted);

    var cont = WindowResolver.CreateContinuation(new[] { Trigger("A"), Trigger("B") });
    var resolver = new WindowResolver();

    WindowRunResult r1 = await resolver.DriveAsync(cont, deps);
    Check(r1 == WindowRunResult.Suspended, "nested: first drive Suspended (C's body parked)");
    Check(cont.InFlightPick is { } p && Id(p) == "C", "nested: in-flight pick is the cut-in C");
    Check(resolved.SequenceEqual(new[] { "A" }), $"nested: only A resolved so far; got [{string.Join(",", resolved)}]");
    Check(committed.SequenceEqual(new[] { "A", "C" }), $"nested: A and C committed (C at its commit, before its parked body); got [{string.Join(",", committed)}]");

    WindowRunResult r2 = await resolver.DriveAsync(cont, deps);
    Check(r2 == WindowRunResult.Completed, "nested: resume completes");
    Check(resolved.SequenceEqual(new[] { "A", "C", "B" }),
        $"nested: cut-in C resolves before the remaining parent stack B, across the suspend (RD-17); got [{string.Join(",", resolved)}]");
    Check(committed.SequenceEqual(new[] { "A", "C", "B" }), $"nested: each committed once; got [{string.Join(",", committed)}]");
    Check(cont.IsExhausted, "nested: continuation exhausted");
}

// --- 6. (adversarial P1-a) Multi-choice-per-pass replay: a CHOICE suspend re-runs the WHOLE pass on resume, so a
//        pass with MORE THAN ONE pre-body choice (order pick, THEN an optional yes/no) re-asks EVERY earlier choice
//        on each resume. This mirrors the AS-IS DeferredChoiceProvider replay contract — but proves the Phase-3b
//        port must be KEY-BASED (keyed by which effect is being ordered / confirmed), NOT positional: as picks
//        resolve, their choice-points DISAPPEAR (once A resolves, the order call and A's confirm are gone), so a
//        by-ordinal replay would misalign. A key-based port answers each choice idempotently whenever it recurs and
//        is simply never re-asked for a resolved pick. Two optionals [A,B]: order->A, confirm-A->yes, confirm-B->yes,
//        with the port suspending the first time it meets each distinct choice. Result must be a clean [A,B] with
//        each committed once and the order choice replayed (not re-decided) every pass it survives. ---
{
    var committed = new List<string>();
    var resolved = new List<string>();
    var bodies = new ScriptedBodies(suspendsBefore: new(), resolved);
    var port = new KeyedReplayPort(new()
    {
        ["O:A,B"] = "A",   // order among [A,B] -> A
        ["C:A"] = true,    // confirm A -> yes
        ["C:B"] = true,    // confirm B -> yes
    });
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)), bodies.Resolve, port, None);

    var cont = WindowResolver.CreateContinuation(
        new[] { Trigger("A", TimingWindowTriggerKind.Optional), Trigger("B", TimingWindowTriggerKind.Optional) });
    var resolver = new WindowResolver();

    int drives = 0;
    WindowRunResult r;
    do { r = await resolver.DriveAsync(cont, deps); drives++; }
    while (r == WindowRunResult.Suspended && drives < 20);

    Check(r == WindowRunResult.Completed, "keyed-replay: window completes after replayed multi-choice passes");
    Check(drives == 4, $"keyed-replay: 3 distinct choice suspends (order, confirm-A, confirm-B) + 1 finishing drive; got {drives}");
    Check(resolved.SequenceEqual(new[] { "A", "B" }), $"keyed-replay: order A then B; got [{string.Join(",", resolved)}]");
    Check(committed.SequenceEqual(new[] { "A", "B" }), $"keyed-replay: each committed exactly once (no re-commit across replays); got [{string.Join(",", committed)}]");
    // Order asked on drive1(suspend) + drive2(replay) + drive3(replay); NOT on drive4 (A already resolved, B alone).
    Check(port.OrderCalls == 3, $"keyed-replay: order re-asked every pass it survives, then gone once A resolves; got {port.OrderCalls} (expected 3)");
    Check(port.LastOrderAnswerId == "A", "keyed-replay: the replayed order answer is stable (always A)");
}

// --- 5. WindowResolutionController contract: holds one pending continuation; Clear and ResetMatchState both drop it. ---
{
    var ctrl = new WindowResolutionController();
    Check(!ctrl.HasPending && ctrl.Pending is null, "controller: starts empty");

    var cont = WindowResolver.CreateContinuation(new[] { Trigger("A") });
    ctrl.Suspend(cont);
    Check(ctrl.HasPending && ReferenceEquals(ctrl.Pending, cont), "controller: Suspend parks the continuation");

    ctrl.Clear();
    Check(!ctrl.HasPending, "controller: Clear drops it");

    ctrl.Suspend(cont);
    ctrl.ResetMatchState();
    Check(!ctrl.HasPending, "controller: ResetMatchState drops it (match boundary)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall Stage-5 WindowResolver resume-model checks passed.");

// Scripted bodies: an id suspends `suspendsBefore[id]` times, then Resolves (recording order). On resolve it may
// emit cut-in triggers (queued for the next DrainEmitted call), modelling a body that stacks new reactions.
sealed class ScriptedBodies
{
    private readonly Dictionary<string, int> _remainingSuspends;
    private readonly List<string> _resolved;
    private readonly Dictionary<string, string[]> _emitOnResolve;
    private readonly Func<string, TimingWindowTriggerKind, TimingWindowTrigger>? _triggerFactory;
    private readonly Queue<TimingWindowTrigger> _emitted = new();

    public ScriptedBodies(
        Dictionary<string, int> suspendsBefore,
        List<string> resolved,
        Dictionary<string, string[]>? emitOnResolve = null,
        Func<string, TimingWindowTriggerKind, TimingWindowTrigger>? triggerFactory = null)
    {
        _remainingSuspends = new(suspendsBefore);
        _resolved = resolved;
        _emitOnResolve = emitOnResolve ?? new();
        _triggerFactory = triggerFactory;
    }

    public Task<WindowResolveOutcome> Resolve(TimingWindowTrigger t, CancellationToken ct)
    {
        string id = t.Request.EffectId.Value;
        if (_remainingSuspends.TryGetValue(id, out int n) && n > 0)
        {
            _remainingSuspends[id] = n - 1;
            return Task.FromResult(WindowResolveOutcome.Suspended);
        }

        _resolved.Add(id);
        if (_emitOnResolve.TryGetValue(id, out string[]? emits) && _triggerFactory is not null)
        {
            _emitOnResolve.Remove(id); // emit once
            foreach (string e in emits) _emitted.Enqueue(_triggerFactory(e, TimingWindowTriggerKind.Mandatory));
        }

        return Task.FromResult(WindowResolveOutcome.Resolved);
    }

    public IReadOnlyList<TimingWindowTrigger> DrainEmitted()
    {
        if (_emitted.Count == 0) return Array.Empty<TimingWindowTrigger>();
        var list = _emitted.ToList();
        _emitted.Clear();
        return list;
    }
}

// Always pick the first active trigger, always accept optionals (equivalent to the legacy FIFO sync path).
sealed class FifoPort : IWindowChoicePort
{
    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct) =>
        Task.FromResult<int?>(0);
    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct) => Task.FromResult(true);
}

// A KEY-BASED record-replay port modelling the Phase-3b agent provider: the first time it meets a distinct choice
// (keyed by the effects being ordered / the effect being confirmed) it "opens an agent choice" and throws to suspend;
// the agent's eventual answer (from `script`) is recorded under that key, so every later pass replays it idempotently.
// Because it keys by effect identity — not call ordinal — it stays correct when resolved picks remove their
// choice-points from later passes. This is the exact contract the live port must honour (adversarial finding P1-a).
sealed class KeyedReplayPort : IWindowChoicePort
{
    private readonly Dictionary<string, object> _script;
    private readonly Dictionary<string, object> _answered = new();
    public int OrderCalls { get; private set; }
    public int ConfirmCalls { get; private set; }
    public string? LastOrderAnswerId { get; private set; }

    public KeyedReplayPort(Dictionary<string, object> script) { _script = script; }

    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct)
    {
        OrderCalls++;
        string key = "O:" + string.Join(",", side.Select(t => t.Request.EffectId.Value));
        var answerId = (string)Resolve(key);
        LastOrderAnswerId = answerId;
        for (int i = 0; i < side.Count; i++) if (side[i].Request.EffectId.Value == answerId) return Task.FromResult<int?>(i);
        return Task.FromResult<int?>(0);
    }

    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct)
    {
        ConfirmCalls++;
        return Task.FromResult((bool)Resolve("C:" + trigger.Request.EffectId.Value));
    }

    // First encounter of a key: record the agent's scripted answer and throw (suspend). Later encounters replay it.
    private object Resolve(string key)
    {
        if (_answered.TryGetValue(key, out object? a)) return a;
        _answered[key] = _script[key];
        throw new WindowChoicePendingException($"test: agent choice opened for {key}");
    }
}

// Models a real agent port across a suspend: the FIRST ChooseOrder throws WindowChoicePendingException (opens an
// agent choice); the next returns the index of `answerId` (the recorded selection replayed on resume).
sealed class SuspendOncePort : IWindowChoicePort
{
    private readonly string _answerId;
    public int OrderCalls { get; private set; }
    public SuspendOncePort(string answerId) { _answerId = answerId; }

    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct)
    {
        OrderCalls++;
        if (OrderCalls == 1) throw new WindowChoicePendingException("test: agent choice opened");
        for (int i = 0; i < side.Count; i++) if (side[i].Request.EffectId.Value == _answerId) return Task.FromResult<int?>(i);
        return Task.FromResult<int?>(0);
    }

    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct) => Task.FromResult(true);
}
