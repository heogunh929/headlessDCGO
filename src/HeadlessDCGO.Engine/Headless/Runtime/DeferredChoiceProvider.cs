namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Thrown by <see cref="DeferredChoiceProvider"/> when an effect asks for a choice the agent has not
/// answered yet. It is a control-flow signal, not an error: the effect resolver converts it into a
/// <see cref="Effects.EffectResolutionStatus.Suspended"/> result, which leaves the effect queued and
/// pauses the common loop while the choice is surfaced to the agent (A2 ResolveChoice action).
/// </summary>
public sealed class DeferredChoicePendingException : Exception
{
    public DeferredChoicePendingException(ChoiceRequest request)
        : base($"Choice deferred to the agent: {request?.Message}")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public ChoiceRequest Request { get; }
}

/// <summary>
/// Resolution-boundary hooks the effect resolver calls around each effect body so a deferred-choice
/// provider can (a) harvest the answer the agent supplied for the previous suspension and (b) know
/// when an effect fully resolved so accumulated answers can be cleared. Implemented by
/// <see cref="DeferredChoiceProvider"/>; a no-op for scripted/policy providers.
/// </summary>
public interface IDeferredChoiceCoordinator
{
    /// <summary>Called before each effect resolution attempt. Harvests any answer the agent supplied
    /// for the last deferred choice and rewinds the replay cursor so the re-run replays answers in
    /// order.</summary>
    void BeginResolution();

    /// <summary>Called after an effect resolves to completion (not suspended) so the accumulated
    /// answers for that effect are discarded before the next one runs.</summary>
    void CompleteResolution();
}

/// <summary>
/// (W7) Bridges effect-driven choices to the agent. When an effect asks for a choice via
/// <see cref="ChooseAsync"/>:
/// <list type="bullet">
/// <item>during a re-run, it replays answers the agent already supplied (in request order);</item>
/// <item>for the first unanswered choice it registers the request on the
/// <see cref="IHeadlessChoiceController"/> (so the dispatcher surfaces a ResolveChoice agent action)
/// and throws <see cref="DeferredChoicePendingException"/> to suspend the effect.</item>
/// </list>
/// The effect re-runs from the start each time the agent answers, so an effect MUST request its
/// choices before mutating state (choose-then-apply) to stay re-run safe — the standard contract for
/// ported card effects.
/// </summary>
public sealed class DeferredChoiceProvider : IChoiceProvider, IDeferredChoiceCoordinator
{
    private readonly IHeadlessChoiceController _controller;
    private readonly List<ChoiceResult> _answers = new();
    private int _cursor;

    public DeferredChoiceProvider(IHeadlessChoiceController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    /// <summary>Answers accumulated for the effect currently resolving.</summary>
    public int AnswerCount => _answers.Count;

    public void BeginResolution()
    {
        // Harvest the answer the agent just supplied (the controller holds the resolved choice after
        // an A2 ResolveChoice action) and append it in the order choices were opened.
        if (_controller.Current.IsResolved)
        {
            _answers.Add(FromResolvedState(_controller.Current));
            _controller.ClearChoice();
        }

        _cursor = 0;
    }

    public void CompleteResolution()
    {
        _answers.Clear();
        _cursor = 0;
        if (_controller.Current.IsResolved)
        {
            _controller.ClearChoice();
        }
    }

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Replay an already-supplied answer for this position in the effect.
        if (_cursor < _answers.Count)
        {
            ChoiceResult replayed = _answers[_cursor++];
            replayed.ThrowIfInvalid(request);
            return Task.FromResult(replayed);
        }

        // Unanswered: surface the choice to the agent and suspend the effect.
        _controller.RequestChoice(request, RequestId(request));
        throw new DeferredChoicePendingException(request);
    }

    private static HeadlessEntityId RequestId(ChoiceRequest request)
    {
        return new HeadlessEntityId($"deferred-choice:{request.PlayerId.Value}:{request.Type}");
    }

    private static ChoiceResult FromResolvedState(HeadlessChoiceState state)
    {
        if (state.IsSkipped)
        {
            return ChoiceResult.Skip();
        }

        if (state.Type == ChoiceType.Count)
        {
            return ChoiceResult.SelectCount(state.SelectedCount ?? 0);
        }

        return ChoiceResult.Select(state.SelectedIds);
    }
}
