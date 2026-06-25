using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId AgumonId = new("agumon-instance");
HeadlessEntityId GabumonId = new("gabumon-instance");
HeadlessEntityId OptionId = new("option-instance");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3D-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS name color trait references are recorded", AsIsNameColorTraitReferencesAreRecorded),
    ("Name requirement supports exact and contains matching", NameRequirementSupportsExactAndContains),
    ("Color requirement supports any and all matching", ColorRequirementSupportsAnyAndAll),
    ("Trait requirement supports exact contains and grouped traits", TraitRequirementSupportsExactContainsAndGroups),
    ("Combined requirements fail on first missing requirement with values", CombinedRequirementsFailWithValues),
    ("Modifier requirements override card definition attributes", ModifiersOverrideDefinitionAttributes),
    ("Invalid source and definition return explicit no match", InvalidSourceAndDefinitionReturnNoMatch),
    ("Requirement results are deterministic", RequirementResultsAreDeterministic),
    ("G3D-002 source files stay inside name color trait scope", SourceFilesStayInsideGoalScope),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

Task GoalRowAndPredecessorAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3D-002")
        ?? throw new InvalidOperationException("G3D-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Requirements", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "이름 색 특성", "scope");
    AssertEqual("requirement helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "name color trait", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3D-002_name_color_trait_requirements_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3D-001", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "requirement", "completion gate");

    AssertComplete("G3D-001_minmax_dp_cost_level_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsNameColorTraitReferencesAreRecorded()
{
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));
    string addDigivolutionRequirement = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory", "AddDigivolutionRequirement.cs"));
    string partition = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Partition.cs"));
    string changeNames = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffects", "ChangeCardNamesClass.cs"));
    string changeColors = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffects", "ChangeCardColorClass.cs"));
    string changeTraits = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffects", "ChangeTraitsClass.cs"));

    AssertContains(cardSource, "public bool EqualsCardName", "AS-IS name check");
    AssertContains(cardSource, "public bool HasCardColor", "AS-IS color check");
    AssertContains(cardSource, "public bool EqualsTraits", "AS-IS trait exact check");
    AssertContains(cardSource, "public bool ContainsTraits", "AS-IS trait contains check");
    AssertContains(addDigivolutionRequirement, "permanent.TopCard.CardColors.Contains(cardColor)", "AS-IS evolution color requirement");
    AssertContains(partition, "source.EqualsCardName", "AS-IS partition name requirement");
    AssertContains(changeNames, "ChangeCardNames", "AS-IS name modifier");
    AssertContains(changeColors, "GetCardColors", "AS-IS color modifier");
    AssertContains(changeTraits, "ChangTraits", "AS-IS trait modifier");
    return Task.CompletedTask;
}

Task NameRequirementSupportsExactAndContains()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    CardRequirementResult exact = CardRequirementHelpers.HasName(state, AgumonId, definitions, "Agumon");
    CardRequirementResult normalized = CardRequirementHelpers.HasName(state, AgumonId, definitions, "Agu mon");
    CardRequirementResult contains = CardRequirementHelpers.HasName(
        state,
        AgumonId,
        definitions,
        "Grey",
        CardRequirementTextMode.Contains);
    CardRequirementResult miss = CardRequirementHelpers.HasName(state, GabumonId, definitions, "Agumon");

    AssertTrue(exact.IsMatch, "exact name");
    AssertTrue(normalized.IsMatch, "normalized exact name");
    AssertTrue(contains.IsMatch, "contains name");
    AssertFalse(miss.IsMatch, "wrong name");
    AssertContains(miss.Reason, "Name", "name miss reason");
    return Task.CompletedTask;
}

Task ColorRequirementSupportsAnyAndAll()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    CardRequirementResult red = CardRequirementHelpers.HasColor(state, AgumonId, definitions, "Red");
    CardRequirementResult all = CardRequirementHelpers.HasAllColors(state, AgumonId, definitions, new[] { "Red", "Blue" });
    CardRequirementResult missingAll = CardRequirementHelpers.HasAllColors(state, AgumonId, definitions, new[] { "Red", "Green" });
    CardRequirementResult optionBlue = CardRequirementHelpers.HasColor(state, OptionId, definitions, "Blue");

    AssertTrue(red.IsMatch, "red color");
    AssertTrue(all.IsMatch, "all colors");
    AssertFalse(missingAll.IsMatch, "missing all color");
    AssertTrue(optionBlue.IsMatch, "option color");
    AssertEqual("All", missingAll.Values["ColorQuantifier"], "quantifier");
    return Task.CompletedTask;
}

Task TraitRequirementSupportsExactContainsAndGroups()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    CardRequirementResult exact = CardRequirementHelpers.HasTrait(state, AgumonId, definitions, "Dinosaur");
    CardRequirementResult contains = CardRequirementHelpers.HasTrait(
        state,
        AgumonId,
        definitions,
        "saur",
        CardRequirementTextMode.Contains);
    CardRequirementResult miss = CardRequirementHelpers.HasTrait(state, GabumonId, definitions, "Dinosaur");
    var profile = new CardRequirementProfile(
        AgumonId,
        new HeadlessEntityId("BT1-001"),
        new[] { "Agumon" },
        new[] { "Red" },
        new[] { "Reptile", "Dinosaur" });

    AssertTrue(exact.IsMatch, "exact trait");
    AssertTrue(contains.IsMatch, "contains trait");
    AssertFalse(miss.IsMatch, "wrong trait");
    AssertTrue(CardRequirementHelpers.HasGroupedTrait(profile, "Dragon"), "group trait");
    return Task.CompletedTask;
}

Task CombinedRequirementsFailWithValues()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();
    var request = new CardRequirementRequest(
        state,
        AgumonId,
        definitions,
        new[]
        {
            CardRequirement.Name("Agumon"),
            new CardRequirement(CardRequirementKind.Color, new[] { "Green" }),
            CardRequirement.Trait("Dinosaur"),
        });

    CardRequirementResult result = CardRequirementHelpers.Evaluate(request);

    AssertFalse(result.IsMatch, "combined miss");
    AssertContains(result.Reason, "Color", "first miss reason");
    if (result.Values["ColorRequired"] is not string[] requiredColors)
    {
        throw new InvalidOperationException("ColorRequired was not recorded as a string array.");
    }

    if (result.Values["ColorAvailable"] is not string[] availableColors)
    {
        throw new InvalidOperationException("ColorAvailable was not recorded as a string array.");
    }

    AssertSequence(new[] { "Green" }, requiredColors, "required color");
    AssertSequence(new[] { "Blue", "Red" }, availableColors, "available colors");
    return Task.CompletedTask;
}

Task ModifiersOverrideDefinitionAttributes()
{
    MatchState state = CreateState(new Dictionary<HeadlessEntityId, IReadOnlyDictionary<string, object?>>
    {
        [AgumonId] = new Dictionary<string, object?>
        {
            [CardRequirementHelpers.CardNamesKey] = new[] { "Altered Agumon" },
            [CardRequirementHelpers.CardColorsKey] = new[] { "Purple" },
            [CardRequirementHelpers.CardTraitsKey] = new[] { "Wizard" },
        },
    });
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    AssertTrue(CardRequirementHelpers.HasName(state, AgumonId, definitions, "Altered Agumon").IsMatch, "modified name");
    AssertFalse(CardRequirementHelpers.HasName(state, AgumonId, definitions, "Agumon").IsMatch, "base name replaced");
    AssertTrue(CardRequirementHelpers.HasColor(state, AgumonId, definitions, "Purple").IsMatch, "modified color");
    AssertFalse(CardRequirementHelpers.HasColor(state, AgumonId, definitions, "Red").IsMatch, "base color replaced");
    AssertTrue(CardRequirementHelpers.HasTrait(state, AgumonId, definitions, "Wizard").IsMatch, "modified trait");
    AssertFalse(CardRequirementHelpers.HasTrait(state, AgumonId, definitions, "Dinosaur").IsMatch, "base trait replaced");
    return Task.CompletedTask;
}

Task InvalidSourceAndDefinitionReturnNoMatch()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();
    var withoutAgumon = definitions
        .Where(pair => pair.Key != new HeadlessEntityId("BT1-001"))
        .ToDictionary(pair => pair.Key, pair => pair.Value);

    CardRequirementResult missingSource = CardRequirementHelpers.HasName(
        state,
        new HeadlessEntityId("missing"),
        definitions,
        "Agumon");
    CardRequirementResult missingDefinition = CardRequirementHelpers.HasName(
        state,
        AgumonId,
        withoutAgumon,
        "Agumon");

    AssertFalse(missingSource.IsMatch, "missing source");
    AssertContains(missingSource.Reason, "not found", "missing source reason");
    AssertFalse(missingDefinition.IsMatch, "missing definition");
    AssertContains(missingDefinition.Reason, "definition", "missing definition reason");
    return Task.CompletedTask;
}

Task RequirementResultsAreDeterministic()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();
    var request = new CardRequirementRequest(
        state,
        AgumonId,
        definitions,
        new[]
        {
            CardRequirement.Name("Grey", CardRequirementTextMode.Contains),
            new CardRequirement(CardRequirementKind.Color, new[] { "Blue", "Red" }, CardRequirementQuantifier.All),
            CardRequirement.Trait("saur", CardRequirementTextMode.Contains),
        });

    string first = Signature(CardRequirementHelpers.Evaluate(request));
    string second = Signature(CardRequirementHelpers.Evaluate(request));

    AssertEqual(first, second, "signature");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "CardRequirementHelpers.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "helper must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "helper must not reference MonoBehaviour");
    AssertFalse(text.Contains("Hashtable", StringComparison.Ordinal), "helper must not reference Hashtable");
    AssertFalse(text.Contains("PlayCost", StringComparison.Ordinal), "helper must not implement next cost scope");
    AssertFalse(text.Contains("TargetFilter", StringComparison.Ordinal), "helper must not implement target filtering scope");
    AssertContains(text, "CardRequirementKind.Name", "name kind");
    AssertContains(text, "CardRequirementKind.Color", "color kind");
    AssertContains(text, "CardRequirementKind.Trait", "trait kind");
    return Task.CompletedTask;
}

MatchState CreateState(IReadOnlyDictionary<HeadlessEntityId, IReadOnlyDictionary<string, object?>>? modifiers = null)
{
    IReadOnlyDictionary<string, object?> Mods(HeadlessEntityId id)
    {
        return modifiers is not null && modifiers.TryGetValue(id, out IReadOnlyDictionary<string, object?>? found)
            ? found
            : new Dictionary<string, object?>();
    }

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [AgumonId] = new CardInstanceState(AgumonId, new HeadlessEntityId("BT1-001"), PlayerOne, Modifiers: Mods(AgumonId)),
        [GabumonId] = new CardInstanceState(GabumonId, new HeadlessEntityId("BT1-002"), PlayerOne, Modifiers: Mods(GabumonId)),
        [OptionId] = new CardInstanceState(OptionId, new HeadlessEntityId("BT1-003"), PlayerOne, Modifiers: Mods(OptionId)),
    };

    return new MatchState(new[] { new PlayerState(PlayerOne) }, cards);
}

IReadOnlyDictionary<HeadlessEntityId, CardRecord> CreateDefinitions()
{
    var agumon = new CardRecord(
        new HeadlessEntityId("BT1-001"),
        "BT1-001",
        "Agumon",
        new Dictionary<string, object?>
        {
            [CardRequirementHelpers.CardNamesKey] = new[] { "Agumon", "Greymon" },
            [CardRequirementHelpers.CardColorsKey] = "Red,Blue",
            [CardRequirementHelpers.CardTraitsKey] = new[] { "Reptile", "Dinosaur" },
        },
        CardType: "Digimon",
        PlayCost: 3);
    var gabumon = new CardRecord(
        new HeadlessEntityId("BT1-002"),
        "BT1-002",
        "Gabumon",
        new Dictionary<string, object?>
        {
            [CardRequirementHelpers.CardColorsKey] = new[] { "Blue" },
            [CardRequirementHelpers.CardTraitsKey] = new[] { "Beast", "Animal" },
        },
        CardType: "Digimon",
        PlayCost: 3);
    var option = new CardRecord(
        new HeadlessEntityId("BT1-003"),
        "BT1-003",
        "Blue Option",
        new Dictionary<string, object?>
        {
            [CardRequirementHelpers.CardColorsKey] = new[] { "Blue" },
            [CardRequirementHelpers.TraitsKey] = "Option",
        },
        CardType: "Option",
        PlayCost: 2);

    return new[] { agumon, gabumon, option }.ToDictionary(record => record.Id, record => record);
}

string Signature(CardRequirementResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsMatch, result.Reason, values);
}

string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        string[] strings => string.Join(";", strings),
        _ => value.ToString() ?? string.Empty,
    };
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    List<List<string>> records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException("CSV file was empty.");
    }

    string[] headers = records[0].ToArray();
    var rows = new List<Dictionary<string, string>>();
    foreach (List<string> record in records.Skip(1))
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            row[headers[index]] = index < record.Count ? record[index] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

static List<List<string>> ParseCsv(string text)
{
    var records = new List<List<string>>();
    var record = new List<string>();
    var field = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int index = 0; index < text.Length; index++)
    {
        char current = text[index];
        if (inQuotes)
        {
            if (current == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(current);
            }

            continue;
        }

        if (current == '"')
        {
            inQuotes = true;
        }
        else if (current == ',')
        {
            record.Add(field.ToString());
            field.Clear();
        }
        else if (current == '\r')
        {
            if (index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            AddRecord();
        }
        else if (current == '\n')
        {
            AddRecord();
        }
        else
        {
            field.Append(current);
        }
    }

    if (inQuotes)
    {
        throw new InvalidOperationException("CSV has an unterminated quoted field.");
    }

    if (field.Length > 0 || record.Count > 0)
    {
        AddRecord();
    }

    return records;

    void AddRecord()
    {
        record.Add(field.ToString());
        field.Clear();
        if (record.Any(value => value.Length > 0))
        {
            records.Add(record);
        }

        record = new List<string>();
    }
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value)
        ? value
        : throw new KeyNotFoundException($"CSV column was not found: {key}");
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{label}: expected count {expected.Count}, actual {actual.Count}.");
    }

    for (int index = 0; index < expected.Count; index++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
        {
            throw new InvalidOperationException($"{label}: index {index} expected '{expected[index]}', actual '{actual[index]}'.");
        }
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
