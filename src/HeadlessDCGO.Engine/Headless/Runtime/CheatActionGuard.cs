namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// (4b B6) Rehomed VERBATIM from the retired OLD Runtime/PassAction.cs (the guard was co-located with the
// OLD pass processor but is RETAINED SUBSTRATE: the cheat/debug filter on every legal-action surface).
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
            // D-6: HatchDigitama / MoveBreedingToBattle are now legitimate agent breeding-step actions,
            // not cheat/debug — they must survive the legal-action filter and the RL action space.
            HeadlessActionTypes.NormalizedShuffleDeck or
            HeadlessActionTypes.NormalizedEnqueueEffect or
            HeadlessActionTypes.NormalizedSetMemory or
            HeadlessActionTypes.NormalizedAddMemory or
            HeadlessActionTypes.NormalizedPayMemory;
    }
}
