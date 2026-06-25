namespace HeadlessDCGO.Engine.Headless.Choices;

public sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly Func<ChoiceRequest, CancellationToken, Task<ChoiceResult>> _policy;

    public PolicyChoiceProvider(Func<ChoiceRequest, CancellationToken, Task<ChoiceResult>>? policy = null)
    {
        _policy = policy ?? DefaultPolicyAsync;
    }

    public async Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Task<ChoiceResult> policyTask = _policy(request, cancellationToken)
            ?? throw new InvalidOperationException("Policy choice delegate returned a null task.");

        ChoiceResult result = await policyTask.ConfigureAwait(false)
            ?? throw new InvalidOperationException("Policy choice delegate returned a null result.");

        result.ThrowIfInvalid(request);
        return result;
    }

    private static Task<ChoiceResult> DefaultPolicyAsync(
        ChoiceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DefaultChoice(request));
    }

    private static ChoiceResult DefaultChoice(ChoiceRequest request)
    {
        if (request.CanSkip)
        {
            return ChoiceResult.Skip();
        }

        if (request.Type == ChoiceType.Count)
        {
            return ChoiceResult.SelectCount(request.MinCount);
        }

        return ChoiceResult.Select(request.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Take(Math.Max(0, request.MinCount))
            .Select(candidate => candidate.Id));
    }
}
