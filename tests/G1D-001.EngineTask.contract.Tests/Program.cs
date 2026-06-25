using System.Reflection;
using HeadlessDCGO.Engine.Headless.Coroutines;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1D-001 goal row keeps the EngineTask contract", GoalRowKeepsExpectedContract),
    ("IEngineTask exposes status wait error and typed step result contract", EngineTaskInterfaceExposesTypedContract),
    ("EngineTaskStepResult factories encode terminal and nonterminal states", StepResultFactoriesEncodeStates),
    ("IEngineTask step transitions from pending to completed deterministically", EngineTaskStepsCompleteDeterministically),
    ("IEngineTask faulted step records error and exposes terminal failure", EngineTaskFaultRecordsError),
    ("EngineTaskRunner propagates faulted task errors to caller", RunnerPropagatesFaultedTaskErrors),
    ("EngineTask contract source file no longer contains placeholder TODO contracts", EngineTaskFileHasNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1D-001")
        ?? throw new InvalidOperationException("G1D-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Coroutines", Value(row, "area"), "area");
    AssertEqual("EngineTask contract", Value(row, "goal"), "goal");
    AssertEqual("IEngineTask EngineTaskStatus", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("task step completion error", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1D-001_engine_task_contract_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("EngineTask contract", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task EngineTaskInterfaceExposesTypedContract()
{
    Type taskType = typeof(IEngineTask);
    PropertyInfo status = taskType.GetProperty(nameof(IEngineTask.Status))
        ?? throw new InvalidOperationException("IEngineTask.Status was not found.");
    PropertyInfo wait = taskType.GetProperty(nameof(IEngineTask.CurrentWait))
        ?? throw new InvalidOperationException("IEngineTask.CurrentWait was not found.");
    PropertyInfo error = taskType.GetProperty(nameof(IEngineTask.Error))
        ?? throw new InvalidOperationException("IEngineTask.Error was not found.");
    MethodInfo step = taskType.GetMethod(nameof(IEngineTask.StepAsync))
        ?? throw new InvalidOperationException("IEngineTask.StepAsync was not found.");

    AssertEqual(typeof(EngineTaskStatus), status.PropertyType, "Status type");
    AssertEqual(typeof(EngineWaitCondition), Nullable.GetUnderlyingType(wait.PropertyType) ?? wait.PropertyType, "CurrentWait type");
    AssertEqual(typeof(Exception), Nullable.GetUnderlyingType(error.PropertyType) ?? error.PropertyType, "Error type");
    AssertEqual(typeof(Task<EngineTaskStepResult>), step.ReturnType, "StepAsync return type");
    AssertSequence(
        new[]
        {
            "Pending",
            "Waiting",
            "Completed",
            "Faulted",
            "Canceled",
        },
        Enum.GetNames<EngineTaskStatus>(),
        "EngineTaskStatus names");
    return Task.CompletedTask;
}

Task StepResultFactoriesEncodeStates()
{
    EngineTaskStepResult pending = EngineTaskStepResult.Pending();
    EngineTaskStepResult completed = EngineTaskStepResult.Completed();
    EngineTaskStepResult canceled = EngineTaskStepResult.Canceled();
    InvalidOperationException error = new("boom");
    EngineTaskStepResult faulted = EngineTaskStepResult.Faulted(error);

    AssertEqual(EngineTaskStatus.Pending, pending.Status, "pending status");
    AssertFalse(pending.IsTerminal, "pending terminal");
    AssertEqual(EngineTaskStatus.Completed, completed.Status, "completed status");
    AssertTrue(completed.IsTerminal, "completed terminal");
    AssertEqual(EngineTaskStatus.Canceled, canceled.Status, "canceled status");
    AssertTrue(canceled.IsTerminal, "canceled terminal");
    AssertEqual(EngineTaskStatus.Faulted, faulted.Status, "faulted status");
    AssertSame(error, faulted.Error!, "faulted error");
    AssertTrue(faulted.IsTerminal, "faulted terminal");
    ExpectThrows<ArgumentNullException>(() => EngineTaskStepResult.Waiting(null!));
    ExpectThrows<ArgumentNullException>(() => EngineTaskStepResult.Faulted(null!));
    return Task.CompletedTask;
}

async Task EngineTaskStepsCompleteDeterministically()
{
    IEngineTask task = new ScriptedEngineTask(
        EngineTaskStepResult.Pending(),
        EngineTaskStepResult.Completed());

    AssertEqual(EngineTaskStatus.Pending, task.Status, "initial status");
    AssertFalse(task.IsCompleted, "initial completed");
    AssertFalse(task.IsTerminal, "initial terminal");

    EngineTaskStepResult first = await task.StepAsync();
    EngineTaskStepResult second = await task.StepAsync();

    AssertEqual(EngineTaskStatus.Pending, first.Status, "first status");
    AssertEqual(EngineTaskStatus.Completed, second.Status, "second status");
    AssertTrue(task.IsCompleted, "final completed");
    AssertTrue(task.IsTerminal, "final terminal");
    AssertFalse(task.IsFaulted, "final faulted");
    AssertEqual(null, task.Error, "final error");
}

async Task EngineTaskFaultRecordsError()
{
    InvalidOperationException error = new("scripted failure");
    IEngineTask task = new ScriptedEngineTask(EngineTaskStepResult.Faulted(error));

    EngineTaskStepResult result = await task.StepAsync();

    AssertEqual(EngineTaskStatus.Faulted, result.Status, "fault result");
    AssertEqual(EngineTaskStatus.Faulted, task.Status, "fault status");
    AssertSame(error, result.Error!, "result error");
    AssertSame(error, task.Error!, "task error");
    AssertTrue(task.IsFaulted, "task faulted");
    AssertTrue(task.IsTerminal, "task terminal");
    AssertFalse(task.IsCompleted, "task completed");
}

async Task RunnerPropagatesFaultedTaskErrors()
{
    InvalidOperationException error = new("runner failure");
    IEngineTask task = new ScriptedEngineTask(EngineTaskStepResult.Faulted(error));
    var runner = new EngineTaskRunner();

    InvalidOperationException thrown = await ExpectThrowsAsync<InvalidOperationException>(
        () => runner.RunAsync(task));

    AssertSame(error, thrown, "runner propagated error");
    AssertTrue(task.IsFaulted, "task faulted after runner");
}

Task EngineTaskFileHasNoTodoContracts()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Coroutines", "IEngineTask.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("IEngineTask.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "contract must not reference UnityEngine");
    AssertFalse(text.Contains("Photon", StringComparison.Ordinal), "contract must not reference Photon");
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
    private readonly Queue<EngineTaskStepResult> _steps;

    public ScriptedEngineTask(params EngineTaskStepResult[] steps)
    {
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
        return Task.FromResult(result);
    }
}
