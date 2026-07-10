namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (Stage 5, Phase 1) Re-entrant trigger window loop — the AS-IS <c>MultipleSkills.ActivateMultipleSkills_OnePlayer</c>
/// (DCGO/Assets/Scripts/Script/MultipleSkills.cs:67-423) mirror that replaces the batch pipeline
/// (collect → fixed-order → FIFO drain → optional prompt). The core is a <c>while(true)</c> that RE-EVALUATES
/// the whole stack's gate every pass (so a trigger whose condition becomes true only after an earlier one
/// resolves still fires — P1-1), lets the controlling player choose which of their simultaneous effects resolves
/// first (RD-14/15), confirms optionals with a yes/no (RD-13), consumes the once-per-turn use ONLY on a
/// successful resolution (VR-1/RD-12: AS-IS registers the use in the effect's OnProcess callback, :358-362), and
/// recurses on newly-emitted events as a cut-in before continuing (RD-17).
///
/// This Phase-1 core takes its side effects as injected delegates (<see cref="WindowResolverDeps"/>) so the loop
/// semantics are unit-testable in isolation; the real wiring (a single <c>ResolveOne</c> that unifies the
/// scheduler + activated-effect dispatch, and pause/resume across the game loop) lands at cut-over (Phase 2/3).
/// </summary>
public sealed class WindowResolver
{
    /// <summary>Default cut-in recursion depth cap (AS-IS ChainActivations / IsCutInEffectUsedMaxCount).</summary>
    public const int DefaultChainLimit = 8;

    /// <summary>Resolve the window seeded by <paramref name="seed"/>. Returns <see cref="WindowRunResult.Suspended"/>
    /// the moment a resolution suspends for an agent choice (the caller parks and resumes); otherwise runs the
    /// stack to exhaustion and returns <see cref="WindowRunResult.Completed"/>.</summary>
    public async Task<WindowRunResult> RunWindowAsync(
        IReadOnlyList<TimingWindowTrigger> seed,
        WindowResolverDeps deps,
        int depth = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(deps);

        // The mutable stack (AS-IS StackedSkillInfos): a trigger stays here until it is picked+resolved or the
        // whole offered side is skipped. A gate-false trigger is NOT removed — it may re-activate next pass.
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
            // turn player's active triggers first; only when none remain, offer the rest.
            var turnSide = active.Where(t => IsTurnSide(t, deps.TurnPlayerId)).ToList();
            List<TimingWindowTrigger> side = turnSide.Count > 0 ? turnSide : active;

            // (RD-14) pick which effect resolves first. A lone trigger auto-resolves; multiple triggers are an
            // order choice by the controlling player. "Don't activate" (skip-all) is offered ONLY when every
            // offered trigger is optional (AS-IS _CanNoSelect: all IsSkippable).
            int pickIndex;
            if (side.Count == 1)
            {
                pickIndex = 0;
            }
            else
            {
                bool canSkip = side.All(t => t.Kind == TimingWindowTriggerKind.Optional);
                int? chosen = await deps.ChoicePort.ChooseOrderAsync(side, canSkip, cancellationToken).ConfigureAwait(false);
                if (chosen is not int valid || valid < 0 || valid >= side.Count)
                {
                    // (AS-IS MultipleSkills.cs:342-345) skip-all clears the offered side and re-evaluates.
                    foreach (TimingWindowTrigger dropped in side)
                    {
                        stack.Remove(dropped);
                    }

                    continue;
                }

                pickIndex = valid;
            }

            TimingWindowTrigger pick = side[pickIndex];

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

            WindowResolveOutcome outcome = await deps.ResolveOne(pick, cancellationToken).ConfigureAwait(false);

            if (outcome == WindowResolveOutcome.Suspended)
            {
                // The resolution paused for an agent choice — the caller owns park/resume; leave the pick on the
                // stack so a resume re-offers it. (Phase 2 wiring persists this stack across the loop pause.)
                return WindowRunResult.Suspended;
            }

            stack.Remove(pick);

            // (VR-1/RD-12) consume the once-per-turn use ONLY on a real resolution — a gate fizzle (Skipped,
            // RD-10) or a declined optional consumes nothing.
            if (outcome == WindowResolveOutcome.Resolved)
            {
                deps.OnResolved(pick);
            }

            // (RD-17) resolving may have emitted new events — resolve them as a cut-in BEFORE continuing the
            // remaining stack (new triggers first), bounded by the chain limit.
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

/// <summary>Outcome of resolving one effect through the window loop.</summary>
public enum WindowResolveOutcome
{
    /// <summary>The effect resolved to completion — its once-per-turn use is consumed.</summary>
    Resolved,

    /// <summary>The effect's resolution-time gate failed (a fizzle) — dequeued, nothing consumed (RD-10).</summary>
    Skipped,

    /// <summary>The effect suspended to ask the agent a choice — the caller parks and resumes.</summary>
    Suspended,
}

/// <summary>Overall result of a window run.</summary>
public enum WindowRunResult
{
    /// <summary>The stack ran to exhaustion (no active trigger remains).</summary>
    Completed,

    /// <summary>A resolution suspended for an agent choice; the caller must park and resume.</summary>
    Suspended,
}

/// <summary>The window's interaction port — order choice among simultaneous triggers and the optional yes/no.
/// The real implementation drives the agent through the choice controller; tests script it.</summary>
public interface IWindowChoicePort
{
    /// <summary>Choose which of <paramref name="side"/> resolves first. Returns the chosen index, or null to skip
    /// the whole side (only reachable when <paramref name="canSkip"/> is true — all offered are optional).</summary>
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
        Func<TimingWindowTrigger, CancellationToken, Task<WindowResolveOutcome>> resolveOne,
        Action<TimingWindowTrigger> onResolved,
        IWindowChoicePort choicePort,
        Func<IReadOnlyList<TimingWindowTrigger>> drainNewTriggers,
        int chainLimit = WindowResolver.DefaultChainLimit)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(resolveOne);
        ArgumentNullException.ThrowIfNull(onResolved);
        ArgumentNullException.ThrowIfNull(choicePort);
        ArgumentNullException.ThrowIfNull(drainNewTriggers);

        TurnPlayerId = turnPlayerId;
        Gate = gate;
        ResolveOne = resolveOne;
        OnResolved = onResolved;
        ChoicePort = choicePort;
        DrainNewTriggers = drainNewTriggers;
        ChainLimit = chainLimit;
    }

    /// <summary>The current turn player — their active triggers are offered before the non-turn player's.</summary>
    public HeadlessPlayerId? TurnPlayerId { get; }

    /// <summary>Whether a trigger can activate RIGHT NOW (CanResolve + not-disabled + once-cap available).
    /// Re-evaluated every loop pass.</summary>
    public Func<TimingWindowTrigger, bool> Gate { get; }

    /// <summary>Resolve exactly one effect; reports Resolved / Skipped (fizzle) / Suspended.</summary>
    public Func<TimingWindowTrigger, CancellationToken, Task<WindowResolveOutcome>> ResolveOne { get; }

    /// <summary>Consume the once-per-turn use for a trigger that just resolved successfully.</summary>
    public Action<TimingWindowTrigger> OnResolved { get; }

    /// <summary>Order choice + optional yes/no port.</summary>
    public IWindowChoicePort ChoicePort { get; }

    /// <summary>Collect triggers newly emitted by the last resolution (for the cut-in recursion).</summary>
    public Func<IReadOnlyList<TimingWindowTrigger>> DrainNewTriggers { get; }

    /// <summary>Cut-in recursion depth cap.</summary>
    public int ChainLimit { get; }
}
