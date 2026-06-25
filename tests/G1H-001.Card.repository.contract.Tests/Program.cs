using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1H-001 goal row keeps the card repository contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("CardRecord exposes gameplay lookup fields without Unity assets", CardRecordExposesLookupFields),
    ("ICardRepository get missing and query contracts are deterministic", RepositoryGetMissingQueryContracts),
    ("CardDatabase preserves repository contract through service interface", CardDatabasePreservesRepositoryContract),
    ("CardAssetJsonLoader fills CardRecord lookup fields", CardAssetJsonLoaderFillsLookupFields),
    ("Card repository contract source has no placeholder or Unity dependency", CardRepositorySourceHasNoPlaceholderOrUnityDependency),
    ("AS-IS card repository references remain read-only inputs", AsIsReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1H-001")
        ?? throw new InvalidOperationException("G1H-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("DataLoading", Value(row, "area"), "area");
    AssertEqual("Card repository contract", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("card lookup contract", StringComparison.Ordinal), "scope");
    AssertEqual("ICardRepository CardRecord", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("get missing query", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Card repository", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1B-001_stable_ids_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1B-001 COMPLETE");
    return Task.CompletedTask;
}

Task CardRecordExposesLookupFields()
{
    var metadata = new Dictionary<string, object?>
    {
        ["imagePath"] = "cards/bt1-010.png",
        ["rarity"] = "R",
    };

    var record = new CardRecord(
        new HeadlessEntityId(" card-bt1-010 "),
        " BT1-010 ",
        " Test Agumon ",
        metadata,
        " Digimon ",
        3,
        2,
        "red level 2",
        "bt1-010-main");

    metadata["rarity"] = "C";
    metadata["prefabPath"] = "prefabs/card.prefab";

    AssertEqual("card-bt1-010", record.Id.Value, "id trim");
    AssertEqual("BT1-010", record.CardNumber, "card number trim");
    AssertEqual("Test Agumon", record.Name, "name trim");
    AssertEqual("Digimon", record.CardType, "type trim");
    AssertEqual(3, record.PlayCost, "play cost");
    AssertEqual(2, record.EvolutionCost, "evolution cost");
    AssertEqual("red level 2", record.EvolutionCondition, "evolution condition");
    AssertEqual("bt1-010-main", record.EffectBindingKey, "effect key");
    AssertEqual("R", record.Metadata["rarity"], "metadata snapshot");
    AssertFalse(record.Metadata.ContainsKey("prefabPath"), "metadata mutation isolation");

    ExpectThrows<ArgumentOutOfRangeException>(() => new CardRecord(new HeadlessEntityId("bad-play"), "BAD", "Bad", new Dictionary<string, object?>(), PlayCost: -1));
    ExpectThrows<ArgumentOutOfRangeException>(() => new CardRecord(new HeadlessEntityId("bad-evo"), "BAD", "Bad", new Dictionary<string, object?>(), EvolutionCost: -1));
    ExpectThrows<ArgumentNullException>(() => new CardRecord(new HeadlessEntityId("bad-metadata"), "BAD", "Bad", null!));
    return Task.CompletedTask;
}

Task RepositoryGetMissingQueryContracts()
{
    var repository = new InMemoryCardRepository();
    CardRecord option = CreateCard("card-option", "BT1-099", "Test Option", "Option", 2, null, null, "option-effect");
    CardRecord agumonAlt = CreateCard("card-agumon-alt", "BT1-010", "Agumon Alt", "Digimon", 3, 2, "red level 2", "agumon-alt");
    CardRecord agumon = CreateCard("card-agumon", "BT1-009", "Agumon", "Digimon", 3, 2, "red level 2", "agumon-main");

    repository.Upsert(option);
    repository.Upsert(agumonAlt);
    repository.Upsert(agumon);

    AssertSame(agumon, repository.GetCard(new HeadlessEntityId("card-agumon")), "get existing");
    AssertTrue(repository.TryGetCard(new HeadlessEntityId("card-option"), out CardRecord? foundOption), "try get existing");
    AssertSame(option, foundOption!, "try get existing instance");
    AssertFalse(repository.TryGetCard(new HeadlessEntityId("missing-card"), out CardRecord? missing), "try get missing");
    AssertEqual(null, missing, "missing out value");

    KeyNotFoundException ex = ExpectThrows<KeyNotFoundException>(() => repository.GetCard(new HeadlessEntityId("missing-card")));
    AssertTrue(ex.Message.Contains("missing-card", StringComparison.Ordinal), "missing exception includes id");

    IReadOnlyList<CardRecord> digimon = repository.Query(new CardQuery(CardType: "digimon"));
    AssertSequence(new[] { "BT1-009", "BT1-010" }, digimon.Select(card => card.CardNumber).ToArray(), "query type deterministic order");

    IReadOnlyList<CardRecord> named = repository.Query(new CardQuery(NameContains: "agu"));
    AssertSequence(new[] { "card-agumon", "card-agumon-alt" }, named.Select(card => card.Id.Value).ToArray(), "query name deterministic order");

    IReadOnlyList<CardRecord> effect = repository.Query(new CardQuery(EffectBindingKey: "option-effect"));
    AssertSequence(new[] { "card-option" }, effect.Select(card => card.Id.Value).ToArray(), "query effect key");

    IReadOnlyList<CardRecord> first = repository.Query(CardQuery.All);
    IReadOnlyList<CardRecord> second = repository.Query(CardQuery.All);
    AssertSequence(first.Select(card => card.Id.Value).ToArray(), second.Select(card => card.Id.Value).ToArray(), "repeat query deterministic");

    IReadOnlyList<CardRecord> snapshot = repository.Snapshot();
    repository.Upsert(CreateCard("later-card", "BT9-999", "Later", "Tamer", 4, null, null, null));
    AssertEqual(3, snapshot.Count, "snapshot isolation");
    ExpectThrows<ArgumentNullException>(() => repository.Query(null!));
    ExpectThrows<ArgumentNullException>(() => repository.Upsert(null!));
    return Task.CompletedTask;
}

Task CardDatabasePreservesRepositoryContract()
{
    var database = new CardDatabase();
    ICardRepository repository = database;
    CardRecord card = CreateCard("db-card", "BT2-001", "Database Card", "Digimon", 4, 1, "blue level 3", "db-effect");

    database.Upsert(card);

    AssertEqual(1, database.Count, "database count");
    AssertSame(card, repository.GetCard(new HeadlessEntityId("db-card")), "database get through interface");
    AssertEqual(1, repository.Query(new CardQuery(CardNumber: "BT2-001")).Count, "database query through interface");
    database.Clear();
    AssertFalse(repository.TryGetCard(new HeadlessEntityId("db-card"), out _), "database clear");
    return Task.CompletedTask;
}

Task CardAssetJsonLoaderFillsLookupFields()
{
    string directory = Path.Combine(root, ".tmp", "g1h-001-card-repository-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    string cardPath = Path.Combine(directory, "BT1-010.json");
    File.WriteAllText(
        cardPath,
        """
        {
          "id": "card-bt1-010",
          "cardNumber": "BT1-010",
          "name": "Test Agumon",
          "cardType": "Digimon",
          "playCost": 3,
          "evolutionCost": "2",
          "evolutionCondition": "red level 2",
          "effectBindingKey": "bt1-010-main",
          "imagePath": "cards/bt1-010.png",
          "prefabPath": "prefabs/card.prefab"
        }
        """);

    var loader = new CardAssetJsonLoader();
    CardRecord record = loader.LoadFile(cardPath);

    AssertEqual(new HeadlessEntityId("card-bt1-010"), record.Id, "json id");
    AssertEqual("BT1-010", record.CardNumber, "json card number");
    AssertEqual("Test Agumon", record.Name, "json name");
    AssertEqual("Digimon", record.CardType, "json type");
    AssertEqual(3, record.PlayCost, "json play cost");
    AssertEqual(2, record.EvolutionCost, "json evolution cost");
    AssertEqual("red level 2", record.EvolutionCondition, "json evolution condition");
    AssertEqual("bt1-010-main", record.EffectBindingKey, "json effect key");
    AssertEqual("cards/bt1-010.png", record.Metadata["imagePath"], "asset path is metadata only");
    AssertEqual("prefabs/card.prefab", record.Metadata["prefabPath"], "prefab path is metadata only");

    string invalidPath = Path.Combine(directory, "invalid.json");
    File.WriteAllText(invalidPath, """{ "id": "bad", "cardNumber": "BAD", "name": "Bad", "playCost": "free" }""");
    InvalidDataException ex = ExpectThrows<InvalidDataException>(() => loader.LoadFile(invalidPath));
    AssertTrue(ex.Message.Contains("playCost", StringComparison.Ordinal), "invalid integer diagnostic");
    return Task.CompletedTask;
}

Task CardRepositorySourceHasNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardRecord.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardQuery.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ICardRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryCardRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "CardDatabase.cs"),
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

static CardRecord CreateCard(
    string id,
    string cardNumber,
    string name,
    string cardType,
    int? playCost,
    int? evolutionCost,
    string? evolutionCondition,
    string? effectBindingKey)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        cardNumber,
        name,
        new Dictionary<string, object?>(),
        cardType,
        playCost,
        evolutionCost,
        evolutionCondition,
        effectBindingKey);
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

static void AssertSame(object expected, object actual, string message)
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected same instance.");
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
