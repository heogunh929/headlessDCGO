// ============================================================================================================
// (coroutine restore, step 2 of the IEnumerator re-migration) THE COROUTINE DRIVER — the headless replacement
// for Unity's player loop.
//
// WHAT IT REPLACES. In the original, a coroutine is COLD until `StartCoroutine` hands it to Unity, and then
// Unity's frame loop calls `MoveNext()` once per frame, inspecting `Current` to decide when to call it again.
// There is no frame loop headless, so this driver owns that decision instead. It keeps the two properties the
// original game logic actually depends on:
//   * SEQUENTIAL — one logical coroutine at a time, on one stack (`_enumerators`), never interleaved;
//   * NESTED-FIRST — `yield return <child>` runs the child TO COMPLETION before the parent's next MoveNext.
//
// HOW EACH YIELDED SHAPE MAKES PROGRESS
//   yield return null;                    the AS-IS "wait one frame". There are no frames, and nothing else
//                                         can run while this coroutine is suspended (the driver is the only
//                                         thing driving), so the wait can only ever elapse with the state
//                                         unchanged => resume immediately. 3130 AS-IS sites.
//   yield return <IEnumerator>;           push. Parent resumes only after the child's MoveNext returns false
//                                         (or the child hits `yield break`). Covers the bare
//                                         `yield return SharedActivateCoroutine(...)` shape too.
//   yield return <Coroutine>;             the StartCoroutine handle — push its routine. Same as above; this
//                                         is the dominant AS-IS shape (14205 + 1628 sites).
//   yield return <Task>;                  await it. This is the seam the CURRENT mirror parks on: the
//                                         async-translated bodies and IChoiceProvider.ChooseAsync return
//                                         Tasks, and awaiting one inside this driver suspends the WHOLE
//                                         coroutine stack in place (the stack is a local of this async
//                                         method, captured by its state machine) — i.e. a parked coroutine
//                                         frame, exactly like Unity's. Lets converted and unconverted mirror
//                                         files call each other during the migration.
//   yield return new WaitUntil/WaitWhile; evaluate the predicate. Already satisfied => resume immediately
//                                         (Unity re-polls before the next frame, so a satisfied predicate
//                                         never costs a frame of game state). Not satisfied => PARK on the
//                                         TurnFlowGate (below). NO busy-wait: the predicate is re-evaluated
//                                         only when the host steps the match.
//   yield return new WaitForSeconds(t);   resume immediately. Same argument as `yield return null`, plus:
//                                         headless has no clock the rules read, and nothing can advance
//                                         while this coroutine is suspended, so no state the original could
//                                         observe after the wait differs from the state before it. All 161
//                                         AS-IS sites are animation/notification pacing.
//   yield return <anything else>;         resume immediately (the `yield return null` argument). Covers the
//                                         AS-IS UnityWebRequest / AsyncOperation yields in unported files.
//
// PARKING — WHY THE GATE. `TurnFlowGate` (Headless/Runtime/TurnFlowPump.cs) is ALREADY the mirror's
// suspension point: `DeferredChoiceProvider.ChooseAsync` parks on it in await-mode, `TurnFlowPumpTask`
// publishes the park condition to the EngineTaskRunner as an `EngineWaitCondition`, and
// `HeadlessGameLoop.StepAsync` steps that runner once per agent action — at which point
// `TurnFlowPumpTask.StepAsync` calls `TurnFlowGate.TryRelease()`, which re-checks the condition and, if it
// holds, completes the park INLINE so the coroutine resumes on the caller's stack.
// A `yield return new WaitUntil(() => player.HasPlayerSelection())` therefore parks in EXACTLY the place a
// `await ChoiceProvider.ChooseAsync(...)` parks today, is released by exactly the same step, and needs no new
// mechanism: the gate's `WaitUntilAsync(Func<bool>)` IS the AS-IS `WaitUntil`, which is what its own doc
// comment already says.
// The gate has a single park slot on purpose (one logical coroutine), so a WaitUntil park and a ChooseAsync
// park can never overlap — the driver runs one stack.
//
// EXCEPTIONS. Unity CATCHES an exception thrown out of MoveNext, logs it, kills that coroutine and lets the
// waiting parent continue on the next frame. This driver PROPAGATES instead, because the mirror's control
// flow depends on propagation: `DeferredChoicePendingException` is a suspend-and-re-run signal that must
// unwind out of an effect body to the resolver. Propagation is also what the mirror's current async
// translation does, so this is the no-change choice. On the way out every abandoned frame is Disposed, so
// `finally` blocks in mirror bodies run — again matching the async translation (`finally` after `await`).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Coroutines;

using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;
using UnityEngine;

/// <summary>Executes an <see cref="IEnumerator"/> with the AS-IS coroutine semantics. Cold until driven:
/// nothing runs before <see cref="RunAsync(IEnumerator, CancellationToken)"/> is called.</summary>
public static class CoroutineDriver
{
    /// <summary>Run <paramref name="routine"/> to completion, parking on the ambient match's
    /// <see cref="TurnFlowGate"/> when a <c>WaitUntil</c>/<c>WaitWhile</c> is not yet satisfied. This is the
    /// entry point for the existing async callers (<c>TurnFlowPump</c> and anything that awaits mirror work
    /// today): <c>await CoroutineDriver.RunAsync(turnStateMachine.StartGame());</c></summary>
    public static Task RunAsync(IEnumerator routine, CancellationToken cancellationToken = default)
    {
        return RunAsync(routine, gate: null, cancellationToken);
    }

    /// <summary>Run <paramref name="routine"/> to completion, parking on <paramref name="gate"/> (or, when
    /// null, on the ambient match's gate). The explicit overload exists for hosts that own a gate without an
    /// installed pump.</summary>
    public static async Task RunAsync(
        IEnumerator routine,
        TurnFlowGate? gate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);

        Stack<IEnumerator> enumerators = new();
        enumerators.Push(routine);

        try
        {
            while (enumerators.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IEnumerator current = enumerators.Peek();
                if (!current.MoveNext())
                {
                    // Ran off the end, or hit `yield break`.
                    enumerators.Pop();
                    (current as IDisposable)?.Dispose();
                    continue;
                }

                switch (current.Current)
                {
                    // `yield return null;` — see the file header.
                    case null:
                        continue;

                    // MUST precede the IEnumerator case: Unity's CustomYieldInstruction is itself an
                    // IEnumerator, and pushing one as a nested coroutine would poll it in a tight loop.
                    case CustomYieldInstruction wait:
                        await ParkAsync(wait, gate).ConfigureAwait(false);
                        continue;

                    // The StartCoroutine handle.
                    case Coroutine handle:
                        enumerators.Push(handle.Routine);
                        continue;

                    // Nested coroutine, yielded directly.
                    case IEnumerator nested:
                        enumerators.Push(nested);
                        continue;

                    // The migration seam: an un-converted (still async) mirror body, or ChooseAsync.
                    case Task task:
                        await task.ConfigureAwait(false);
                        continue;

                    // Animation pacing — see the file header.
                    case WaitForSeconds:
                        continue;

                    default:
                        continue;
                }
            }
        }
        catch
        {
            // Unwind the abandoned frames so their `finally`/`using` blocks run (the async translation's
            // behaviour). The throwing frame already unwound itself inside MoveNext.
            while (enumerators.Count > 0)
            {
                (enumerators.Pop() as IDisposable)?.Dispose();
            }

            throw;
        }
    }

    /// <summary>Run <paramref name="routine"/> for the AS-IS FIRE-AND-FORGET shape
    /// (<c>StartCoroutine(x);</c> with the handle discarded — 191 AS-IS sites, none of them in the
    /// <c>yield return</c> family). Unity would run such a coroutine in PARALLEL with its starter, one
    /// MoveNext per frame; headless there is no frame loop and no second stack to run it on, so this drives
    /// it inline to completion and THROWS if it tries to park. Fail fast rather than silently drop the
    /// routine or invent a second scheduler.</summary>
    public static void RunDetached(IEnumerator routine)
    {
        ArgumentNullException.ThrowIfNull(routine);

        Task running = RunAsync(routine, NoPark, CancellationToken.None);
        if (!running.IsCompleted)
        {
            throw new InvalidOperationException(
                "A detached coroutine (fire-and-forget StartCoroutine) suspended. Headless has no frame loop " +
                "to resume it on — it must be yielded by its caller, or hoisted onto the driven stack.");
        }

        running.GetAwaiter().GetResult();
    }

    /// <summary>Sentinel gate whose park always fails: a detached coroutine may not occupy the real gate's
    /// single park slot (that slot belongs to the driven stack).</summary>
    private static readonly TurnFlowGate NoPark = new();

    private static Task ParkAsync(CustomYieldInstruction wait, TurnFlowGate? gate)
    {
        // Unity polls the predicate BEFORE granting a frame, so an already-satisfied wait costs nothing.
        if (!wait.keepWaiting)
        {
            return Task.CompletedTask;
        }

        if (ReferenceEquals(gate, NoPark))
        {
            throw new InvalidOperationException(
                "A detached coroutine hit an unsatisfied WaitUntil/WaitWhile. See CoroutineDriver.RunDetached.");
        }

        TurnFlowGate resolved = gate
            ?? AmbientGate()
            ?? throw new InvalidOperationException(
                "A coroutine hit an unsatisfied WaitUntil/WaitWhile with no TurnFlowGate to park on. The " +
                "predicate can only be re-evaluated by the host that steps the match, so the driver must run " +
                "under an installed TurnFlowPumpHost or be handed a gate explicitly.");

        // The gate re-evaluates this on TryRelease (once per host step) and completes the park inline —
        // the same release path DeferredChoiceProvider's await-mode uses.
        return resolved.WaitUntilAsync(() => !wait.keepWaiting);
    }

    private static TurnFlowGate? AmbientGate()
    {
        return AmbientMatchContext.Current is { } context
            ? TurnFlowPumpHost.Find(context)?.Gate
            : null;
    }
}
