namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace UI Select* flows with deterministic choice state transitions.
public interface IHeadlessChoiceController : IHeadlessMatchStateResettable
{
    HeadlessChoiceState Current { get; }

    ChoiceRequest? PendingRequest { get; }

    HeadlessChoiceState RequestChoice(
        ChoiceRequest request,
        HeadlessEntityId? requestId = null);

    HeadlessChoiceState ResolveChoice(ChoiceResult result);

    // (B5-1, 설계 §B5.4) Toggle one candidate in the pending choice's partial-selection session
    // (AS-IS tap/re-tap on a selection target — SelectPermanentEffect.cs:431-464, SelectHandEffect.cs:271-311).
    // Pure state transition over Current.PendingSelectedIds; never resolves the choice (the AS-IS session
    // only ends through the confirm/skip surfaces, which stay on ResolveChoice). Per-pick legality
    // (IsSelectable / PartialPickGate) is the dispatcher's lane filter (B5-2), mirroring the AS-IS split
    // where only gate-passing cards get a click handler / the tap handler early-returns.
    HeadlessChoiceState ToggleCandidate(HeadlessEntityId candidateId);

    HeadlessChoiceState ClearChoice();
}
