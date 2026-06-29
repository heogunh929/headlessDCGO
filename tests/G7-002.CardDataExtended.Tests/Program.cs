using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// G7-002: the card data loader now carries traits (type/attribute/form), full evolution conditions
// (from-color @ level : cost) and multi-color, in addition to the core stats.

var tests = new (string Name, Action Body)[]
{
    ("ST1_07 Greymon carries trait + evolution condition", GreymonTraits),
    ("Multi-color cards expose all their colors", MultiColor),
    ("Evolution conditions populate CardRecord.EvolutionCondition", EvoCondition),
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

void GreymonTraits()
{
    CardRecord c = Card("ST1_07");
    AssertContains(Strings(c, "types"), "Dinosaur", "type");
    AssertContains(Strings(c, "attributes"), "Vaccine", "attribute");
    AssertContains(Strings(c, "forms"), "Champion", "form");
    AssertContains(Strings(c, "colors"), "Red", "color");
}

void MultiColor()
{
    // Find any loaded card with more than one color.
    CardDatabase db = CardBaseEntityLoader.CreateDatabase();
    CardRecord? dual = db.Snapshot().FirstOrDefault(r =>
        r.Metadata.TryGetValue("colors", out object? raw) && raw is string[] cs && cs.Length > 1);
    AssertTrue(dual is not null, "at least one multi-color card loaded");
    AssertTrue(((string[])dual!.Metadata["colors"]!).Length > 1, "multi-color card exposes >1 color");
}

void EvoCondition()
{
    CardRecord c = Card("ST1_07");
    AssertTrue(!string.IsNullOrEmpty(c.EvolutionCondition), $"EvolutionCondition populated ('{c.EvolutionCondition}')");
    AssertTrue(c.EvolutionCondition!.Contains("Red@3:2"), $"Red level-3 cost-2 condition present ('{c.EvolutionCondition}')");
}

// --- Helpers -------------------------------------------------------------

CardRecord Card(string number)
{
    CardDatabase db = CardBaseEntityLoader.CreateDatabase();
    AssertTrue(db.TryGetCard(new HeadlessEntityId(number), out CardRecord? c) && c is not null, $"{number} present");
    return c!;
}

static string[] Strings(CardRecord c, string key) =>
    c.Metadata.TryGetValue(key, out object? raw) && raw is string[] arr ? arr : Array.Empty<string>();

static void AssertContains(string[] arr, string value, string label)
{
    if (!arr.Contains(value)) throw new InvalidOperationException($"{label}: expected '{value}' in [{string.Join(",", arr)}]");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
