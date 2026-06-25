using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-004 goal row keeps the card instance state contract", GoalRowKeepsExpectedContract),
    ("CardInstanceState snapshots identities sources modifiers and flags", CardInstanceStateSnapshotsMutableState),
    ("CardInstanceState suspend and face-up transitions are immutable", SuspendAndFaceUpTransitionsAreImmutable),
    ("CardInstanceState source operations preserve order and reject invalid detach", SourceOperationsPreserveContract),
    ("CardInstanceState modifier operations normalize replace and remove keys", ModifierOperationsPreserveContract),
    ("CardInstanceState flag operations set clear and query values", FlagOperationsPreserveContract),
    ("CardInstanceState fingerprint is deterministic and state sensitive", FingerprintIsDeterministicAndStateSensitive),
    ("Card instance state source file no longer contains placeholder TODO contracts", CardInstanceStateFileHasNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-004")
        ?? throw new InvalidOperationException("G1B-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("CardInstanceState", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("suspend face-up source modifier flag", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-004_card_instance_state_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("card instance state", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task CardInstanceStateSnapshotsMutableState()
{
    var sources = new List<HeadlessEntityId> { new("source-1") };
    var modifiers = new Dictionary<string, object?> { [" dp "] = 1000 };
    var flags = new Dictionary<string, bool> { [" blocker "] = true };

    CardInstanceState state = new(
        new HeadlessEntityId("card-1"),
        new HeadlessEntityId("BT1-001"),
        new HeadlessPlayerId(1),
        SourceIds: sources,
        Modifiers: modifiers,
        Flags: flags);

    sources.Add(new HeadlessEntityId("source-2"));
    modifiers["dp"] = 2000;
    flags["blocker"] = false;

    AssertSequence(new[] { new HeadlessEntityId("source-1") }, state.SourceIds, "source snapshot");
    AssertEqual(1000, state.Modifiers["dp"], "modifier snapshot and trim");
    AssertTrue(state.Flags["blocker"], "flag snapshot and trim");

    ExpectThrows<ArgumentException>(() => new CardInstanceState(default, new HeadlessEntityId("BT1-001"), new HeadlessPlayerId(1)));
    ExpectThrows<ArgumentException>(() => new CardInstanceState(new HeadlessEntityId("card-1"), default, new HeadlessPlayerId(1)));
    ExpectThrows<ArgumentException>(() => new CardInstanceState(new HeadlessEntityId("card-1"), new HeadlessEntityId("BT1-001"), default));
    ExpectThrows<ArgumentException>(() => _ = state with { SourceIds = new[] { default(HeadlessEntityId) } });
    ExpectThrows<InvalidOperationException>(() => _ = state with
    {
        SourceIds = new[] { new HeadlessEntityId("same"), new HeadlessEntityId("same") }
    });
    return Task.CompletedTask;
}

Task SuspendAndFaceUpTransitionsAreImmutable()
{
    CardInstanceState original = CreateCard();
    CardInstanceState suspended = original.Suspend();
    CardInstanceState unsuspended = suspended.Unsuspend();
    CardInstanceState revealed = original.Reveal();
    CardInstanceState hidden = revealed.Hide();

    AssertFalse(original.IsSuspended, "original suspended");
    AssertTrue(suspended.IsSuspended, "suspend transition");
    AssertFalse(unsuspended.IsSuspended, "unsuspend transition");
    AssertFalse(original.IsFaceUp, "original face-up");
    AssertTrue(revealed.IsFaceUp, "reveal transition");
    AssertFalse(hidden.IsFaceUp, "hide transition");
    return Task.CompletedTask;
}

Task SourceOperationsPreserveContract()
{
    var sourceOne = new HeadlessEntityId("source-1");
    var sourceTwo = new HeadlessEntityId("source-2");
    CardInstanceState original = CreateCard();
    CardInstanceState withSources = original
        .AttachSource(sourceOne)
        .AttachSource(sourceTwo)
        .AttachSource(sourceOne);

    AssertEqual(0, original.SourceIds.Count, "original source count");
    AssertSequence(new[] { sourceOne, sourceTwo }, withSources.SourceIds, "attached source order");

    CardInstanceState detached = withSources.DetachSource(sourceOne);
    AssertSequence(new[] { sourceTwo }, detached.SourceIds, "detached source order");
    AssertEqual(0, detached.ClearSources().SourceIds.Count, "clear sources");

    ExpectThrows<ArgumentException>(() => withSources.AttachSource(default));
    ExpectThrows<ArgumentException>(() => withSources.DetachSource(default));
    ExpectThrows<InvalidOperationException>(() => detached.DetachSource(sourceOne));
    return Task.CompletedTask;
}

Task ModifierOperationsPreserveContract()
{
    CardInstanceState original = CreateCard();
    CardInstanceState modified = original
        .AddModifier(" dp ", 1000)
        .AddModifier("dp", 2000)
        .AddModifier("keyword", "retaliation")
        .RemoveModifier(" keyword ");

    AssertFalse(original.Modifiers.ContainsKey("dp"), "original modifier unchanged");
    AssertEqual(1, modified.Modifiers.Count, "modifier count");
    AssertEqual(2000, modified.Modifiers["dp"], "modifier replace");
    AssertFalse(modified.Modifiers.ContainsKey("keyword"), "modifier remove");

    ExpectThrows<ArgumentException>(() => modified.AddModifier(" ", 1));
    ExpectThrows<ArgumentException>(() => modified.RemoveModifier(" "));
    ExpectThrows<InvalidOperationException>(() => modified.RemoveModifier("missing"));
    return Task.CompletedTask;
}

Task FlagOperationsPreserveContract()
{
    CardInstanceState original = CreateCard();
    CardInstanceState flagged = original
        .SetFlag(" blocker ", true)
        .SetFlag("summoningSickness", false);
    CardInstanceState cleared = flagged.ClearFlag("summoningSickness");

    AssertFalse(original.HasFlag("blocker"), "original flag unchanged");
    AssertTrue(flagged.HasFlag("blocker"), "true flag query");
    AssertFalse(flagged.HasFlag("summoningSickness"), "false flag query");
    AssertFalse(cleared.Flags.ContainsKey("summoningSickness"), "clear flag");

    ExpectThrows<ArgumentException>(() => flagged.SetFlag(" ", true));
    ExpectThrows<ArgumentException>(() => flagged.ClearFlag(" "));
    ExpectThrows<ArgumentException>(() => flagged.HasFlag(" "));
    ExpectThrows<InvalidOperationException>(() => flagged.ClearFlag("missing"));
    return Task.CompletedTask;
}

Task FingerprintIsDeterministicAndStateSensitive()
{
    CardInstanceState first = CreateCard()
        .Reveal()
        .Suspend()
        .AttachSource(new HeadlessEntityId("source-1"))
        .AddModifier("dp", 1000)
        .SetFlag("blocker", true);
    CardInstanceState same = new(
        new HeadlessEntityId("card-1"),
        new HeadlessEntityId("BT1-001"),
        new HeadlessPlayerId(1),
        IsSuspended: true,
        IsFaceUp: true,
        SourceIds: new[] { new HeadlessEntityId("source-1") },
        Modifiers: new Dictionary<string, object?> { ["dp"] = 1000 },
        Flags: new Dictionary<string, bool> { ["blocker"] = true });
    CardInstanceState changed = first.SetFlag("blocker", false);

    AssertEqual(first.FingerprintSegment(), same.FingerprintSegment(), "deterministic fingerprint");
    AssertNotEqual(first.FingerprintSegment(), changed.FingerprintSegment(), "fingerprint changes");
    return Task.CompletedTask;
}

Task CardInstanceStateFileHasNoTodoContracts()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "CardInstanceState.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("CardInstanceState.cs still contains a TODO placeholder.");
    }

    return Task.CompletedTask;
}

static CardInstanceState CreateCard()
{
    return new CardInstanceState(
        new HeadlessEntityId("card-1"),
        new HeadlessEntityId("BT1-001"),
        new HeadlessPlayerId(1));
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
