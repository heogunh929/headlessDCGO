namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>(R4 S2, decision 2 = option A) The substrate step-position sub-cursor within a <see cref="HeadlessPhase"/>
/// — the turn-flow analogue of the window engine's resume cursor. This is NOT a game phase (game phases are the
/// AS-IS 6-value <see cref="HeadlessPhase"/>); it records WHERE inside a phase the turn driver has advanced, so the
/// former invented 9-value phase model (Setup / Unsuspend / MemoryPass) is expressed as (phase, cursor) pairs with
/// no loss of state-distinction. Deliberately named in substrate vocabulary, not game-phase vocabulary.
///
/// Old 9-value HeadlessPhase → new (HeadlessPhase, TurnStepCursor):
///   None       → (None,     PhaseStart)
///   Setup      → (None,     Starting)              — pre-game setup sequence in progress
///   Active     → (Active,   PhaseStart)
///   Unsuspend  → (Active,   Unsuspending)          — unsuspend sub-step of the active phase
///   Draw       → (Draw,     PhaseStart)
///   Breeding   → (Breeding, PhaseStart)
///   Main       → (Main,     PhaseStart)
///   MemoryPass → (Main,     AwaitingMemoryPassEnd) — memory-pass window at end of main
///   End        → (End,      PhaseStart)</summary>
public enum TurnStepCursor
{
    /// <summary>The default entry position of any phase.</summary>
    PhaseStart = 0,

    /// <summary>The pre-game setup sequence is in progress (former HeadlessPhase.Setup; phase == None).</summary>
    Starting = 1,

    /// <summary>The unsuspend sub-step of the active phase (former HeadlessPhase.Unsuspend; phase == Active).</summary>
    Unsuspending = 2,

    /// <summary>The memory-pass window at the end of the main phase (former HeadlessPhase.MemoryPass; phase == Main).</summary>
    AwaitingMemoryPassEnd = 3
}
