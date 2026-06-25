namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace these coarse checks with final DCGO deck construction rules.
public sealed class DeckValidator(DeckValidationOptions? options = null)
{
    private readonly DeckValidationOptions _options = options ?? DeckValidationOptions.Default;

    public DeckValidationResult Validate(
        DeckList deckList,
        ICardRepository? cardRepository = null,
        Banlist? banlist = null)
    {
        ArgumentNullException.ThrowIfNull(deckList);

        List<DeckValidationIssue> issues = new();

        ValidateSectionCount(
            "MainDeck",
            deckList.MainDeckCount,
            _options.MinimumMainDeckCount,
            _options.MaximumMainDeckCount,
            issues);
        ValidateSectionCount(
            "DigitamaDeck",
            deckList.DigitamaDeckCount,
            _options.MinimumDigitamaDeckCount,
            _options.MaximumDigitamaDeckCount,
            issues);
        ValidateEntries(deckList.MainDeck, "MainDeck", cardRepository, banlist, issues);
        ValidateEntries(deckList.DigitamaDeck, "DigitamaDeck", cardRepository, banlist, issues);

        return new DeckValidationResult(deckList.Name, issues.ToArray());
    }

    private static void ValidateSectionCount(
        string section,
        int count,
        int minimum,
        int maximum,
        List<DeckValidationIssue> issues)
    {
        if (count < minimum)
        {
            issues.Add(new DeckValidationIssue(
                DeckValidationIssueSeverity.Error,
                section,
                null,
                $"Deck section has too few cards: {count} < {minimum}."));
        }

        if (count > maximum)
        {
            issues.Add(new DeckValidationIssue(
                DeckValidationIssueSeverity.Error,
                section,
                null,
                $"Deck section has too many cards: {count} > {maximum}."));
        }
    }

    private void ValidateEntries(
        IReadOnlyList<DeckListEntry> entries,
        string section,
        ICardRepository? cardRepository,
        Banlist? banlist,
        List<DeckValidationIssue> issues)
    {
        Dictionary<HeadlessEntityId, int> counts = new();

        foreach (DeckListEntry entry in entries)
        {
            if (entry.Count <= 0)
            {
                issues.Add(new DeckValidationIssue(
                    DeckValidationIssueSeverity.Error,
                    section,
                    entry.CardId,
                    "Deck entry count must be positive."));
                continue;
            }

            counts[entry.CardId] = counts.TryGetValue(entry.CardId, out int existingCount)
                ? existingCount + entry.Count
                : entry.Count;

            if (_options.RequireKnownCards &&
                cardRepository is not null &&
                !cardRepository.TryGetCard(entry.CardId, out _))
            {
                issues.Add(new DeckValidationIssue(
                    DeckValidationIssueSeverity.Error,
                    section,
                    entry.CardId,
                    "Card id is not present in the card repository."));
            }
        }

        foreach (KeyValuePair<HeadlessEntityId, int> pair in counts)
        {
            int limit = banlist?.GetLimit(pair.Key, _options.DefaultCardLimit) ?? _options.DefaultCardLimit;
            if (pair.Value > limit)
            {
                issues.Add(new DeckValidationIssue(
                    DeckValidationIssueSeverity.Error,
                    section,
                    pair.Key,
                    $"Card count exceeds limit: {pair.Value} > {limit}."));
            }
        }
    }
}

public sealed record DeckValidationOptions
{
    public static DeckValidationOptions Default { get; } = new();

    public int MinimumMainDeckCount { get; init; }

    public int MaximumMainDeckCount { get; init; } = 60;

    public int MinimumDigitamaDeckCount { get; init; }

    public int MaximumDigitamaDeckCount { get; init; } = 10;

    public int DefaultCardLimit { get; init; } = 4;

    public bool RequireKnownCards { get; init; }
}

public sealed record DeckValidationResult(
    string DeckName,
    IReadOnlyList<DeckValidationIssue> Issues)
{
    public bool IsValid => !Issues.Any(issue => issue.Severity == DeckValidationIssueSeverity.Error);

    public int ErrorCount => Issues.Count(issue => issue.Severity == DeckValidationIssueSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == DeckValidationIssueSeverity.Warning);
}

public sealed record DeckValidationIssue(
    DeckValidationIssueSeverity Severity,
    string Section,
    HeadlessEntityId? CardId,
    string Message);

public enum DeckValidationIssueSeverity
{
    Warning = 0,
    Error = 1
}
