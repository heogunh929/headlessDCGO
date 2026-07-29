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
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING
//     StopCoroutine / StopAllCoroutines   The driver owns the routine stack; there is no registry of running
//                                         routines to cancel. Whether cancellation is reproduced at all is
//                                         roadmap step 2.3.
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

        return new Coroutine(routine);
    }

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

    /// <summary>Unity <c>StopCoroutine</c>/<c>StopAllCoroutines</c>. DECLARATIONS DOING NOTHING — the driver
    /// owns the routine stack and there is no registry of running routines to cancel. Roadmap step 2.3 decides
    /// whether cancellation is reproduced at all.</summary>
    public void StopCoroutine(Coroutine? routine)
    {
    }

    public void StopCoroutine(IEnumerator? routine)
    {
    }

    public void StopCoroutine(string methodName)
    {
    }

    public void StopAllCoroutines()
    {
    }

    /// <summary>Unity <c>Invoke</c> — a delayed message. There is no frame clock; nothing schedules.</summary>
    public void Invoke(string methodName, float time)
    {
    }

    public void CancelInvoke()
    {
    }
}
