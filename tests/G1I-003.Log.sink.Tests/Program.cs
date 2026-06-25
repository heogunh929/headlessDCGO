using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1I-003 goal row keeps the log sink contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("ILogSink exposes info warn error commands", LogSinkInterfaceKeepsExpectedContract),
    ("InMemoryLogSink records info warn error entries in sequence", InMemoryLogSinkRecordsLevelsInSequence),
    ("InMemoryLogSink records error exception type and message", ErrorRecordsExceptionDetails),
    ("InMemoryLogSink snapshot is isolated and clear resets sequence", SnapshotIsIsolatedAndClearResetsSequence),
    ("InMemoryLogSink rejects null messages explicitly", NullMessagesAreRejected),
    ("NullLogSink accepts info warn error without observable state", NullLogSinkAcceptsCalls),
    ("EngineContext can use InMemoryLogSink through ILogSink", EngineContextUsesInMemoryLogSink),
    ("Log sink source files have no placeholder or Unity dependency", SourcesHaveNoPlaceholderOrUnityDependency),
    ("AS-IS Unity log references remain read-only inputs", AsIsUnityLogReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1I-003")
        ?? throw new InvalidOperationException("G1I-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Diagnostics", Value(row, "area"), "area");
    AssertEqual("Log sink", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("log sink contract", StringComparison.Ordinal), "scope");
    AssertTrue(Value(row, "deliverables").Contains("ILogSink", StringComparison.Ordinal), "ILogSink deliverable");
    AssertTrue(Value(row, "deliverables").Contains("NullLogSink", StringComparison.Ordinal), "NullLogSink deliverable");
    AssertTrue(Value(row, "deliverables").Contains("InMemoryLogSink", StringComparison.Ordinal), "InMemoryLogSink deliverable");
    AssertTrue(Value(row, "unit_test_scope").Contains("info warn error null sink", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1I-003_log_sink_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1I-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Log sink", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1I-002_engine_trace_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1I-002 COMPLETE");
    return Task.CompletedTask;
}

Task LogSinkInterfaceKeepsExpectedContract()
{
    string[] methodNames = typeof(ILogSink)
        .GetMethods()
        .Select(method => method.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    AssertSequence(new[] { "Error", "Info", "Warn" }, methodNames, "ILogSink method names");
    AssertTrue(typeof(ILogSink).IsAssignableFrom(typeof(NullLogSink)), "NullLogSink implements ILogSink");
    AssertTrue(typeof(ILogSink).IsAssignableFrom(typeof(InMemoryLogSink)), "InMemoryLogSink implements ILogSink");
    return Task.CompletedTask;
}

Task InMemoryLogSinkRecordsLevelsInSequence()
{
    var sink = new InMemoryLogSink();

    sink.Info("match initialized");
    sink.Warn("choice fallback");
    sink.Error("illegal action");

    IReadOnlyList<LogEntry> entries = sink.Snapshot();
    AssertEqual(3, entries.Count, "entry count");
    AssertEntry(entries[0], 1, LogLevel.Info, "match initialized");
    AssertEntry(entries[1], 2, LogLevel.Warn, "choice fallback");
    AssertEntry(entries[2], 3, LogLevel.Error, "illegal action");
    AssertTrue(entries.All(entry => entry.ExceptionType is null), "no exception types");
    return Task.CompletedTask;
}

Task ErrorRecordsExceptionDetails()
{
    var sink = new InMemoryLogSink();
    var exception = new InvalidOperationException("bad state");

    sink.Error("engine failed", exception);

    LogEntry entry = sink.Snapshot().Single();
    AssertEntry(entry, 1, LogLevel.Error, "engine failed");
    AssertEqual(typeof(InvalidOperationException).FullName, entry.ExceptionType, "exception type");
    AssertEqual("bad state", entry.ExceptionMessage, "exception message");
    return Task.CompletedTask;
}

Task SnapshotIsIsolatedAndClearResetsSequence()
{
    var sink = new InMemoryLogSink();

    sink.Info("one");
    IReadOnlyList<LogEntry> snapshot = sink.Snapshot();
    sink.Warn("two");

    AssertEqual(1, snapshot.Count, "snapshot isolated count");
    AssertEqual(2, sink.Snapshot().Count, "current count");

    sink.Clear();
    AssertEqual(0, sink.Snapshot().Count, "cleared count");

    sink.Error("after clear");
    LogEntry afterClear = sink.Snapshot().Single();
    AssertEntry(afterClear, 1, LogLevel.Error, "after clear");
    return Task.CompletedTask;
}

Task NullMessagesAreRejected()
{
    var sink = new InMemoryLogSink();

    ExpectThrows<ArgumentNullException>(() => sink.Info(null!));
    ExpectThrows<ArgumentNullException>(() => sink.Warn(null!));
    ExpectThrows<ArgumentNullException>(() => sink.Error(null!));
    return Task.CompletedTask;
}

Task NullLogSinkAcceptsCalls()
{
    ILogSink sink = new NullLogSink();

    sink.Info("info");
    sink.Warn("warn");
    sink.Error("error", new InvalidOperationException("ignored"));
    sink.Info(null!);
    sink.Warn(null!);
    sink.Error(null!);
    return Task.CompletedTask;
}

Task EngineContextUsesInMemoryLogSink()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 3);
    var sink = new InMemoryLogSink();

    context.RegisterService<ILogSink>(sink);
    context.GetService<ILogSink>().Info("registered");

    LogEntry entry = sink.Snapshot().Single();
    AssertEntry(entry, 1, LogLevel.Info, "registered");
    return Task.CompletedTask;
}

Task SourcesHaveNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ILogSink.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "NullLogSink.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryLogSink.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string text = File.ReadAllText(Path.Combine(root, relativeFile));
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), $"{relativeFile} TODO");
        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{relativeFile} UnityEngine");
        AssertFalse(text.Contains("Debug.Log", StringComparison.Ordinal), $"{relativeFile} Debug.Log");
        AssertFalse(text.Contains("PlayLog", StringComparison.Ordinal), $"{relativeFile} PlayLog");
        AssertFalse(text.Contains("TMPro", StringComparison.Ordinal), $"{relativeFile} TMPro");
    }

    return Task.CompletedTask;
}

Task AsIsUnityLogReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayLog.cs"),
            new[] { "PlayLog", "OnAddLog", "UnityEngine", "TMPro" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameRandom.cs"),
            new[] { "GameRandom", "Seed", "Range" }),
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

static void AssertEntry(LogEntry entry, long sequence, LogLevel level, string message)
{
    AssertEqual(sequence, entry.Sequence, "entry sequence");
    AssertEqual(level, entry.Level, "entry level");
    AssertEqual(message, entry.Message, "entry message");
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
