namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// LEGACY TEST SCAFFOLD (R4 S3c-d1): the OLD step-cadence driver's main-phase/turn-end body (EndTurn,
// memory-pass cursor, PostActionMemorySettle). It survives ONLY for the pre-R4 test corpus; new/RL
// matches use the TurnFlowPump (DcgoMatch.CreatePumpDriven), where the real AS-IS EndTurnCheck runs
// in-pump and this flow is skipped (HeadlessGameLoop pump guard). Physical retirement gate = the
// suite re-targeting goal (design doc S3c-d ledger).
public sealed class HeadlessMainPhaseFlow
{
    public const int DefaultMemoryPassValue = 3;
    public const int DefaultTurnEndMinMemory = 1;

    /// <summary>(d-remediation, R3-W3c-4b B2) AS-IS <c>AutoProcessing.TurnEndMinMemory</c> (AutoProcessing.cs:645-671):
    /// seed 1, folded LIVE across every usable <see cref="IChangeEndTurnMinMemoryEffect"/> on (1) the players'
    /// own effects then (2) the players' field permanents' effects — <c>Players_ForTurnPlayer</c> (BOTH players,
    /// turn player first), each effect's <c>GetMinMemory(seed)</c> updating the running value. Rehoused from the
    /// retired registry <c>ContinuousScopeEvaluation.ApplicableEffects</c> key-read to this AS-IS-literal live
    /// scan so the new-model kind-class producer (<see cref="CardEffects.ChangeEndTurnMinMemoryClass"/>) is seen.
    /// The ported cards (BT14_081/BT17_069) SET a constant, so the fold reduces to last-write-wins.</summary>
    private static int ResolveTurnEndMinMemory(EngineContext context, HeadlessPlayerId? turnPlayer)
    {
        if (turnPlayer is not { })
        {
            return DefaultTurnEndMinMemory;
        }

        int turnEndMinMemory = DefaultTurnEndMinMemory;
        var gameContext = new GameContext(context);

        // #region the effects of players (AS-IS :651-657)
        foreach (Player scanPlayer in gameContext.Players_ForTurnPlayer)
        {
            foreach (ICardEffect cardEffect in scanPlayer.EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeEndTurnMinMemoryEffect changeEffect && cardEffect.CanUse(null))
                {
                    turnEndMinMemory = changeEffect.GetMinMemory(turnEndMinMemory);
                }
            }
        }

        // #region the effects of permanents (AS-IS :659-667)
        foreach (Player scanPlayer in gameContext.Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in scanPlayer.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IChangeEndTurnMinMemoryEffect changeEffect && cardEffect.CanUse(null))
                    {
                        turnEndMinMemory = changeEffect.GetMinMemory(turnEndMinMemory);
                    }
                }
            }
        }

        return turnEndMinMemory;
    }

    /// <summary>(A-2 / RD-6) The turn-end threshold RE-CHECK after the [End of Your Turn] window drains, mirroring
    /// AS-IS AutoProcessing.EndTurnProcess:714 (<c>NonTurnPlayer.MemoryForPlayer &gt;= TurnEndMinMemory</c> → the
    /// turn ENDS; otherwise it CONTINUES / SetMainPhase). In the headless single-memory coordinate the opponent's
    /// memory is <c>-memory.Current</c> when negative, so <c>memory.Current &lt;= -threshold</c> is the faithful
    /// mirror (identical to <see cref="EvaluateMemoryPass"/>'s gate). Called by EndTurnAsync with the memory the
    /// [End of Your Turn] effects have already mutated in the ending player's frame — a memory-GAINING effect can
    /// lift the opponent back below the threshold and keep the turn going.</summary>
    public bool ShouldTurnEndAfterEndOfTurnWindow(EngineContext context, HeadlessPlayerId? turnPlayer)
    {
        ArgumentNullException.ThrowIfNull(context);
        int threshold = ResolveTurnEndMinMemory(context, turnPlayer);
        return context.MemoryController.Current.Current <= -threshold;
    }

    public MainPhaseMemoryResult EvaluateMainPhaseEntry(
        EngineContext context,
        LegalAction action,
        PhaseTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(transition);

        // (R4 S2) A fresh main-phase entry is the interactive main-play step (Main, PhaseStart) — NOT the
        // memory-pass end-of-main step, which also has Phase == Main. Gate on IsMainPlayPhase.
        if (!transition.Current.IsMainPlayPhase)
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
        // (R4 S2) Pass is legal only during the interactive main-play step (Main, PhaseStart), not the
        // memory-pass end-of-main step (which also has Phase == Main).
        if (!previousTurn.IsMainPlayPhase)
        {
            throw new InvalidOperationException("Pass can only be processed during the Main phase.");
        }

        EnsureCurrentTurnPlayer(action, previousTurn, "pass the main phase");
        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        HeadlessMemoryState currentMemory = context.MemoryController.Set(-DefaultMemoryPassValue);
        // (R4 S2) The former SetPhase(MemoryPass) is now the (Main, AwaitingMemoryPassEnd) step.
        HeadlessTurnState currentTurn = context.TurnController.SetPhase(HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd);

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
        // (R4 S2) A memory mutation only triggers a pass evaluation during interactive main-play (Main, PhaseStart).
        if (!previousTurn.IsMainPlayPhase)
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

        // (R4 S2) The former MemoryPass phase is the (Main, AwaitingMemoryPassEnd) step.
        if (!previousTurn.IsMemoryPassPhase)
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
        // (d-remediation) AS-IS AutoProcessing.TurnEndMinMemory: a ChangeEndTurnMinMemory effect (BT14_081/
        // BT17_069) on the turn player's board raises the threshold the opponent must reach for the turn to auto-end.
        int turnEndMinMemory = ResolveTurnEndMinMemory(context, currentTurn.TurnPlayerId);
        if (currentMemory.Current <= -turnEndMinMemory)
        {
            // (R4 S2) The former SetPhase(MemoryPass) is now the (Main, AwaitingMemoryPassEnd) step.
            HeadlessTurnState memoryPassTurn = context.TurnController.SetPhase(HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd);
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
