using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Action Body)[]
{
    ("G1A-001 goal row keeps the runtime DTO contract", GoalRowKeepsExpectedContract),
    ("MatchConfig creates deterministic immutable player and memory configuration", MatchConfigCreatesImmutableConfiguration),
    ("MatchConfig rejects invalid memory ranges and duplicate players", MatchConfigRejectsInvalidValues),
    ("StepResult copies event collections and rejects null DTO dependencies", StepResultProtectsRequiredValues),
    ("MatchResult normalizes reason and rejects winner draw contradictions", MatchResultProtectsTerminalOutcome),
    ("GameEvent copies metadata and rejects invalid sequence or null metadata", GameEventProtectsEventEnvelope),
    ("Runtime DTO source files no longer contain placeholder TODO contracts", RuntimeDtoFilesHaveNoTodoContracts),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Body();
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

void GoalRowKeepsExpectedContract()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-001")
        ?? throw new InvalidOperationException("G1A-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Runtime", Value(row, "area"), "area");
    AssertEqual("MatchConfig StepResult MatchResult GameEvent 계약 고정", Value(row, "scope"), "scope");
    AssertEqual("runtime model files", Value(row, "deliverables"), "deliverables");
    AssertEqual("DTO 생성과 기본 불변성 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G1A-001_runtime_models_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G0-003", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Runtime DTO 테스트 통과", Value(row, "completion_gate"), "completion_gate");
}

void MatchConfigCreatesImmutableConfiguration()
{
    var players = new List<HeadlessPlayerId>
    {
        new(1),
        new(2)
    };

    var config = MatchConfig.Create(
        players,
        randomSeed: 42,
        initialMemory: 1,
        minimumMemory: -5,
        maximumMemory: 5);

    players.Add(new HeadlessPlayerId(3));

    AssertEqual(2, config.PlayerIds.Count, "player count after source mutation");
    AssertEqual(42, config.RandomSeed, "random seed");
    AssertEqual(1, config.InitialMemory, "initial memory");
    AssertEqual(-5, config.MinimumMemory, "minimum memory");
    AssertEqual(5, config.MaximumMemory, "maximum memory");
    AssertTrue(config.UseDeterministicChoices, "deterministic choices default");
}

void MatchConfigRejectsInvalidValues()
{
    ExpectThrows<ArgumentNullException>(() => MatchConfig.Create(null!));
    ExpectThrows<InvalidOperationException>(() => MatchConfig.Create(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(1) }));
    ExpectThrows<ArgumentOutOfRangeException>(() => MatchConfig.Create(new[] { new HeadlessPlayerId(1) }, initialMemory: 11));
    ExpectThrows<ArgumentOutOfRangeException>(() => MatchConfig.Create(new[] { new HeadlessPlayerId(1) }, minimumMemory: 1));
    ExpectThrows<ArgumentOutOfRangeException>(() => MatchConfig.Create(new[] { new HeadlessPlayerId(1) }, maximumMemory: -1));
}

void StepResultProtectsRequiredValues()
{
    var events = new List<GameEvent>
    {
        new(1, GameEventType.StateChanged, "created", new Dictionary<string, object?>())
    };

    var result = new StepResult(
        IsTerminal: false,
        HasPendingChoice: true,
        Events: events,
        Observation: ObservationSnapshot.Empty,
        ActionMask: ActionMask.Empty);

    events.Add(new GameEvent(2, GameEventType.GameEnded, "mutated", new Dictionary<string, object?>()));

    AssertEqual(1, result.Events.Count, "event count after source mutation");
    AssertTrue(result.HasPendingChoice, "pending choice");
    AssertFalse(result.IsTerminal, "terminal flag");
    AssertSame(ObservationSnapshot.Empty, result.Observation, "observation reference");
    AssertSame(ActionMask.Empty, result.ActionMask, "action mask reference");

    ExpectThrows<ArgumentNullException>(() => new StepResult(false, false, null!, ObservationSnapshot.Empty, ActionMask.Empty));
    ExpectThrows<ArgumentNullException>(() => new StepResult(false, false, Array.Empty<GameEvent>(), null!, ActionMask.Empty));
    ExpectThrows<ArgumentNullException>(() => new StepResult(false, false, Array.Empty<GameEvent>(), ObservationSnapshot.Empty, null!));
}

void MatchResultProtectsTerminalOutcome()
{
    var winner = new HeadlessPlayerId(7);
    var win = new MatchResult(WinnerId: winner, Reason: null!);

    AssertEqual(winner, win.WinnerId, "winner id");
    AssertEqual(string.Empty, win.Reason, "null reason normalization");
    AssertFalse(win.IsDraw, "win is not draw");

    var draw = new MatchResult(IsDraw: true, Reason: "deck out");
    AssertTrue(draw.IsDraw, "draw flag");
    AssertEqual("deck out", draw.Reason, "draw reason");

    ExpectThrows<ArgumentException>(() => new MatchResult(WinnerId: winner, IsDraw: true));
    ExpectThrows<ArgumentException>(() => _ = win with { IsDraw = true });
    ExpectThrows<ArgumentException>(() => _ = draw with { WinnerId = winner });
}

void GameEventProtectsEventEnvelope()
{
    var metadata = new Dictionary<string, object?>
    {
        ["cardId"] = "BT1-001"
    };

    var gameEvent = new GameEvent(3, GameEventType.CardMoved, null!, metadata);
    metadata["cardId"] = "mutated";
    metadata["newKey"] = 1;

    AssertEqual(3L, gameEvent.Sequence, "sequence");
    AssertEqual(GameEventType.CardMoved, gameEvent.Type, "event type");
    AssertEqual(string.Empty, gameEvent.Message, "null message normalization");
    AssertEqual("BT1-001", gameEvent.Metadata["cardId"], "metadata copy value");
    AssertFalse(gameEvent.Metadata.ContainsKey("newKey"), "metadata copy keys");

    ExpectThrows<ArgumentOutOfRangeException>(() => new GameEvent(-1, GameEventType.Unknown, "bad", new Dictionary<string, object?>()));
    ExpectThrows<ArgumentNullException>(() => new GameEvent(0, GameEventType.Unknown, "bad", null!));
}

void RuntimeDtoFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MatchConfig.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "StepResult.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MatchResult.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameEvent.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameEventType.cs"),
    };

    foreach (var relativeFile in relativeFiles)
    {
        var path = Path.Combine(root, relativeFile);
        var text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{relativeFile} still contains a TODO placeholder.");
        }
    }
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

static void AssertSame<T>(T expected, T actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same reference.");
    }
}
