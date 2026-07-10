using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 1) WindowResolver loop-semantics unit tests. Validates the AS-IS MultipleSkills
// (DCGO/Assets/Scripts/Script/MultipleSkills.cs:67-423) mirror in isolation with stub dependencies:
//   re-evaluation (P1-1), player order-choice (RD-14/15), skip-all-when-all-optional, optional yes/no (RD-13),
//   consume-on-success only (VR-1/RD-12), and cut-in recursion (RD-17).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

TimingWindowTrigger Trigger(string id, HeadlessPlayerId controller, TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory)
{
    var ctx = new EffectContext(controller, controller, new HeadlessEntityId($"src:{id}"), triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>());
    var request = new EffectRequest(new HeadlessEntityId(id), controller, "OnTest", ctx);
    return new TimingWindowTrigger(request, EffectResolutionMode.MainStack, kind, priority: 0, sequence: 0);
}

string Id(TimingWindowTrigger t) => t.Request.EffectId.Value;

// --- 1. (P1-1) Re-evaluation: B's gate is false until A resolves, then B fires in the SAME window. ---
{
    var a = Trigger("A", P1);
    var b = Trigger("B", P1);
    bool aResolved = false;
    var order = new List<string>();
    var deps = new WindowResolverDeps(
        turnPlayerId: P1,
        gate: t => Id(t) == "A" || (Id(t) == "B" && aResolved),   // B gated on A having resolved
        resolveOne: (t, ct) => { order.Add(Id(t)); if (Id(t) == "A") aResolved = true; return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(),
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(new[] { a, b }, deps);
    Check(order.SequenceEqual(new[] { "A", "B" }),
        $"a trigger whose gate becomes true only AFTER an earlier one resolves still fires this window (P1-1); got [{string.Join(",", order)}]");
}

// --- 2. (RD-14) Order choice: the player picks resolution order among simultaneous mandatory triggers. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1,
        gate: _ => true,
        resolveOne: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(orderPicks: new[] { "C", "A", "B" }),
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1), Trigger("B", P1), Trigger("C", P1) }, deps);
    Check(order.SequenceEqual(new[] { "C", "A", "B" }), $"the player chooses resolution order (RD-14); got [{string.Join(",", order)}]");
}

// --- 3. Skip-all is offered only when ALL offered are optional; skipping resolves nothing. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1,
        gate: _ => true,
        resolveOne: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(orderPicks: new[] { "skip" }),
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(
        new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional), Trigger("B", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(order.Count == 0, "skip-all (all-optional side) resolves nothing");
}

// --- 4. (RD-13) Optional yes/no: a declined optional does not resolve; an accepted one does. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1,
        gate: _ => true,
        resolveOne: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(orderPicks: new[] { "A", "B" }, declineOptional: new[] { "A" }),
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(
        new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional), Trigger("B", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(order.SequenceEqual(new[] { "B" }), $"a declined optional does not resolve, an accepted one does (RD-13); got [{string.Join(",", order)}]");
}

// --- 5. (VR-1/RD-12) Consume-on-success only: no consume for a fizzle (Skipped) or a declined optional. ---
{
    var consumed = new List<string>();
    var deps = new WindowResolverDeps(P1,
        gate: _ => true,
        // A resolves, B fizzles (Skipped), C is optional-declined
        resolveOne: (t, ct) => Task.FromResult(Id(t) == "B" ? WindowResolveOutcome.Skipped : WindowResolveOutcome.Resolved),
        onResolved: t => consumed.Add(Id(t)),
        choicePort: new ScriptPort(orderPicks: new[] { "A", "B", "C" }, declineOptional: new[] { "C" }),
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(
        new[] { Trigger("A", P1), Trigger("B", P1), Trigger("C", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(consumed.SequenceEqual(new[] { "A" }),
        $"once-per-turn use is consumed ONLY on a successful resolution (not fizzle/decline); consumed [{string.Join(",", consumed)}]");
}

// --- 6. (RD-17) Cut-in recursion: resolving A emits C, which resolves BEFORE the remaining stack B. ---
{
    var order = new List<string>();
    bool emitted = false;
    var deps = new WindowResolverDeps(P1,
        gate: _ => true,
        resolveOne: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(orderPicks: new[] { "A", "B" }),
        drainNewTriggers: () =>
        {
            if (!emitted && order.LastOrDefault() == "A") { emitted = true; return new[] { Trigger("C", P1) }; }
            return Array.Empty<TimingWindowTrigger>();
        });

    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1), Trigger("B", P1) }, deps);
    Check(order.SequenceEqual(new[] { "A", "C", "B" }),
        $"a cut-in trigger emitted mid-window resolves before the remaining stack (RD-17); got [{string.Join(",", order)}]");
}

// --- 7. (RD-15) Turn-side ordering: the turn player's triggers resolve before the non-turn player's. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(turnPlayerId: P1,
        gate: _ => true,
        resolveOne: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        onResolved: _ => { },
        choicePort: new ScriptPort(),   // single per side -> auto-picks
        drainNewTriggers: () => Array.Empty<TimingWindowTrigger>());

    await new WindowResolver().RunWindowAsync(new[] { Trigger("foe", P2), Trigger("mine", P1) }, deps);
    Check(order.SequenceEqual(new[] { "mine", "foe" }),
        $"the turn player's trigger resolves before the non-turn player's (RD-15); got [{string.Join(",", order)}]");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall Stage-5 WindowResolver loop-semantics checks passed.");

// A scripted choice port: order picks by a queue of effectIds (or "skip"); optional answers by a set.
sealed class ScriptPort : IWindowChoicePort
{
    private readonly Queue<string> _orderPicks;
    private readonly HashSet<string> _declineOptional;
    public ScriptPort(IEnumerable<string>? orderPicks = null, IEnumerable<string>? declineOptional = null)
    { _orderPicks = new(orderPicks ?? Array.Empty<string>()); _declineOptional = new(declineOptional ?? Array.Empty<string>()); }

    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct)
    {
        if (_orderPicks.Count == 0)
        {
            // default: pick the first, or skip if allowed and no script
            return Task.FromResult<int?>(canSkip ? null : 0);
        }
        string next = _orderPicks.Dequeue();
        if (next == "skip") return Task.FromResult<int?>(null);
        for (int i = 0; i < side.Count; i++) if (side[i].Request.EffectId.Value == next) return Task.FromResult<int?>(i);
        return Task.FromResult<int?>(0);
    }

    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct) =>
        Task.FromResult(!_declineOptional.Contains(trigger.Request.EffectId.Value));
}
