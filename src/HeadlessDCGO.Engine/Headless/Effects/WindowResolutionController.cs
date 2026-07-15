namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (A-2 RD-6) Holds the per-match [End of Your Turn] window drain-once marker. Formerly this also parked the old
/// registry-currency trigger window's suspended continuation (<c>WindowContinuation</c> + a recorded-answer replay
/// map), but that window loop was retired at the SkillInfo cutover (C2) and physically deleted (C2b) — the mirror
/// MultipleSkills window engine parks on the choice controller instead. Only the end-of-turn drain marker survives
/// as live state.
/// </summary>
public sealed class WindowResolutionController : IHeadlessMatchStateResettable
{
    /// <summary>Post-cutover there is no old-loop suspended window to park (the new engine parks on the choice
    /// controller), so this is always false. Retained as a live call site in the end-of-turn drain loop
    /// (MetadataActionProcessor) — the choice-controller pending state is checked alongside it.</summary>
    public bool HasPending => false;

    /// <summary>(A-2 RD-6, task 6) The turn number whose pre-flip [End of Your Turn] window EndTurnAsync has already
    /// EMITTED (and begun draining). When the drain suspends for an interactive body or a multi-trigger order choice,
    /// EndTurnAsync returns without flipping; the agent resolves the choice and re-applies EndTurn — this marker makes
    /// that re-application skip re-emitting OnEndTurn (a double-fire) and proceed straight to the threshold re-check +
    /// flip. EndTurnAsync sets it before the emit and clears it at the turn boundary (flip) or when the turn continues
    /// (so the next end-turn attempt re-fires the window, AS-IS).</summary>
    public int? EndOfTurnDrainedTurn { get; set; }

    public void ResetMatchState()
    {
        EndOfTurnDrainedTurn = null;
    }
}
