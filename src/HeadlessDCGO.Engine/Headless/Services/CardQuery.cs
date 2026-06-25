namespace HeadlessDCGO.Engine.Headless.Services;

public sealed record CardQuery(
    string? CardNumber = null,
    string? NameContains = null,
    string? CardType = null,
    string? EffectBindingKey = null)
{
    public static CardQuery All { get; } = new();

    public bool Matches(CardRecord card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return MatchesExact(CardNumber, card.CardNumber) &&
            MatchesContains(NameContains, card.Name) &&
            MatchesExact(CardType, card.CardType) &&
            MatchesExact(EffectBindingKey, card.EffectBindingKey);
    }

    private static bool MatchesExact(string? expected, string? actual)
    {
        return string.IsNullOrWhiteSpace(expected) ||
            string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesContains(string? expected, string actual)
    {
        return string.IsNullOrWhiteSpace(expected) ||
            actual.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
