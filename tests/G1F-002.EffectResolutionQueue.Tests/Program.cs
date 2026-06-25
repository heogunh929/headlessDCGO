using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-002 goal row keeps the EffectResolutionQueue contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("PendingEffect preserves request and mode while rejecting invalid values", PendingEffectPreservesRequestAndMode),
    ("EffectResolutionQueue enqueues and dequeues effects in FIFO order", QueueEnqueuesAndDequeuesInFifoOrder),
    ("EffectResolutionQueue peek does not consume first effect", QueuePeekDoesNotConsumeFirstEffect),
    ("EffectResolutionQueue clear returns removed count and empties queue", QueueClearReturnsRemovedCountAndEmptiesQueue),
    ("EffectResolutionQueue snapshot is isolated from later mutations", QueueSnapshotIsIsolatedFromLaterMutations),
    ("EffectResolutionQueue rejects null effects and empty dequeue is explicit", QueueRejectsNullAndEmptyDequeueIsExplicit),
    ("AS-IS queue references remain read-only inputs", AsIsQueueReferencesRemainReadOnlyInputs),
    ("Effect queue source files have no placeholder or Unity dependency", EffectQueueSourceFilesHaveNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-002")
        ?? throw new InvalidOperationException("G1F-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("EffectResolutionQueue", Value(row, "goal"), "goal");
    AssertEqual("effect queue ordering 확정", Value(row, "scope"), "scope");
    AssertEqual("EffectResolutionQueue PendingEffect", Value(row, "deliverables"), "deliverables");
    AssertEqual("enqueue dequeue order clear 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-002_effect_resolution_queue_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1F-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Effect queue 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1F-001_effect_context_schema_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1F-001 COMPLETE");
    return Task.CompletedTask;
}

Task PendingEffectPreservesRequestAndMode()
{
    EffectRequest request = CreateRequest("effect-a");
    var pending = new PendingEffect(request, EffectResolutionMode.MainStack);

    AssertSame(request, pending.Request, "request");
    AssertEqual(EffectResolutionMode.MainStack, pending.Mode, "mode");
    ExpectThrows<ArgumentNullException>(() => new PendingEffect(null!, EffectResolutionMode.MainStack));
    ExpectThrows<ArgumentOutOfRangeException>(() => new PendingEffect(request, (EffectResolutionMode)999));
    return Task.CompletedTask;
}

Task QueueEnqueuesAndDequeuesInFifoOrder()
{
    var queue = new EffectResolutionQueue();
    PendingEffect first = CreatePending("effect-1", EffectResolutionMode.MainStack);
    PendingEffect second = CreatePending("effect-2", EffectResolutionMode.CutIn);
    PendingEffect third = CreatePending("effect-3", EffectResolutionMode.Background);

    queue.Enqueue(first);
    queue.Enqueue(second);
    queue.Enqueue(third);

    AssertEqual(3, queue.Count, "count after enqueue");
    AssertDequeued(queue, first, "first");
    AssertDequeued(queue, second, "second");
    AssertDequeued(queue, third, "third");
    AssertFalse(queue.TryDequeue(out PendingEffect? empty), "empty dequeue");
    AssertEqual(null, empty, "empty effect");
    AssertEqual(0, queue.Count, "count after dequeue");
    return Task.CompletedTask;
}

Task QueuePeekDoesNotConsumeFirstEffect()
{
    var queue = new EffectResolutionQueue();
    PendingEffect first = CreatePending("effect-1", EffectResolutionMode.MainStack);
    PendingEffect second = CreatePending("effect-2", EffectResolutionMode.CutIn);

    AssertFalse(queue.TryPeek(out PendingEffect? empty), "empty peek");
    AssertEqual(null, empty, "empty peek effect");

    queue.Enqueue(first);
    queue.Enqueue(second);

    AssertTrue(queue.TryPeek(out PendingEffect? peeked), "peek result");
    AssertSame(first, peeked!, "peeked effect");
    AssertEqual(2, queue.Count, "count after peek");

    AssertDequeued(queue, first, "first after peek");
    return Task.CompletedTask;
}

Task QueueClearReturnsRemovedCountAndEmptiesQueue()
{
    var queue = new EffectResolutionQueue();
    AssertEqual(0, queue.Clear(), "empty clear count");

    queue.Enqueue(CreatePending("effect-1", EffectResolutionMode.MainStack));
    queue.Enqueue(CreatePending("effect-2", EffectResolutionMode.CutIn));

    AssertEqual(2, queue.Clear(), "clear count");
    AssertEqual(0, queue.Count, "count after clear");
    AssertFalse(queue.TryDequeue(out _), "dequeue after clear");
    return Task.CompletedTask;
}

Task QueueSnapshotIsIsolatedFromLaterMutations()
{
    var queue = new EffectResolutionQueue();
    PendingEffect first = CreatePending("effect-1", EffectResolutionMode.MainStack);
    PendingEffect second = CreatePending("effect-2", EffectResolutionMode.CutIn);

    queue.Enqueue(first);
    IReadOnlyList<PendingEffect> snapshot = queue.Snapshot();
    queue.Enqueue(second);
    AssertDequeued(queue, first, "first");

    AssertEqual(1, snapshot.Count, "snapshot count");
    AssertSame(first, snapshot[0], "snapshot first");
    AssertEqual(1, queue.Count, "queue count after mutation");
    return Task.CompletedTask;
}

Task QueueRejectsNullAndEmptyDequeueIsExplicit()
{
    var queue = new EffectResolutionQueue();

    ExpectThrows<ArgumentNullException>(() => queue.Enqueue(null!));
    AssertFalse(queue.TryPeek(out PendingEffect? peeked), "empty peek");
    AssertEqual(null, peeked, "empty peek effect");
    AssertFalse(queue.TryDequeue(out PendingEffect? dequeued), "empty dequeue");
    AssertEqual(null, dequeued, "empty dequeue effect");
    AssertEqual(0, queue.Count, "empty count");
    return Task.CompletedTask;
}

Task AsIsQueueReferencesRemainReadOnlyInputs()
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

Task EffectQueueSourceFilesHaveNoPlaceholderOrUnityDependency()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectResolutionQueue.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "PendingEffect.cs"),
    };

    foreach (string path in paths)
    {
        string text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} still contains a TODO placeholder.");
        }

        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference UnityEngine");
        AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference MonoBehaviour");
    }

    string queueText = File.ReadAllText(paths[0]);
    AssertTrue(queueText.Contains("TryPeek", StringComparison.Ordinal), "peek contract");
    AssertTrue(queueText.Contains("Snapshot", StringComparison.Ordinal), "snapshot contract");
    return Task.CompletedTask;
}

static PendingEffect CreatePending(
    string effectId,
    EffectResolutionMode mode)
{
    return new PendingEffect(CreateRequest(effectId), mode);
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

static void AssertDequeued(
    EffectResolutionQueue queue,
    PendingEffect expected,
    string label)
{
    AssertTrue(queue.TryDequeue(out PendingEffect? actual), $"{label} dequeue result");
    AssertSame(expected, actual!, $"{label} effect");
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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
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
