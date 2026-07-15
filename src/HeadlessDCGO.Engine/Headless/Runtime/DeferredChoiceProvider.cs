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

    /// <summary>(C-Del 3c-2b nested cycles) Called when a resolution attempt SUSPENDS (a pending-choice
    /// exception unwinds the cycle). Marks the active cycle frame as parked (its answers survive for the
    /// re-run) without discarding it, so a NESTED cycle that suspended does not corrupt the enclosing
    /// cycle's replay position.</summary>
    void SuspendResolution();
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
    // (C-Del 3c-2b nested cycles) One replay FRAME per resolution cycle. Cycles can NEST (a window pick's body
    // deletes a permanent, whose PRE would-be-deleted window resolves a CHILD pick with its own choices) and
    // their lifetimes can overlap non-LIFO (the child suspends, the sink promote-to-defer SWALLOWS the pause,
    // and the PARENT completes while the child stays parked). The former flat answers+cursor model rewound the
    // shared cursor at every BeginResolution, so a child cycle replayed the PARENT's answer into its own first
    // request (observed: the Scapegoat pick's answer replayed into the sacrificed ally's nested Evade optional).
    // Frames are positional by ACTIVE DEPTH: BeginResolution re-enters the parked frame at the current depth
    // (harvest + rewind = the re-run contract) or pushes a fresh one (a nested/new cycle replays NOTHING of its
    // parent); SuspendResolution parks the frame (depth retreats, answers survive); CompleteResolution discards
    // the completing cycle's frame. A parked child left behind by a completed parent shifts down and is re-entered
    // at its resume (deepest-first), replaying only its own answers.
    private sealed class Frame
    {
        public List<ChoiceResult> Answers { get; } = new();
        public int Cursor { get; set; }
    }

    private readonly IHeadlessChoiceController _controller;
    private readonly List<Frame> _frames = new();
    private int _activeDepth;

    public DeferredChoiceProvider(IHeadlessChoiceController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    /// <summary>Answers accumulated for the effect currently resolving.</summary>
    public int AnswerCount => _activeDepth > 0 ? _frames[_activeDepth - 1].Answers.Count
        : _frames.Count > 0 ? _frames[0].Answers.Count : 0;

    public void BeginResolution()
    {
        if (_activeDepth < _frames.Count)
        {
            // Re-entering a PARKED cycle (the re-run after the agent answered its suspension): harvest the
            // answer the agent just supplied (the controller holds the resolved choice after an A2
            // ResolveChoice action), append it in the order the cycle opened its choices, and rewind so the
            // re-run replays them in order.
            Frame frame = _frames[_activeDepth];
            if (CanHarvest())
            {
                frame.Answers.Add(FromResolvedState(_controller.Current));
                _controller.ClearChoice();
            }

            frame.Cursor = 0;
        }
        else
        {
            // A FRESH cycle (root or nested): it starts with its own empty frame — it must NOT replay an
            // enclosing cycle's answers.
            var frame = new Frame();
            if (CanHarvest())
            {
                frame.Answers.Add(FromResolvedState(_controller.Current));
                _controller.ClearChoice();
            }

            _frames.Add(frame);
        }

        _activeDepth++;
    }

    // (C-Del 3c-2b) A resolved WindowChoice (the MultipleSkills ORDER pick) is the WINDOW port's currency —
    // recorded on the SkillWindowContinuation and replayed by AgentSkillWindowChoicePort.TryGetAnswer, NOT by
    // this provider. Harvesting it here would replay an order token ("cardId#ordinal") into the next
    // provider-served request (observed: the order answer replayed into an Evade OptionalSkill — invalid
    // candidate). Leave it on the controller for the port machinery; the next RequestChoice overwrites it.
    private bool CanHarvest() =>
        _controller.Current.IsResolved && _controller.Current.Type != ChoiceType.WindowChoice;

    public void SuspendResolution()
    {
        // Park the active frame: depth retreats but the frame (and its answers) survives for the re-run.
        if (_activeDepth > 0)
        {
            _activeDepth--;
        }
    }

    public void CompleteResolution()
    {
        if (_activeDepth > 0)
        {
            // The completing cycle discards ITS frame; a parked deeper frame (a swallowed child suspension)
            // shifts down and is re-entered when its window resumes.
            _frames.RemoveAt(_activeDepth - 1);
            _activeDepth--;
            return;
        }

        // Legacy external reset (a harness clearing setup-phase answers): full clear.
        _frames.Clear();
        if (_controller.Current.IsResolved)
        {
            _controller.ClearChoice();
        }
    }

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // A request outside any Begin/Complete cycle (setup-phase choices; direct drives) uses an implicit
        // root frame — the former flat behaviour (no harvest here; BeginResolution owns harvesting).
        if (_activeDepth == 0 && _frames.Count == 0)
        {
            _frames.Add(new Frame());
        }

        Frame active = _frames[Math.Max(0, _activeDepth - 1)];

        // Replay an already-supplied answer for this position in the effect.
        if (active.Cursor < active.Answers.Count)
        {
            ChoiceResult replayed = active.Answers[active.Cursor++];
            replayed.ThrowIfInvalid(request);
            return Task.FromResult(replayed);
        }

        // (C-Del 3c-2b nested cycles) Idempotent re-ask: when the SAME unanswered question is still pending on
        // the controller (a parked nested window is resumed by the drain loop before the agent answered —
        // e.g. the outer window completed via the sink's promote-to-defer swallow and ResumeSuspendedWindowsAsync
        // immediately re-entered the parked child), do NOT re-register (the controller rejects a second
        // RequestChoice while one pends) — just re-throw the pending signal so the window re-parks cleanly.
        if (_controller.Current.IsPending)
        {
            throw new DeferredChoicePendingException(request);
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
