using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var fixtureRoot = Path.Combine(root, ".tmp", "g1h-004-banlist-loader-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1H-004 goal row keeps the Banlist loader contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("BanlistLoader parses valid banlist code and limits", ParsesValidBanlistCode),
    ("BanlistLoader loads banlist files with deterministic results", LoadsBanlistFilesDeterministically),
    ("BanlistLoader rejects invalid entries with line diagnostics", RejectsInvalidEntriesWithLineDiagnostics),
    ("BanlistLoader rejects invalid file entries with path diagnostics", RejectsInvalidFileEntriesWithPathDiagnostics),
    ("Banlist applies limits through DeckValidator", BanlistAppliesLimitsThroughDeckValidator),
    ("BanlistLoader async file load matches sync load", AsyncFileLoadMatchesSyncLoad),
    ("Banlist loader source has no placeholder or Unity dependency", LoaderSourceHasNoPlaceholderOrUnityDependency),
    ("AS-IS banlist references remain read-only inputs", AsIsReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1H-004")
        ?? throw new InvalidOperationException("G1H-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("DataLoading", Value(row, "area"), "area");
    AssertEqual("Banlist loader", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("banlist loading contract", StringComparison.Ordinal), "scope");
    AssertEqual("BanlistLoader", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("valid invalid banlist", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1H-004_banlist_loader_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1H-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Banlist loader", StringComparison.Ordinal), "completion gate");
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

Task ParsesValidBanlistCode()
{
    string code =
        """
        # ruleset sample
        [limits]
        BT1-001 1
        0 BT1-002
        BT1-003
        BT1-004,2
        """;

    Banlist banlist = new BanlistLoader().ParseCode(code, "ruleset");

    AssertEqual("ruleset", banlist.Name, "name");
    AssertEqual(1, banlist.GetLimit(new HeadlessEntityId("BT1-001")), "trailing limit");
    AssertEqual(0, banlist.GetLimit(new HeadlessEntityId("BT1-002")), "leading banned limit");
    AssertEqual(0, banlist.GetLimit(new HeadlessEntityId("BT1-003")), "implicit banned limit");
    AssertEqual(2, banlist.GetLimit(new HeadlessEntityId("BT1-004")), "comma limit");
    AssertEqual(4, banlist.GetLimit(new HeadlessEntityId("BT1-999")), "default limit");
    AssertTrue(banlist.IsBanned(new HeadlessEntityId("BT1-002")), "banned");
    AssertFalse(banlist.IsBanned(new HeadlessEntityId("BT1-001")), "limited is not banned");
    return Task.CompletedTask;
}

Task LoadsBanlistFilesDeterministically()
{
    string path = WriteFixture(
        "deterministic.banlist",
        """
        [limits]
        BT1-001 1
        BT1-002 0
        BT1-003 2
        """);

    var loader = new BanlistLoader();
    Banlist first = loader.LoadFile(path);
    Banlist second = loader.LoadFile(path);

    AssertEqual("deterministic", first.Name, "default name");
    AssertEqual(first.GetLimit(new HeadlessEntityId("BT1-001")), second.GetLimit(new HeadlessEntityId("BT1-001")), "repeat limit");
    AssertSequence(
        first.Limits.OrderBy(p => p.Key.Value, StringComparer.Ordinal).Select(p => $"{p.Key.Value}:{p.Value}").ToArray(),
        second.Limits.OrderBy(p => p.Key.Value, StringComparer.Ordinal).Select(p => $"{p.Key.Value}:{p.Value}").ToArray(),
        "repeat limits");
    return Task.CompletedTask;
}

Task RejectsInvalidEntriesWithLineDiagnostics()
{
    string invalidLimit =
        """
        [limits]
        BT1-001 1
        BT1-002 many
        """;

    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => new BanlistLoader().ParseCode(invalidLimit, "bad"));
    AssertTrue(ex.Message.Contains("line 3", StringComparison.Ordinal), "line number diagnostic");
    AssertTrue(ex.Message.Contains("limit is invalid", StringComparison.Ordinal), "invalid limit diagnostic");

    InvalidDataException negative = ExpectThrows<InvalidDataException>(() => new BanlistLoader().ParseCode("BT1-001 -1", "negative"));
    AssertTrue(negative.Message.Contains("non-negative", StringComparison.Ordinal), "negative limit diagnostic");

    InvalidDataException tooMany = ExpectThrows<InvalidDataException>(() => new BanlistLoader().ParseCode("BT1-001 1 extra", "too-many"));
    AssertTrue(tooMany.Message.Contains("too many fields", StringComparison.Ordinal), "too many fields diagnostic");
    return Task.CompletedTask;
}

Task RejectsInvalidFileEntriesWithPathDiagnostics()
{
    string path = WriteFixture(
        "invalid-file.banlist",
        """
        [limits]
        BT1-001 1
        BT1-002 many
        """);

    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => new BanlistLoader().LoadFile(path));
    AssertTrue(ex.Message.Contains(path, StringComparison.Ordinal), "path diagnostic");
    AssertTrue(ex.Message.Contains("line 3", StringComparison.Ordinal), "line diagnostic");
    return Task.CompletedTask;
}

Task BanlistAppliesLimitsThroughDeckValidator()
{
    Banlist banlist = new BanlistLoader().ParseCode(
        """
        BT1-001 1
        BT1-002 0
        """,
        "limits");
    DeckList deck = new DeckListLoader().ParseCode(
        """
        [main]
        BT1-001 2
        BT1-002 1
        BT1-003 3
        """,
        "deck");

    var repository = new InMemoryCardRepository();
    repository.Upsert(CreateCard("BT1-001"));
    repository.Upsert(CreateCard("BT1-002"));
    repository.Upsert(CreateCard("BT1-003"));

    DeckValidationResult result = new DeckValidator().Validate(deck, repository, banlist);

    AssertFalse(result.IsValid, "deck should fail banlist limits");
    AssertEqual(2, result.ErrorCount, "banlist error count");
    AssertTrue(result.Issues.Any(issue => issue.CardId == new HeadlessEntityId("BT1-001") && issue.Message.Contains("2 > 1", StringComparison.Ordinal)), "limited card issue");
    AssertTrue(result.Issues.Any(issue => issue.CardId == new HeadlessEntityId("BT1-002") && issue.Message.Contains("1 > 0", StringComparison.Ordinal)), "banned card issue");
    return Task.CompletedTask;
}

async Task AsyncFileLoadMatchesSyncLoad()
{
    string path = WriteFixture(
        "async.banlist",
        """
        BT1-001 1
        BT1-002 0
        """);

    var loader = new BanlistLoader();
    Banlist sync = loader.LoadFile(path);
    Banlist async = await loader.LoadFileAsync(path);

    AssertEqual(sync.GetLimit(new HeadlessEntityId("BT1-001")), async.GetLimit(new HeadlessEntityId("BT1-001")), "async limit one");
    AssertEqual(sync.GetLimit(new HeadlessEntityId("BT1-002")), async.GetLimit(new HeadlessEntityId("BT1-002")), "async limit two");
}

Task LoaderSourceHasNoPlaceholderOrUnityDependency()
{
    string relativeFile = Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "BanlistLoader.cs");
    string text = File.ReadAllText(Path.Combine(root, relativeFile));
    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "BanlistLoader TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "BanlistLoader UnityEngine");
    AssertFalse(text.Contains("Resources.", StringComparison.Ordinal), "BanlistLoader Resources");
    AssertFalse(text.Contains("ScriptableObject", StringComparison.Ordinal), "BanlistLoader ScriptableObject");
    return Task.CompletedTask;
}

Task AsIsReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ContinuousController.cs"),
            new[] { "BanList", "LoadBanListOnline", "useBanlist" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameplayOption.cs"),
            new[] { "GameplayOption", "_banlistToggle", "OnUseBanlistToggleChanged" }),
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
