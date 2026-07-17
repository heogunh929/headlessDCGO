namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>(R4 S2, decision 2 = option A) The turn phase — the AS-IS 6-value game model, value-and-order
/// faithful to <c>GameContext.phase</c> (Active/Draw/Breeding/Main/End/None; the mirror keeps None at 0 for
/// the default-empty convention). The step POSITION within a phase (the former invented Setup/Unsuspend/
/// MemoryPass splits) now lives in the substrate sub-cursor <see cref="TurnStepCursor"/> on
/// <see cref="HeadlessTurnState"/>, so representation-collapsing 9→6 loses no state distinction. Keeping this
/// enum 1:1 with the upstream game model means a phase-related upstream change translates once (the machine-diff
/// premise), not as a permanent per-change translation tax.</summary>
public enum HeadlessPhase
{
    None = 0,
    Active = 1,
    Draw = 2,
    Breeding = 3,
    Main = 4,
    End = 5
}
