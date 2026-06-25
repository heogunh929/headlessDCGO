namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessLegalActionDispatcher
{
    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsDispatchAvailable(context, playerId))
        {
            return Array.Empty<LegalAction>();
        }

        HeadlessTurnState turn = context.TurnController.Current;
        if (turn.TurnPlayerId is null || turn.TurnPlayerId.Value != playerId)
        {
            return Array.Empty<LegalAction>();
        }

        return (turn.Phase switch
        {
            HeadlessPhase.Setup or
            HeadlessPhase.Active or
            HeadlessPhase.Unsuspend or
            HeadlessPhase.Draw or
            HeadlessPhase.Breeding => new[] { HeadlessActionFactory.AdvancePhase(playerId) },
            HeadlessPhase.Main => new[] { HeadlessActionFactory.Pass(playerId) }
                .Concat(new PlayCardAction().GetLegalActions(context, playerId))
                .Concat(new DigivolveAction().GetLegalActions(context, playerId))
                .Concat(new OptionActivateAction().GetLegalActions(context, playerId))
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .ToArray(),
            HeadlessPhase.MemoryPass or
            HeadlessPhase.End => new[] { HeadlessActionFactory.EndTurn(playerId) },
            _ => Array.Empty<LegalAction>()
        })
        .Where(action => !CheatActionGuard.IsCheatOrDebugAction(action.ActionType))
        .ToArray();
    }

    private static bool IsDispatchAvailable(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty ||
            context.RuleQueryService.IsTerminal() ||
            context.ChoiceController.Current.IsPending ||
            context.EffectScheduler.HasPendingEffects ||
            context.CardInstanceRepository.Snapshot().Count == 0)
        {
            return false;
        }

        return true;
    }
}
