namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Choices;
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

    /// <summary>(A-2 RD-6, task 6) The turn number whose pre-flip [End of Your Turn] window EndTurnAsync has already
    /// EMITTED (and begun draining). When the drain suspends for an interactive body or a multi-trigger order choice,
    /// EndTurnAsync returns without flipping; the agent resolves the choice (which resumes THIS window via the normal
    /// WindowChoice path) and re-applies EndTurn — this marker makes that re-application skip re-emitting OnEndTurn
    /// (a double-fire) and proceed straight to the threshold re-check + flip. Deliberately INDEPENDENT of
    /// <see cref="Pending"/>/<see cref="Clear"/> (the window's own lifecycle clears Pending when it exhausts, but the
    /// re-applied EndTurn still must not re-emit): EndTurnAsync sets it before the emit and clears it at the turn
    /// boundary (flip) or when the turn continues (so the next end-turn attempt re-fires the window, AS-IS).</summary>
    public int? EndOfTurnDrainedTurn { get; set; }

    // The agent's recorded answers for this window's choice points, keyed by the choice's stable identity (the
    // ordered effect-ids of the offered side / the confirmed effect-id). Because a CHOICE suspend re-runs the whole
    // pass on resume, the port re-asks every earlier choice; these recorded answers let it REPLAY them idempotently
    // (adversarial finding P1-a — the replay must be key-based, not positional, so a resolved pick whose choice
    // point vanishes simply never recurs). Accumulates across a window's suspends, cleared with the window.
    private readonly Dictionary<string, ChoiceResult> _answers = new(StringComparer.Ordinal);

    /// <summary>Park a suspended window's continuation to be resumed on the next ResolveChoice.</summary>
    public void Suspend(WindowContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        Pending = continuation;
    }

    /// <summary>Record the agent's answer for a window choice, keyed by its stable identity, so the port replays it
    /// when the resumed pass re-offers that same choice.</summary>
    public void RecordAnswer(string key, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(result);
        _answers[key] = result;
    }

    /// <summary>Look up a previously recorded answer for a choice key. True when the agent has already answered it.</summary>
    public bool TryGetAnswer(string key, out ChoiceResult? result) => _answers.TryGetValue(key, out result);

    public void Clear()
    {
        Pending = null;
        _answers.Clear();
    }

    public void ResetMatchState()
    {
        Clear();
        EndOfTurnDrainedTurn = null;
    }
}
