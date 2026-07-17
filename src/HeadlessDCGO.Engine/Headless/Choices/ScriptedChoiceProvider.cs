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

        HeadlessEntityId[] selectable = request.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Select(candidate => candidate.Id)
            .ToArray();

        HeadlessEntityId[] selectedIds = selectable
            .Take(request.MinCount)
            .ToArray();

        // (RD-R4P4-02) AS-IS auto-select confirms only a selection the combination validator accepts: the AI
        // branch (AS-IS SelectPermanentEffect.cs :629-684) retries random _maxCount-size candidate sets and
        // confirms the first one `_canEndSelectCondition(GetCharas)` passes (bounded at 200 tries), and the
        // player UI's confirm button likewise only activates when CanEndSelect passes. The former fallback
        // ignored the request's SelectionValidator (e.g. ST1_15's non-empty gate over a MinCount==0 request),
        // confirming an invalid set that then threw at ThrowIfInvalid. Mirror: keep the historical MinCount
        // pick whenever it is valid (validator-less requests are byte-for-byte unaffected); otherwise search
        // deterministically for a validator-passing set — max-size first (the AS-IS AI's set size), then
        // smaller sizes (the canEndNotMax early-end surface), lexicographic within a size, capped at the
        // AS-IS 200 evaluations. When nothing passes, fall through to the previous pick so the invalid
        // confirmation surfaces at ThrowIfInvalid exactly as before.
        if (request.SelectionValidator is not null && !request.SelectionValidator(selectedIds))
        {
            int tries = 0;
            for (int size = Math.Min(request.MaxCount, selectable.Length); size >= request.MinCount && tries < 200; size--)
            {
                foreach (HeadlessEntityId[] combination in Combinations(selectable, size))
                {
                    if (++tries > 200)
                    {
                        break;
                    }

                    if (request.SelectionValidator(combination))
                    {
                        return ChoiceResult.Select(combination);
                    }
                }
            }
        }

        return ChoiceResult.Select(selectedIds);
    }

    /// <summary>Lexicographic k-combinations of <paramref name="items"/> in candidate order (deterministic —
    /// the substrate translation of the AS-IS AI's bounded random subset retry).</summary>
    private static IEnumerable<HeadlessEntityId[]> Combinations(HeadlessEntityId[] items, int size)
    {
        if (size < 0 || size > items.Length)
        {
            yield break;
        }

        int[] indexes = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return indexes.Select(index => items[index]).ToArray();

            int position = size - 1;
            while (position >= 0 && indexes[position] == items.Length - size + position)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            indexes[position]++;
            for (int next = position + 1; next < size; next++)
            {
                indexes[next] = indexes[next - 1] + 1;
            }
        }
    }
}
