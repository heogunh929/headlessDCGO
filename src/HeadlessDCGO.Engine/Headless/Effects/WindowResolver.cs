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

    /// <summary>Resolve the window seeded by <paramref name="seed"/>. Returns <see cref="WindowRunResult.Suspended"/>
    /// the moment a resolution suspends for an agent choice (the caller parks and resumes the in-flight body);
    /// otherwise runs the stack to exhaustion and returns <see cref="WindowRunResult.Completed"/>.</summary>
    public async Task<WindowRunResult> RunWindowAsync(
        IReadOnlyList<TimingWindowTrigger> seed,
        WindowResolverDeps deps,
        int depth = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(deps);

        // The mutable stack (AS-IS StackedSkillInfos): a trigger stays here until it is picked+committed, its
        // whole owning side is skipped, or its optional is declined. A gate-false trigger is NOT removed — it may
        // re-activate on a later pass.
        var stack = new List<TimingWindowTrigger>(seed);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // (P1-1) re-evaluate the gate of EVERY stacked trigger every pass.
            var active = stack.Where(t => deps.Gate(t)).ToList();
            if (active.Count == 0)
            {
                return WindowRunResult.Completed;
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
                // The body paused for an agent choice; the use is already consumed. The caller resumes the
                // IN-FLIGHT body (never re-picks) — see the Phase-2 wiring contract above.
                return WindowRunResult.Suspended;
            }

            // (RD-17) resolving may have emitted new events — resolve them as a cut-in BEFORE continuing the
            // remaining stack (new triggers first), bounded by the runaway safety limit.
            IReadOnlyList<TimingWindowTrigger> cutIn = deps.DrainNewTriggers();
            if (cutIn.Count > 0 && depth < deps.ChainLimit)
            {
                await RunWindowAsync(cutIn, deps, depth + 1, cancellationToken).ConfigureAwait(false);
            }
        }
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
}

/// <summary>Overall result of a window run.</summary>
public enum WindowRunResult
{
    /// <summary>The stack ran to exhaustion (no active trigger remains).</summary>
    Completed,

    /// <summary>A resolution suspended for an agent choice; the caller must park and resume the in-flight body.</summary>
    Suspended,
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
