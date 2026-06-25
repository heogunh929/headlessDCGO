using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2D-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS Card identity references are recorded", AsIsCardIdentityReferencesAreRecorded),
    ("Card identity adapter creates and binds instance owner definition token and location", AdapterCreatesAndBindsIdentity),
    ("Card identity adapter moves cards through player owned zones", AdapterMovesCardsThroughOwnedZones),
    ("Card identity adapter updates reveal hide suspend unsuspend state", AdapterUpdatesFaceAndSuspendState),
    ("Card identity adapter attaches detaches and clears owned sources", AdapterUpdatesSources),
    ("Card identity adapter rejects invalid source binding without changing state", AdapterRejectsInvalidSourceBindingWithoutChangingState),
    ("Card identity adapter exports repository record metadata", AdapterExportsRepositoryRecordMetadata),
    ("Card identity adapter rejects duplicate and invalid movement without changing state", AdapterRejectsInvalidIdentityMutationsWithoutChangingState),
    ("Card identity adapter snapshots are deterministic and source files contain no placeholder TODOs", AdapterSnapshotsAreDeterministicAndScopedFilesHaveNoTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2D-001")
        ?? throw new InvalidOperationException("G2D-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("CardController", Value(row, "area"), "area");
    AssertEqual("card identity adapter", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "CardController", "scope");
    AssertContains(Value(row, "unit_test_scope"), "card instance binding", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2D-001_card_identity_binding_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2C-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2C-001_player_zone_ownership_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2C-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsCardIdentityReferencesAreRecorded()
{
    string cardObjectController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(cardObjectController, "CreateCardSource", "AS-IS card creation");
    AssertContains(cardObjectController, "SetBaseData", "AS-IS definition and owner binding");
    AssertContains(cardObjectController, "SetUpCardIndex", "AS-IS unique card index binding");
    AssertContains(cardObjectController, "SetIsToken", "AS-IS token binding");
    AssertContains(cardObjectController, "ActiveCardList", "AS-IS active card identity list");
    AssertContains(cardObjectController, "RemoveFromAllArea", "AS-IS zone identity removal");
    AssertContains(cardObjectController, "AddSecurityCard", "AS-IS zone and face-up binding");
    AssertContains(cardController, "CardSource", "AS-IS CardSource identity");
    AssertContains(cardController, "CardID", "AS-IS card definition id");
    AssertContains(cardController, "AddTrashCard", "AS-IS trash movement");
    AssertContains(cardController, "IsExistOnTrash", "AS-IS identity after movement check");
    return Task.CompletedTask;
}

Task AdapterCreatesAndBindsIdentity()
{
    HeadlessPlayerId p1 = new(1);
    CardIdentityAdapter adapter = new(MatchState.CreateInitial(new[] { p1, new HeadlessPlayerId(2) }));
    adapter = adapter.CreateCard(new HeadlessEntityId("p1-card"), new HeadlessEntityId("BT1-001"), p1, ChoiceZone.Hand, isToken: true);

    CardIdentitySnapshot snapshot = adapter.Bind(new HeadlessEntityId("p1-card"));
    AssertEqual(new HeadlessEntityId("p1-card"), snapshot.InstanceId, "instance id");
    AssertEqual(new HeadlessEntityId("BT1-001"), snapshot.DefinitionId, "definition id");
    AssertEqual(p1, snapshot.OwnerId, "owner id");
    AssertEqual(ChoiceZone.Hand, snapshot.Zone, "zone");
    AssertEqual(0, snapshot.ZoneIndex, "zone index");
    AssertTrue(snapshot.IsToken, "token flag");
    AssertTrue(snapshot.IsInOwnerZone, "owner zone");
    AssertTrue(adapter.TryBind(new HeadlessEntityId("p1-card"), out CardIdentitySnapshot? trySnapshot), "try bind existing");
    AssertEqual(snapshot, trySnapshot, "try bind snapshot");
    AssertFalse(adapter.TryBind(new HeadlessEntityId("missing"), out CardIdentitySnapshot? missing), "try bind missing");
    AssertEqual(null, missing, "missing snapshot");
    return Task.CompletedTask;
}

Task AdapterMovesCardsThroughOwnedZones()
{
    HeadlessPlayerId p1 = new(1);
    CardIdentityAdapter adapter = new(CreateState());
    string before = adapter.State.ComputeFingerprint();

    CardIdentityAdapter moved = adapter.MoveCard(new ZoneMoveRequest(
        p1,
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash,
        FaceUp: true));

    CardIdentitySnapshot snapshot = moved.Bind(new HeadlessEntityId("p1-hand"));
    AssertEqual(ChoiceZone.Trash, snapshot.Zone, "moved zone");
    AssertEqual(1, snapshot.ZoneIndex, "trash appends after existing card");
    AssertTrue(snapshot.IsFaceUp, "face up on move");
    AssertEqual(ChoiceZone.Hand, adapter.Bind(new HeadlessEntityId("p1-hand")).Zone, "original adapter unchanged");
    AssertFalse(before == moved.State.ComputeFingerprint(), "moved fingerprint changed");
    return Task.CompletedTask;
}

Task AdapterUpdatesFaceAndSuspendState()
{
    CardIdentityAdapter adapter = new(CreateState());
    HeadlessEntityId cardId = new("p1-hand");

    CardIdentitySnapshot updated = adapter
        .Reveal(cardId)
        .Suspend(cardId)
        .Bind(cardId);

    AssertTrue(updated.IsFaceUp, "revealed");
    AssertTrue(updated.IsSuspended, "suspended");

    CardIdentitySnapshot reset = adapter
        .Reveal(cardId)
        .Suspend(cardId)
        .Hide(cardId)
        .Unsuspend(cardId)
        .Bind(cardId);

    AssertFalse(reset.IsFaceUp, "hidden");
    AssertFalse(reset.IsSuspended, "unsuspended");
    AssertFalse(adapter.Bind(cardId).IsFaceUp, "original face unchanged");
    AssertFalse(adapter.Bind(cardId).IsSuspended, "original suspend unchanged");
    return Task.CompletedTask;
}

Task AdapterUpdatesSources()
{
    CardIdentityAdapter adapter = new(CreateState());
    HeadlessEntityId target = new("p1-battle");
    HeadlessEntityId sourceA = new("p1-hand");
    HeadlessEntityId sourceB = new("p1-trash");

    CardIdentitySnapshot attached = adapter
        .AttachSource(target, sourceA)
        .AttachSource(target, sourceA)
        .AttachSource(target, sourceB)
        .Bind(target);

    AssertSequence(new[] { "p1-hand", "p1-trash" }, attached.SourceIds.Select(id => id.Value).ToArray(), "attached sources");

    CardIdentitySnapshot detached = adapter
        .AttachSource(target, sourceA)
        .AttachSource(target, sourceB)
        .DetachSource(target, sourceA)
        .Bind(target);

    AssertSequence(new[] { "p1-trash" }, detached.SourceIds.Select(id => id.Value).ToArray(), "detached sources");

    CardIdentitySnapshot cleared = adapter
        .AttachSource(target, sourceA)
        .AttachSource(target, sourceB)
        .ClearSources(target)
        .Bind(target);

    AssertEqual(0, cleared.SourceIds.Count, "cleared sources");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidSourceBindingWithoutChangingState()
{
    CardIdentityAdapter adapter = new(CreateState());
    string before = adapter.State.ComputeFingerprint();
    HeadlessEntityId target = new("p1-battle");

    ExpectThrows<InvalidOperationException>(() => adapter.AttachSource(target, target));
    ExpectThrows<InvalidOperationException>(() => adapter.AttachSource(target, new HeadlessEntityId("p2-hand")));
    ExpectThrows<InvalidOperationException>(() => adapter.AttachSource(target, new HeadlessEntityId("missing-source")));
    ExpectThrows<InvalidOperationException>(() => adapter.DetachSource(target, new HeadlessEntityId("p1-hand")));
    AssertEqual(before, adapter.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertEqual(0, adapter.Bind(target).SourceIds.Count, "sources unchanged");
    return Task.CompletedTask;
}

Task AdapterExportsRepositoryRecordMetadata()
{
    HeadlessPlayerId p1 = new(1);
    CardIdentityAdapter adapter = new CardIdentityAdapter(MatchState.CreateInitial(new[] { p1, new HeadlessPlayerId(2) }))
        .CreateCard(new HeadlessEntityId("token-1"), new HeadlessEntityId("BT1-TOKEN"), p1, ChoiceZone.BattleArea, isToken: true, faceUp: true)
        .Suspend(new HeadlessEntityId("token-1"));

    InMemoryCardInstanceRepository repository = new();
    adapter.UpsertRecord(repository, new HeadlessEntityId("token-1"));

    AssertTrue(repository.TryGetInstance(new HeadlessEntityId("token-1"), out CardInstanceRecord? record), "repository upsert");
    AssertEqual(new HeadlessEntityId("BT1-TOKEN"), record!.DefinitionId, "record definition");
    AssertEqual(p1, record.OwnerId, "record owner");
    AssertTrue(record.IsToken, "record token");
    AssertEqual(ChoiceZone.BattleArea.ToString(), record.Metadata["zone"], "record zone");
    AssertEqual(0, record.Metadata["zoneIndex"], "record zone index");
    AssertEqual(true, record.Metadata["isSuspended"], "record suspended");
    AssertEqual(true, record.Metadata["isFaceUp"], "record face up");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidIdentityMutationsWithoutChangingState()
{
    CardIdentityAdapter adapter = new(CreateState());
    string before = adapter.State.ComputeFingerprint();

    ExpectThrows<InvalidOperationException>(() => adapter.CreateCard(
        new HeadlessEntityId("p1-hand"),
        new HeadlessEntityId("duplicate-def"),
        new HeadlessPlayerId(1),
        ChoiceZone.Hand));
    ExpectThrows<InvalidOperationException>(() => adapter.MoveCard(new ZoneMoveRequest(
        new HeadlessPlayerId(2),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash)));
    ExpectThrows<ArgumentException>(() => adapter.PlaceCard(new HeadlessEntityId("p1-hand"), ChoiceZone.Clock));
    ExpectThrows<InvalidOperationException>(() => adapter.Bind(new HeadlessEntityId("missing")));

    AssertEqual(before, adapter.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertEqual(ChoiceZone.Hand, adapter.Bind(new HeadlessEntityId("p1-hand")).Zone, "hand location unchanged");
    return Task.CompletedTask;
}

Task AdapterSnapshotsAreDeterministicAndScopedFilesHaveNoTodos()
{
    CardIdentityAdapter first = new CardIdentityAdapter(CreateState())
        .Reveal(new HeadlessEntityId("p1-battle"))
        .AttachSource(new HeadlessEntityId("p1-battle"), new HeadlessEntityId("p1-hand"));
    CardIdentityAdapter second = new CardIdentityAdapter(CreateState())
        .Reveal(new HeadlessEntityId("p1-battle"))
        .AttachSource(new HeadlessEntityId("p1-battle"), new HeadlessEntityId("p1-hand"));

    AssertEqual(first.State.ComputeFingerprint(), second.State.ComputeFingerprint(), "state fingerprint");
    CardIdentitySnapshot firstSnapshot = first.Bind(new HeadlessEntityId("p1-battle"));
    CardIdentitySnapshot secondSnapshot = second.Bind(new HeadlessEntityId("p1-battle"));
    AssertEqual(firstSnapshot.InstanceId, secondSnapshot.InstanceId, "snapshot instance");
    AssertEqual(firstSnapshot.DefinitionId, secondSnapshot.DefinitionId, "snapshot definition");
    AssertEqual(firstSnapshot.OwnerId, secondSnapshot.OwnerId, "snapshot owner");
    AssertEqual(firstSnapshot.Zone, secondSnapshot.Zone, "snapshot zone");
    AssertEqual(firstSnapshot.ZoneIndex, secondSnapshot.ZoneIndex, "snapshot zone index");
    AssertSequence(
        firstSnapshot.SourceIds.Select(id => id.Value).ToArray(),
        secondSnapshot.SourceIds.Select(id => id.Value).ToArray(),
        "snapshot sources");

    string adapterFile = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "CardIdentityAdapter.cs");
    AssertFalse(File.ReadAllText(adapterFile).Contains("TODO", StringComparison.OrdinalIgnoreCase), "adapter TODO");
    return Task.CompletedTask;
}

static MatchState CreateState()
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    MatchState state = MatchState.CreateInitial(new[] { p1, p2 });

    foreach (var card in new[]
    {
        ("p1-library", p1, ChoiceZone.Library),
        ("p1-hand", p1, ChoiceZone.Hand),
        ("p1-battle", p1, ChoiceZone.BattleArea),
        ("p1-trash", p1, ChoiceZone.Trash),
        ("p1-security", p1, ChoiceZone.Security),
        ("p2-hand", p2, ChoiceZone.Hand),
        ("p2-battle", p2, ChoiceZone.BattleArea)
    })
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.Item1), new HeadlessEntityId($"def-{card.Item1}"), card.Item2))
            .PlaceCard(new HeadlessEntityId(card.Item1), card.Item3);
    }

    return state;
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
