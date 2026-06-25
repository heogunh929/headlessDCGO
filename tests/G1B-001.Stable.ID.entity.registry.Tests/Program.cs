using System.Text.Json;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-001 goal row keeps the stable id registry contract", GoalRowKeepsExpectedContract),
    ("Headless ids preserve equality serialization and invalid value contracts", HeadlessIdsPreserveStableContracts),
    ("Card definition and instance records preserve metadata snapshots", CardRecordsPreserveMetadataSnapshots),
    ("HeadlessEntityRegistry enforces player definition and instance uniqueness", HeadlessEntityRegistryEnforcesUniqueness),
    ("Repositories preserve stable id lookup and immutable snapshots", RepositoriesPreserveStableLookupSnapshots),
    ("Zone mover preserves single-zone ordering and missing card failures", ZoneMoverPreservesOrderingAndFailures),
    ("Stable id registry source files no longer contain placeholder TODO contracts", StableIdFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-001")
        ?? throw new InvalidOperationException("G1B-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("HeadlessPlayerId HeadlessEntityId registry", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("id equality serialization uniqueness", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("stable id", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task HeadlessIdsPreserveStableContracts()
{
    var entityId = new HeadlessEntityId(" card-001 ");
    var sameEntityId = HeadlessEntityId.Parse("card-001");
    var otherEntityId = new HeadlessEntityId("card-002");

    AssertEqual("card-001", entityId.Value, "entity id trim");
    AssertEqual("card-001", entityId.ToString(), "entity id tostring");
    AssertEqual(entityId, sameEntityId, "entity id equality");
    AssertNotEqual(entityId, otherEntityId, "entity id inequality");

    var entitySet = new HashSet<HeadlessEntityId> { entityId, sameEntityId, otherEntityId };
    AssertEqual(2, entitySet.Count, "entity id hash uniqueness");
    AssertTrue(HeadlessEntityId.TryParse(" effect-1 ", out HeadlessEntityId parsedEntity), "entity id try parse");
    AssertEqual(new HeadlessEntityId("effect-1"), parsedEntity, "entity id parsed value");
    AssertFalse(HeadlessEntityId.TryParse(" ", out _), "entity id invalid try parse");

    string entityJson = JsonSerializer.Serialize(entityId);
    AssertEqual("\"card-001\"", entityJson, "entity id json");
    AssertEqual(entityId, JsonSerializer.Deserialize<HeadlessEntityId>(entityJson), "entity id json roundtrip");

    var playerId = new HeadlessPlayerId(2);
    AssertEqual("2", playerId.ToString(), "player id tostring");
    AssertEqual(playerId, HeadlessPlayerId.Parse("2"), "player id parse");
    AssertTrue(HeadlessPlayerId.TryParse("2", out HeadlessPlayerId parsedPlayer), "player id try parse");
    AssertEqual(playerId, parsedPlayer, "player id parsed value");
    AssertFalse(HeadlessPlayerId.TryParse("0", out _), "player id zero try parse");
    AssertEqual("2", JsonSerializer.Serialize(playerId), "player id json");
    AssertEqual(playerId, JsonSerializer.Deserialize<HeadlessPlayerId>("2"), "player id json roundtrip");

    ExpectThrows<ArgumentException>(() => new HeadlessEntityId(""));
    ExpectThrows<ArgumentOutOfRangeException>(() => new HeadlessPlayerId(0));
    ExpectThrows<FormatException>(() => HeadlessPlayerId.Parse("not-number"));
    ExpectThrows<JsonException>(() => JsonSerializer.Deserialize<HeadlessEntityId>("123"));
    return Task.CompletedTask;
}

Task CardRecordsPreserveMetadataSnapshots()
{
    var metadata = new Dictionary<string, object?>
    {
        ["level"] = 3
    };
    var card = new CardRecord(
        new HeadlessEntityId("BT1-001"),
        " BT1-001 ",
        " Agumon ",
        metadata);

    metadata["level"] = 4;
    metadata["color"] = "red";

    AssertEqual("BT1-001", card.CardNumber, "card number trim");
    AssertEqual("Agumon", card.Name, "card name trim");
    AssertEqual(3, card.Metadata["level"], "card metadata snapshot");
    AssertFalse(card.Metadata.ContainsKey("color"), "card metadata mutation isolation");

    var instanceMetadata = new Dictionary<string, object?>
    {
        ["source"] = "deck"
    };
    var instance = new CardInstanceRecord(
        new HeadlessEntityId("p1-main-001"),
        card.Id,
        new HeadlessPlayerId(1),
        Metadata: instanceMetadata);

    instanceMetadata["source"] = "hand";
    AssertEqual("deck", instance.Metadata["source"], "instance metadata snapshot");

    ExpectThrows<ArgumentException>(() => new CardRecord(default, "C", "Name", new Dictionary<string, object?>()));
    ExpectThrows<ArgumentException>(() => new CardInstanceRecord(default, card.Id, new HeadlessPlayerId(1)));
    ExpectThrows<ArgumentException>(() => new CardInstanceRecord(new HeadlessEntityId("i-1"), default, new HeadlessPlayerId(1)));
    ExpectThrows<ArgumentException>(() => new CardInstanceRecord(new HeadlessEntityId("i-1"), card.Id, default));
    return Task.CompletedTask;
}

Task HeadlessEntityRegistryEnforcesUniqueness()
{
    var registry = new HeadlessEntityRegistry();
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var cardA = CreateCard("card-a");
    var cardB = CreateCard("card-b");
    var instanceA = new CardInstanceRecord(new HeadlessEntityId("p1-card-a-001"), cardA.Id, playerOne);

    registry.RegisterPlayer(playerTwo);
    registry.RegisterPlayer(playerOne);
    registry.RegisterCardDefinition(cardB);
    registry.RegisterCardDefinition(cardA);
    registry.RegisterCardInstance(instanceA);

    AssertEqual(2, registry.PlayerCount, "registered player count");
    AssertEqual(2, registry.CardDefinitionCount, "registered card definition count");
    AssertEqual(1, registry.CardInstanceCount, "registered card instance count");
    AssertTrue(registry.ContainsPlayer(playerOne), "contains player one");
    AssertTrue(registry.TryGetCardDefinition(cardA.Id, out CardRecord? foundCard), "find card definition");
    AssertSame(cardA, foundCard!, "found card definition instance");
    AssertTrue(registry.TryGetCardInstance(instanceA.InstanceId, out CardInstanceRecord? foundInstance), "find card instance");
    AssertSame(instanceA, foundInstance!, "found card instance");

    HeadlessEntityRegistrySnapshot snapshot = registry.Snapshot();
    AssertEqual(playerOne, snapshot.Players[0], "snapshot deterministic player order");
    AssertEqual(cardA.Id, snapshot.CardDefinitions[0].Id, "snapshot deterministic card order");

    ExpectThrows<InvalidOperationException>(() => registry.RegisterPlayer(playerOne));
    ExpectThrows<InvalidOperationException>(() => registry.RegisterCardDefinition(cardA));
    ExpectThrows<InvalidOperationException>(() => registry.RegisterCardInstance(instanceA));
    ExpectThrows<InvalidOperationException>(() => registry.RegisterCardInstance(
        new CardInstanceRecord(new HeadlessEntityId("missing-owner-card"), cardA.Id, new HeadlessPlayerId(3))));
    ExpectThrows<InvalidOperationException>(() => registry.RegisterCardInstance(
        new CardInstanceRecord(new HeadlessEntityId("missing-definition-card"), new HeadlessEntityId("missing-definition"), playerOne)));
    return Task.CompletedTask;
}

Task RepositoriesPreserveStableLookupSnapshots()
{
    var definitionRepository = new InMemoryCardRepository();
    var card = CreateCard("stable-card");
    definitionRepository.Upsert(card);

    AssertTrue(definitionRepository.TryGetCard(new HeadlessEntityId("stable-card"), out CardRecord? foundCard), "definition lookup by equal id");
    AssertSame(card, foundCard!, "definition lookup instance");
    IReadOnlyList<CardRecord> definitionSnapshot = definitionRepository.Snapshot();
    definitionRepository.Upsert(CreateCard("later-card"));
    AssertEqual(1, definitionSnapshot.Count, "definition snapshot isolation");

    var instanceRepository = new InMemoryCardInstanceRepository();
    var instance = new CardInstanceRecord(
        new HeadlessEntityId("stable-instance"),
        card.Id,
        new HeadlessPlayerId(1));
    instanceRepository.Upsert(instance);

    AssertTrue(instanceRepository.TryGetInstance(new HeadlessEntityId("stable-instance"), out CardInstanceRecord? foundInstance), "instance lookup by equal id");
    AssertSame(instance, foundInstance!, "instance lookup instance");
    IReadOnlyList<CardInstanceRecord> instanceSnapshot = instanceRepository.Snapshot();
    instanceRepository.Upsert(new CardInstanceRecord(new HeadlessEntityId("later-instance"), card.Id, new HeadlessPlayerId(1)));
    AssertEqual(1, instanceSnapshot.Count, "instance snapshot isolation");
    return Task.CompletedTask;
}

async Task ZoneMoverPreservesOrderingAndFailures()
{
    var mover = new InMemoryZoneMover();
    var player = new HeadlessPlayerId(1);
    var cardOne = new HeadlessEntityId("card-1");
    var cardTwo = new HeadlessEntityId("card-2");

    await mover.MoveToDeckBottomAsync(player, cardOne);
    await mover.MoveToDeckBottomAsync(player, cardTwo);
    AssertSequence(new[] { cardOne, cardTwo }, mover.GetCards(player, ChoiceZone.Library), "library insertion order");

    IReadOnlyList<HeadlessEntityId> drawn = await mover.DrawAsync(player, 1);
    AssertSequence(new[] { cardOne }, drawn, "draw result order");
    AssertSequence(new[] { cardTwo }, mover.GetCards(player, ChoiceZone.Library), "library after draw");
    AssertSequence(new[] { cardOne }, mover.GetCards(player, ChoiceZone.Hand), "hand after draw");

    await mover.MoveAsync(new ZoneMoveRequest(player, cardOne, ChoiceZone.Hand, ChoiceZone.Trash));
    AssertEqual(0, mover.GetCards(player, ChoiceZone.Hand).Count, "hand after explicit move");
    AssertSequence(new[] { cardOne }, mover.GetCards(player, ChoiceZone.Trash), "trash after explicit move");

    await ExpectThrowsAsync<InvalidOperationException>(() =>
        mover.MoveAsync(new ZoneMoveRequest(player, cardTwo, ChoiceZone.Hand, ChoiceZone.Trash)));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(default, cardOne, ChoiceZone.Hand, ChoiceZone.Trash));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, default, ChoiceZone.Hand, ChoiceZone.Trash));
}

Task StableIdFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "HeadlessEntityId.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "HeadlessPlayerId.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "HeadlessEntityRegistry.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardRecord.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardInstanceRecord.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ICardRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ICardInstanceRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryCardRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryCardInstanceRepository.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IZoneMover.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ZoneMoveRequest.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryZoneMover.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "CardDatabase.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "CardAssetJsonLoader.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string path = Path.Combine(root, relativeFile);
        string text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{relativeFile} still contains a TODO placeholder.");
        }
    }

    return Task.CompletedTask;
}

static CardRecord CreateCard(string id)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id.ToUpperInvariant(),
        id,
        new Dictionary<string, object?>());
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    var records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException($"CSV file has no header row: {path}");
    }

    var headers = records[0];
    var rows = new List<Dictionary<string, string>>();
    foreach (var record in records.Skip(1))
    {
        if (record.Count != headers.Count)
        {
            throw new InvalidOperationException($"{path} has a row with {record.Count} fields; expected {headers.Count}.");
        }

        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            row[headers[i]] = record[i];
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
    var inQuotes = false;

    for (var i = 0; i < text.Length; i++)
    {
        var ch = text[i];
        if (inQuotes)
        {
            if (ch == '"')
            {
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(ch);
            }

            continue;
        }

        switch (ch)
        {
            case '"':
                inQuotes = true;
                break;
            case ',':
                record.Add(field.ToString());
                field.Clear();
                break;
            case '\r':
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                AddRecord();
                break;
            case '\n':
                AddRecord();
                break;
            default:
                field.Append(ch);
                break;
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

        if (record.Count > 1 || record[0].Length > 0)
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
        var docsPath = Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv");
        if (File.Exists(docsPath))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find docs/headless_complete_goal_breakdown.csv from the test binary path.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out var value)
        ? value
        : throw new InvalidOperationException($"Missing key '{key}'.");
}

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static async Task ExpectThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertNotEqual<T>(T unexpected, T actual, string label)
{
    if (EqualityComparer<T>.Default.Equals(unexpected, actual))
    {
        throw new InvalidOperationException($"{label}: value should not equal '{unexpected}'.");
    }
}

static void AssertSame<T>(T expected, T actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected the same instance.");
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
