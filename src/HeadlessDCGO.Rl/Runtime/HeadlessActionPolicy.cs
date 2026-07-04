namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace these adapters with trainer-backed policy integration.
public interface IHeadlessActionPolicy
{
    Task<HeadlessActionDecision> ChooseActionAsync(
        RlStepResult state,
        CancellationToken cancellationToken = default);
}

public sealed record HeadlessActionDecision(
    EncodedAction? Action,
    string Reason = "")
{
    public bool HasAction => Action is not null;

    public static HeadlessActionDecision None(string reason)
    {
        return new HeadlessActionDecision(null, reason);
    }

    public static HeadlessActionDecision Select(EncodedAction action, string reason = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        return new HeadlessActionDecision(action, reason);
    }
}

public sealed class FirstLegalActionPolicy : IHeadlessActionPolicy
{
    public Task<HeadlessActionDecision> ChooseActionAsync(
        RlStepResult state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        EncodedAction? action = state.ActionMask.LegalActions.FirstOrDefault();
        return Task.FromResult(action is null
            ? HeadlessActionDecision.None("No legal action is available.")
            : HeadlessActionDecision.Select(action, "Selected first legal action."));
    }
}

public sealed class RandomLegalActionPolicy(IRandomSource randomSource) : IHeadlessActionPolicy
{
    public Task<HeadlessActionDecision> ChooseActionAsync(
        RlStepResult state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<EncodedAction> legalActions = state.ActionMask.LegalActions;
        if (legalActions.Count == 0)
        {
            return Task.FromResult(HeadlessActionDecision.None("No legal action is available."));
        }

        int actionIndex = randomSource.NextInt(0, legalActions.Count);
        return Task.FromResult(HeadlessActionDecision.Select(
            legalActions[actionIndex],
            "Selected random legal action."));
    }
}

public sealed class DelegateHeadlessActionPolicy(
    Func<RlStepResult, CancellationToken, Task<HeadlessActionDecision>> policy) : IHeadlessActionPolicy
{
    public Task<HeadlessActionDecision> ChooseActionAsync(
        RlStepResult state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        return policy(state, cancellationToken);
    }
}
