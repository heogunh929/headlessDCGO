using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// G7-002: the card data loader now carries traits (type/attribute/form), full evolution conditions
// (from-color @ level : cost) and multi-color, in addition to the core stats.

var tests = new (string Name, Action Body)[]
{
    ("ST1_07 Greymon carries trait + evolution condition", GreymonTraits),
    ("Multi-color cards expose all their colors", MultiColor),
    ("Evolution conditions populate CardRecord.EvolutionCondition", EvoCondition),
    ("Real-card CardTraits reads forms+attributes+types (RD-TRAITS-KEY accessor witness)", RealCardTraitsAccessor),
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

// RD-TRAITS-KEY: the CardSource.CardTraits accessor (the one real card logic queries via EqualsTraits/
// ContainsTraits) must fold the loader's forms+attributes+types metadata — the AS-IS Form_ENG⧺Attribute_ENG⧺
// Type_ENG. Before the fix it read a lone "traits" key that the cards.json loader never writes, so every real
// (loaded) card returned an EMPTY trait set. This drives the accessor on a genuinely-loaded card, not the raw
// metadata keys (which GreymonTraits already covers).
void RealCardTraitsAccessor()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 1);
    CardDatabase real = CardBaseEntityLoader.CreateDatabase();
    AssertTrue(real.TryGetCard(new HeadlessEntityId("ST1_07"), out CardRecord? rec) && rec is not null, "ST1_07 loaded");
    ((CardDatabase)ctx.CardRepository).Upsert(rec!);   // Id is already "ST1_07" — the instance's definition id

    var owner = new HeadlessPlayerId(1);
    var instId = new HeadlessEntityId("inst:ST1_07");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(instId, rec!.Id, owner));
    var card = new Cec.CardSource(ctx, instId, owner, owner);

    string[] traits = card.CardTraits.ToArray();
    AssertContains(traits, "Dinosaur", "CardTraits type (folded from \"types\")");
    AssertContains(traits, "Vaccine", "CardTraits attribute (folded from \"attributes\")");
    AssertContains(traits, "Champion", "CardTraits form (folded from \"forms\")");
    AssertTrue(card.EqualsTraits("Dinosaur"), "EqualsTraits(\"Dinosaur\") true on a real loaded card");
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
