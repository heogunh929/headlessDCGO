namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (Stage 5, Phase 3) Holds the ONE window whose resolution suspended mid-flight waiting for an agent choice — the
/// direct analogue of <see cref="Runtime.DeferredActivationController"/>, but for a whole trigger window (a cut-in
/// frame stack) rather than a single activation. The C# call stack does not survive the choice pause, so the
/// window's live state lives in a <see cref="WindowContinuation"/> here; the next <c>ResolveChoice</c> hands it back
/// to <see cref="WindowResolver.DriveAsync"/> to resume (replaying the in-flight body or re-offering the pending
/// choice, whichever suspended). Cleared once the window runs to exhaustion.
///
/// There is at most one suspended window at a time: a window suspends only by opening a choice, the main loop then
/// pauses on that pending choice, and no new window opens until it is resolved. A nested cut-in that itself suspends
/// is already inside the SAME continuation's frame stack, so it does not need a second slot.
/// </summary>
public sealed class WindowResolutionController : IHeadlessMatchStateResettable
{
    /// <summary>The suspended window's continuation, or null when no window is parked.</summary>
    public WindowContinuation? Pending { get; private set; }

    public bool HasPending => Pending is not null;

    /// <summary>Park a suspended window's continuation to be resumed on the next ResolveChoice.</summary>
    public void Suspend(WindowContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        Pending = continuation;
    }

    public void Clear() => Pending = null;

    public void ResetMatchState() => Pending = null;
}
