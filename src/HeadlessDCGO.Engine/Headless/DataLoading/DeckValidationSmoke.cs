namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Move to tests once executable test infrastructure is available.
public static class DeckValidationSmoke
{
    public static DeckValidationResult ValidSmallDeck()
    {
        CardDatabase database = new();
        HeadlessEntityId cardA = new("validator-card-a");
        HeadlessEntityId cardB = new("validator-card-b");
        HeadlessEntityId digitama = new("validator-digitama-a");

        database.Upsert(new CardRecord(cardA, cardA.Value, "Validator Card A", new Dictionary<string, object?>()));
        database.Upsert(new CardRecord(cardB, cardB.Value, "Validator Card B", new Dictionary<string, object?>()));
        database.Upsert(new CardRecord(digitama, digitama.Value, "Validator Digitama A", new Dictionary<string, object?>()));

        DeckList deckList = new(
            "valid-small-deck",
            MainDeck: new[]
            {
                new DeckListEntry(cardA, Count: 2),
                new DeckListEntry(cardB, Count: 1)
            },
            DigitamaDeck: new[]
            {
                new DeckListEntry(digitama, Count: 1)
            });

        return new DeckValidator(new DeckValidationOptions
        {
            MaximumMainDeckCount = 10,
            MaximumDigitamaDeckCount = 5,
            RequireKnownCards = true
        }).Validate(deckList, database);
    }

    public static DeckValidationResult OverLimitDeck()
    {
        HeadlessEntityId limitedCard = new("validator-limited-card");
        DeckList deckList = new(
            "over-limit-deck",
            MainDeck: new[]
            {
                new DeckListEntry(limitedCard, Count: 3)
            },
            DigitamaDeck: Array.Empty<DeckListEntry>());
        Banlist banlist = new(
            "smoke-banlist",
            new Dictionary<HeadlessEntityId, int>
            {
                [limitedCard] = 1
            });

        return new DeckValidator(new DeckValidationOptions
        {
            MaximumMainDeckCount = 10,
            MaximumDigitamaDeckCount = 5
        }).Validate(deckList, banlist: banlist);
    }
}
