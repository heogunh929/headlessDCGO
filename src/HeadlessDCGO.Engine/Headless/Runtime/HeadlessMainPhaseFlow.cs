namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessMainPhaseFlow
{
    public const int DefaultMemoryPassValue = 3;
    public const int DefaultTurnEndMinMemory = 1;

    /// <summary>(d-remediation) AS-IS AutoProcessing.TurnEndMinMemory: default <see cref="DefaultTurnEndMinMemory"/>,
    /// overridden by any ChangeEndTurnMinMemory effect on the turn player's board (the ported cards SET it).</summary>
    private static int ResolveTurnEndMinMemory(EngineContext context, HeadlessPlayerId? turnPlayer)
    {
        if (turnPlayer is not { } player || context.ZoneMover is not IZoneStateReader zones)
        {
            return DefaultTurnEndMinMemory;
        }

        int threshold = DefaultTurnEndMinMemory;
        // (RD-6 both-player scan / design item #67) AS-IS TurnEndMinMemory scans Players_ForTurnPlayer — BOTH
        // players, turn player FIRST — folding
        // each IChangeEndTurnMinMemory effect's GetMinMemory (AutoProcessing.cs:645-671). An effect can sit on
        // EITHER board (e.g. one that raises the min memory the OPPONENT must reach to end this turn lives on the
        // non-turn player's card), so scanning only the turn player missed those. The ported cards
        // (BT14_081/BT17_069) SET a fixed value (GetMinMemory returns a constant, per FAILd-07), so last-write-wins
        // over the turn-player-first order mirrors the fold for the common single-effect case; a dependent-fold
        // multi-effect edge is deferred (no such ported card). No live change today — the ported cards are
        // turn-player-board — so this is latent infra for an opponent-board threshold card.
        foreach (HeadlessPlayerId scanPlayer in
            new[] { player }.Concat(context.TurnController.Current.PlayerOrder.Where(p => p != player && !p.IsEmpty)))
        {
            foreach (HeadlessEntityId cardId in zones.GetCards(scanPlayer, ChoiceZone.BattleArea))
            {
                foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId))
                {
                    if (effect.Context.Values.TryGetValue(ModifierHelpers.EndTurnMinMemoryKey, out object? raw) && raw is int minMemory)
                    {
                        threshold = minMemory;
                    }
                }
            }
        }

        return threshold;
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
        // (d-remediation) AS-IS AutoProcessing.TurnEndMinMemory: a ChangeEndTurnMinMemory effect (BT14_081/
        // BT17_069) on the turn player's board raises the threshold the opponent must reach for the turn to auto-end.
        int turnEndMinMemory = ResolveTurnEndMinMemory(context, currentTurn.TurnPlayerId);
        if (currentMemory.Current <= -turnEndMinMemory)
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
