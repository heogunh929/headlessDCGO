namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// The official ST1/ST2/ST3 preconstructed starter decks (DigimonCard lists): 50 main-deck cards + 4
/// digi-egg (digitama) cards each. Canonical game data — a single source of truth so the self-play smoke,
/// rule audit, and rule-invariant gate exercise REAL starter-deck matchups instead of synthetic decks.
/// Card numbers use the engine's underscore form (ST1_01), matching cards.json / CardBaseEntity.
/// </summary>
public static class StarterDecks
{
    /// <summary>An expanded starter deck: each definition id repeated per its printed quantity.</summary>
    public sealed record StarterDeck(
        string Code,
        string Name,
        IReadOnlyList<HeadlessEntityId> MainDefinitions,
        IReadOnlyList<HeadlessEntityId> DigitamaDefinitions);

    // (number, count) quantities exactly per the official lists. _01 is the digi-egg; _02.._16 the main deck.
    private static readonly Dictionary<string, (string Name, (string Number, int Count)[] Main, (string Number, int Count)[] Digitama)> Lists =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ST1"] = ("ST-1 Gaia Red", new[]
            {
                ("ST1_02", 4), ("ST1_03", 4), ("ST1_04", 4), ("ST1_05", 4), ("ST1_06", 4), ("ST1_07", 2),
                ("ST1_08", 4), ("ST1_09", 4), ("ST1_10", 2), ("ST1_11", 2), ("ST1_12", 4), ("ST1_13", 4),
                ("ST1_14", 4), ("ST1_15", 2), ("ST1_16", 2),
            }, new[] { ("ST1_01", 4) }),

            ["ST2"] = ("ST-2 Cocytus Blue", new[]
            {
                ("ST2_02", 4), ("ST2_03", 4), ("ST2_04", 4), ("ST2_05", 4), ("ST2_06", 2), ("ST2_07", 4),
                ("ST2_08", 4), ("ST2_09", 4), ("ST2_10", 2), ("ST2_11", 2), ("ST2_12", 4), ("ST2_13", 4),
                ("ST2_14", 4), ("ST2_15", 2), ("ST2_16", 2),
            }, new[] { ("ST2_01", 4) }),

            ["ST3"] = ("ST-3 Heaven's Yellow", new[]
            {
                ("ST3_02", 4), ("ST3_03", 4), ("ST3_04", 4), ("ST3_05", 2), ("ST3_06", 4), ("ST3_07", 4),
                ("ST3_08", 4), ("ST3_09", 4), ("ST3_10", 2), ("ST3_11", 2), ("ST3_12", 4), ("ST3_13", 4),
                ("ST3_14", 2), ("ST3_15", 4), ("ST3_16", 2),
            }, new[] { ("ST3_01", 4) }),
        };

    /// <summary>The starter-deck codes available (ST1, ST2, ST3).</summary>
    public static IReadOnlyCollection<string> Codes => Lists.Keys;

    /// <summary>Resolve a starter deck by code (ST1/ST2/ST3), with each card expanded to its quantity.</summary>
    public static StarterDeck Get(string code)
    {
        if (!Lists.TryGetValue(code, out var list))
        {
            throw new ArgumentException($"Unknown starter deck code '{code}'. Known: {string.Join(", ", Codes)}.", nameof(code));
        }

        return new StarterDeck(code, list.Name, Expand(list.Main), Expand(list.Digitama));
    }

    private static IReadOnlyList<HeadlessEntityId> Expand((string Number, int Count)[] entries) =>
        entries.SelectMany(e => Enumerable.Repeat(new HeadlessEntityId(e.Number), e.Count)).ToArray();
}
