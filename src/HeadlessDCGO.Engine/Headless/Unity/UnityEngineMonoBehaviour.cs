// ============================================================================================================
// THE AS-IS BASE TYPE `UnityEngine.MonoBehaviour`.
//
// WHY THIS FILE EXISTS. The original game logic (DCGO/Assets/Scripts) declares its classes as
// `public class CardSource : MonoBehaviour`, `public class ContinuousController : MonoBehaviour`, and so on.
// `MonoBehaviour` lives in a compiled Unity assembly, so there is no AS-IS *file* to mirror — the AS-IS HOME is
// the namespace `UnityEngine`, exactly as for the yield types in
// Headless/Coroutines/UnityEngineYieldInstructions.cs. Declaring it here lets a mirror file carry the
// original's class declaration VERBATIM.
//
// UNITY IS NOT RUNNING. Nothing in this process creates a GameObject, attaches a component, or ticks a frame
// loop. Read the member list below literally.
//
// MEMBERS THAT DO REAL WORK
//     StartCoroutine(IEnumerator)   Returns a `UnityEngine.Coroutine` handle wrapping the routine. This is the
//                                   handle type Headless/Coroutines/CoroutineDriver.cs already understands:
//                                   when a driven coroutine does `yield return StartCoroutine(x)`, the driver
//                                   matches `case Coroutine handle` and pushes `handle.Routine`, running the
//                                   child to completion before the parent advances — the AS-IS net effect of
//                                   the 1628 bare `yield return StartCoroutine(x)` sites.
//                                   NOTE the handle is COLD, like every other routine before the driver takes
//                                   it: constructing it starts nothing. That is correct for the
//                                   `yield return StartCoroutine(x)` shape (the driver starts it) and WRONG for
//                                   the AS-IS fire-and-forget shape `StartCoroutine(x);` with the handle
//                                   discarded — there the routine would simply never run. The fire-and-forget
//                                   shape has its own entry point, `CoroutineDriver.RunDetached(IEnumerator)`,
//                                   which runs inline and throws if the routine tries to park. No mirror file
//                                   calls either shape today.
//
// CANCELLATION IS REAL (2026-07-29, roadmap step 2.3 — this header previously said it was not)
//     StopCoroutine(Coroutine/IEnumerator)  Raises `Stopped`; the driver unwinds that routine. The AS-IS engine
//                                           ends its own `while (true)` animations this way.
//     StopAllCoroutines()                   Ends every routine started on THIS component. `TurnStateMachine.
//                                           EndGame` uses it to halt the match; see the member's own docs for
//                                           the loop that never terminates without it.
//     StopCoroutine(string)                 Still inert — there is no name-to-routine registry here.
//
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING
//     Invoke / CancelInvoke               A delayed message needs a frame clock. There is none.
//
// INHERITED, AND REAL (2026-07-29). MonoBehaviour now sits on the AS-IS hierarchy
// `Object -> Component -> Behaviour -> MonoBehaviour`, declared in Headless/Unity/UnityEngineObjectModel.cs.
// That base supplies `gameObject`, `transform`, `enabled` and `GetComponent<T>()`, and those DO real work —
// the AS-IS engine instantiates itself through them (`GManager.cs:269` creates the TurnStateMachine with
// `gameObject.AddComponent<TurnStateMachine>()`; `CEntity_EffectController.cs:221` creates every card's effect
// class with `gameObject.AddComponent(Type.GetType($"DCGO.CardEffects.{ID}.{ClassName}"))`). Read that file's
// header for what is real there and what is only held.
//
// NOT DECLARED, ON PURPOSE. The message hooks Unity calls by reflection
// (`Awake`/`Start`/`Update`/`OnDestroy`/…). Each would be a lie about a subsystem that is not present. In particular AddComponent does NOT invoke Awake — how the
// lifecycle is driven headless is roadmap step 2.1, and burying that decision in a shim would hide it.
// `UnityEngine.Object`'s `==`/`!=` overloads (Unity makes a destroyed object compare equal to null) are
// likewise absent, so equality stays plain reference identity. Add a member here only when an AS-IS file cannot
// compile without it, and say in this header what it does.
// ============================================================================================================

namespace UnityEngine;

using System.Collections;

/// <summary>Unity <c>UnityEngine.MonoBehaviour</c> — the base class of the original's scene components. Sits on
/// <see cref="Behaviour"/> so the AS-IS sources reach `gameObject`/`transform`/`GetComponent`. See the file
/// header for the full member inventory.</summary>
public class MonoBehaviour : Behaviour
{
    /// <summary>Unity <c>MonoBehaviour.StartCoroutine(IEnumerator)</c>. Unity hands the routine to the player
    /// loop and returns an opaque handle; headless there is no player loop, so this returns the
    /// <see cref="Coroutine"/> handle CARRYING the routine and
    /// <see cref="Headless.Coroutines.CoroutineDriver"/> runs it when the handle is yielded. The routine is
    /// cold until then — see the file header for the fire-and-forget caveat.</summary>
    public Coroutine StartCoroutine(IEnumerator routine)
    {
        ArgumentNullException.ThrowIfNull(routine);

        Coroutine handle = new(routine);
        _running.Add(handle);
        Started?.Invoke(handle);

        return handle;
    }

    private readonly List<Coroutine> _running = new();

    /// <summary>[계측 2026-07-29] 판당 메모리 조사용. 이 컴포넌트가 지금까지 시작한 코루틴 핸들 수.
    /// 조사 종료 후 제거 대상.</summary>

    /// <summary>Unity STOPS every coroutine a component started when its GameObject is deactivated, and the
    /// AS-IS code relies on that: `LoadingObject` starts `SetLoadingText`, a `while (true)` loop, and never
    /// keeps its handle — only `moveAgumonCoroutine` is stored and stopped explicitly (LoadingObject.cs:44,96).
    /// The text loop ends in the original solely because `Off()` deactivates the object. Without this rule the
    /// loop runs forever and the scheduler never reports the engine as waiting.</summary>
    internal bool HasRunningCoroutines => _running.Count > 0;

    internal void StopRunningCoroutines()
    {
        foreach (Coroutine handle in _running.ToArray())
        {
            Stopped?.Invoke(handle.Routine);
        }

        _running.Clear();
    }

    /// <summary>Raised for every StartCoroutine. The scheduler subscribes and admits the routine as an
    /// independent one, which is what makes the FIRE-AND-FORGET shape `StartCoroutine(x);` work — 153 AS-IS
    /// sites discard the handle, so nothing would ever advance those routines otherwise.
    ///
    /// The `yield return StartCoroutine(x)` shape (15,833 sites) hands the SAME handle to the parent, and the
    /// scheduler recognises a handle it already owns: the parent still waits for the child, and the child is
    /// not advanced twice. See Headless/Coroutines/CoroutineDriver.cs.</summary>
    public static event Action<Coroutine>? Started;

    /// <summary>Unity <c>MonoBehaviour.StartCoroutine(string)</c>. Unity looks the method up BY NAME on this
    /// component and starts it; if no such method exists it logs and does nothing. Reproduced faithfully,
    /// including the doing-nothing case — `BT17_026.cs:392` calls this with the name of a LOCAL FUNCTION, which
    /// Unity cannot see either, so that branch is inert in the original too. AS-IS behaviour is reproduced, not
    /// corrected (roadmap R7).</summary>
    public Coroutine? StartCoroutine(string methodName)
    {
        System.Reflection.MethodInfo? method = GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

        if (method?.Invoke(this, null) is IEnumerator routine)
        {
            return new Coroutine(routine);
        }

        return null;
    }

    public Coroutine? StartCoroutine(string methodName, object value) => StartCoroutine(methodName);

    /// <summary>Unity <c>StopCoroutine</c>. Kills a running routine. REAL WORK — the AS-IS engine relies on
    /// it to end its own infinite loops: `LoadingObject.EndLoading` stops the walking-Agumon animation
    /// (LoadingObject.cs:96), which is a `while (true)`. A no-op here leaves that routine spinning forever and
    /// the scheduler never goes idle.</summary>
    public void StopCoroutine(Coroutine? routine)
    {
        if (routine is not null)
        {
            Stopped?.Invoke(routine.Routine);
        }
    }

    public void StopCoroutine(IEnumerator? routine)
    {
        if (routine is not null)
        {
            Stopped?.Invoke(routine);
        }
    }

    /// <summary>The string form resolves the method by name, as Unity does, and stops nothing if it finds no
    /// running routine — there is no name-to-routine registry here.</summary>
    public void StopCoroutine(string methodName)
    {
    }

    /// <summary>Unity <c>StopAllCoroutines</c> — ends every coroutine THIS component started. REAL WORK, and
    /// load-bearing: it is what ends the match.
    ///
    /// `TurnStateMachine.EndGame` calls it on the state machine, GManager and ContinuousController
    /// (TurnStateMachine.cs:3349-3351) after setting `endGame = true` (:3325). The AS-IS attack loop
    /// `while (attackProcess.ActiveAttack())` (TurnStateMachine.cs:938) has NO `endGame` guard — unlike the
    /// loops enclosing it at :936 and :972 — precisely because this call is what stops it. And it CANNOT
    /// terminate on its own: `AttackProcess.DetermineAttackOutcome` reaches game end at AttackProcess.cs:425
    /// and `yield break`s WITHOUT advancing `State` from `Battle`, so `ActiveAttack()` (:37) stays true
    /// forever.
    ///
    /// Left as a no-op the match therefore never ends: :938 re-enters `DetermineAttackOutcome`, which calls
    /// `EndGame` again, which starts another `BattleBGM.FadeOut` coroutine (:3353) — the schedule grows without
    /// bound and every tick gets slower. Measured 2026-07-29: RSS to 12.3GB, no match ever completing.
    ///
    /// Same mechanism as <see cref="StopRunningCoroutines"/>, which GameObject deactivation already uses; Unity
    /// scopes both to the coroutines this component started.</summary>
    public void StopAllCoroutines() => StopRunningCoroutines();

    /// <summary>Raised by <see cref="StopCoroutine(Coroutine)"/>. The scheduler subscribes and drops the
    /// routine.</summary>
    public static event Action<IEnumerator>? Stopped;

    /// <summary>Unity <c>Invoke</c> — a delayed message. There is no frame clock; nothing schedules.</summary>
    public void Invoke(string methodName, float time)
    {
    }

    public void CancelInvoke()
    {
    }
}
