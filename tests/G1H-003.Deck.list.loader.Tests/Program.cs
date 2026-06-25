using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var fixtureRoot = Path.Combine(root, ".tmp", "g1h-003-deck-list-loader-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1H-003 goal row keeps the Deck list loader contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("DeckListLoader parses deck code sections and counts", ParsesDeckCodeSectionsAndCounts),
    ("DeckListLoader loads deck files with deterministic results", LoadsDeckFilesDeterministically),
    ("DeckListLoader rejects invalid entry with line diagnostics", RejectsInvalidEntryWithLineDiagnostics),
    ("DeckListLoader rejects invalid file entry with path diagnostics", RejectsInvalidFileEntryWithPathDiagnostics),
    ("Deck loader output verifies card count limits through DeckValidator", LoaderOutputVerifiesCardCountLimits),
    ("DeckListLoader async file load matches sync load", AsyncFileLoadMatchesSyncLoad),
    ("Deck loader source has no placeholder or Unity dependency", LoaderSourceHasNoPlaceholderOrUnityDependency),
    ("AS-IS deck loader references remain read-only inputs", AsIsReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1H-003")
        ?? throw new InvalidOperationException("G1H-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("DataLoading", Value(row, "area"), "area");
    AssertEqual("Deck list loader", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("decklist parsing contract", StringComparison.Ordinal), "scope");
    AssertEqual("DeckListLoader", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("deck code file invalid entry", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1H-003_deck_list_loader_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1H-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Deck loader", StringComparison.Ordinal), "completion gate");
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

Task ParsesDeckCodeSectionsAndCounts()
{
    string deckCode =
        """
        # exported deck
        [main]
        3 BT1-001
        BT1-002 x2
        BT1-003

        [digitama]
        4 BT1-004
        BT1-005 1
        """;

    DeckList deck = new DeckListLoader().ParseCode(deckCode, "fixture");

    AssertEqual("fixture", deck.Name, "deck name");
    AssertEqual(6, deck.MainDeckCount, "main count");
    AssertEqual(5, deck.DigitamaDeckCount, "digitama count");
    AssertSequence(new[] { "BT1-001", "BT1-002", "BT1-003" }, deck.MainDeck.Select(e => e.CardId.Value).ToArray(), "main order");
    AssertSequence(new[] { 3, 2, 1 }, deck.MainDeck.Select(e => e.Count).ToArray(), "main counts");
    AssertSequence(new[] { "BT1-004", "BT1-005" }, deck.DigitamaDeck.Select(e => e.CardId.Value).ToArray(), "digitama order");
    return Task.CompletedTask;
}

Task LoadsDeckFilesDeterministically()
{
    string path = WriteFixture(
        "deterministic.deck",
        """
        [digitama]
        BT1-004 1
        [main]
        BT1-001 2
        BT1-002 1
        """);

    var loader = new DeckListLoader();
    DeckList first = loader.LoadFile(path);
    DeckList second = loader.LoadFile(path);

    AssertEqual("deterministic", first.Name, "default name");
    AssertEqual(first.MainDeckCount, second.MainDeckCount, "repeat main count");
    AssertSequence(first.MainDeck.Select(e => e.CardId.Value).ToArray(), second.MainDeck.Select(e => e.CardId.Value).ToArray(), "repeat main order");
    AssertSequence(first.DigitamaDeck.Select(e => e.CardId.Value).ToArray(), second.DigitamaDeck.Select(e => e.CardId.Value).ToArray(), "repeat digitama order");
    return Task.CompletedTask;
}

Task RejectsInvalidEntryWithLineDiagnostics()
{
    string deckCode =
        """
        [main]
        BT1-001 2
        BT1-002 many
        """;

    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => new DeckListLoader().ParseCode(deckCode, "bad"));
    AssertTrue(ex.Message.Contains("line 3", StringComparison.Ordinal), "line number diagnostic");
    AssertTrue(ex.Message.Contains("count is invalid", StringComparison.Ordinal), "invalid count diagnostic");

    InvalidDataException zero = ExpectThrows<InvalidDataException>(() => new DeckListLoader().ParseCode("BT1-001 0", "zero"));
    AssertTrue(zero.Message.Contains("positive", StringComparison.Ordinal), "zero count diagnostic");

    InvalidDataException tooMany = ExpectThrows<InvalidDataException>(() => new DeckListLoader().ParseCode("BT1-001 2 extra", "too-many"));
    AssertTrue(tooMany.Message.Contains("too many fields", StringComparison.Ordinal), "too many fields diagnostic");
    return Task.CompletedTask;
}

Task RejectsInvalidFileEntryWithPathDiagnostics()
{
    string path = WriteFixture(
        "invalid-file.deck",
        """
        [main]
        BT1-001 1
        BT1-002 many
        """);

    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => new DeckListLoader().LoadFile(path));
    AssertTrue(ex.Message.Contains(path, StringComparison.Ordinal), "path diagnostic");
    AssertTrue(ex.Message.Contains("line 3", StringComparison.Ordinal), "line diagnostic");
    return Task.CompletedTask;
}

Task LoaderOutputVerifiesCardCountLimits()
{
    DeckList deck = new DeckListLoader().ParseCode(
        """
        [main]
        BT1-001 5
        BT1-002 1
        """,
        "limit");

    var repository = new InMemoryCardRepository();
    repository.Upsert(CreateCard("BT1-001"));
    repository.Upsert(CreateCard("BT1-002"));

    DeckValidationResult result = new DeckValidator().Validate(deck, repository);

    AssertFalse(result.IsValid, "deck should fail default 4-card limit");
    AssertEqual(1, result.ErrorCount, "limit error count");
    AssertTrue(result.Issues[0].Message.Contains("exceeds limit", StringComparison.Ordinal), "limit message");
    AssertEqual(new HeadlessEntityId("BT1-001"), result.Issues[0].CardId, "limit card id");
    return Task.CompletedTask;
}

async Task AsyncFileLoadMatchesSyncLoad()
{
    string path = WriteFixture(
        "async.deck",
        """
        [main]
        BT1-001 2
        [digitama]
        BT1-004 1
        """);

    var loader = new DeckListLoader();
    DeckList sync = loader.LoadFile(path);
    DeckList async = await loader.LoadFileAsync(path);

    AssertEqual(sync.MainDeckCount, async.MainDeckCount, "async main count");
    AssertEqual(sync.DigitamaDeckCount, async.DigitamaDeckCount, "async digitama count");
    AssertSequence(sync.MainDeck.Select(e => e.CardId.Value).ToArray(), async.MainDeck.Select(e => e.CardId.Value).ToArray(), "async main order");
}

Task LoaderSourceHasNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "DeckListLoader.cs"),
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
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckData.cs"),
            new[] { "DeckData", "IsValidDeckCode", "DeckCardIDs", "DigitamaDeckCardIDs" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckCodeUtility.cs"),
            new[] { "DeckCodeUtility", "GetDeckBuilderDeckCode", "CardID" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
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
    string path = Path.Combine(fixtureRoot, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
}

static CardRecord CreateCard(string id)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        id,
        new Dictionary<string, object?>(),
        "Digimon",
        3,
        1,
        null,
        null);
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
