namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryHeadlessChoiceController : IHeadlessChoiceController
{
    public HeadlessChoiceState Current { get; private set; } = HeadlessChoiceState.Empty;

    public ChoiceRequest? PendingRequest { get; private set; }

    public HeadlessChoiceState RequestChoice(
        ChoiceRequest request,
        HeadlessEntityId? requestId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Current.IsPending)
        {
            throw new InvalidOperationException("Cannot request a new choice while another choice is pending.");
        }

        PendingRequest = request;
        Current = new HeadlessChoiceState(
            requestId ?? new HeadlessEntityId($"choice:{request.PlayerId.Value}:{request.Type}"),
            request.Type,
            request.PlayerId,
            request.Message,
            request.MinCount,
            request.MaxCount,
            request.CanSkip,
            request.SourceZone,
            request.Candidates.Count,
            IsPending: true,
            IsResolved: false,
            IsSkipped: false,
            SelectedCount: null,
            SelectedIds: Array.Empty<HeadlessEntityId>());

        return Current;
    }

    public HeadlessChoiceState ResolveChoice(ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!Current.IsPending)
        {
            return Current;
        }

        if (PendingRequest is not null)
        {
            result.ThrowIfInvalid(PendingRequest);
        }

        PendingRequest = null;
        Current = Current with
        {
            IsPending = false,
            IsResolved = true,
            IsSkipped = result.IsSkipped,
            SelectedCount = result.SelectedCount,
            SelectedIds = result.SelectedIds.ToArray()
        };

        return Current;
    }

    public HeadlessChoiceState ClearChoice()
    {
        PendingRequest = null;
        Current = HeadlessChoiceState.Empty;
        return Current;
    }

    public void ResetMatchState()
    {
        ClearChoice();
    }
}
