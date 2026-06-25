using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2C-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS Player zone ownership references are recorded", AsIsPlayerZoneOwnershipReferencesAreRecorded),
    ("Player zone adapter reads deck hand field trash security ownership", AdapterReadsPlayerOwnedZones),
    ("Player zone adapter locates card owner zone and index", AdapterLocatesCardOwnerZoneAndIndex),
    ("Player zone adapter applies owner checked zone mutations", AdapterAppliesOwnerCheckedMutation),
    ("Player zone adapter rejects non-owner and invalid zone mutations without changing state", AdapterRejectsInvalidMutationsWithoutChangingState),
    ("Player zone adapter rejects mismatched existing zone ownership", AdapterRejectsMismatchedExistingZoneOwnership),
    ("Player zone adapter exposes deck loss state deterministically", AdapterExposesDeckLossStateDeterministically),
    ("G2C-001 source files contain no placeholder TODOs", PlayerZoneAdapterFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2C-001")
        ?? throw new InvalidOperationException("G2C-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("Player", Value(row, "area"), "area");
    AssertEqual("Player zone adapter", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "zone owner", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2C-001_player_zone_ownership_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2B-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2B-001_gamecontext_state_accessor_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2B-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsPlayerZoneOwnershipReferencesAreRecorded()
{
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));

    AssertContains(player, "public List<CardSource> LibraryCards", "AS-IS library zone");
    AssertContains(player, "public List<CardSource> HandCards", "AS-IS hand zone");
    AssertContains(player, "public List<CardSource> TrashCards", "AS-IS trash zone");
    AssertContains(player, "public List<CardSource> SecurityCards", "AS-IS security zone");
    AssertContains(player, "public Permanent[] FieldPermanents", "AS-IS field zone");
    AssertContains(player, "GetBattleAreaPermanents", "AS-IS battle area ownership");
    AssertContains(player, "GetBreedingAreaPermanents", "AS-IS breeding area ownership");
    AssertContains(player, "MaxMemoryCost", "AS-IS memory ownership logic");
    AssertContains(player, "CanHatch", "AS-IS digitama ownership logic");
    return Task.CompletedTask;
}

Task AdapterReadsPlayerOwnedZones()
{
    PlayerZoneAdapter adapter = new(CreateState());
    PlayerZoneOwnershipSnapshot p1 = adapter.ReadPlayer(new HeadlessPlayerId(1));

    AssertEqual(5, p1.Memory, "memory");
    AssertEqual(1, p1.LibraryCount, "library count");
    AssertEqual(1, p1.HandCount, "hand count");
    AssertEqual(1, p1.BattleAreaCount, "battle count");
    AssertEqual(1, p1.BreedingAreaCount, "breeding count");
    AssertEqual(2, p1.FieldCount, "field count battle plus breeding");
    AssertEqual(1, p1.TrashCount, "trash count");
    AssertEqual(1, p1.SecurityCount, "security count");
    AssertEqual(1, p1.DigitamaLibraryCount, "digitama count");
    AssertSequence(new[] { "p1-library" }, p1.Zone(ChoiceZone.Library).CardIds.Select(id => id.Value).ToArray(), "library ids");
    AssertSequence(new[] { "p1-battle" }, p1.Zone(ChoiceZone.BattleArea).CardIds.Select(id => id.Value).ToArray(), "battle ids");
    AssertSequence(new[] { "p1-breeding" }, p1.Zone(ChoiceZone.BreedingArea).CardIds.Select(id => id.Value).ToArray(), "breeding ids");
    AssertEqual(new HeadlessPlayerId(1), adapter.GetZone(new HeadlessPlayerId(1), ChoiceZone.Hand).OwnerId, "zone owner");
    return Task.CompletedTask;
}

Task AdapterLocatesCardOwnerZoneAndIndex()
{
    PlayerZoneAdapter adapter = new(CreateState());

    PlayerZoneCardLocation hand = adapter.LocateCard(new HeadlessEntityId("p1-hand"));
    AssertEqual(new HeadlessPlayerId(1), hand.ZoneOwnerId, "hand zone owner");
    AssertEqual(new HeadlessPlayerId(1), hand.CardOwnerId, "hand card owner");
    AssertEqual(ChoiceZone.Hand, hand.Zone, "hand zone");
    AssertEqual(0, hand.Index, "hand index");
    AssertTrue(hand.IsInOwnerZone, "hand in owner zone");

    AssertFalse(adapter.TryLocateCard(new HeadlessEntityId("p1-floating"), out PlayerZoneCardLocation? missing), "floating location");
    AssertEqual(null, missing, "missing location value");
    return Task.CompletedTask;
}

Task AdapterAppliesOwnerCheckedMutation()
{
    PlayerZoneAdapter adapter = new(CreateState());
    HeadlessEntityId cardId = new("p1-hand");

    PlayerZoneAdapter moved = adapter.ApplyPlayerMutation(new ZoneMoveRequest(
        new HeadlessPlayerId(1),
        cardId,
        ChoiceZone.Hand,
        ChoiceZone.Trash));

    AssertSequence(new[] { "p1-trash", "p1-hand" }, moved.GetZone(new HeadlessPlayerId(1), ChoiceZone.Trash).CardIds.Select(id => id.Value).ToArray(), "trash after mutation");
    AssertSequence(Array.Empty<string>(), moved.GetZone(new HeadlessPlayerId(1), ChoiceZone.Hand).CardIds.Select(id => id.Value).ToArray(), "hand after mutation");
    AssertSequence(new[] { "p1-hand" }, adapter.GetZone(new HeadlessPlayerId(1), ChoiceZone.Hand).CardIds.Select(id => id.Value).ToArray(), "original hand unchanged");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidMutationsWithoutChangingState()
{
    PlayerZoneAdapter adapter = new(CreateState());
    string before = adapter.State.ComputeFingerprint();

    ExpectThrows<InvalidOperationException>(() => adapter.ApplyPlayerMutation(new ZoneMoveRequest(
        new HeadlessPlayerId(2),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash)));
    ExpectThrows<ArgumentException>(() => adapter.ApplyPlayerMutation(new ZoneMoveRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Clock)));
    ExpectThrows<ArgumentException>(() => adapter.GetZone(new HeadlessPlayerId(1), ChoiceZone.Execution));
    ExpectThrows<InvalidOperationException>(() => adapter.ApplyPlayerMutation(new ZoneMoveRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Security,
        ChoiceZone.Trash)));

    AssertEqual(before, adapter.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertSequence(new[] { "p1-hand" }, adapter.GetZone(new HeadlessPlayerId(1), ChoiceZone.Hand).CardIds.Select(id => id.Value).ToArray(), "hand unchanged");
    return Task.CompletedTask;
}

Task AdapterRejectsMismatchedExistingZoneOwnership()
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    HeadlessEntityId card = new("p1-card");

    MatchState invalid = MatchState.CreateInitial(new[] { p1, p2 })
        .WithCardInstance(new CardInstanceState(card, new HeadlessEntityId("def-p1-card"), p1));
    invalid = invalid with
    {
        Players = invalid.Players
            .Select(player => player.PlayerId == p2
                ? player.WithZone(ChoiceZone.Hand, new[] { card })
                : player)
            .ToArray()
    };

    ExpectThrows<InvalidOperationException>(() => new PlayerZoneAdapter(invalid));
    return Task.CompletedTask;
}

Task AdapterExposesDeckLossStateDeterministically()
{
    PlayerZoneAdapter first = new(CreateState(emptyP2Library: true));
    PlayerZoneAdapter second = new(CreateState(emptyP2Library: true));

    PlayerZoneOwnershipSnapshot p2 = first.ReadPlayer(new HeadlessPlayerId(2));
    AssertTrue(p2.IsDeckEmpty, "p2 deck empty");
    AssertTrue(first.WouldDeckOutOnDraw(new HeadlessPlayerId(2), 1), "p2 draw one deck out");
    AssertFalse(first.WouldDeckOutOnDraw(new HeadlessPlayerId(2), 0), "p2 draw zero deck out");
    ExpectThrows<ArgumentOutOfRangeException>(() => first.WouldDeckOutOnDraw(new HeadlessPlayerId(2), -1));

    AssertSequence(Flatten(first), Flatten(second), "deterministic adapter snapshot");
    return Task.CompletedTask;
}

Task PlayerZoneAdapterFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "PlayerZoneAdapter.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static MatchState CreateState(bool emptyP2Library = false)
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    MatchState state = new MatchState(new[]
    {
        new PlayerState(p1, Memory: 5),
        new PlayerState(p2, Memory: -2)
    });

    foreach (var card in CardFixture(p1, p2, emptyP2Library))
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.Id), new HeadlessEntityId($"def-{card.Id}"), card.Owner))
            .PlaceCard(new HeadlessEntityId(card.Id), card.Zone);
    }

    state = state
        .WithCardInstance(new CardInstanceState(new HeadlessEntityId("p1-floating"), new HeadlessEntityId("def-p1-floating"), p1))
        .WithCardInstance(new CardInstanceState(new HeadlessEntityId("p2-floating"), new HeadlessEntityId("def-p2-floating"), p2));

    return state;
}

static IReadOnlyList<(string Id, HeadlessPlayerId Owner, ChoiceZone Zone)> CardFixture(
    HeadlessPlayerId p1,
    HeadlessPlayerId p2,
    bool emptyP2Library)
{
    var cards = new List<(string Id, HeadlessPlayerId Owner, ChoiceZone Zone)>
    {
        ("p1-library", p1, ChoiceZone.Library),
        ("p1-hand", p1, ChoiceZone.Hand),
        ("p1-battle", p1, ChoiceZone.BattleArea),
        ("p1-breeding", p1, ChoiceZone.BreedingArea),
        ("p1-trash", p1, ChoiceZone.Trash),
        ("p1-security", p1, ChoiceZone.Security),
        ("p1-digitama", p1, ChoiceZone.DigitamaLibrary),
        ("p2-hand", p2, ChoiceZone.Hand),
        ("p2-battle", p2, ChoiceZone.BattleArea),
        ("p2-trash", p2, ChoiceZone.Trash),
        ("p2-security", p2, ChoiceZone.Security),
        ("p2-digitama", p2, ChoiceZone.DigitamaLibrary),
    };

    if (!emptyP2Library)
    {
        cards.Add(("p2-library", p2, ChoiceZone.Library));
    }

    return cards;
}

static IReadOnlyList<string> Flatten(PlayerZoneAdapter adapter)
{
    return adapter.ReadAllPlayers()
        .SelectMany(player => player.Zones.Select(zone =>
            $"{player.PlayerId.Value}:{player.Memory}:{zone.Zone}:{zone.Count}:{string.Join("|", zone.CardIds.Select(id => id.Value))}"))
        .ToArray();
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}
