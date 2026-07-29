// ============================================================================================================
// THE OFFICIAL ST1 / ST2 / ST3 PRECONSTRUCTED DECKS.
//
// WHY THIS FILE EXISTS. A match needs a deck, and the deck the original gets comes from the player's saved
// collection or a Photon room property — neither of which exists here. These three are the printed starter
// decks, so they are GAME DATA (a published card list), not a headless invention.
//
// WHAT IS DATA AND WHAT IS CODE. Only the numbers and quantities below were carried over from the retired
// hand-port tree; its code was not, and must not be — it was written against types that no longer exist and
// would be a self-citation loop besides. The lists are checked against the AS-IS card definitions instead:
// every entry resolves through `CardID`, and each deck comes to exactly 50 main + 4 digi-egg, which is what
// `DeckData.IsValidDeckData()` (DeckData.cs:689) requires.
//
// HOW A DECK REACHES THE ENGINE. The AS-IS `DeckData` does NOT take a list of ids — its constructor parses a
// COMPRESSED deck code (name, then base-256 card ids two characters per id, then run-length counts, then the
// same again for the digi-egg deck). Rather than reimplement that encoding, this builds the deck through the
// AS-IS encoder `DeckData.GetDeckCode(name, mainCards, digitamaCards, keyCard)` (DeckData.cs:509) and hands
// the result straight back to `new DeckData(code)`. The format therefore stays owned by the original: if the
// encoding ever changes, both directions change with it.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.DataLoading;

/// <summary>The printed starter decks, resolved against the AS-IS card definitions.</summary>
public static class StarterDeckCatalog
{
    private static readonly Dictionary<string, (string Name, (string Number, int Count)[] Main, (string Number, int Count)[] Digitama)> Lists =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ST1"] = ("ST-1 Gaia Red", new[]
            {
                ("ST1-02", 4), ("ST1-03", 4), ("ST1-04", 4), ("ST1-05", 4), ("ST1-06", 4), ("ST1-07", 2),
                ("ST1-08", 4), ("ST1-09", 4), ("ST1-10", 2), ("ST1-11", 2), ("ST1-12", 4), ("ST1-13", 4),
                ("ST1-14", 4), ("ST1-15", 2), ("ST1-16", 2),
            }, new[] { ("ST1-01", 4) }),

            ["ST2"] = ("ST-2 Cocytus Blue", new[]
            {
                ("ST2-02", 4), ("ST2-03", 4), ("ST2-04", 4), ("ST2-05", 4), ("ST2-06", 2), ("ST2-07", 4),
                ("ST2-08", 4), ("ST2-09", 4), ("ST2-10", 2), ("ST2-11", 2), ("ST2-12", 4), ("ST2-13", 4),
                ("ST2-14", 4), ("ST2-15", 2), ("ST2-16", 2),
            }, new[] { ("ST2-01", 4) }),

            ["ST3"] = ("ST-3 Heaven's Yellow", new[]
            {
                ("ST3-02", 4), ("ST3-03", 4), ("ST3-04", 4), ("ST3-05", 2), ("ST3-06", 4), ("ST3-07", 4),
                ("ST3-08", 4), ("ST3-09", 4), ("ST3-10", 2), ("ST3-11", 2), ("ST3-12", 4), ("ST3-13", 4),
                ("ST3-14", 2), ("ST3-15", 4), ("ST3-16", 2),
            }, new[] { ("ST3-01", 4) }),
        };

    /// <summary>The deck codes available.</summary>
    public static IReadOnlyCollection<string> Codes => Lists.Keys;

    /// <summary>Builds a <c>DeckData</c> for one starter deck out of the supplied card definitions, using the
    /// AS-IS encoder so the deck-code format stays owned by the original.</summary>
    public static DeckData Build(string code, IReadOnlyCollection<CEntity_Base> definitions)
    {
        if (!Lists.TryGetValue(code, out (string Name, (string Number, int Count)[] Main, (string Number, int Count)[] Digitama) list))
        {
            throw new ArgumentException($"Unknown starter deck '{code}'. Known: {string.Join(", ", Lists.Keys)}.", nameof(code));
        }

        Dictionary<string, CEntity_Base> byCardId = new(StringComparer.OrdinalIgnoreCase);

        foreach (CEntity_Base definition in definitions)
        {
            byCardId.TryAdd(definition.CardID, definition);
        }

        List<CEntity_Base> main = Expand(list.Main, byCardId, code);
        List<CEntity_Base> digitama = Expand(list.Digitama, byCardId, code);

        string deckCode = DeckData.GetDeckCode(list.Name, main, digitama, main[0]);

        return new DeckData(deckCode, code);
    }

    private static List<CEntity_Base> Expand(
        (string Number, int Count)[] entries,
        Dictionary<string, CEntity_Base> byCardId,
        string deck)
    {
        List<CEntity_Base> expanded = new();

        foreach ((string number, int count) in entries)
        {
            if (!byCardId.TryGetValue(number, out CEntity_Base? definition))
            {
                throw new InvalidOperationException(
                    $"Starter deck {deck} names card '{number}', which is not among the loaded definitions. "
                    + "Card definitions come from DCGO/Assets/CardBaseEntity — see CardEntityLoader.");
            }

            for (int copy = 0; copy < count; copy++)
            {
                expanded.Add(definition);
            }
        }

        return expanded;
    }
}
