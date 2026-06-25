using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1C-002 goal row keeps the GManagerBridge contract", GoalRowKeepsExpectedContract),
    ("GManagerBridge maps turn effect attack and log services from EngineContext", BridgeMapsCoreGManagerServices),
    ("GManagerBridge exposes current match and state from EngineContext", BridgeExposesCurrentMatchAndState),
    ("GManagerBridge service access delegates to EngineContext lookup contracts", BridgeDelegatesServiceLookup),
    ("GManagerBridge rejects invalid construction and missing services clearly", BridgeRejectsInvalidAccess),
    ("GManagerBridge source file no longer contains placeholder TODO contracts", GManagerBridgeFileHasNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1C-002")
        ?? throw new InvalidOperationException("G1C-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Bridge", Value(row, "area"), "area");
    AssertEqual("GManagerBridge", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("turn effect attack state service access", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1C-002_gmanager_bridge_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1C-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("GManagerBridge", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task BridgeMapsCoreGManagerServices()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 23);
    GManagerBridge bridge = new(context);

    AssertSame(context, bridge.Context, "context");
    AssertSame(context.TurnController, bridge.Turn, "turn property");
    AssertSame(context.TurnController, bridge.GetTurnStateMachine(), "turn alias");
    AssertSame(context.EffectScheduler, bridge.Effects, "effects property");
    AssertSame(context.EffectScheduler, bridge.AutoProcessing, "auto processing property");
    AssertSame(context.EffectScheduler, bridge.GetAutoProcessing(), "auto processing alias");
    AssertSame(context.EffectScheduler, bridge.GetEffectScheduler(), "effect scheduler alias");
    AssertSame(context.AttackController, bridge.Attack, "attack property");
    AssertSame(context.AttackController, bridge.GetAttackProcess(), "attack alias");
    AssertSame(context.LogSink, bridge.Log, "log property");
    AssertSame(context.LogSink, bridge.GetLog(), "log alias");
    return Task.CompletedTask;
}

async Task BridgeExposesCurrentMatchAndState()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 31);
    GManagerBridge bridge = new(context);
    AssertEqual(null, bridge.CurrentMatch, "initial current match");
    AssertSame(ObservationSnapshot.Empty, bridge.State, "initial state");
    AssertSame(ObservationSnapshot.Empty, bridge.GetCurrentState(), "initial state alias");

    var match = new DcgoMatch(context);
    await match.InitializeAsync(MatchConfig.Create(
        new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) },
        randomSeed: 31));
    StepResult step = await match.StepAsync();

    AssertSame(match, bridge.CurrentMatch!, "attached current match");
    AssertSame(step.Observation, bridge.State, "state after step");
    AssertSame(step.Observation, bridge.GetCurrentState(), "state alias after step");
    AssertEqual(2, bridge.State.PlayerCount, "state player count");
    AssertEqual(31, bridge.State.RandomSeed, "state random seed");
}

Task BridgeDelegatesServiceLookup()
{
    EngineContext context = EngineContext.CreateDefault();
    GManagerBridge bridge = new(context);
    var sink = new RecordingLogSink();

    context.RegisterService<ILogSink>(sink);

    AssertSame(sink, bridge.GetService<ILogSink>(), "updated log sink");
    AssertSame(context.ZoneMover, bridge.GetService<IZoneMover>(), "zone mover lookup");
    AssertSame(context.EffectScheduler, bridge.GetService(typeof(EffectScheduler)), "type lookup");
    AssertTrue(bridge.TryGetService<IZoneMover>(out IZoneMover? zoneMover), "try get zone mover");
    AssertSame(context.ZoneMover, zoneMover!, "try get zone mover instance");

    bridge.GetService<ILogSink>().Info("bridge");
    AssertSequence(new[] { "INFO:bridge" }, sink.Messages, "log sink messages");
    return Task.CompletedTask;
}

Task BridgeRejectsInvalidAccess()
{
    EngineContext context = EngineContext.CreateDefault();
    GManagerBridge bridge = new(context);

    ExpectThrows<ArgumentNullException>(() => new GManagerBridge(null!));
    AssertFalse(bridge.TryGetService<RecordingLogSink>(out _), "missing try get");
    ExpectThrows<InvalidOperationException>(() => bridge.GetService<RecordingLogSink>());
    ExpectThrows<ArgumentNullException>(() => bridge.GetService(null!));
    return Task.CompletedTask;
}

Task GManagerBridgeFileHasNoTodoContracts()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Bridge", "GManagerBridge.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("GManagerBridge.cs still contains a TODO placeholder.");
    }

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

static void AssertSame(object expected, object actual, string label)
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same instance.");
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

sealed class RecordingLogSink : ILogSink
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public void Info(string message)
    {
        _messages.Add($"INFO:{message}");
    }

    public void Warn(string message)
    {
        _messages.Add($"WARN:{message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        _messages.Add($"ERROR:{message}");
    }
}
