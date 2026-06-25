namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

// TODO: No-op processor until real AS-IS action handlers are ported.
public sealed class NoOpActionProcessor : IActionProcessor
{
    public Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionProcessResult.Success(
            $"No-op processed action: {action.ActionType}",
            new Dictionary<string, object?>
            {
                ["actionId"] = action.Id.Value,
                ["playerId"] = action.PlayerId.Value,
                ["actionType"] = action.ActionType
            }));
    }
}
