using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var fixtureRoot = Path.Combine(root, ".tmp", "g1h-002-card-json-loader-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1H-002 goal row keeps the Card JSON loader contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("CardAssetJsonLoader loads valid card JSON into required CardRecord fields", LoadsValidCardJson),
    ("CardAssetJsonLoader rejects missing required schema fields with diagnostics", RejectsMissingRequiredFields),
    ("CardAssetJsonLoader rejects invalid schema values with deterministic errors", RejectsInvalidSchemaValues),
    ("CardAssetJsonLoader loads directories in deterministic path order", LoadDirectoryIsDeterministic),
    ("CardAssetJsonLoader reports deterministic first failure for directory loads", DirectoryFailureIsDeterministic),
    ("CardAssetJsonLoader loads directory results into CardDatabase", LoadDirectoryIntoDatabase),
    ("Card JSON loader source has no placeholder or Unity dependency", LoaderSourceHasNoPlaceholderOrUnityDependency),
    ("AS-IS card JSON references remain read-only inputs", AsIsReferencesRemainReadOnlyInputs),
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

Task GoalRowKeepsExpectedContract()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1H-002")
        ?? throw new InvalidOperationException("G1H-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("DataLoading", Value(row, "area"), "area");
    AssertEqual("Card JSON loader", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("card JSON loading contract", StringComparison.Ordinal), "scope");
    AssertEqual("CardAssetJsonLoader", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("valid invalid schema deterministic failure", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1H-002_card_json_loader_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1H-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Card JSON loader", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1H-001_card_repository_contract_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1H-001 COMPLETE");
    return Task.CompletedTask;
}

Task LoadsValidCardJson()
{
    string path = WriteFixture(
        "valid-card.json",
        """
        {
          "id": "card-bt2-001",
          "cardNumber": " BT2-001 ",
          "name": " Test Gabumon ",
          "cardType": " Digimon ",
          "playCost": "3",
          "evolutionCost": 1,
          "evolutionCondition": "blue level 2",
          "effectBindingKey": "bt2-001-main",
          "imagePath": "cards/bt2-001.png",
          "audioPath": "audio/card.wav",
          "nested": { "dp": 2000 },
          "tags": ["rookie", "blue"]
        }
        """);

    CardRecord card = new CardAssetJsonLoader().LoadFile(path);

    AssertEqual(new HeadlessEntityId("card-bt2-001"), card.Id, "id");
    AssertEqual("BT2-001", card.CardNumber, "card number");
    AssertEqual("Test Gabumon", card.Name, "name");
    AssertEqual("Digimon", card.CardType, "card type");
    AssertEqual(3, card.PlayCost, "play cost");
    AssertEqual(1, card.EvolutionCost, "evolution cost");
    AssertEqual("blue level 2", card.EvolutionCondition, "evolution condition");
    AssertEqual("bt2-001-main", card.EffectBindingKey, "effect key");
    AssertEqual("cards/bt2-001.png", card.Metadata["imagePath"], "image path metadata");
    AssertEqual("audio/card.wav", card.Metadata["audioPath"], "audio path metadata");
    AssertEqual(path, card.Metadata["sourceFile"], "source file metadata");
    return Task.CompletedTask;
}

Task RejectsMissingRequiredFields()
{
    string missingName = WriteFixture(
        "missing-name.json",
        """{ "id": "card-missing-name", "cardNumber": "BT0-001", "cardType": "Option" }""");

    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => new CardAssetJsonLoader().LoadFile(missingName));
    AssertTrue(ex.Message.Contains("name", StringComparison.Ordinal), "missing field diagnostic");
    AssertTrue(ex.Message.Contains(missingName, StringComparison.Ordinal), "missing field source path");

    string missingId = WriteFixture(
        "missing-id.json",
        """{ "cardNumber": "BT0-002", "name": "Missing Id" }""");

    InvalidDataException idEx = ExpectThrows<InvalidDataException>(() => new CardAssetJsonLoader().LoadFile(missingId));
    AssertTrue(idEx.Message.Contains("id", StringComparison.Ordinal), "missing id diagnostic");
    return Task.CompletedTask;
}

Task RejectsInvalidSchemaValues()
{
    string invalidJson = WriteFixture("invalid-json.json", """{ "id": "broken", """);
    InvalidDataException jsonEx = ExpectThrows<InvalidDataException>(() => new CardAssetJsonLoader().LoadFile(invalidJson));
    AssertTrue(jsonEx.Message.Contains("not valid JSON", StringComparison.Ordinal), "json diagnostic");
    AssertTrue(jsonEx.Message.Contains(invalidJson, StringComparison.Ordinal), "json source path");

    string invalidCost = WriteFixture(
        "invalid-cost.json",
        """{ "id": "bad-cost", "cardNumber": "BAD-001", "name": "Bad Cost", "playCost": "free" }""");
    InvalidDataException costEx = ExpectThrows<InvalidDataException>(() => new CardAssetJsonLoader().LoadFile(invalidCost));
    AssertTrue(costEx.Message.Contains("playCost", StringComparison.Ordinal), "invalid cost diagnostic");

    string negativeCost = WriteFixture(
        "negative-cost.json",
        """{ "id": "negative-cost", "cardNumber": "BAD-002", "name": "Negative Cost", "playCost": -1 }""");
    InvalidDataException negativeEx = ExpectThrows<InvalidDataException>(() => new CardAssetJsonLoader().LoadFile(negativeCost));
    AssertTrue(negativeEx.Message.Contains("non-negative", StringComparison.Ordinal), "negative cost diagnostic");
    return Task.CompletedTask;
}

Task LoadDirectoryIsDeterministic()
{
    string directory = NewFixtureDirectory();
    WriteFile(
        Path.Combine(directory, "02-option.json"),
        """{ "id": "card-option", "cardNumber": "BT3-002", "name": "Option Card", "cardType": "Option" }""");
    WriteFile(
        Path.Combine(directory, "01-digimon.json"),
        """{ "id": "card-digimon", "cardNumber": "BT3-001", "name": "Digimon Card", "cardType": "Digimon" }""");

    var loader = new CardAssetJsonLoader();
    IReadOnlyList<CardRecord> first = loader.LoadDirectory(directory);
    IReadOnlyList<CardRecord> second = loader.LoadDirectory(directory);

    AssertSequence(new[] { "card-digimon", "card-option" }, first.Select(card => card.Id.Value).ToArray(), "directory order");
    AssertSequence(first.Select(card => card.Id.Value).ToArray(), second.Select(card => card.Id.Value).ToArray(), "repeat directory order");
    return Task.CompletedTask;
}

Task DirectoryFailureIsDeterministic()
{
    string directory = NewFixtureDirectory();
    string firstInvalid = WriteFile(
        Path.Combine(directory, "01-invalid.json"),
        """{ "id": "bad-first", "cardNumber": "BAD-001", "name": "Bad First", "playCost": "free" }""");
    WriteFile(
        Path.Combine(directory, "02-invalid.json"),
        """{ "id": "bad-second", "cardNumber": "BAD-002" }""");

    var loader = new CardAssetJsonLoader();
    InvalidDataException first = ExpectThrows<InvalidDataException>(() => loader.LoadDirectory(directory));
    InvalidDataException second = ExpectThrows<InvalidDataException>(() => loader.LoadDirectory(directory));

    AssertTrue(first.Message.Contains(firstInvalid, StringComparison.Ordinal), "first invalid path");
    AssertEqual(first.Message, second.Message, "repeat failure message");
    return Task.CompletedTask;
}

Task LoadDirectoryIntoDatabase()
{
    string directory = NewFixtureDirectory();
    WriteFile(
        Path.Combine(directory, "card.json"),
        """{ "id": "card-database", "cardNumber": "BT4-001", "name": "Database Card", "cardType": "Tamer" }""");

    var database = new CardDatabase();
    new CardAssetJsonLoader().LoadDirectoryInto(database, directory);

    AssertEqual(1, database.Count, "database count");
    AssertEqual("Database Card", database.GetCard(new HeadlessEntityId("card-database")).Name, "database card");
    return Task.CompletedTask;
}

Task LoaderSourceHasNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "CardAssetJsonLoader.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string text = File.ReadAllText(Path.Combine(root, relativeFile));
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), $"{relativeFile} TODO");
        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{relativeFile} UnityEngine");
        AssertFalse(text.Contains("Resources.", StringComparison.Ordinal), $"{relativeFile} Resources");
        AssertFalse(text.Contains("ScriptableObject", StringComparison.Ordinal), $"{relativeFile} ScriptableObject");
    }

    return Task.CompletedTask;
}

Task AsIsReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "CardBaseEntity"),
            Array.Empty<string>()),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckData.cs"),
            new[] { "DeckData", "CEntity_Base", "CardID" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckCodeUtility.cs"),
            new[] { "DeckCodeUtility", "GetDeckBuilderDeckCode", "CardID" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
        if (Directory.Exists(path))
        {
            AssertTrue(Directory.EnumerateFileSystemEntries(path).Any(), $"AS-IS directory has entries: {path}");
            continue;
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"AS-IS reference file was not found: {path}");
        }

        string text = File.ReadAllText(path);
        foreach (string pattern in patterns)
        {
            AssertTrue(text.Contains(pattern, StringComparison.Ordinal), $"{Path.GetFileName(path)} contains {pattern}");
        }
    }

    return Task.CompletedTask;
}

string WriteFixture(string relativePath, string content)
{
    return WriteFile(Path.Combine(fixtureRoot, relativePath), content);
}

static string WriteFile(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
}

string NewFixtureDirectory()
{
    string directory = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
}

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    if (lines.Length == 0)
    {
        return Array.Empty<Dictionary<string, string>>();
    }

    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] cells = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < cells.Length ? cells[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

static IEnumerable<string> SplitCsvLine(string line)
{
    var current = new List<char>();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Add('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (c == ',' && !inQuotes)
        {
            yield return new string(current.ToArray());
            current.Clear();
        }
        else
        {
            current.Add(c);
        }
    }

    yield return new string(current.ToArray());
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var docsPath = Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv");
        var srcPath = Path.Combine(current.FullName, "src", "HeadlessDCGO.Engine", "HeadlessDCGO.Engine.csproj");
        if (File.Exists(docsPath) && File.Exists(srcPath))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from the test binary path.");
}

static TException ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected true: {message}.");
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException($"Expected false: {message}.");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{message}: expected count {expected.Count}, actual {actual.Count}.");
    }

    for (int i = 0; i < expected.Count; i++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
        {
            throw new InvalidOperationException($"{message}: index {i} expected {expected[i]}, actual {actual[i]}.");
        }
    }
}
