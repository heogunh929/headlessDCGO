using HeadlessDCGO.Engine.Headless.Coroutines;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1D-002 goal row keeps the WaitCondition contract", GoalRowKeepsExpectedContract),
    ("EngineWaitCondition Seconds is deterministic from explicit elapsed input", SecondsConditionIsDeterministic),
    ("EngineWaitCondition Seconds rejects invalid time input", SecondsRejectsInvalidInput),
    ("EngineWaitCondition Until represents WaitWhile replacement by predicate inversion", UntilRepresentsWaitWhileReplacement),
    ("EngineWaitCondition IsSatisfied without elapsed is deterministic zero elapsed", DefaultSatisfactionUsesZeroElapsed),
    ("AS-IS wait references remain read-only inputs", AsIsWaitReferencesRemainReadOnlyInputs),
    ("EngineWaitCondition source file no longer contains placeholder TODO or wall clock dependency", EngineWaitConditionFileHasNoTodoOrWallClock),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1D-002")
        ?? throw new InvalidOperationException("G1D-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Coroutines", Value(row, "area"), "area");
    AssertEqual("WaitCondition", Value(row, "goal"), "goal");
    AssertEqual("EngineWaitCondition", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("seconds condition deterministic", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1D-002_wait_condition_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1D-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("wait condition", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task SecondsConditionIsDeterministic()
{
    EngineWaitCondition wait = EngineWaitCondition.Seconds(2.5);

    AssertEqual(EngineWaitConditionKind.Seconds, wait.Kind, "kind");
    AssertTrue(wait.IsTimeBased, "time based");
    AssertFalse(wait.IsPredicateBased, "predicate based");
    AssertEqual(TimeSpan.FromSeconds(2.5), wait.Duration, "duration");
    AssertFalse(wait.IsSatisfied(TimeSpan.Zero), "zero elapsed");
    AssertFalse(wait.IsSatisfied(TimeSpan.FromSeconds(2.499)), "before duration");
    AssertTrue(wait.IsSatisfied(TimeSpan.FromSeconds(2.5)), "at duration");
    AssertTrue(wait.IsSatisfied(TimeSpan.FromSeconds(12)), "after duration");

    bool first = wait.IsSatisfied(TimeSpan.FromSeconds(2.25));
    bool second = wait.IsSatisfied(TimeSpan.FromSeconds(2.25));
    AssertEqual(first, second, "repeated explicit elapsed result");

    EngineWaitCondition immediate = EngineWaitCondition.Seconds(TimeSpan.Zero);
    AssertTrue(immediate.IsSatisfied(TimeSpan.Zero), "zero duration");
    return Task.CompletedTask;
}

Task SecondsRejectsInvalidInput()
{
    ExpectThrows<ArgumentOutOfRangeException>(() => EngineWaitCondition.Seconds(-0.1));
    ExpectThrows<ArgumentOutOfRangeException>(() => EngineWaitCondition.Seconds(double.NaN));
    ExpectThrows<ArgumentOutOfRangeException>(() => EngineWaitCondition.Seconds(double.PositiveInfinity));
    ExpectThrows<ArgumentOutOfRangeException>(() => EngineWaitCondition.Seconds(TimeSpan.FromTicks(-1)));

    EngineWaitCondition wait = EngineWaitCondition.Seconds(1);
    ExpectThrows<ArgumentOutOfRangeException>(() => wait.IsSatisfied(TimeSpan.FromTicks(-1)));
    return Task.CompletedTask;
}

Task UntilRepresentsWaitWhileReplacement()
{
    bool waiting = true;
    EngineWaitCondition waitWhileReplacement = EngineWaitCondition.Until(() => !waiting);

    AssertEqual(EngineWaitConditionKind.Until, waitWhileReplacement.Kind, "kind");
    AssertFalse(waitWhileReplacement.IsTimeBased, "time based");
    AssertTrue(waitWhileReplacement.IsPredicateBased, "predicate based");
    AssertFalse(waitWhileReplacement.IsSatisfied(TimeSpan.Zero), "still waiting");

    waiting = false;
    AssertTrue(waitWhileReplacement.IsSatisfied(TimeSpan.Zero), "wait while predicate cleared");
    ExpectThrows<ArgumentNullException>(() => EngineWaitCondition.Until(null!));
    return Task.CompletedTask;
}

Task DefaultSatisfactionUsesZeroElapsed()
{
    EngineWaitCondition delayed = EngineWaitCondition.Seconds(1);
    EngineWaitCondition immediate = EngineWaitCondition.Seconds(0);
    bool ready = false;
    EngineWaitCondition predicate = EngineWaitCondition.Until(() => ready);

    AssertFalse(delayed.IsSatisfied(), "default delayed");
    AssertTrue(immediate.IsSatisfied(), "default immediate");
    AssertFalse(predicate.IsSatisfied(), "default predicate false");
    ready = true;
    AssertTrue(predicate.IsSatisfied(), "default predicate true");
    return Task.CompletedTask;
}

Task AsIsWaitReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "WaitForSeconds", "WaitWhile", "WaitUntil" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"),
            new[] { "IEnumerator", "StartCoroutine" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"),
            new[] { "IEnumerator", "StartCoroutine" }),
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

Task EngineWaitConditionFileHasNoTodoOrWallClock()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Coroutines", "EngineWaitCondition.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("EngineWaitCondition.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UtcNow", StringComparison.Ordinal), "wait condition must not use UtcNow");
    AssertFalse(text.Contains("DateTimeOffset", StringComparison.Ordinal), "wait condition must not use DateTimeOffset");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "wait condition must not reference UnityEngine");
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
