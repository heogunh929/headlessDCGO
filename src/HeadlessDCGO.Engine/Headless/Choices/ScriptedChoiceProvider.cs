namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class ScriptedChoiceProvider : IChoiceProvider
{
    private readonly Queue<ChoiceResult> _choices = new();

    public ScriptedChoiceProvider()
    {
    }

    public ScriptedChoiceProvider(IEnumerable<ChoiceResult> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        foreach (ChoiceResult choice in choices)
        {
            Enqueue(choice);
        }
    }

    public int Count => _choices.Count;

    public void Enqueue(ChoiceResult choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        _choices.Enqueue(choice);
    }

    public void Clear()
    {
        _choices.Clear();
    }

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ChoiceResult choice = _choices.Count > 0
            ? _choices.Peek()
            : CreateFallbackChoice(request);

        choice.ThrowIfInvalid(request);

        if (_choices.Count > 0)
        {
            _choices.Dequeue();
        }

        return Task.FromResult(choice);
    }

    private static ChoiceResult CreateFallbackChoice(ChoiceRequest request)
    {
        if (request.CanSkip)
        {
            return ChoiceResult.Skip();
        }

        if (request.Type == ChoiceType.Count)
        {
            return ChoiceResult.SelectCount(request.MinCount);
        }

        HeadlessEntityId[] selectedIds = request.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Take(request.MinCount)
            .Select(candidate => candidate.Id)
            .ToArray();

        return ChoiceResult.Select(selectedIds);
    }
}
