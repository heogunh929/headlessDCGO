using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// G6-003: real card-stat data loads from the embedded CardBaseEntity JSON into a CardDatabase, with the
// stats landing on CardRecord (CardType / PlayCost / EvolutionCost) and Metadata (color / set / level / dp).

var tests = new (string Name, Action Body)[]
{
    ("Embedded card data loads a full card pool", LoadsFullPool),
    ("ST1_07 Greymon: Digimon, play 5 / evo 2, DP 4000, level 4, Red", Greymon),
    ("ST1_01 Koromon: DigiEgg, no play cost, level 2", Koromon),
    ("ST1_16 Gaia Force: Option, play 8", GaiaForce),
    ("ST1_12 Tai Kamiya: Tamer, play 2", Tai),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
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

void LoadsFullPool()
{
    CardDatabase db = CardBaseEntityLoader.CreateDatabase();
    AssertTrue(db.Count > 8000, $"loaded a full card pool (got {db.Count})");
}

void Greymon()
{
    CardRecord c = Card("ST1_07");
    AssertEqual("Greymon", c.Name, "name");
    AssertEqual("Digimon", c.CardType, "card type");
    AssertEqual(5, c.PlayCost, "play cost");
    AssertEqual(2, c.EvolutionCost, "evolution cost");
    AssertEqual(4000, Meta(c, "dp"), "DP");
    AssertEqual(4, Meta(c, "level"), "level");
    AssertEqual("Red", c.Metadata["color"], "color");
}

void Koromon()
{
    CardRecord c = Card("ST1_01");
    AssertEqual("DigiEgg", c.CardType, "card type");
    AssertEqual(null, c.PlayCost, "DigiEgg has no play cost");
    AssertEqual(2, Meta(c, "level"), "level");
    AssertEqual(0, Meta(c, "dp"), "DP");
}

void GaiaForce()
{
    CardRecord c = Card("ST1_16");
    AssertEqual("Option", c.CardType, "card type");
    AssertEqual(8, c.PlayCost, "play cost");
}

void Tai()
{
    CardRecord c = Card("ST1_12");
    AssertEqual("Tamer", c.CardType, "card type");
    AssertEqual(2, c.PlayCost, "play cost");
}

// --- Helpers -------------------------------------------------------------

CardRecord Card(string number)
{
    CardDatabase db = CardBaseEntityLoader.CreateDatabase();
    AssertTrue(db.TryGetCard(new HeadlessEntityId(number), out CardRecord? card) && card is not null, $"{number} present");
    return card!;
}

static int Meta(CardRecord c, string key) =>
    c.Metadata.TryGetValue(key, out object? raw) && raw is int i ? i : throw new InvalidOperationException($"meta '{key}' missing/not int");

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
