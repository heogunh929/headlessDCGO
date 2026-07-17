namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record HeadlessTurnState(
    int TurnNumber,
    HeadlessPlayerId? TurnPlayerId,
    HeadlessPlayerId? NonTurnPlayerId,
    HeadlessPhase Phase,
    TurnStepCursor StepCursor,
    bool IsFirstTurn,
    IReadOnlyList<HeadlessPlayerId> PlayerOrder)
{
    public static HeadlessTurnState Empty { get; } = new(
        TurnNumber: 0,
        TurnPlayerId: null,
        NonTurnPlayerId: null,
        Phase: HeadlessPhase.None,
        StepCursor: TurnStepCursor.PhaseStart,
        IsFirstTurn: false,
        PlayerOrder: Array.Empty<HeadlessPlayerId>());

    public string AsIsPhaseName => HeadlessPhaseMapping.ToAsIsName(Phase);

    /// <summary>(R4 S2) The pre-game setup sequence — former HeadlessPhase.Setup, now (None, Starting).</summary>
    public bool IsSetupPhase => Phase == HeadlessPhase.None && StepCursor == TurnStepCursor.Starting;

    /// <summary>(R4 S2) The active phase's natural-unsuspend sub-step — former HeadlessPhase.Unsuspend, now
    /// (Active, Unsuspending).</summary>
    public bool IsUnsuspendPhase => Phase == HeadlessPhase.Active && StepCursor == TurnStepCursor.Unsuspending;

    // (R4 P2b, review-2 P2-②) The former `IsMainPhase` (any Main-phase step, memory-pass INCLUDED) is removed:
    // it had 0 consumers and was a standing trap — every real gate wants the interactive main-play step only
    // (IsMainPlayPhase) or the memory-pass step only (IsMemoryPassPhase); a raw Phase==Main read that genuinely
    // wants both spells it out.

    /// <summary>(R4 S2) The interactive main-play step — former HeadlessPhase.Main, now (Main, PhaseStart);
    /// distinct from the memory-pass end-of-main step.</summary>
    public bool IsMainPlayPhase => Phase == HeadlessPhase.Main && StepCursor == TurnStepCursor.PhaseStart;

    /// <summary>(R4 S2) The memory-pass window at end of main — former HeadlessPhase.MemoryPass, now
    /// (Main, AwaitingMemoryPassEnd).</summary>
    public bool IsMemoryPassPhase => Phase == HeadlessPhase.Main && StepCursor == TurnStepCursor.AwaitingMemoryPassEnd;

    public bool IsEndPhase => Phase == HeadlessPhase.End;
}
