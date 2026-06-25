using System.Collections;
using HeadlessDCGO.Engine.Headless.Coroutines;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1D-003 goal row keeps the CoroutineAdapter contract", GoalRowKeepsExpectedContract),
    ("CoroutineAdapter FromEnumerator rejects null input", FromEnumeratorRejectsNullInput),
    ("CoroutineAdapter completes nested enumerators before resuming parent", NestedEnumeratorCompletesBeforeParentContinues),
    ("CoroutineAdapter keeps wait condition pending until predicate is satisfied", WaitConditionKeepsTaskPendingUntilSatisfied),
    ("CoroutineAdapter propagates enumerator exceptions as faulted task result", EnumeratorExceptionFaultsTask),
    ("CoroutineAdapter treats null and unknown yields as deterministic pending steps", NullAndUnknownYieldsArePendingSteps),
    ("AS-IS coroutine references remain read-only inputs", AsIsCoroutineReferencesRemainReadOnlyInputs),
    ("CoroutineAdapter source file no longer contains placeholder TODO or Unity dependency", CoroutineAdapterFileHasNoTodoOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1D-003")
        ?? throw new InvalidOperationException("G1D-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Coroutines", Value(row, "area"), "area");
    AssertEqual("CoroutineAdapter", Value(row, "goal"), "goal");
    AssertEqual("IEnumerator adapter 확정", Value(row, "scope"), "scope");
    AssertEqual("CoroutineAdapter", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("nested enumerator completion", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1D-003_coroutine_adapter_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1D-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("CoroutineAdapter", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task FromEnumeratorRejectsNullInput()
{
    ExpectThrows<ArgumentNullException>(() => CoroutineAdapter.FromEnumerator(null!));
    return Task.CompletedTask;
}

async Task NestedEnumeratorCompletesBeforeParentContinues()
{
    var log = new List<string>();
    IEngineTask task = CoroutineAdapter.FromEnumerator(Parent(log));

    EngineTaskStepResult first = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Pending, first.Status, "first status");
    AssertSequence(new[] { "parent:start", "child:start" }, log, "first log");
    AssertEqual(EngineTaskStatus.Pending, task.Status, "task status after first");

    EngineTaskStepResult second = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Pending, second.Status, "second status");
    AssertSequence(
        new[] { "parent:start", "child:start", "child:after-null", "parent:after-child" },
        log,
        "second log");

    EngineTaskStepResult third = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Completed, third.Status, "third status");
    AssertTrue(task.IsCompleted, "task completed");
    AssertSequence(
        new[] { "parent:start", "child:start", "child:after-null", "parent:after-child", "parent:after-null" },
        log,
        "third log");
}

async Task WaitConditionKeepsTaskPendingUntilSatisfied()
{
    bool ready = false;
    var log = new List<string>();
    IEngineTask task = CoroutineAdapter.FromEnumerator(WaitThenContinue(log, () => ready));

    EngineTaskStepResult first = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Waiting, first.Status, "first status");
    AssertTrue(first.Wait is not null, "wait result");
    AssertTrue(ReferenceEquals(first.Wait, task.CurrentWait), "current wait reference");
    AssertSequence(new[] { "before-wait" }, log, "first log");

    EngineTaskStepResult second = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Waiting, second.Status, "second status");
    AssertSequence(new[] { "before-wait" }, log, "second log");

    ready = true;
    EngineTaskStepResult third = await task.StepAsync();
    AssertEqual(EngineTaskStatus.Completed, third.Status, "third status");
    AssertTrue(task.IsCompleted, "task completed");
    AssertEqual(null, task.CurrentWait, "current wait cleared");
    AssertSequence(new[] { "before-wait", "after-wait" }, log, "third log");
}

async Task EnumeratorExceptionFaultsTask()
{
    IEngineTask task = CoroutineAdapter.FromEnumerator(ThrowingEnumerator());

    EngineTaskStepResult result = await task.StepAsync();

    AssertEqual(EngineTaskStatus.Faulted, result.Status, "result status");
    AssertTrue(result.Error is InvalidOperationException, "result error type");
    AssertEqual("enumerator failure", result.Error!.Message, "result error message");
    AssertTrue(task.IsFaulted, "task faulted");
    AssertSame(result.Error, task.Error!, "task error");
}

async Task NullAndUnknownYieldsArePendingSteps()
{
    IEngineTask task = CoroutineAdapter.FromEnumerator(NullAndUnknownEnumerator());

    EngineTaskStepResult first = await task.StepAsync();
    EngineTaskStepResult second = await task.StepAsync();
    EngineTaskStepResult third = await task.StepAsync();

    AssertEqual(EngineTaskStatus.Pending, first.Status, "null yield status");
    AssertEqual(EngineTaskStatus.Pending, second.Status, "unknown yield status");
    AssertEqual(EngineTaskStatus.Completed, third.Status, "completed status");
    AssertTrue(task.IsCompleted, "task completed");
}

Task AsIsCoroutineReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"),
            new[] { "IEnumerator", "StartCoroutine", "yield return" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"),
            new[] { "IEnumerator", "StartCoroutine", "yield return" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "IEnumerator", "StartCoroutine", "WaitForSeconds", "WaitWhile" }),
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

Task CoroutineAdapterFileHasNoTodoOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Coroutines", "CoroutineAdapter.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("CoroutineAdapter.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "adapter must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "adapter must not reference MonoBehaviour");
    AssertFalse(text.Contains("StartCoroutine", StringComparison.Ordinal), "adapter must not call StartCoroutine");
    return Task.CompletedTask;
}

static IEnumerator Parent(List<string> log)
{
    log.Add("parent:start");
    yield return Child(log);
    log.Add("parent:after-child");
    yield return null;
    log.Add("parent:after-null");
}

static IEnumerator Child(List<string> log)
{
    log.Add("child:start");
    yield return null;
    log.Add("child:after-null");
}

static IEnumerator WaitThenContinue(List<string> log, Func<bool> isReady)
{
    log.Add("before-wait");
    yield return EngineWaitCondition.Until(isReady);
    log.Add("after-wait");
}

static IEnumerator ThrowingEnumerator()
{
    throw new InvalidOperationException("enumerator failure");
    #pragma warning disable CS0162
    yield return null;
    #pragma warning restore CS0162
}

static IEnumerator NullAndUnknownEnumerator()
{
    yield return null;
    yield return "unknown-yield-object";
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
