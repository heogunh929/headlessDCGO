namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record HeadlessTurnState(
    int TurnNumber,
    HeadlessPlayerId? TurnPlayerId,
    HeadlessPlayerId? NonTurnPlayerId,
    HeadlessPhase Phase,
    bool IsFirstTurn,
    IReadOnlyList<HeadlessPlayerId> PlayerOrder)
{
    public static HeadlessTurnState Empty { get; } = new(
        TurnNumber: 0,
        TurnPlayerId: null,
        NonTurnPlayerId: null,
        Phase: HeadlessPhase.None,
        IsFirstTurn: false,
        PlayerOrder: Array.Empty<HeadlessPlayerId>());

    public string AsIsPhaseName => HeadlessPhaseMapping.ToAsIsName(Phase);

    public bool IsSetupPhase => Phase == HeadlessPhase.Setup;

    public bool IsMainPhase => Phase == HeadlessPhase.Main;

    public bool IsMemoryPassPhase => Phase == HeadlessPhase.MemoryPass;

    public bool IsEndPhase => Phase == HeadlessPhase.End;
}
