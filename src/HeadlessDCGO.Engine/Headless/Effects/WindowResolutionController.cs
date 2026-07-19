namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (4b B6) Retired shell. Formerly this parked the old registry-currency trigger window's suspended
/// continuation (retired at the SkillInfo cutover, C2/C2b — the mirror MultipleSkills window engine parks on
/// the choice controller) and, later, only the OLD step driver's per-turn [End of Your Turn] drain-once
/// marker (<c>EndOfTurnDrainedTurn</c>). That marker was an invented once-per-turn cap; the AS-IS semantic is
/// per-effect caps inside the window members, which the pump's EndTurnProcess drain carries — so the marker
/// was deleted with the OLD EndTurn body. The type survives only as EngineContext plumbing until the
/// registered-service seam is compacted.
/// </summary>
public sealed class WindowResolutionController : IHeadlessMatchStateResettable
{
    /// <summary>Post-cutover there is no old-loop suspended window to park (the new engine parks on the
    /// choice controller), so this is always false.</summary>
    public bool HasPending => false;

    public void ResetMatchState()
    {
    }
}
