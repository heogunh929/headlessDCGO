namespace HeadlessDCGO.Engine.Headless.Choices;

public interface IChoiceProvider
{
    Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default);
}
