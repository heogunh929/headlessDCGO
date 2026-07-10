namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (Stage 5, Phase 1) Re-entrant trigger window loop — the AS-IS <c>MultipleSkills.ActivateMultipleSkills_OnePlayer</c>
/// (DCGO/Assets/Scripts/Script/MultipleSkills.cs:67-423) mirror that replaces the batch pipeline
/// (collect → fixed-order → FIFO drain → optional prompt). The core is a <c>while(true)</c> that RE-EVALUATES
/// the whole stack's gate every pass (so a trigger whose condition becomes true only after an earlier one
/// resolves still fires — P1-1), lets the controlling player choose which of their simultaneous effects resolves
/// first (RD-14/15), confirms optionals with a yes/no (RD-13), CONSUMES the once-per-turn use at COMMIT — right
/// after the accept decision and a commit-time gate re-check, BEFORE the body runs (AS-IS
/// <c>Activate_Execute</c> fires <c>OnProcessCallbuck</c>→<c>RegisterUseEffectThisTurn</c> at ICardEffect.cs:1120
/// then runs the body at :1124), and recurses on newly-emitted events as a cut-in before continuing (RD-17).
///
/// This Phase-1 core takes its side effects as injected delegates (<see cref="WindowResolverDeps"/>) so the loop
/// semantics are unit-testable in isolation.
///
/// PHASE-2 WIRING CONTRACT (not implemented here — flagged so the wiring honours it):
///  - <b>Rule processing between picks</b>: AS-IS runs <c>RuleProcess()</c> (state-based deletions, end-game check)
///    after every pick (MultipleSkills.cs:398-403) BEFORE re-evaluating the stack. The wiring must run rule
///    processing (and honour end-game) between <see cref="WindowResolverDeps.ResolveBody"/> and the next pass —
///    the injected <c>Gate</c>/<c>DrainNewTriggers</c> see the post-rule-process board.
///  - <b>Suspend/resume</b>: on a body that suspends for an agent choice, the once-use is ALREADY consumed (AS-IS
///    consumes at commit, before the body). The resume must CONTINUE the in-flight body (the scheduler/
///    DeferredChoiceProvider replays), it must NOT re-pick or re-commit the same trigger — else double-consume
///    and double-mutation. The loop signals this by returning <see cref="WindowRunResult.Suspended"/> after the
///    commit; the caller (a WindowResolutionController) owns persisting the remaining stack + the in-flight pick.
///  - <b>Per-effect cut-in caps</b>: AS-IS caps cut-ins PER EFFECT USE (ChainActivations / IsCutInEffectUsedMaxCount,
///    AutoProcessing.cs:1090-1104), tracked in the collection Gate — NOT a global recursion depth. The
///    <see cref="WindowResolverDeps.ChainLimit"/> here is only a runaway-recursion SAFETY bound; per-effect caps
///    belong in <c>Gate</c>.
/// </summary>
public sealed class WindowResolver
{
    /// <summary>Runaway-recursion safety bound (NOT an AS-IS cap — see class remarks). Set high; a real chain is
    /// bounded by per-effect cut-in caps in the Gate, not by depth.</summary>
    public const int DefaultChainLimit = 64;

    /// <summary>Resolve the window seeded by <paramref name="seed"/> to exhaustion (fresh, non-resumable path used
    /// by the synchronous windows). Returns <see cref="WindowRunResult.Suspended"/> the moment a resolution
    /// suspends; otherwise <see cref="WindowRunResult.Completed"/>. To PERSIST and later resume a suspended window,
    /// build a <see cref="WindowContinuation"/> with <see cref="CreateContinuation"/> and drive it with
    /// <see cref="DriveAsync"/> instead — that hands the frame stack to the caller (a WindowResolutionController).</summary>
    public Task<WindowRunResult> RunWindowAsync(
        IReadOnlyList<TimingWindowTrigger> seed,
        WindowResolverDeps deps,
        int depth = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(deps);
        return DriveAsync(CreateContinuation(seed, depth), deps, cancellationToken);
    }

    /// <summary>Build a fresh, resumable continuation seeded by <paramref name="seed"/>. The caller owns it across
    /// a suspend/resume — the C# call stack does not survive the agent-choice pause, so the nested window state
    /// (the cut-in frame stack + any in-flight pick) lives in this object instead.</summary>
    public static WindowContinuation CreateContinuation(IReadOnlyList<TimingWindowTrigger> seed, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var frames = new Stack<WindowFrame>();
        frames.Push(new WindowFrame(seed, depth));
        return new WindowContinuation(frames);
    }

    /// <summary>Drive a continuation — fresh or resumed — until it exhausts (<see cref="WindowRunResult.Completed"/>)
    /// or suspends for an agent choice (<see cref="WindowRunResult.Suspended"/>). On a suspend the caller must hold
    /// this same <paramref name="continuation"/> and call <see cref="DriveAsync"/> again once the pending choice is
    /// resolved: a BODY suspend records the in-flight pick (its body is replayed on resume — the once-use is already
    /// consumed, so it is NOT re-picked or re-committed); a CHOICE suspend (order / optional yes-no) leaves the frame
    /// stack unmutated and the loop re-offers the same choice, which the resumed port answers from the recorded
    /// selection. Either way the frame stack — including deeper cut-in frames — persists intact.</summary>
    public async Task<WindowRunResult> DriveAsync(
        WindowContinuation continuation, WindowResolverDeps deps, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(deps);

        Stack<WindowFrame> frames = continuation.Frames;

        // (resume of a BODY suspend) the picked effect's body parked mid-resolution for an agent choice. Replay it
        // (the scheduler / DeferredChoiceProvider resumes the in-flight body) BEFORE re-entering the loop — never
        // re-pick or re-commit (the once-use was consumed at the original commit). If it re-suspends, stay parked.
        if (continuation.InFlightPick is { } resumePick)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowResolveOutcome resumeOutcome = await deps.ResolveBody(resumePick, cancellationToken).ConfigureAwait(false);
            if (resumeOutcome == WindowResolveOutcome.Suspended)
            {
                return WindowRunResult.Suspended;
            }

            continuation.InFlightPick = null;

            // (RD-17) the resumed body may have emitted cut-ins — drain them into a child of the frame that owned
            // the pick (still the top frame; the suspend removed only the pick, never popped its frame), matching
            // the in-line post-body draining below.
            DrainCutInInto(frames, deps);
        }

        try
        {
            return await RunFrameLoopAsync(continuation, deps, cancellationToken).ConfigureAwait(false);
        }
        catch (WindowChoicePendingException)
        {
            // (CHOICE suspend) the port opened an agent choice for the order / optional decision and unwound the
            // loop. Nothing was picked or committed this pass, so the frame stack is consistent; the caller resumes
            // by calling DriveAsync again (the port then answers from the recorded selection). No in-flight pick.
            return WindowRunResult.Suspended;
        }
    }

    /// <summary>(RD-17) drain triggers emitted by the just-resolved body into a new cut-in frame, one level below
    /// the current top frame, bounded by the runaway limit. No-op when nothing was emitted or the limit is hit.</summary>
    private static void DrainCutInInto(Stack<WindowFrame> frames, WindowResolverDeps deps)
    {
        IReadOnlyList<TimingWindowTrigger> cutIn = deps.DrainNewTriggers();
        if (cutIn.Count > 0 && frames.Count > 0 && frames.Peek().Depth < deps.ChainLimit)
        {
            frames.Push(new WindowFrame(cutIn, frames.Peek().Depth + 1));
        }
    }

    private static async Task<WindowRunResult> RunFrameLoopAsync(
        WindowContinuation continuation, WindowResolverDeps deps, CancellationToken cancellationToken)
    {
        Stack<WindowFrame> frames = continuation.Frames;
        while (frames.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Process the TOP frame (deepest cut-in). Its mutable stack IS the AS-IS StackedSkillInfos: a trigger
            // stays until it is picked+committed, its whole owning side is skipped, or its optional is declined. A
            // gate-false trigger is NOT removed — it may re-activate on a later pass.
            WindowFrame frame = frames.Peek();
            List<TimingWindowTrigger> stack = frame.Stack;

            // (P1-1) re-evaluate the gate of EVERY stacked trigger every pass.
            var active = stack.Where(t => deps.Gate(t)).ToList();
            if (active.Count == 0)
            {
                // This frame is exhausted; pop back to its parent (whose loop head re-evaluates next), or finish.
                frames.Pop();
                continue;
            }

            // (RD-15) AS-IS resolves ALL of the turn player's windows before the non-turn player's. Offer the
            // turn player's active triggers first; only when none remain, offer the rest. `side` is therefore
            // always a single player's active set (there are only two players).
            var turnSide = active.Where(t => IsTurnSide(t, deps.TurnPlayerId)).ToList();
            List<TimingWindowTrigger> side = turnSide.Count > 0 ? turnSide : active;
            HeadlessPlayerId sidePlayer = side[0].Request.ControllerId;

            // (RD-14) pick which effect resolves first. A lone trigger auto-resolves; multiple triggers are an
            // order choice by the controlling player. "Don't activate" (skip-all) is offered ONLY when every
            // offered trigger is optional (AS-IS _CanNoSelect: all IsSkippable, MultipleSkills.cs:284/293).
            TimingWindowTrigger pick;
            if (side.Count == 1)
            {
                pick = side[0];
            }
            else
            {
                bool canSkip = side.All(t => t.Kind == TimingWindowTriggerKind.Optional);
                int? chosen = await deps.ChoicePort.ChooseOrderAsync(side, canSkip, cancellationToken).ConfigureAwait(false);
                if (chosen is null)
                {
                    if (!canSkip)
                    {
                        // A non-skippable set must never yield "no pick" (AS-IS panel _CanNoSelect=false cannot
                        // produce skillIndex=-1). Refuse to silently drop mandatory effects.
                        throw new InvalidOperationException(
                            "ChooseOrderAsync returned no selection for a side that contains a mandatory trigger.");
                    }

                    // (AS-IS MultipleSkills.cs:342-345) skip-all clears the CURRENT PLAYER'S WHOLE stack (active
                    // AND gate-false leftovers) and ends that player's window — the outer loop then offers the
                    // other player. Mirror by removing every stack trigger owned by this side's player.
                    stack.RemoveAll(t => t.Request.ControllerId == sidePlayer);
                    continue;
                }

                if (chosen.Value < 0 || chosen.Value >= side.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(deps), $"ChooseOrderAsync returned out-of-range index {chosen.Value} for a side of {side.Count}.");
                }

                pick = side[chosen.Value];
            }

            // (RD-13) an optional effect asks yes/no before running; declining removes it and consumes nothing.
            if (pick.Kind == TimingWindowTriggerKind.Optional)
            {
                bool accept = await deps.ChoicePort.ConfirmOptionalAsync(pick, cancellationToken).ConfigureAwait(false);
                if (!accept)
                {
                    stack.Remove(pick);
                    continue;
                }
            }

            // (RD-10) commit-time gate re-check (AS-IS MultipleSkills.cs:366 re-checks CanActivate right before
            // Execute): if the condition lapsed since this pass began — e.g. an earlier resolution changed the
            // board — the trigger FIZZLES: removed, nothing consumed, no body.
            if (!deps.Gate(pick))
            {
                stack.Remove(pick);
                continue;
            }

            stack.Remove(pick);

            // (F5 / VR-1 / RD-12) CONSUME at commit — after accept + gate re-check, BEFORE the body (AS-IS
            // Activate_Execute :1120 OnProcessCallbuck then :1124 body). A declined optional / commit-fizzle
            // above consumed nothing; a body that later suspends or soft-fizzles keeps the use spent (AS-IS).
            deps.Commit(pick);

            WindowResolveOutcome outcome = await deps.ResolveBody(pick, cancellationToken).ConfigureAwait(false);
            if (outcome == WindowResolveOutcome.Suspended)
            {
                // The body paused for an agent choice; the use is already consumed. Record the in-flight pick so a
                // resumable caller replays THIS body (never re-picks/re-commits) on the next DriveAsync. The whole
                // frame stack is still intact — this pick was removed from its (still-top) frame, so on resume the
                // resumed body's cut-ins drain into a child of that same frame.
                continuation.InFlightPick = pick;
                return WindowRunResult.Suspended;
            }

            if (outcome == WindowResolveOutcome.SuspendedExternally)
            {
                // (3b-iii) the body paused for an agent choice but RESUMES OUTSIDE this window — an activated-effect
                // bridge body suspends via DeferredActivations, which the action-processor re-invokes directly (not
                // by replaying this window's ResolveBody). So DON'T record an in-flight pick: the pick is already
                // removed from its frame, its use consumed, and on the re-drive (after the external body finishes)
                // the loop simply continues the remaining stack. Suspending the window still pauses the main loop.
                return WindowRunResult.Suspended;
            }

            // (RD-17) resolving may have emitted new events — resolve them as a cut-in BEFORE continuing the
            // remaining stack (new triggers first), bounded by the runaway safety limit. Pushing a frame makes the
            // next loop pass process the cut-in depth-first; when it exhausts, the pop returns here to this frame.
            DrainCutInInto(frames, deps);
        }

        return WindowRunResult.Completed;
    }

    /// <summary>One level of the cut-in recursion, made explicit so the nested window state survives a suspend.
    /// <see cref="Stack"/> is the frame's live AS-IS StackedSkillInfos; <see cref="Depth"/> bounds further cut-ins.</summary>
    internal sealed class WindowFrame
    {
        public WindowFrame(IReadOnlyList<TimingWindowTrigger> seed, int depth)
        {
            Stack = new List<TimingWindowTrigger>(seed);
            Depth = depth;
        }

        public List<TimingWindowTrigger> Stack { get; }

        public int Depth { get; }
    }

    private static bool IsTurnSide(TimingWindowTrigger trigger, HeadlessPlayerId? turnPlayerId) =>
        turnPlayerId is HeadlessPlayerId tp && trigger.Request.ControllerId == tp;
}

/// <summary>Outcome of running one effect's BODY through the window loop (the once-use is already consumed at
/// commit before this runs, so there is no "Skipped/fizzle" here — a fizzle is the commit-time gate re-check).</summary>
public enum WindowResolveOutcome
{
    /// <summary>The body ran to completion.</summary>
    Resolved,

    /// <summary>The body suspended to ask the agent a choice — the caller parks and resumes the in-flight body.</summary>
    Suspended,

    /// <summary>(3b-iii) The body suspended for an agent choice but its resume is owned OUTSIDE the window (an
    /// activated-effect bridge body parked in DeferredActivations, re-invoked by the action-processor). The window
    /// records NO in-flight pick — the pick is already removed and consumed; on the caller's re-drive the loop
    /// continues the remaining stack. Distinguished from <see cref="Suspended"/> so the loop does not replay it.</summary>
    SuspendedExternally,
}

/// <summary>Overall result of a window run.</summary>
public enum WindowRunResult
{
    /// <summary>The stack ran to exhaustion (no active trigger remains).</summary>
    Completed,

    /// <summary>A resolution suspended for an agent choice; the caller must park and resume the in-flight body.</summary>
    Suspended,
}

/// <summary>The persistable state of a suspended (or fresh, not-yet-run) window: the cut-in frame stack plus the
/// pick whose body parked mid-resolution, if any. Held by a WindowResolutionController across the agent-choice
/// pause (the C# call stack does not survive it), then handed back to <see cref="WindowResolver.DriveAsync"/> to
/// resume. Opaque to callers — only the resolver reads its innards.</summary>
public sealed class WindowContinuation
{
    internal WindowContinuation(Stack<WindowResolver.WindowFrame> frames)
    {
        Frames = frames;
    }

    /// <summary>The cut-in frame stack (top = deepest cut-in). Non-empty while the window has unresolved triggers.</summary>
    internal Stack<WindowResolver.WindowFrame> Frames { get; }

    /// <summary>The pick whose body suspended mid-resolution (its once-use is ALREADY consumed). Replayed — never
    /// re-picked or re-committed — on the next <see cref="WindowResolver.DriveAsync"/>; null when the suspend was a
    /// pre-commit CHOICE (order / optional yes-no) rather than a body. Read-only to callers; only the resolver sets it.</summary>
    public TimingWindowTrigger? InFlightPick { get; internal set; }

    /// <summary>True once the window is fully resolved (no frames left and no in-flight body) — the caller clears it.</summary>
    public bool IsExhausted => Frames.Count == 0 && InFlightPick is null;
}

/// <summary>Thrown by a real <see cref="IWindowChoicePort"/> to SUSPEND the window at an order / optional decision:
/// the port has opened an agent choice on the choice controller and cannot answer synchronously. Unwinds the loop
/// leaving the frame stack unmutated (nothing was picked or committed this pass); <see cref="WindowResolver.DriveAsync"/>
/// catches it and reports <see cref="WindowRunResult.Suspended"/>. Mirrors the engine's existing
/// <c>DeferredChoicePendingException</c> for activated-effect bodies. Scripted test ports never throw it.</summary>
public sealed class WindowChoicePendingException : Exception
{
    public WindowChoicePendingException(string message) : base(message)
    {
    }

    public WindowChoicePendingException() : base("Window suspended for an agent choice.")
    {
    }
}

/// <summary>The window's interaction port — order choice among simultaneous triggers and the optional yes/no.
/// The real implementation drives the agent through the choice controller; tests script it.</summary>
public interface IWindowChoicePort
{
    /// <summary>Choose which of <paramref name="side"/> resolves first. Returns the chosen index, or null to skip
    /// the whole side (which the caller honours ONLY when <paramref name="canSkip"/> is true — all offered are
    /// optional; returning null for a non-skippable side is a contract violation).</summary>
    Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken cancellationToken);

    /// <summary>Ask the controlling player whether to activate an optional effect (RD-13). True = activate.</summary>
    Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken cancellationToken);
}

/// <summary>Injected side effects for <see cref="WindowResolver.RunWindowAsync"/>. Real wiring supplies registry/
/// scheduler-backed delegates at cut-over; tests supply stubs.</summary>
public sealed record WindowResolverDeps
{
    public WindowResolverDeps(
        HeadlessPlayerId? turnPlayerId,
        Func<TimingWindowTrigger, bool> gate,
        Action<TimingWindowTrigger> commit,
        Func<TimingWindowTrigger, CancellationToken, Task<WindowResolveOutcome>> resolveBody,
        IWindowChoicePort choicePort,
        Func<IReadOnlyList<TimingWindowTrigger>> drainNewTriggers,
        int chainLimit = WindowResolver.DefaultChainLimit)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(resolveBody);
        ArgumentNullException.ThrowIfNull(choicePort);
        ArgumentNullException.ThrowIfNull(drainNewTriggers);

        TurnPlayerId = turnPlayerId;
        Gate = gate;
        Commit = commit;
        ResolveBody = resolveBody;
        ChoicePort = choicePort;
        DrainNewTriggers = drainNewTriggers;
        ChainLimit = chainLimit;
    }

    /// <summary>The current turn player — their active triggers are offered before the non-turn player's.</summary>
    public HeadlessPlayerId? TurnPlayerId { get; }

    /// <summary>Whether a trigger can activate RIGHT NOW (CanResolve + not-disabled + once-cap available + any
    /// per-effect cut-in cap). Re-evaluated every loop pass AND once more at commit time.</summary>
    public Func<TimingWindowTrigger, bool> Gate { get; }

    /// <summary>Consume the once-per-turn use for a trigger being committed (fired BEFORE the body, matching
    /// AS-IS OnProcessCallbuck). Must be idempotent across a suspend/resume of the same in-flight pick.</summary>
    public Action<TimingWindowTrigger> Commit { get; }

    /// <summary>Run one effect's body; reports Resolved or Suspended.</summary>
    public Func<TimingWindowTrigger, CancellationToken, Task<WindowResolveOutcome>> ResolveBody { get; }

    /// <summary>Order choice + optional yes/no port.</summary>
    public IWindowChoicePort ChoicePort { get; }

    /// <summary>Collect triggers newly emitted by the last resolution (for the cut-in recursion). The wiring runs
    /// rule-processing before this so the cut-in reflects the settled board.</summary>
    public Func<IReadOnlyList<TimingWindowTrigger>> DrainNewTriggers { get; }

    /// <summary>Runaway-recursion safety bound (not an AS-IS cap).</summary>
    public int ChainLimit { get; }
}
