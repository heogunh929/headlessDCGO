namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class PassAction
{
    public ActionProcessResult Process(
        LegalAction action,
        EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        MainPhaseMemoryResult mainPhase;
        try
        {
            mainPhase = new HeadlessMainPhaseFlow().PassTurn(context, action);
        }
        catch (InvalidOperationException ex)
        {
            return ActionProcessResult.Illegal(action, ex.Message, BaseMetadata(action));
        }

        Dictionary<string, object?> metadata = MetadataWithTurn(action, mainPhase.CurrentTurn);
        AddMainPhaseMetadata(metadata, mainPhase);
        metadata["passIntent"] = "PassAction";

        return ActionProcessResult.Success(
            $"Main phase passed; memory is {mainPhase.CurrentMemory.Current}.",
            metadata);
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType
        };
    }

    private static Dictionary<string, object?> MetadataWithTurn(
        LegalAction action,
        HeadlessTurnState turn)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.TurnNumber] = turn.TurnNumber;
        metadata[HeadlessActionParameterKeys.Phase] = turn.Phase.ToString();
        metadata[HeadlessActionParameterKeys.TurnPlayerId] = turn.TurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.NonTurnPlayerId] = turn.NonTurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.IsFirstTurn] = turn.IsFirstTurn;
        return metadata;
    }

    private static void AddMainPhaseMetadata(
        Dictionary<string, object?> metadata,
        MainPhaseMemoryResult result)
    {
        metadata[HeadlessActionParameterKeys.Phase] = result.CurrentTurn.Phase.ToString();
        metadata[HeadlessActionParameterKeys.TurnNumber] = result.CurrentTurn.TurnNumber;
        metadata[HeadlessActionParameterKeys.TurnPlayerId] = result.CurrentTurn.TurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.NonTurnPlayerId] = result.CurrentTurn.NonTurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.IsFirstTurn] = result.CurrentTurn.IsFirstTurn;
        metadata[HeadlessActionParameterKeys.PreviousMemory] = result.PreviousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = result.CurrentMemory.Current;
        metadata[HeadlessActionParameterKeys.MemoryMinimum] = result.CurrentMemory.Minimum;
        metadata[HeadlessActionParameterKeys.MemoryMaximum] = result.CurrentMemory.Maximum;
        metadata[HeadlessActionParameterKeys.MainPhaseEntered] = result.MainPhaseEntered;
        metadata[HeadlessActionParameterKeys.MemoryPassTriggered] = result.MemoryPassTriggered;
        metadata[HeadlessActionParameterKeys.MemoryPassCompleted] = result.MemoryPassCompleted;
        metadata[HeadlessActionParameterKeys.MemoryPassReason] = result.Reason;
        metadata[HeadlessActionParameterKeys.MemoryPassThreshold] = result.MemoryPassThreshold;
        metadata[HeadlessActionParameterKeys.PassedMemory] = result.MemoryPassTriggered
            ? Math.Abs(result.CurrentMemory.Current)
            : null;
    }
}

public static class CheatActionGuard
{
    public static ActionProcessResult Reject(LegalAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dictionary<string, object?> metadata = new(StringComparer.Ordinal)
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType,
            ["cheatGuard"] = "Rejected"
        };

        if (action.Parameters.TryGetValue(HeadlessActionParameterKeys.CheatType, out object? cheatType))
        {
            metadata[HeadlessActionParameterKeys.CheatType] = cheatType;
        }

        return ActionProcessResult.Illegal(
            action,
            "Cheat actions are excluded from the headless legal action path.",
            metadata);
    }

    public static bool IsCheatOrDebugAction(string actionType)
    {
        string normalized = HeadlessActionTypes.Normalize(actionType);
        return normalized is
            HeadlessActionTypes.NormalizedCheat or
            HeadlessActionTypes.NormalizedMoveCard or
            HeadlessActionTypes.NormalizedAddToHand or
            HeadlessActionTypes.NormalizedAddToTrash or
            HeadlessActionTypes.NormalizedAddToSecurity or
            HeadlessActionTypes.NormalizedMoveToDeckTop or
            HeadlessActionTypes.NormalizedMoveToDeckBottom or
            HeadlessActionTypes.NormalizedDrawCards or
            HeadlessActionTypes.NormalizedAddSecurityFromLibrary or
            HeadlessActionTypes.NormalizedTrashSecurity or
            HeadlessActionTypes.NormalizedHatchDigitama or
            HeadlessActionTypes.NormalizedMoveBreedingToBattle or
            HeadlessActionTypes.NormalizedShuffleDeck or
            HeadlessActionTypes.NormalizedEnqueueEffect or
            HeadlessActionTypes.NormalizedSetMemory or
            HeadlessActionTypes.NormalizedAddMemory or
            HeadlessActionTypes.NormalizedPayMemory;
    }
}
