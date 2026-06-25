using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-003 goal row keeps the EffectScheduler contract", GoalRowKeepsExpectedContract),
    ("Predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("ResolveNext resolves one effect and consumes only on success", ResolveNextResolvesOneEffectAndConsumesOnlyOnSuccess),
    ("ResolveAll resolves pending effects in FIFO order", ResolveAllResolvesPendingEffectsInFifoOrder),
    ("Choice pause result keeps pending effect and stops ResolveAll", ChoicePauseKeepsPendingEffectAndStopsResolveAll),
    ("Resolver failure returns traceable failure and keeps pending effect", ResolverFailureKeepsPendingEffect),
    ("Cancellation propagates and keeps pending effect", CancellationPropagatesAndKeepsPendingEffect),
    ("Scheduler validates queue request and mode inputs", SchedulerValidatesInputs),
    ("Clear empties queue and resets scheduler counters", ClearEmptiesQueueAndResetsCounters),
    ("AS-IS effect scheduler references remain read-only inputs", AsIsEffectSchedulerReferencesRemainReadOnlyInputs),
    ("EffectScheduler source has no placeholder or Unity dependency", EffectSchedulerSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-003")
        ?? throw new InvalidOperationException("G1F-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("EffectScheduler", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("effect resolve orchestration", StringComparison.Ordinal), "scope");
    AssertEqual("EffectScheduler", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("resolve next resolve all choice pause", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-003_effect_scheduler_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1F-002; G1E-005", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("EffectScheduler", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1F-002_effect_resolution_queue_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1E-005_choice_pause_resume_unit_test_results.md"),
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

async Task ResolveNextResolvesOneEffectAndConsumesOnlyOnSuccess()
{
    var seen = new List<string>();
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (request, _) =>
        {
            seen.Add(request.EffectId.Value);
            return Task.FromResult(EffectResult.Success($"resolved {request.EffectId.Value}"));
        });

    scheduler.Enqueue(CreateRequest("effect-1"), EffectResolutionMode.MainStack);
    scheduler.Enqueue(CreateRequest("effect-2"), EffectResolutionMode.CutIn);

    EffectResult result = await scheduler.ResolveNextAsync();

    AssertTrue(result.Resolved, "resolved");
    AssertEqual("resolved effect-1", result.Message, "message");
    AssertEqual(2, scheduler.TotalEnqueuedCount, "total enqueued");
    AssertEqual(1, scheduler.TotalResolvedCount, "total resolved");
    AssertEqual(1, scheduler.LastResolvedCount, "last resolved");
    AssertEqual(1, scheduler.PendingCount, "pending count");
    AssertEqual("effect-1", string.Join(",", seen), "resolver order");
}

async Task ResolveAllResolvesPendingEffectsInFifoOrder()
{
    var seen = new List<string>();
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (request, _) =>
        {
            seen.Add(request.EffectId.Value);
            return Task.FromResult(EffectResult.Success(values: new Dictionary<string, object?>
            {
                ["effectId"] = request.EffectId.Value,
            }));
        });

    scheduler.Enqueue(CreateRequest("effect-1"), EffectResolutionMode.MainStack);
    scheduler.Enqueue(CreateRequest("effect-2"), EffectResolutionMode.CutIn);
    scheduler.Enqueue(CreateRequest("effect-3"), EffectResolutionMode.Background);

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertEqual(3, results.Count, "result count");
    AssertTrue(results.All(result => result.Resolved), "all resolved");
    AssertEqual("effect-1,effect-2,effect-3", string.Join(",", seen), "FIFO order");
    AssertEqual(0, scheduler.PendingCount, "pending count");
    AssertEqual(3, scheduler.TotalResolvedCount, "total resolved");
    AssertEqual(3, scheduler.LastResolvedCount, "last resolved");
}

async Task ChoicePauseKeepsPendingEffectAndStopsResolveAll()
{
    var seen = new List<string>();
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (request, _) =>
        {
            seen.Add(request.EffectId.Value);
            if (request.EffectId.Value == "effect-choice")
            {
                return Task.FromResult(EffectResult.Failure(
                    "Choice pending.",
                    new Dictionary<string, object?> { ["requiresChoice"] = true }));
            }

            return Task.FromResult(EffectResult.Success());
        });

    scheduler.Enqueue(CreateRequest("effect-resolved"), EffectResolutionMode.MainStack);
    scheduler.Enqueue(CreateRequest("effect-choice"), EffectResolutionMode.MainStack);
    scheduler.Enqueue(CreateRequest("effect-after-choice"), EffectResolutionMode.MainStack);

    IReadOnlyList<EffectResult> results = await scheduler.ResolveAllAsync();

    AssertEqual(2, results.Count, "result count");
    AssertTrue(results[0].Resolved, "first resolved");
    AssertFalse(results[1].Resolved, "choice pause unresolved");
    AssertEqual("Choice pending.", results[1].Message, "pause message");
    AssertEqual(true, ReadValue<bool>(results[1], "requiresChoice"), "requires choice metadata");
    AssertEqual("effect-resolved,effect-choice", string.Join(",", seen), "resolver order before pause");
    AssertEqual(2, scheduler.PendingCount, "pending count after pause");
    AssertEqual(1, scheduler.TotalResolvedCount, "total resolved");
    AssertEqual(1, scheduler.LastResolvedCount, "last resolved batch");

    EffectResult secondAttempt = await scheduler.ResolveNextAsync();
    AssertFalse(secondAttempt.Resolved, "choice remains first pending effect");
    AssertEqual("effect-resolved,effect-choice,effect-choice", string.Join(",", seen), "retry same effect");
    AssertEqual(2, scheduler.PendingCount, "pending count after retry");
}

async Task ResolverFailureKeepsPendingEffect()
{
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (_, _) => throw new InvalidOperationException("resolver boom"));

    scheduler.Enqueue(CreateRequest("effect-fail"), EffectResolutionMode.RuleProcess);

    EffectResult result = await scheduler.ResolveNextAsync();

    AssertFalse(result.Resolved, "failure result");
    AssertEqual("Effect resolver failed.", result.Message, "failure message");
    AssertEqual("effect-fail", ReadValue<string>(result, "effectId"), "effect id metadata");
    AssertEqual("RuleProcess", ReadValue<string>(result, "mode"), "mode metadata");
    AssertEqual("resolver boom", ReadValue<string>(result, "error"), "error metadata");
    AssertEqual(nameof(InvalidOperationException), ReadValue<string>(result, "errorType"), "error type metadata");
    AssertEqual(1, scheduler.PendingCount, "pending kept");
    AssertEqual(0, scheduler.TotalResolvedCount, "total resolved");
    AssertEqual(0, scheduler.LastResolvedCount, "last resolved");
}

async Task CancellationPropagatesAndKeepsPendingEffect()
{
    using var cts = new CancellationTokenSource();
    var scheduler = new EffectScheduler(
        new EffectResolutionQueue(),
        (_, token) =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(EffectResult.Success());
        });

    scheduler.Enqueue(CreateRequest("effect-cancel"), EffectResolutionMode.MainStack);

    await ExpectThrowsAsync<OperationCanceledException>(() => scheduler.ResolveNextAsync(cts.Token));
    AssertEqual(1, scheduler.PendingCount, "pending kept");
    AssertEqual(0, scheduler.TotalResolvedCount, "total resolved");
    AssertEqual(0, scheduler.LastResolvedCount, "last resolved");
}

Task SchedulerValidatesInputs()
{
    var scheduler = new EffectScheduler();
    EffectRequest request = CreateRequest("effect-validation");

    ExpectThrows<ArgumentNullException>(() => new EffectScheduler(null!));
    ExpectThrows<ArgumentNullException>(() => scheduler.Enqueue(null!));
    ExpectThrows<ArgumentOutOfRangeException>(() => scheduler.Enqueue(request, (EffectResolutionMode)999));
    AssertEqual(0, scheduler.TotalEnqueuedCount, "total enqueued after rejected inputs");
    AssertEqual(0, scheduler.PendingCount, "pending after rejected inputs");
    return Task.CompletedTask;
}

Task ClearEmptiesQueueAndResetsCounters()
{
    var scheduler = new EffectScheduler();

    scheduler.Enqueue(CreateRequest("effect-1"), EffectResolutionMode.MainStack);
    scheduler.Enqueue(CreateRequest("effect-2"), EffectResolutionMode.CutIn);

    AssertEqual(2, scheduler.PendingCount, "pending before clear");
    AssertEqual(2, scheduler.TotalEnqueuedCount, "total enqueued before clear");

    scheduler.Clear();

    AssertFalse(scheduler.HasPendingEffects, "has pending after clear");
    AssertEqual(0, scheduler.PendingCount, "pending after clear");
    AssertEqual(0, scheduler.TotalEnqueuedCount, "total enqueued after clear");
    AssertEqual(0, scheduler.TotalResolvedCount, "total resolved after clear");
    AssertEqual(0, scheduler.LastResolvedCount, "last resolved after clear");
    return Task.CompletedTask;
}

Task AsIsEffectSchedulerReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"),
            new[] { "Skill", "Stack", "Effect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Effects.cs"),
            new[] { "Effects", "MonoBehaviour" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MultipleSkills.cs"),
            new[] { "MultipleSkills", "SkillInfo" }),
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

Task EffectSchedulerSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectScheduler.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("EffectScheduler.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "EffectScheduler must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "EffectScheduler must not reference MonoBehaviour");
    AssertTrue(text.Contains("TryPeek", StringComparison.Ordinal), "resolve should inspect before consuming");
    AssertTrue(text.Contains("break;", StringComparison.Ordinal), "resolve all should stop on unresolved result");
    return Task.CompletedTask;
}

static EffectRequest CreateRequest(string effectId)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        "Main",
        new EffectContext(
            player,
            player,
            new HeadlessEntityId($"source-{effectId}"),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static T ReadValue<T>(EffectResult result, string key)
{
    if (!result.Values.TryGetValue(key, out object? value) || value is not T typedValue)
    {
        throw new InvalidOperationException($"Expected value '{key}' with type {typeof(T).Name}.");
    }

    return typedValue;
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
