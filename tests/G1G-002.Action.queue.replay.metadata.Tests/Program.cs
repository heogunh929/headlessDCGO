using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1G-002 goal row keeps the action queue replay contract", GoalRowKeepsExpectedContract),
    ("Predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("HeadlessActionQueue preserves FIFO legal action ordering", QueuePreservesLegalActionOrdering),
    ("HeadlessActionQueue records deterministic replay sequences", QueueRecordsDeterministicReplaySequences),
    ("ReplayActionRecord preserves immutable metadata snapshot", ReplayRecordPreservesMetadataSnapshot),
    ("ReplayActionRecord serializes and deserializes deterministic metadata", ReplayRecordSerializesAndDeserializes),
    ("Replay snapshot is isolated from later queue mutations", ReplaySnapshotIsIsolated),
    ("Clear resets queue count and replay sequence", ClearResetsQueueAndSequence),
    ("ReplayActionRecord rejects invalid sequence action and metadata", ReplayRecordRejectsInvalidValues),
    ("AS-IS action queue references remain read-only inputs", AsIsActionQueueReferencesRemainReadOnlyInputs),
    ("Action queue source has no placeholder or Unity dependency", ActionQueueSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1G-002")
        ?? throw new InvalidOperationException("G1G-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Session", Value(row, "area"), "area");
    AssertEqual("Action queue replay metadata", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("action queue", StringComparison.Ordinal), "scope");
    AssertEqual("HeadlessActionQueue ReplayActionRecord", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("ordering metadata serialization", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1G-002_action_queue_replay_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1G-001; G1A-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("action queue", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1G-001_player_session_model_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1A-003_action_contract_unit_test_results.md"),
    };

    foreach (string path in paths)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Predecessor result document was not found: {path}");
        }

        string text = File.ReadAllText(path);
        AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), $"{Path.GetFileName(path)} COMPLETE");
    }

    return Task.CompletedTask;
}

Task QueuePreservesLegalActionOrdering()
{
    var queue = new HeadlessActionQueue();
    LegalAction first = HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1");
    LegalAction second = HeadlessActionFactory.Pass(new HeadlessPlayerId(2), "action-2");

    queue.Enqueue(first);
    queue.Enqueue(second);

    AssertEqual(2, queue.Count, "count after enqueue");
    AssertTrue(queue.TryPeek(out LegalAction? peeked), "peek result");
    AssertSame(first, peeked, "peeked action");
    AssertTrue(queue.TryDequeue(out LegalAction? dequeuedFirst), "first dequeue result");
    AssertSame(first, dequeuedFirst, "first action");
    AssertTrue(queue.TryDequeue(out LegalAction? dequeuedSecond), "second dequeue result");
    AssertSame(second, dequeuedSecond, "second action");
    AssertFalse(queue.TryDequeue(out LegalAction? empty), "empty dequeue result");
    AssertEqual(null, empty, "empty action");
    return Task.CompletedTask;
}

Task QueueRecordsDeterministicReplaySequences()
{
    var queue = new HeadlessActionQueue();
    queue.Enqueue(HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1"));
    queue.Enqueue(HeadlessActionFactory.Pass(new HeadlessPlayerId(1), "action-2"));

    IReadOnlyList<ReplayActionRecord> records = queue.ReplaySnapshot();

    AssertEqual(2, records.Count, "record count");
    AssertEqual(0L, records[0].Sequence, "first sequence");
    AssertEqual(1L, records[1].Sequence, "second sequence");
    AssertEqual("local", records[0].SessionId, "default session");
    AssertEqual("action-1", records[0].Action.Id.Value, "first action id");
    AssertEqual("action-2", records[1].Action.Id.Value, "second action id");
    return Task.CompletedTask;
}

Task ReplayRecordPreservesMetadataSnapshot()
{
    var metadata = new Dictionary<string, object?>
    {
        [" turn "] = 2,
        ["reason"] = "test",
    };
    LegalAction action = HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1");

    var record = new ReplayActionRecord(7, action, " session-a ", metadata);
    metadata["turn"] = 99;
    metadata["mutated"] = true;

    AssertEqual(7L, record.Sequence, "sequence");
    AssertEqual("session-a", record.SessionId, "session trim");
    AssertSame(action, record.Action, "action");
    AssertEqual(2, record.Metadata["turn"], "metadata snapshot");
    AssertEqual("test", record.Metadata["reason"], "metadata reason");
    AssertFalse(record.Metadata.ContainsKey("mutated"), "metadata mutation isolation");
    return Task.CompletedTask;
}

Task ReplayRecordSerializesAndDeserializes()
{
    LegalAction action = HeadlessActionFactory.Create(
        "NoOp",
        new HeadlessPlayerId(1),
        "action-1",
        new Dictionary<string, object?>
        {
            ["z"] = 3,
            ["a"] = true,
        });
    var record = new ReplayActionRecord(
        3,
        action,
        "session-a",
        new Dictionary<string, object?>
        {
            ["turn"] = 2,
            ["note"] = "ok",
        });

    string serialized = record.Serialize();
    ReplayActionRecord deserialized = ReplayActionRecord.Deserialize(serialized);

    AssertTrue(serialized.Contains("\"sequence\":3", StringComparison.Ordinal), "serialized sequence");
    AssertTrue(serialized.IndexOf("\"a\"", StringComparison.Ordinal) < serialized.IndexOf("\"z\"", StringComparison.Ordinal), "deterministic parameter key order");
    AssertEqual(record.Sequence, deserialized.Sequence, "roundtrip sequence");
    AssertEqual(record.SessionId, deserialized.SessionId, "roundtrip session");
    AssertEqual(record.Action.Id, deserialized.Action.Id, "roundtrip action id");
    AssertEqual(record.Action.PlayerId, deserialized.Action.PlayerId, "roundtrip player");
    AssertEqual(record.Action.ActionType, deserialized.Action.ActionType, "roundtrip type");
    AssertEqual(true, deserialized.Action.Parameters["a"], "roundtrip bool parameter");
    AssertEqual(3, deserialized.Action.Parameters["z"], "roundtrip int parameter");
    AssertEqual(2, deserialized.Metadata["turn"], "roundtrip metadata int");
    AssertEqual("ok", deserialized.Metadata["note"], "roundtrip metadata string");
    return Task.CompletedTask;
}

Task ReplaySnapshotIsIsolated()
{
    var queue = new HeadlessActionQueue();
    queue.Enqueue(HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1"));

    IReadOnlyList<ReplayActionRecord> snapshot = queue.ReplaySnapshot();
    queue.Enqueue(HeadlessActionFactory.Pass(new HeadlessPlayerId(1), "action-2"));
    queue.TryDequeue(out _);

    AssertEqual(1, snapshot.Count, "snapshot count");
    AssertEqual("action-1", snapshot[0].Action.Id.Value, "snapshot action id");
    AssertEqual(1, queue.Count, "queue count after mutations");
    return Task.CompletedTask;
}

Task ClearResetsQueueAndSequence()
{
    var queue = new HeadlessActionQueue();
    queue.Enqueue(HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1"));
    queue.Enqueue(HeadlessActionFactory.Pass(new HeadlessPlayerId(1), "action-2"));

    queue.Clear();
    queue.Enqueue(HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-3"));

    AssertEqual(1, queue.Count, "count after reset");
    AssertEqual(0L, queue.ReplaySnapshot()[0].Sequence, "sequence after reset");
    return Task.CompletedTask;
}

Task ReplayRecordRejectsInvalidValues()
{
    LegalAction action = HeadlessActionFactory.NoOp(new HeadlessPlayerId(1), "action-1");

    ExpectThrows<ArgumentOutOfRangeException>(() => new ReplayActionRecord(-1, action));
    ExpectThrows<ArgumentNullException>(() => new ReplayActionRecord(0, null!));
    AssertEqual(0, new ReplayActionRecord(0, action, Metadata: null).Metadata.Count, "null metadata defaults to empty");
    ExpectThrows<ArgumentException>(() => new ReplayActionRecord(0, action, Metadata: new Dictionary<string, object?> { [" "] = 1 }));
    ExpectThrows<ArgumentException>(() => ReplayActionRecord.Deserialize(" "));
    ExpectThrows<ArgumentException>(() => ReplayActionRecord.Deserialize("{}"));
    ExpectThrows<ArgumentNullException>(() => new HeadlessActionQueue().Enqueue((LegalAction)null!));
    ExpectThrows<ArgumentNullException>(() => new HeadlessActionQueue().Enqueue((ReplayActionRecord)null!));
    return Task.CompletedTask;
}

Task AsIsActionQueueReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GManager.cs"),
            new[] { "GManager", "Photon", "Player" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "TurnStateMachine", "Queue" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"),
            new[] { "Player", "MainPhaseAction" }),
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

Task ActionQueueSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionQueue.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("HeadlessActionQueue.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "HeadlessActionQueue must not reference UnityEngine");
    AssertFalse(text.Contains("Photon", StringComparison.Ordinal), "HeadlessActionQueue must not reference Photon");
    AssertTrue(text.Contains("ReplayActionRecord", StringComparison.Ordinal), "replay record contract");
    AssertTrue(text.Contains("Serialize", StringComparison.Ordinal), "serialization contract");
    AssertTrue(text.Contains("ReplaySnapshot", StringComparison.Ordinal), "replay snapshot contract");
    return Task.CompletedTask;
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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertSame<T>(T expected, T? actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same reference.");
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
