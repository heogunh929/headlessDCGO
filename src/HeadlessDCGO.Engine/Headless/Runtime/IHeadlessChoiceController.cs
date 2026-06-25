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

    HeadlessChoiceState ClearChoice();
}
