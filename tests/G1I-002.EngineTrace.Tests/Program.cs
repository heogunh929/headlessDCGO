using HeadlessDCGO.Engine.Headless.Diagnostics;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1I-002 goal row keeps the EngineTrace contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("EngineTrace records ordered events and isolated snapshots", RecordsOrderedEventsAndIsolatedSnapshots),
    ("TraceEvent validates required fields and copies metadata", TraceEventValidatesAndCopiesMetadata),
    ("EngineTrace fingerprint is deterministic for equivalent events", FingerprintIsDeterministicForEquivalentEvents),
    ("EngineTrace fingerprint changes when sequence category message or metadata changes", FingerprintChangesForTraceChanges),
    ("EngineTrace clear removes events and resets sequence", ClearRemovesEventsAndResetsSequence),
    ("EngineTrace honors disabled and max event options", OptionsAreHonored),
    ("NullTraceSink ignores records without mutating observable state", NullSinkIgnoresRecords),
    ("Diagnostics source files have no placeholder or Unity dependency", DiagnosticsSourcesHaveNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1I-002")
        ?? throw new InvalidOperationException("G1I-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Diagnostics", Value(row, "area"), "area");
    AssertEqual("EngineTrace", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("trace sequence", StringComparison.Ordinal), "scope sequence");
    AssertTrue(Value(row, "scope").Contains("fingerprint", StringComparison.Ordinal), "scope fingerprint");
    AssertTrue(Value(row, "deliverables").Contains("EngineTrace", StringComparison.Ordinal), "EngineTrace deliverable");
    AssertTrue(Value(row, "deliverables").Contains("TraceEvent", StringComparison.Ordinal), "TraceEvent deliverable");
    AssertTrue(Value(row, "unit_test_scope").Contains("record snapshot clear fingerprint", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("EngineTrace", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1A-001_runtime_models_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1A-001 COMPLETE");
    return Task.CompletedTask;
}

Task RecordsOrderedEventsAndIsolatedSnapshots()
{
    var trace = new EngineTrace();
    var metadata = new Dictionary<string, object?> { ["turn"] = 1, ["player"] = "P1" };

    trace.Record("match", "initialized", metadata);
    metadata["turn"] = 99;
    trace.Record("choice", "resolved", new Dictionary<string, object?> { ["option"] = "attack" });

    IReadOnlyList<TraceEvent> snapshot = trace.Snapshot();
    AssertEqual(2, snapshot.Count, "snapshot count");
    AssertEqual(1L, snapshot[0].Sequence, "first sequence");
    AssertEqual(2L, snapshot[1].Sequence, "second sequence");
    AssertEqual("match", snapshot[0].Category, "first category");
    AssertEqual("initialized", snapshot[0].Message, "first message");
    AssertEqual(1, snapshot[0].Metadata["turn"], "metadata copied");

    trace.Record("after", "snapshot");
    AssertEqual(2, snapshot.Count, "snapshot isolated from later record");
    AssertEqual(3, trace.Snapshot().Count, "trace received later record");
    return Task.CompletedTask;
}

Task TraceEventValidatesAndCopiesMetadata()
{
    var metadata = new Dictionary<string, object?> { ["value"] = 3 };
    var traceEvent = new TraceEvent(1, " category ", "message", metadata);
    metadata["value"] = 5;

    AssertEqual(1L, traceEvent.Sequence, "sequence");
    AssertEqual("category", traceEvent.Category, "trimmed category");
    AssertEqual("message", traceEvent.Message, "message");
    AssertEqual(3, traceEvent.Metadata["value"], "metadata copy");

    ExpectThrows<ArgumentOutOfRangeException>(() => new TraceEvent(0, "category", "message"));
    ExpectThrows<ArgumentException>(() => new TraceEvent(1, "", "message"));
    ExpectThrows<ArgumentNullException>(() => new TraceEvent(1, "category", null!));
    return Task.CompletedTask;
}

Task FingerprintIsDeterministicForEquivalentEvents()
{
    var first = new EngineTrace();
    first.Record(
        "random",
        "choice",
        new Dictionary<string, object?>
        {
            ["seed"] = 7,
            ["choices"] = new[] { "A", "B", "C" },
            ["nested"] = new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 },
        });

    var second = new EngineTrace();
    second.Record(
        "random",
        "choice",
        new Dictionary<string, object?>
        {
            ["nested"] = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 },
            ["choices"] = new[] { "A", "B", "C" },
            ["seed"] = 7,
        });

    AssertEqual(first.Fingerprint(), second.Fingerprint(), "fingerprint ignores metadata insertion order");
    AssertEqual(first.Fingerprint(), first.Fingerprint(), "fingerprint repeat");
    return Task.CompletedTask;
}

Task FingerprintChangesForTraceChanges()
{
    EngineTrace baseline = CreateTrace(category: "match", message: "step", metadataValue: 1);

    AssertNotEqual(baseline.Fingerprint(), CreateTrace(category: "match", message: "step", metadataValue: 2).Fingerprint(), "metadata change");
    AssertNotEqual(baseline.Fingerprint(), CreateTrace(category: "choice", message: "step", metadataValue: 1).Fingerprint(), "category change");
    AssertNotEqual(baseline.Fingerprint(), CreateTrace(category: "match", message: "other", metadataValue: 1).Fingerprint(), "message change");

    var extra = CreateTrace(category: "match", message: "step", metadataValue: 1);
    extra.Record("match", "step", new Dictionary<string, object?> { ["value"] = 1 });
    AssertNotEqual(baseline.Fingerprint(), extra.Fingerprint(), "sequence and count change");
    return Task.CompletedTask;
}

Task ClearRemovesEventsAndResetsSequence()
{
    var trace = new EngineTrace();
    trace.Record("match", "first");
    string emptyFingerprint = new EngineTrace().Fingerprint();

    trace.Clear();
    AssertEqual(0, trace.Snapshot().Count, "clear count");
    AssertEqual(emptyFingerprint, trace.Fingerprint(), "clear fingerprint");

    trace.Record("match", "after clear");
    TraceEvent afterClear = trace.Snapshot().Single();
    AssertEqual(1L, afterClear.Sequence, "sequence reset");
    return Task.CompletedTask;
}

Task OptionsAreHonored()
{
    var disabled = new EngineTrace(new TraceOptions(Enabled: false));
    disabled.Record("match", "ignored");
    AssertEqual(0, disabled.Snapshot().Count, "disabled count");

    var limited = new EngineTrace(new TraceOptions(MaxEvents: 2));
    limited.Record("match", "one");
    limited.Record("match", "two");
    limited.Record("match", "three");
    IReadOnlyList<TraceEvent> snapshot = limited.Snapshot();

    AssertEqual(2, snapshot.Count, "limited count");
    AssertEqual(2L, snapshot[0].Sequence, "limited first retained sequence");
    AssertEqual("two", snapshot[0].Message, "limited first retained message");
    AssertEqual(3L, snapshot[1].Sequence, "limited second retained sequence");
    return Task.CompletedTask;
}

Task NullSinkIgnoresRecords()
{
    ITraceSink sink = new NullTraceSink();
    sink.Record("match", "ignored", new Dictionary<string, object?> { ["value"] = 1 });
    return Task.CompletedTask;
}

Task DiagnosticsSourcesHaveNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Diagnostics", "EngineTrace.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Diagnostics", "TraceEvent.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Diagnostics", "ITraceSink.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Diagnostics", "NullTraceSink.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Diagnostics", "TraceOptions.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string text = File.ReadAllText(Path.Combine(root, relativeFile));
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), $"{relativeFile} TODO");
        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{relativeFile} UnityEngine");
        AssertFalse(text.Contains("Photon", StringComparison.Ordinal), $"{relativeFile} Photon");
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
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "TurnStateMachine", "Debug", "UnityEngine" }),
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

static EngineTrace CreateTrace(string category, string message, int metadataValue)
{
    var trace = new EngineTrace();
    trace.Record(category, message, new Dictionary<string, object?> { ["value"] = metadataValue });
    return trace;
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

static void AssertNotEqual<T>(T expectedDifferent, T actual, string message)
{
    if (EqualityComparer<T>.Default.Equals(expectedDifferent, actual))
    {
        throw new InvalidOperationException($"{message}: values should differ but both were {actual}.");
    }
}
