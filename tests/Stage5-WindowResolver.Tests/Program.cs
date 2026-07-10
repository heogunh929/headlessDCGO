using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 1) WindowResolver loop-semantics unit tests. Validates the AS-IS MultipleSkills
// (DCGO/Assets/Scripts/Script/MultipleSkills.cs:67-423) mirror in isolation with stub dependencies. Expected
// values are derived from the AS-IS control flow (cited per test), not from the loop's own decisions.

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
IReadOnlyList<TimingWindowTrigger> None() => Array.Empty<TimingWindowTrigger>();
Func<TimingWindowTrigger, CancellationToken, Task<WindowResolveOutcome>> RecordBody(List<string> order) =>
    (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); };

// --- 1. (P1-1) Re-evaluation: B's gate is false until A resolves, then B fires in the SAME window.
//        A collect-once/fixed-order batch (active=[A] at t0) could never fire B. (AS-IS while-loop re-filters
//        StackedSkillInfos by CanActivate every pass, MultipleSkills.cs:122/164.) ---
{
    var order = new List<string>();
    bool aResolved = false;
    var deps = new WindowResolverDeps(P1,
        gate: t => Id(t) == "A" || (Id(t) == "B" && aResolved),
        commit: _ => { },
        resolveBody: (t, ct) => { order.Add(Id(t)); if (Id(t) == "A") aResolved = true; return Task.FromResult(WindowResolveOutcome.Resolved); },
        choicePort: new ScriptPort(),
        drainNewTriggers: None);

    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1), Trigger("B", P1) }, deps);
    Check(order.SequenceEqual(new[] { "A", "B" }),
        $"a trigger whose gate becomes true only AFTER an earlier one resolves still fires this window (P1-1); got [{string.Join(",", order)}]");
}

// --- 2. (RD-14) Order choice: the player picks resolution order among simultaneous mandatory triggers
//        (AS-IS OpenSelectCardPanel, MultipleSkills.cs:272-300). ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1, _ => true, _ => { }, RecordBody(order),
        new ScriptPort(orderPicks: new[] { "C", "A", "B" }), None);
    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1), Trigger("B", P1), Trigger("C", P1) }, deps);
    Check(order.SequenceEqual(new[] { "C", "A", "B" }), $"the player chooses resolution order (RD-14); got [{string.Join(",", order)}]");
}

// --- 3. (F1) skip-all clears the CURRENT PLAYER'S WHOLE stack — including a same-side trigger that is gate-false
//        at skip time but would flip true later. (AS-IS: skillIndex=-1 → StackedSkillInfos = new List() → window
//        ends, MultipleSkills.cs:342-345.) A "remove only the active side" bug would let the leftover fire. ---
{
    var order = new List<string>();
    int cEvals = 0;
    var deps = new WindowResolverDeps(P1,
        gate: t => { if (Id(t) == "C") { cEvals++; return cEvals >= 2; } return true; },  // C gate-false first pass, true after
        commit: _ => { },
        resolveBody: RecordBody(order),
        choicePort: new ScriptPort(orderPicks: new[] { "skip" }),
        drainNewTriggers: None);

    await new WindowResolver().RunWindowAsync(
        new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional), Trigger("B", P1, TimingWindowTriggerKind.Optional), Trigger("C", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(order.Count == 0,
        $"skip-all wipes the whole player stack, so a gate-false leftover (C) never fires (F1); got [{string.Join(",", order)}]");
}

// --- 4. (RD-13) Optional yes/no: a declined optional does not resolve; an accepted one does. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1, _ => true, _ => { }, RecordBody(order),
        new ScriptPort(orderPicks: new[] { "A", "B" }, declineOptional: new[] { "A" }), None);
    await new WindowResolver().RunWindowAsync(
        new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional), Trigger("B", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(order.SequenceEqual(new[] { "B" }), $"a declined optional does not resolve, an accepted one does (RD-13); got [{string.Join(",", order)}]");
}

// --- 5. (F5/VR-1/RD-12) CONSUME at COMMIT — an accepted effect is consumed BEFORE its body runs, so it stays
//        consumed even when the body SUSPENDS (AS-IS Activate_Execute :1120 OnProcessCallbuck then :1124 body).
//        A declined optional consumes nothing. ---
{
    var committed = new List<string>();
    var deps = new WindowResolverDeps(P1, _ => true, t => committed.Add(Id(t)),
        resolveBody: (t, ct) => Task.FromResult(WindowResolveOutcome.Suspended),   // body suspends AFTER commit
        choicePort: new ScriptPort(), drainNewTriggers: None);
    WindowRunResult r = await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(committed.SequenceEqual(new[] { "A" }) && r == WindowRunResult.Suspended,
        "an accepted optional is consumed at COMMIT even though its body then suspends (F5)");

    var committed2 = new List<string>();
    var deps2 = new WindowResolverDeps(P1, _ => true, t => committed2.Add(Id(t)),
        (t, ct) => Task.FromResult(WindowResolveOutcome.Resolved),
        new ScriptPort(declineOptional: new[] { "A" }), None);
    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional) }, deps2);
    Check(committed2.Count == 0, "a declined optional consumes nothing");
}

// --- 5b. (RD-10) commit-time gate re-check: a trigger active at pass start but gate-false by commit fizzles —
//         removed, nothing consumed, no body. ---
{
    var committed = new List<string>();
    var order = new List<string>();
    bool gateA = true;
    var deps = new WindowResolverDeps(P1,
        gate: t => Id(t) != "A" || gateA,
        commit: t => committed.Add(Id(t)),
        resolveBody: (t, ct) => { order.Add(Id(t)); return Task.FromResult(WindowResolveOutcome.Resolved); },
        // ChooseOrder picks A first; ConfirmOptional turns A's gate off just before commit re-check.
        choicePort: new FlipPort(onOptionalOrOrder: () => gateA = false),
        drainNewTriggers: None);
    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1, TimingWindowTriggerKind.Optional) }, deps);
    Check(committed.Count == 0 && order.Count == 0,
        "a trigger whose gate lapses by commit time fizzles — no consume, no body (RD-10)");
}

// --- 6. (RD-17) Cut-in recursion: resolving A emits C, which resolves BEFORE the remaining stack B. ---
{
    var order = new List<string>();
    bool emitted = false;
    var deps = new WindowResolverDeps(P1, _ => true, _ => { }, RecordBody(order),
        new ScriptPort(orderPicks: new[] { "A", "B" }),
        drainNewTriggers: () => { if (!emitted && order.LastOrDefault() == "A") { emitted = true; return new[] { Trigger("C", P1) }; } return None(); });
    await new WindowResolver().RunWindowAsync(new[] { Trigger("A", P1), Trigger("B", P1) }, deps);
    Check(order.SequenceEqual(new[] { "A", "C", "B" }),
        $"a cut-in trigger emitted mid-window resolves before the remaining stack (RD-17); got [{string.Join(",", order)}]");
}

// --- 7. (RD-15) Turn-side ordering: the turn player's triggers resolve before the non-turn player's. ---
{
    var order = new List<string>();
    var deps = new WindowResolverDeps(P1, _ => true, _ => { }, RecordBody(order), new ScriptPort(), None);
    await new WindowResolver().RunWindowAsync(new[] { Trigger("foe", P2), Trigger("mine", P1) }, deps);
    Check(order.SequenceEqual(new[] { "mine", "foe" }),
        $"the turn player's trigger resolves before the non-turn player's (RD-15); got [{string.Join(",", order)}]");
}

// --- 8. (F4) A non-skippable side (contains a mandatory) whose port returns "no selection" must THROW, not
//        silently drop the mandatory effects. ---
{
    var deps = new WindowResolverDeps(P1, _ => true, _ => { },
        (t, ct) => Task.FromResult(WindowResolveOutcome.Resolved),
        new ScriptPort(orderPicks: new[] { "skip" }), None);   // returns null for a mixed (mandatory-present) side
    bool threw = false;
    try
    {
        await new WindowResolver().RunWindowAsync(
            new[] { Trigger("mand", P1), Trigger("opt", P1, TimingWindowTriggerKind.Optional) }, deps);
    }
    catch (InvalidOperationException) { threw = true; }
    Check(threw, "a null pick on a non-skippable side throws instead of silently dropping the mandatory (F4)");
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
        if (_orderPicks.Count == 0) return Task.FromResult<int?>(canSkip ? null : 0);
        string next = _orderPicks.Dequeue();
        if (next == "skip") return Task.FromResult<int?>(null);
        for (int i = 0; i < side.Count; i++) if (side[i].Request.EffectId.Value == next) return Task.FromResult<int?>(i);
        return Task.FromResult<int?>(0);
    }

    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct) =>
        Task.FromResult(!_declineOptional.Contains(trigger.Request.EffectId.Value));
}

// A port that fires a side effect when asked (used to flip a gate off just before the commit re-check).
sealed class FlipPort : IWindowChoicePort
{
    private readonly Action _onOptionalOrOrder;
    public FlipPort(Action onOptionalOrOrder) { _onOptionalOrOrder = onOptionalOrOrder; }
    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken ct)
    { _onOptionalOrOrder(); return Task.FromResult<int?>(0); }
    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken ct)
    { _onOptionalOrOrder(); return Task.FromResult(true); }
}
