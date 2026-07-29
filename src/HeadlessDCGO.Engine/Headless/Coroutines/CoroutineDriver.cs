// ============================================================================================================
// THE HEADLESS REPLACEMENT FOR UNITY'S COROUTINE SCHEDULER.
//
// WHY THIS FILE EXISTS. The AS-IS game logic is written as `IEnumerator` coroutines. An `IEnumerator` method
// runs NOTHING when called — it returns a plan, and the body advances only when somebody calls `MoveNext()`.
// In Unity that somebody is the player loop, once per frame per running routine. There is no player loop here,
// so without this driver the engine compiles and then never executes a single statement.
//
// WHY IT SCHEDULES MANY ROUTINES AND NOT ONE. An earlier version drove a single stack and "parked" whenever a
// predicate wait was not yet satisfied. That was wrong about the original, and measurement showed it:
//
//   * `StartCoroutine(x);` with the handle DISCARDED appears at 153 AS-IS sites, including the engine's own
//     entry `StartCoroutine(turnStateMachine.Init())` (GManager.cs:275). Unity runs those CONCURRENTLY with
//     the starter. Running them inline changes what has happened by the time the starter's next statement
//     executes; deferring them changes it differently. Neither is the original.
//
//   * `WaitUntil`/`WaitWhile` predicates are re-polled every frame precisely because SOMETHING ELSE is
//     expected to be running and to change their inputs. With one routine at a time there is nothing else, so
//     such a wait could only spin or park.
//
// So this driver does what Unity does: it keeps a list of independent routine stacks and advances each of them
// one step per tick. A tick is one frame.
//
// WHAT IS FAITHFUL
//   `yield return <Coroutine>`   The child runs to completion before its PARENT advances, while other routines
//                                keep running. 15,833 AS-IS sites (14,205 via ContinuousController + 1,628
//                                bare). Reproduced by pushing the child onto that routine's own stack.
//   `StartCoroutine(x);`         A new independent routine, advancing alongside the starter. 153 sites.
//   `yield return null`          Unity yields the rest of the frame. Reproduced exactly: that routine stops
//                                for this tick and the others still get their step.
//   `WaitUntil` / `WaitWhile`    Re-polled every tick, so another routine can satisfy them — which is how the
//                                original works.
//
// WHAT IS DELIBERATELY NOT
//   `WaitForSeconds(t)`          There is no clock; it completes on the next tick. 161 sites, all presentation
//                                pacing. The STATE SEQUENCE is unchanged, only the wall-clock gap disappears.
//   EXCEPTIONS                   Unity swallows an exception thrown inside a coroutine: it logs, kills that
//                                routine, and lets everything else continue. This driver PROPAGATES, because a
//                                rule that throws is a defect we want to see. If an AS-IS path is later found
//                                to DEPEND on Unity's swallowing, that dependency gets recorded rather than
//                                silently emulated.
//
// WHERE THE PLAYER GETS TO DECIDE. When a whole tick passes with no routine making progress, the engine is
// waiting for something outside itself. Measured across the AS-IS tree, the predicate waits that can be in
// that state for a real reason are `Player.HasPlayerSelection()` (8 sites) — and the original already carries
// the seam: `Player` holds a `Queue<IPlayerSelection>` and `QueuePlayerSelection` fills it. So a stalled tick
// is the cue to supply a decision, not a bug by itself. `RunToCompletion` reports the stall instead of
// spinning; wiring a provider to it is roadmap step 2.5.
// (The other high-count predicates are NOT decision points, contrary to first impressions:
//  `!end` ×24 are DOTween completion flags, satisfied by the tween shim running its callbacks on Play();
//  `commandText.gameObject.activeSelf` ×21 are checked AFTER the same routine closed the panel — 21 of 21
//  measured — so they pass immediately; the rest are Photon/lobby, which is out of scope.)
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Coroutines;

using System.Collections;
using UnityEngine;

/// <summary>Why <see cref="CoroutineDriver.RunToCompletion"/> stopped.</summary>
public enum DriverStopReason
{
    /// <summary>Every routine finished.</summary>
    Idle,

    /// <summary>A whole tick passed with no routine advancing. Something outside the engine has to change —
    /// normally a player decision; see the file header.</summary>
    Stalled,

    /// <summary>The tick budget ran out while routines were still progressing.</summary>
    BudgetExhausted,
}

/// <summary>The outcome of running the scheduler.</summary>
public sealed record DriverResult(DriverStopReason Reason, int Ticks, int ActiveRoutines)
{
    public bool IsIdle => Reason == DriverStopReason.Idle;
}

/// <summary>Drives AS-IS coroutines in place of Unity's player loop: many routines, one step each per tick.
/// See the file header for which Unity semantics are reproduced and which are deliberately not.</summary>
public sealed class CoroutineDriver
{
    /// <summary>One scheduled routine: its call stack, plus the predicate wait it is currently suspended on.
    ///
    /// The wait MUST be remembered. A `yield return new WaitWhile(pred)` suspends the routine until the
    /// predicate is satisfied, which can take any number of frames; calling MoveNext again while it is still
    /// waiting resumes the routine PAST the yield and the wait never happens. That was a real defect here —
    /// `TurnStateMachine.Init()` walked past `WaitWhile(() => !DoneSetRandom)` with the flag still false and
    /// then shuffled with an unseeded RNG.</summary>
    private sealed class Routine
    {
        public Stack<IEnumerator> Stack { get; } = new();

        /// <summary>What this routine was started as — the bottom of its stack, kept for diagnostics.</summary>
        public string Root { get; set; } = "";

        public CustomYieldInstruction? Waiting { get; set; }
    }

    private readonly List<Routine> _routines = new();
    private readonly List<IEnumerator> _pending = new();

    /// <summary>Handles this driver has taken over. A handle that a parent later yields on is NOT an
    /// independent routine — see <see cref="Adopt"/>.</summary>
    private readonly HashSet<Coroutine> _adopted = new();

    /// <summary>Subscribes to <see cref="MonoBehaviour.Started"/> so every StartCoroutine becomes a scheduled
    /// routine. Without this the fire-and-forget shape never runs: `MonoBehaviour.StartCoroutine` returns a
    /// COLD handle, and 153 AS-IS sites discard it.</summary>
    public IDisposable AttachToStartCoroutine()
    {
        Action<Coroutine> started = Adopt;
        Action<IEnumerator> stopped = Kill;
        MonoBehaviour.Started += started;
        MonoBehaviour.Stopped += stopped;

        return new Subscription(() =>
        {
            MonoBehaviour.Started -= started;
            MonoBehaviour.Stopped -= stopped;
        });
    }

    private void Adopt(Coroutine handle)
    {
        if (_adopted.Add(handle))
        {
            _pending.Add(handle.Routine);
            _handles[handle.Routine] = handle;
        }
    }

    /// <summary>enumerator → 핸들. 루틴이 세상을 떠나는 모든 지점(완료 pop·Kill)에서 소유 컴포넌트의
    /// `_running`으로 핸들을 반납하기 위한 지도 — Unity는 완료 시점에 핸들을 풀어준다. 이게 없어서
    /// 완료된 코루틴 수천 개(캡처 상태 포함)가 판 내내 잔류했다(22:36-22:40 OOM의 축적기 2).</summary>
    private readonly Dictionary<object, Coroutine> _handles = new(ReferenceEqualityComparer.Instance);

    private void ReleaseHandleFor(IEnumerator finished)
    {
        if (_handles.Remove(finished, out Coroutine? handle))
        {
            handle.Owner?.ReleaseFinished(handle);
        }
    }

    /// <summary>Ends a routine, as Unity's StopCoroutine does. The AS-IS engine stops its own `while (true)`
    /// animations this way, so without it the scheduler never goes idle.</summary>
    private void Kill(IEnumerator routine)
    {
        // Unity stops a routine by identity: the routine ITSELF ends, and whatever started it carries on.
        // Here a routine can be a FRAME on somebody else's stack — `GameStateMachine` was sitting eight frames
        // deep in `MainPhase > ... > SecurityCheck > Battle > Destroy > ShowCardEffect` when the card-display
        // panel deactivated and stopped its own coroutine. Removing the whole stack took the game with it.
        //
        // So: if the target is a frame on a stack, unwind only that frame and let the parent resume — which is
        // exactly what Unity does when a nested coroutine is stopped. Only when the target IS the routine (its
        // whole stack) does the routine end.
        foreach (Routine scheduled in _routines)
        {
            int depth = scheduled.Stack.Count;

            if (depth <= 1 || !scheduled.Stack.Contains(routine))
            {
                continue;
            }

            List<IEnumerator> frames = scheduled.Stack.ToList();   // top-first
            int index = frames.FindIndex(frame => ReferenceEquals(frame, routine));

            if (index <= 0)
            {
                continue;   // index 0 handled below; the root case is handled after this loop
            }

            scheduled.Stack.Clear();

            for (int i = frames.Count - 1; i > index; i--)
            {
                scheduled.Stack.Push(frames[i]);
            }

            for (int i = 0; i <= index; i++)
            {
                ReleaseHandleFor(frames[i]);    // 버려진 프레임들의 핸들 반납
            }

            scheduled.Waiting = null;
            Removals.Add($"프레임정지:{routine.GetType().Name} (부모 {scheduled.Root} 유지)");
        }

        // A handle that a parent adopted with `yield return StartCoroutine(child)` is NOT an independent
        // routine any more — it is a frame on the parent's stack. Unity's "stop the coroutines this object
        // started" must not reach it: doing so deletes the CALLER. That is what happened here — closing the
        // mulligan panel deactivated it, and the game state machine disappeared with it, because
        // `TurnStateMachine.GameStateMachine` was sitting on `yield return StartCoroutine(StartGame())` and
        // `StartGame` was the top of its stack.
        if (!_adopted.Any(handle => ReferenceEquals(handle.Routine, routine)))
        {
            return;
        }

        _pending.Remove(routine);

        foreach (Routine scheduled in _routines)
        {
            if (scheduled.Stack.Count == 0 || !ReferenceEquals(scheduled.Stack.Last(), routine))
            {
                continue;
            }

            Removals.Add($"정지:{scheduled.Root} @ {string.Join(" < ", scheduled.Stack.Select(f => f.GetType().Name))}");

            foreach (IEnumerator frame in scheduled.Stack)
            {
                ReleaseHandleFor(frame);
            }
        }

        Killed += _routines.RemoveAll(
            scheduled => scheduled.Stack.Count > 0 && ReferenceEquals(scheduled.Stack.Last(), routine));
    }

    /// <summary>Routines ended by StopCoroutine or by their object deactivating.</summary>
    public int Killed { get; private set; }

    /// <summary>Every routine that left the schedule, and why.</summary>
    public List<string> Removals { get; } = new();

    /// <summary>What each killed routine was running, innermost first.</summary>
    public List<string> KillLog { get; } = new();

    /// <summary>Drops a handle from the independent set because a parent is waiting on it directly. Called
    /// when the scheduler sees `yield return &lt;Coroutine&gt;`: the child belongs to that parent's stack, and
    /// running it independently as well would advance it twice.</summary>
    private bool Reclaim(Coroutine handle)
    {
        if (!_adopted.Remove(handle))
        {
            return false;
        }

        _pending.Remove(handle.Routine);
        _routines.RemoveAll(routine => routine.Stack.Count == 1 && ReferenceEquals(routine.Stack.Peek(), handle.Routine));

        return true;
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    /// <summary>Routines that have not finished.</summary>
    public int ActiveRoutines => _routines.Count;

    /// <summary>How many routines are suspended on a predicate wait right now.
    ///
    /// This — not "did anything advance" — is how the substrate tells that the engine is blocked. The AS-IS UI
    /// keeps IDLE ANIMATION LOOPS running for the life of the match (`LoadingObject.SetLoadingText` is a
    /// `while (true)` started on `ContinuousController`, so it outlives the loading screen that spawned it and
    /// Unity never stops it either). Something is therefore ALWAYS advancing, and a no-progress test can never
    /// fire.</summary>
    public int SuspendedRoutines => _routines.Count(routine => routine.Waiting is not null);

    /// <summary>The predicate waits routines are currently parked on.
    ///
    /// Exposed because the substrate has no other way to tell WHICH question the engine is asking: the AS-IS
    /// selectors all park on `WaitUntil(() => somePlayer.HasPlayerSelection())`, and the only thing that
    /// distinguishes them is the closure the lambda came from. Its declaring type names the selector
    /// (`SelectPermanentEffect+&lt;&gt;c__DisplayClass…`) and its captured fields hold the player being asked.</summary>
    public IEnumerable<CustomYieldInstruction> PendingWaits =>
        _routines.Select(routine => routine.Waiting).Where(wait => wait is not null)!;

    /// <summary>What each live routine is doing right now: the innermost frame it is executing, and the
    /// predicate it is suspended on if any. For diagnosing a scheduler that ticks without progressing.</summary>
    public IEnumerable<string> Describe()
    {
        foreach (Routine routine in _routines)
        {
            string frame = routine.Stack.Count > 0 ? routine.Stack.Peek().GetType().ToString() : "(empty)";
            string waiting = routine.Waiting is null ? "실행중" : $"대기 {routine.Waiting.GetType().Name}";

            yield return $"{waiting}  {frame}  깊이{routine.Stack.Count}";
        }
    }

    /// <summary>Ticks elapsed since construction. One tick is one Unity frame.</summary>
    public int Ticks { get; private set; }

    /// <summary>Starts a routine. It begins advancing on the next <see cref="Tick"/>, alongside everything
    /// already running — the AS-IS `StartCoroutine(x);` shape.</summary>
    public void Start(IEnumerator routine)
    {
        ArgumentNullException.ThrowIfNull(routine);

        _pending.Add(routine);
    }

    /// <summary>Advances every active routine by one step, as Unity's player loop does once per frame.</summary>
    /// <returns>True when at least one routine advanced.</returns>
    public bool Tick()
    {
        Ticks++;

        // A tween scheduled by Play() completes here — one tick later, as Unity applies it on the next frame.
        // See Headless/Unity/ThirdPartyShims.cs for the AS-IS loop that reads the value in between.
        DG.Tweening.Tween.AdvanceScheduled();

        AdmitPending();

        bool progressed = false;

        // Snapshot: a routine may start others, and those wait for the next tick exactly as in Unity.
        foreach (Routine routine in _routines.ToArray())
        {
            if (StepOnce(routine))
            {
                progressed = true;
            }
        }

        foreach (Routine finished in _routines.Where(static r => r.Stack.Count == 0))
        {
            Removals.Add($"완료:{finished.Root}");
        }

        _routines.RemoveAll(static routine => routine.Stack.Count == 0);

        // A routine started during this tick counts as progress: the engine did move forward.
        if (_pending.Count > 0)
        {
            progressed = true;
        }

        return progressed;
    }

    /// <summary>Ticks until every routine finishes, nothing can advance, or the budget runs out.</summary>
    public DriverResult RunToCompletion(int maxTicks = 100_000)
    {
        while (_routines.Count > 0 || _pending.Count > 0)
        {
            if (Ticks >= maxTicks)
            {
                return new DriverResult(DriverStopReason.BudgetExhausted, Ticks, _routines.Count);
            }

            if (!Tick())
            {
                return new DriverResult(DriverStopReason.Stalled, Ticks, _routines.Count);
            }
        }

        return new DriverResult(DriverStopReason.Idle, Ticks, _routines.Count);
    }

    private void AdmitPending()
    {
        foreach (IEnumerator routine in _pending)
        {
            Routine scheduled = new() { Root = routine.GetType().Name };
            scheduled.Stack.Push(routine);
            _routines.Add(scheduled);
        }

        _pending.Clear();
    }

    /// <summary>Advances one routine by a single step, mirroring Unity's per-frame handling of each yielded
    /// value. A nested coroutine is pushed and consumes this routine's step; the routine resumes past it only
    /// once the child is done.</summary>
    private bool StepOnce(Routine routine)
    {
        // Still suspended on a predicate? Re-evaluate it, as Unity re-polls every frame. The routine does NOT
        // advance until it is satisfied — see Routine.Waiting.
        if (routine.Waiting is { } pending)
        {
            if (pending.keepWaiting)
            {
                return false;
            }

            routine.Waiting = null;
        }

        while (routine.Stack.Count > 0)
        {
            IEnumerator top = routine.Stack.Peek();

            bool advanced = top.MoveNext();

            // A ROUTINE CAN STOP ITSELF WHILE IT IS EXECUTING. `TurnStateMachine.EndGame` calls
            // `StopAllCoroutines()` on three objects (TurnStateMachine.cs:3349-3351) from inside the attack
            // coroutine that is running right now, and Kill empties this very stack under us. Unity's routine
            // simply ends there; the frames must not be touched afterwards. Without this check the next line
            // popped an emptied stack and threw `InvalidOperationException: Stack empty`.
            if (routine.Stack.Count == 0 || !ReferenceEquals(routine.Stack.Peek(), top))
            {
                return advanced;
            }

            if (!advanced)
            {
                routine.Stack.Pop();
                ReleaseHandleFor(top);    // 완료 — Unity가 핸들을 풀어주는 시점

                continue;   // the parent gets this tick's step, as Unity resumes it the same frame
            }

            switch (top.Current)
            {
                // A predicate wait. Checked before IEnumerator: CustomYieldInstruction implements it, and
                // treating one as a nested routine would poll it forever inside a single step.
                case CustomYieldInstruction wait:
                    if (wait.keepWaiting)
                    {
                        routine.Waiting = wait;
                    }

                    return !wait.keepWaiting;

                case Coroutine handle:
                    // The parent is waiting on this child, so it is not independent after all.
                    Reclaim(handle);
                    routine.Stack.Push(handle.Routine);

                    continue;

                case IEnumerator nested:
                    routine.Stack.Push(nested);

                    continue;

                // `yield return null` and every YieldInstruction (WaitForSeconds included): the routine is done
                // for this frame. There is no clock, so the wait is one tick rather than t seconds.
                default:
                    return true;
            }
        }

        return true;
    }
}
