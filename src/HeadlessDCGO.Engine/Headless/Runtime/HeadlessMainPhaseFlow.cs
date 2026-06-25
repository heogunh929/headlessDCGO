namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessMainPhaseFlow
{
    public const int DefaultMemoryPassValue = 3;
    public const int DefaultTurnEndMinMemory = 1;

    public MainPhaseMemoryResult EvaluateMainPhaseEntry(
        EngineContext context,
        LegalAction action,
        PhaseTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(transition);

        if (transition.Current.Phase != HeadlessPhase.Main)
        {
            return MainPhaseMemoryResult.NotApplicable(
                transition.Previous,
                transition.Current,
                context.MemoryController.Current,
                "NotMainPhase");
        }

        EnsureCurrentTurnPlayer(action, transition.Current, "enter the main phase");
        return EvaluateMemoryPass(
            context,
            transition.Previous,
            transition.Current,
            context.MemoryController.Current,
            context.MemoryController.Current,
            mainPhaseEntered: true,
            reason: "MainPhaseEntry");
    }

    public MainPhaseMemoryResult PassTurn(
        EngineContext context,
        LegalAction action)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        HeadlessTurnState previousTurn = context.TurnController.Current;
        if (previousTurn.Phase != HeadlessPhase.Main)
        {
            throw new InvalidOperationException("Pass can only be processed during the Main phase.");
        }

        EnsureCurrentTurnPlayer(action, previousTurn, "pass the main phase");
        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        HeadlessMemoryState currentMemory = context.MemoryController.Set(-DefaultMemoryPassValue);
        HeadlessTurnState currentTurn = context.TurnController.SetPhase(HeadlessPhase.MemoryPass);

        return new MainPhaseMemoryResult(
            previousTurn,
            currentTurn,
            previousMemory,
            currentMemory,
            MainPhaseEntered: false,
            MemoryPassTriggered: true,
            MemoryPassCompleted: false,
            Reason: "ExplicitPass",
            MemoryPassThreshold: DefaultTurnEndMinMemory);
    }

    public MainPhaseMemoryResult EvaluateAfterMemoryMutation(
        EngineContext context,
        LegalAction action,
        HeadlessMemoryState previousMemory,
        HeadlessMemoryState currentMemory,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        HeadlessTurnState previousTurn = context.TurnController.Current;
        if (previousTurn.Phase != HeadlessPhase.Main)
        {
            return MainPhaseMemoryResult.NotApplicable(
                previousTurn,
                previousTurn,
                previousMemory,
                currentMemory,
                reason);
        }

        if (previousTurn.TurnPlayerId is null || action.PlayerId != previousTurn.TurnPlayerId.Value)
        {
            return MainPhaseMemoryResult.NotApplicable(
                previousTurn,
                previousTurn,
                previousMemory,
                currentMemory,
                reason);
        }

        return EvaluateMemoryPass(
            context,
            previousTurn,
            previousTurn,
            previousMemory,
            currentMemory,
            mainPhaseEntered: false,
            reason);
    }

    public MainPhaseMemoryResult CompleteMemoryPassTurn(
        EngineContext context,
        HeadlessTurnState previousTurn,
        HeadlessTurnState nextTurn,
        HeadlessMemoryState previousMemory)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (previousTurn.Phase != HeadlessPhase.MemoryPass)
        {
            return MainPhaseMemoryResult.NotApplicable(
                previousTurn,
                nextTurn,
                previousMemory,
                context.MemoryController.Current,
                "NotMemoryPass");
        }

        HeadlessMemoryState currentMemory = context.MemoryController.Current.Current < 0
            ? context.MemoryController.Set(Math.Abs(context.MemoryController.Current.Current))
            : context.MemoryController.Current;

        return new MainPhaseMemoryResult(
            previousTurn,
            nextTurn,
            previousMemory,
            currentMemory,
            MainPhaseEntered: false,
            MemoryPassTriggered: false,
            MemoryPassCompleted: true,
            Reason: "MemoryPassEndTurn",
            MemoryPassThreshold: DefaultTurnEndMinMemory);
    }

    private MainPhaseMemoryResult EvaluateMemoryPass(
        EngineContext context,
        HeadlessTurnState previousTurn,
        HeadlessTurnState currentTurn,
        HeadlessMemoryState previousMemory,
        HeadlessMemoryState currentMemory,
        bool mainPhaseEntered,
        string reason)
    {
        if (currentMemory.Current <= -DefaultTurnEndMinMemory)
        {
            HeadlessTurnState memoryPassTurn = context.TurnController.SetPhase(HeadlessPhase.MemoryPass);
            return new MainPhaseMemoryResult(
                previousTurn,
                memoryPassTurn,
                previousMemory,
                currentMemory,
                mainPhaseEntered,
                MemoryPassTriggered: true,
                MemoryPassCompleted: false,
                Reason: "MemoryThreshold",
                MemoryPassThreshold: DefaultTurnEndMinMemory);
        }

        return new MainPhaseMemoryResult(
            previousTurn,
            currentTurn,
            previousMemory,
            currentMemory,
            mainPhaseEntered,
            MemoryPassTriggered: false,
            MemoryPassCompleted: false,
            reason,
            DefaultTurnEndMinMemory);
    }

    private static void EnsureCurrentTurnPlayer(
        LegalAction action,
        HeadlessTurnState turn,
        string operation)
    {
        if (turn.TurnPlayerId is null)
        {
            throw new InvalidOperationException($"Cannot {operation} before turn state is initialized.");
        }

        if (action.PlayerId != turn.TurnPlayerId.Value)
        {
            throw new InvalidOperationException($"Only the current turn player can {operation}.");
        }
    }
}

public sealed record MainPhaseMemoryResult(
    HeadlessTurnState PreviousTurn,
    HeadlessTurnState CurrentTurn,
    HeadlessMemoryState PreviousMemory,
    HeadlessMemoryState CurrentMemory,
    bool MainPhaseEntered,
    bool MemoryPassTriggered,
    bool MemoryPassCompleted,
    string Reason,
    int MemoryPassThreshold)
{
    public static MainPhaseMemoryResult NotApplicable(
        HeadlessTurnState previousTurn,
        HeadlessTurnState currentTurn,
        HeadlessMemoryState memory,
        string reason)
    {
        return NotApplicable(previousTurn, currentTurn, memory, memory, reason);
    }

    public static MainPhaseMemoryResult NotApplicable(
        HeadlessTurnState previousTurn,
        HeadlessTurnState currentTurn,
        HeadlessMemoryState previousMemory,
        HeadlessMemoryState currentMemory,
        string reason)
    {
        return new MainPhaseMemoryResult(
            previousTurn,
            currentTurn,
            previousMemory,
            currentMemory,
            MainPhaseEntered: false,
            MemoryPassTriggered: false,
            MemoryPassCompleted: false,
            reason,
            HeadlessMainPhaseFlow.DefaultTurnEndMinMemory);
    }
}
