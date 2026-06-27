using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-4: the original shuffles both decks at game start (RandomUtility.ShuffledDeckCards for every deck).
// MatchSetupConfig.Create now defaults shuffleDecks/shuffleDigitamaDecks to TRUE. Shuffle is seeded, so
// a fixed seed is still fully reproducible; deterministic scenario setups opt out with shuffleDecks:false.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Default setup shuffles the deck (opening hand differs from deck-list order)", DefaultShuffles),
    ("Shuffle is deterministic for the same seed", ShuffleIsSeedDeterministic),
    ("Different seeds produce different opening hands", DifferentSeedsDiffer),
    ("shuffleDecks:false preserves the deck-list order", OptOutPreservesOrder),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task DefaultShuffles()
{
    // Default Create => shuffle on. With a 30-card main deck the chance the shuffle reproduces the exact
    // deck-list opening hand is negligible, so the hand should differ from the unshuffled order.
    string shuffled = await OpeningHand(seed: 123, shuffle: true);
    string ordered = await OpeningHand(seed: 123, shuffle: false);

    AssertNotEqual(ordered, shuffled, "default-shuffled opening hand differs from deck-list order");
}

async Task ShuffleIsSeedDeterministic()
{
    string first = await OpeningHand(seed: 77, shuffle: true);
    string second = await OpeningHand(seed: 77, shuffle: true);

    AssertEqual(first, second, "same seed => identical shuffled hand");
}

async Task DifferentSeedsDiffer()
{
    string a = await OpeningHand(seed: 1, shuffle: true);
    string b = await OpeningHand(seed: 999, shuffle: true);

    AssertNotEqual(a, b, "different seeds => different opening hands");
}

async Task OptOutPreservesOrder()
{
    string ordered = await OpeningHand(seed: 50, shuffle: false);

    AssertEqual("P1-M01,P1-M02,P1-M03,P1-M04,P1-M05", ordered, "opt-out keeps deck-list order");
}

// --- Helpers -------------------------------------------------------------

async Task<string> OpeningHand(int seed, bool shuffle)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 30; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1,
        shuffleDecks: shuffle,
        shuffleDigitamaDecks: shuffle);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));

    return string.Join(",", ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(P1, ChoiceZone.Hand)
        .Select(id => DefinitionOf(match, id)));
}

static string DefinitionOf(DcgoMatch match, HeadlessEntityId instanceId) =>
    match.Context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? record) && record is not null
        ? record.DefinitionId.Value
        : instanceId.Value;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 30).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 5).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Assertions ----------------------------------------------------------

static void AssertEqual(string expected, string actual, string label)
{
    if (expected != actual) throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertNotEqual(string notExpected, string actual, string label)
{
    if (notExpected == actual) throw new InvalidOperationException($"{label}: expected a different value than '{actual}'.");
}
