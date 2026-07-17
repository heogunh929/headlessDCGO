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
            SelectedIds: Array.Empty<HeadlessEntityId>())
        {
            CandidateIds = request.Candidates.Select(candidate => candidate.Id).ToArray()
        };

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
            SelectedIds = result.SelectedIds.ToArray(),
            // (B5-1) The partial-selection session dies with the pending choice: a batch ResolveChoice
            // (the pre-B5 backward-compatible path) resolves identically whether or not toggles happened
            // first — the AS-IS session accumulator is a coroutine local that goes away when the
            // selection UI closes (SelectPermanentEffect.cs:362), it never outlives the selection.
            PendingSelectedIds = Array.Empty<HeadlessEntityId>()
        };

        return Current;
    }

    public HeadlessChoiceState ToggleCandidate(HeadlessEntityId candidateId)
    {
        if (!Current.IsPending)
        {
            throw new InvalidOperationException("Cannot toggle a choice candidate while no choice is pending.");
        }

        if (!Current.CandidateIds.Contains(candidateId))
        {
            throw new ArgumentException(
                $"Toggled id '{candidateId.Value}' is not a candidate of the pending choice.",
                nameof(candidateId));
        }

        // (B5-1, 설계 §B5.4/핀 1) AS-IS tap/re-tap toggle, order preserved — SelectPermanentEffect.cs
        // OnClickFieldPermanentCard :431-464 (SelectHandEffect.cs :271-311 is the same shape):
        //   * already picked   -> Remove (deselect; always allowed — the pin-3 escape lane),
        //   * count < maxCount -> Add (append keeps pick order),
        //   * count == maxCount-> replace-last: RemoveAt(Count-1) then Add(new) (AS-IS :457-463),
        //     guarded by `if (Count > 0)` — so at MaxCount 0 a tap is a no-op, exactly as AS-IS.
        // Per-pick gates (canTargetCondition_ByPreSelecetedList -> ChoiceRequest.PartialPickGate) are NOT
        // evaluated here: the dispatcher's toggle lanes filter them at enumeration time (B5-2), the mirror
        // of AS-IS registering click handlers only on selectable cards / early-returning on a failing gate.
        List<HeadlessEntityId> picks = Current.PendingSelectedIds.ToList();
        if (picks.Contains(candidateId))
        {
            picks.Remove(candidateId);
        }
        else if (picks.Count < Current.MaxCount)
        {
            picks.Add(candidateId);
        }
        else if (picks.Count > 0)
        {
            picks.RemoveAt(picks.Count - 1);
            picks.Add(candidateId);
        }

        Current = Current with { PendingSelectedIds = picks };
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
