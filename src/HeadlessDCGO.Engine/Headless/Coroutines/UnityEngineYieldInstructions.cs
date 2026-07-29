// ============================================================================================================
// (coroutine restore, step 1 of the IEnumerator re-migration) THE AS-IS-NAMED YIELD TYPES.
//
// The original game logic (DCGO/Assets/Scripts) is written against Unity's coroutine yield vocabulary, which
// lives in namespace `UnityEngine` (a compiled assembly, not source — there is no AS-IS *file* to mirror, so
// the AS-IS HOME is the namespace). Every original file already opens with `using UnityEngine;`, so declaring
// these types under that exact namespace is what lets a mirror file carry the original statement VERBATIM:
//
//     yield return new WaitUntil(() => player.HasPlayerSelection());
//     yield return new WaitWhile(() => !end);
//     yield return new WaitForSeconds(0.5f);
//     yield return ContinuousController.instance.StartCoroutine(x);   // returns Coroutine
//
// These are declarations only — they carry NO behaviour. All progress semantics belong to
// HeadlessDCGO.Engine.Headless.Coroutines.CoroutineDriver, which is the headless replacement for Unity's
// frame loop. See that file for how each shape makes progress.
//
// Shapes measured in DCGO/Assets/Scripts (whole tree, `grep --binary-files=text`):
//     yield return ContinuousController.instance.StartCoroutine(x)   14205   -> Coroutine
//     yield return StartCoroutine(x)      (bare, MonoBehaviour self)  1628   -> Coroutine
//     yield return null;                                              3130   -> (frame yield)
//     yield return new WaitForSeconds(t)                               161   -> WaitForSeconds
//     yield return new WaitWhile(cond)                                 103   -> WaitWhile
//     yield return new WaitUntil(cond)                                  21   -> WaitUntil
// ============================================================================================================

namespace UnityEngine;

using System.Collections;

/// <summary>Unity <c>UnityEngine.YieldInstruction</c> — the base class of every non-enumerator value the
/// original yields. Opaque in Unity too (no public members); it exists so the driver can recognise the
/// family.</summary>
public abstract class YieldInstruction
{
}

/// <summary>Unity <c>UnityEngine.Coroutine</c> — the handle <c>StartCoroutine(IEnumerator)</c> returns and
/// <c>yield return</c> waits on. In Unity the handle is opaque (internal constructor, no public members) and
/// the routine is owned by the player loop; headless there is no player loop, so the handle CARRIES the
/// routine and the <see cref="Headless.Coroutines.CoroutineDriver"/> that consumes it runs it to completion
/// before the parent advances — the exact net effect of the AS-IS
/// <c>yield return ContinuousController.instance.StartCoroutine(x)</c>.</summary>
public sealed class Coroutine : YieldInstruction
{
    internal Coroutine(IEnumerator routine)
    {
        Routine = routine ?? throw new ArgumentNullException(nameof(routine));
    }

    /// <summary>The routine this handle stands for. Consumed by the driver; not part of Unity's surface.</summary>
    internal IEnumerator Routine { get; }

    /// <summary>The component that started this coroutine — so the driver can hand the handle back
    /// (`_running` 해제) when the routine finishes. Unity frees handles on completion; keeping them for the
    /// component's whole lifetime was the in-match accumulator behind the 22:36-22:40 OOMs.</summary>
    internal MonoBehaviour? Owner { get; set; }
}

/// <summary>Unity <c>UnityEngine.WaitForSeconds</c> — suspends for <paramref name="seconds"/> of scaled game
/// time. Mirrored with the AS-IS constructor so the original statement compiles verbatim.</summary>
public sealed class WaitForSeconds : YieldInstruction
{
    public WaitForSeconds(float seconds)
    {
        Seconds = seconds;
    }

    /// <summary>Unity keeps this in a private <c>m_Seconds</c>; exposed here only so a diagnostic can read
    /// what the original asked for.</summary>
    internal float Seconds { get; }
}

/// <summary>Unity <c>UnityEngine.CustomYieldInstruction</c> — the base of the predicate waits. Unity's
/// implements <see cref="IEnumerator"/> (that is how the player loop polls it) and so does this one, for
/// fidelity; the driver nevertheless matches this type BEFORE <see cref="IEnumerator"/> so a predicate wait is
/// never mistaken for a nested coroutine.</summary>
public abstract class CustomYieldInstruction : IEnumerator
{
    /// <summary>Unity's polling contract: keep suspending while this is true.</summary>
    public abstract bool keepWaiting { get; }

    public object? Current => null;

    public bool MoveNext() => keepWaiting;

    public void Reset()
    {
    }
}

/// <summary>Unity <c>UnityEngine.WaitUntil</c> — suspend until the predicate turns true.</summary>
public sealed class WaitUntil : CustomYieldInstruction
{
    private readonly Func<bool> _predicate;

    /// <summary>The predicate itself. The headless substrate reads it to identify WHICH question the engine is
    /// waiting on — see Headless/Choices/VirtualPlayer.cs. Unity keeps it private; nothing in the AS-IS
    /// sources touches this.</summary>
    public Func<bool> Predicate => _predicate;

    public WaitUntil(Func<bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override bool keepWaiting => !_predicate();
}

/// <summary>Unity <c>UnityEngine.WaitWhile</c> — suspend while the predicate stays true.</summary>
public sealed class WaitWhile : CustomYieldInstruction
{
    private readonly Func<bool> _predicate;

    /// <summary>The predicate itself; see <see cref="WaitUntil.Predicate"/>.</summary>
    public Func<bool> Predicate => _predicate;

    public WaitWhile(Func<bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override bool keepWaiting => _predicate();
}
