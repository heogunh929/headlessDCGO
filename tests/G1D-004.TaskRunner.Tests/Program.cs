using System.Collections;
using HeadlessDCGO.Engine.Headless.Coroutines;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1D-004 goal row keeps the TaskRunner contract", GoalRowKeepsExpectedContract),
    ("EngineTaskRunner enqueue and clear expose deterministic queue state", EnqueueAndClearExposeQueueState),
    ("EngineTaskRunner RunUntilIdle completes multiple tasks in enqueue round order", RunUntilIdleCompletesTasksInRoundOrder),
    ("EngineTaskRunner Step leaves unsatisfied wait task queued and idle", StepLeavesUnsatisfiedWaitTaskQueuedAndIdle),
    ("EngineTaskRunner resumes waited task after condition is satisfied", RunnerResumesWaitedTaskAfterConditionSatisfied),
    ("EngineTaskRunner propagates faulted task result to caller", RunnerPropagatesFaultedTaskResult),
    ("EngineTaskRunner drives CoroutineAdapter nested enumerator to completion", RunnerDrivesCoroutineAdapterNestedEnumerator),
    ("AS-IS runner references remain read-only inputs", AsIsRunnerReferencesRemainReadOnlyInputs),
    ("EngineTaskRunner source file no longer contains placeholder TODO or Unity dependency", EngineTaskRunnerFileHasNoTodoOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1D-004")
        ?? throw new InvalidOperationException("G1D-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Coroutines", Value(row, "area"), "area");
    AssertEqual("TaskRunner 안정화", Value(row, "goal"), "goal");
    AssertEqual("task queue runner 확정", Value(row, "scope"), "scope");
    AssertEqual("EngineTaskRunner", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("run until idle queue order error propagation", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1D-004_task_runner_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1D-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("TaskRunner", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task EnqueueAndClearExposeQueueState()
{
    var runner = new EngineTaskRunner();
    AssertEqual(0, runner.PendingTaskCount, "initial count");
    AssertTrue(runner.IsIdle(), "initial idle");

    runner.Enqueue(new ScriptedEngineTask("task", new List<string>(), EngineTaskStepResult.Pending()));
    AssertEqual(1, runner.PendingTaskCount, "queued count");
    AssertFalse(runner.IsIdle(), "queued ready task not idle");

    runner.Clear();
    AssertEqual(0, runner.PendingTaskCount, "cleared count");
    AssertTrue(runner.IsIdle(), "cleared idle");
    ExpectThrows<ArgumentNullException>(() => runner.Enqueue(null!));
    return Task.CompletedTask;
}

async Task RunUntilIdleCompletesTasksInRoundOrder()
{
    var log = new List<string>();
    var runner = new EngineTaskRunner();
    runner.Enqueue(new ScriptedEngineTask("A", log, EngineTaskStepResult.Pending(), EngineTaskStepResult.Completed()));
    runner.Enqueue(new ScriptedEngineTask("B", log, EngineTaskStepResult.Pending(), EngineTaskStepResult.Completed()));

    await runner.RunUntilIdleAsync();

    AssertSequence(new[] { "A:Pending", "B:Pending", "A:Completed", "B:Completed" }, log, "execution log");
    AssertEqual(0, runner.PendingTaskCount, "queue count");
    AssertTrue(runner.IsIdle(), "runner idle");
}

async Task StepLeavesUnsatisfiedWaitTaskQueuedAndIdle()
{
    bool ready = false;
    var log = new List<string>();
    EngineWaitCondition wait = EngineWaitCondition.Until(() => ready);
    var runner = new EngineTaskRunner();
    runner.Enqueue(new ScriptedEngineTask("waiter", log, EngineTaskStepResult.Waiting(wait), EngineTaskStepResult.Completed()));

    await runner.StepAsync();

    AssertSequence(new[] { "waiter:Waiting" }, log, "execution log");
    AssertEqual(1, runner.PendingTaskCount, "queue count");
    AssertTrue(runner.IsIdle(), "unsatisfied wait is idle");

    await runner.RunUntilIdleAsync();

    AssertSequence(new[] { "waiter:Waiting" }, log, "run until idle does not spin");
    AssertEqual(1, runner.PendingTaskCount, "queue count after idle");
}

async Task RunnerResumesWaitedTaskAfterConditionSatisfied()
{
    bool ready = false;
    var log = new List<string>();
    var runner = new EngineTaskRunner();
    runner.Enqueue(CoroutineAdapter.FromEnumerator(WaitThenContinue(log, () => ready)));

    await runner.StepAsync();
    AssertSequence(new[] { "before-wait" }, log, "after first step");
    AssertTrue(runner.IsIdle(), "waiting runner idle");
    AssertEqual(1, runner.PendingTaskCount, "waiting task stays queued");

    ready = true;
    await runner.RunUntilIdleAsync();

    AssertSequence(new[] { "before-wait", "after-wait" }, log, "after wait satisfied");
    AssertEqual(0, runner.PendingTaskCount, "completed queue count");
    AssertTrue(runner.IsIdle(), "completed idle");
}

async Task RunnerPropagatesFaultedTaskResult()
{
    var log = new List<string>();
    var error = new InvalidOperationException("runner fault");
    var runner = new EngineTaskRunner();
    runner.Enqueue(new ScriptedEngineTask("fault", log, EngineTaskStepResult.Faulted(error)));

    InvalidOperationException thrown = await ExpectThrowsAsync<InvalidOperationException>(() => runner.RunUntilIdleAsync());

    AssertSame(error, thrown, "propagated error");
    AssertSequence(new[] { "fault:Faulted" }, log, "execution log");
    AssertEqual(0, runner.PendingTaskCount, "faulted task removed");
}

async Task RunnerDrivesCoroutineAdapterNestedEnumerator()
{
    var log = new List<string>();
    var runner = new EngineTaskRunner();
    runner.Enqueue(CoroutineAdapter.FromEnumerator(Parent(log)));

    await runner.RunUntilIdleAsync();

    AssertSequence(
        new[] { "parent:start", "child:start", "child:after-null", "parent:after-child", "parent:after-null" },
        log,
        "nested coroutine log");
    AssertEqual(0, runner.PendingTaskCount, "queue count");
}

Task AsIsRunnerReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GManager.cs"),
            new[] { "StartCoroutine", "IEnumerator" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ContinuousController.cs"),
            new[] { "StartCoroutine", "IEnumerator" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "StartCoroutine", "WaitForSeconds", "WaitWhile" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"),
            new[] { "StartCoroutine", "IEnumerator" }),
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

Task EngineTaskRunnerFileHasNoTodoOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Coroutines", "EngineTaskRunner.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("EngineTaskRunner.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "runner must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "runner must not reference MonoBehaviour");
    AssertFalse(text.Contains("StartCoroutine", StringComparison.Ordinal), "runner must not call StartCoroutine");
    return Task.CompletedTask;
}

static IEnumerator WaitThenContinue(List<string> log, Func<bool> isReady)
{
    log.Add("before-wait");
    yield return EngineWaitCondition.Until(isReady);
    log.Add("after-wait");
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

static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException ex)
    {
        return ex;
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

sealed class ScriptedEngineTask : IEngineTask
{
    private readonly string _name;
    private readonly List<string> _log;
    private readonly Queue<EngineTaskStepResult> _steps;

    public ScriptedEngineTask(string name, List<string> log, params EngineTaskStepResult[] steps)
    {
        _name = name;
        _log = log;
        _steps = new Queue<EngineTaskStepResult>(steps);
    }

    public EngineTaskStatus Status { get; private set; } = EngineTaskStatus.Pending;

    public EngineWaitCondition? CurrentWait { get; private set; }

    public Exception? Error { get; private set; }

    public Task<EngineTaskStepResult> StepAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EngineTaskStepResult result = _steps.Count == 0
            ? EngineTaskStepResult.Completed()
            : _steps.Dequeue();

        Status = result.Status;
        CurrentWait = result.Wait;
        Error = result.Error;
        _log.Add($"{_name}:{result.Status}");
        return Task.FromResult(result);
    }
}
