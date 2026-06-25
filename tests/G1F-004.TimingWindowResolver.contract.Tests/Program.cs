using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-004 goal row keeps the TimingWindowResolver contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("TimingWindow trims timing and rejects invalid values", TimingWindowValidatesTiming),
    ("CollectTriggers returns only matching timing requests in registration order", CollectTriggersReturnsMatchingTimingRequests),
    ("SortTriggers orders mandatory before optional and priority before sequence", SortTriggersAppliesContractOrdering),
    ("SortTriggers is deterministic for repeated equivalent inputs", SortTriggersIsDeterministic),
    ("OpenWindow returns sorted PendingEffect values for scheduler enqueue", OpenWindowReturnsSortedPendingEffects),
    ("Timing trigger validates request mode kind and sequence", TimingTriggerValidatesInputs),
    ("Empty window has no pending effects", EmptyWindowHasNoPendingEffects),
    ("AS-IS timing references remain read-only inputs", AsIsTimingReferencesRemainReadOnlyInputs),
    ("TimingWindowResolver source has no placeholder or Unity dependency", TimingWindowResolverSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-004")
        ?? throw new InvalidOperationException("G1F-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("TimingWindowResolver contract", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("trigger timing contract", StringComparison.Ordinal), "scope");
    AssertEqual("TimingWindowResolver interface", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("trigger collection ordering contract", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-004_timing_window_contract_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1F-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Timing", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1F-003_effect_scheduler_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1F-003 COMPLETE");
    return Task.CompletedTask;
}

Task TimingWindowValidatesTiming()
{
    var window = new TimingWindow(" Main ");

    AssertEqual("Main", window.Timing, "trimmed timing");
    ExpectThrows<ArgumentNullException>(() => new TimingWindow(null!));
    ExpectThrows<ArgumentException>(() => new TimingWindow(" "));
    return Task.CompletedTask;
}

Task CollectTriggersReturnsMatchingTimingRequests()
{
    var effects = new InMemoryEffectQueryService();
    EffectRequest first = CreateRequest("effect-1", "Main");
    EffectRequest second = CreateRequest("effect-2", "Attack");
    EffectRequest third = CreateRequest("effect-3", "Main");
    effects.Register(first);
    effects.Register(second);
    effects.Register(third);

    var resolver = new DefaultTimingWindowResolver(effects);

    IReadOnlyList<TimingWindowTrigger> triggers = resolver.CollectTriggers(new TimingWindow("Main"));

    AssertEqual(2, triggers.Count, "trigger count");
    AssertSame(first, triggers[0].Request, "first request");
    AssertSame(third, triggers[1].Request, "third request");
    AssertEqual(EffectResolutionMode.MainStack, triggers[0].Mode, "default mode");
    AssertEqual(TimingWindowTriggerKind.Mandatory, triggers[0].Kind, "default kind");
    AssertEqual(0, triggers[0].Priority, "default priority");
    AssertEqual(0L, triggers[0].Sequence, "first sequence");
    AssertEqual(1L, triggers[1].Sequence, "second sequence");
    return Task.CompletedTask;
}

Task SortTriggersAppliesContractOrdering()
{
    var resolver = new DefaultTimingWindowResolver(new InMemoryEffectQueryService());
    TimingWindowTrigger optionalFast = CreateTrigger("optional-fast", TimingWindowTriggerKind.Optional, priority: -10, sequence: 0);
    TimingWindowTrigger mandatoryLate = CreateTrigger("mandatory-late", TimingWindowTriggerKind.Mandatory, priority: 10, sequence: 0);
    TimingWindowTrigger mandatoryEarly = CreateTrigger("mandatory-early", TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 2);
    TimingWindowTrigger mandatoryFirst = CreateTrigger("mandatory-first", TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 1);
    TimingWindowTrigger optionalNormal = CreateTrigger("optional-normal", TimingWindowTriggerKind.Optional, priority: 0, sequence: 0);

    IReadOnlyList<TimingWindowTrigger> sorted = resolver.SortTriggers(new[]
    {
        optionalFast,
        mandatoryLate,
        mandatoryEarly,
        mandatoryFirst,
        optionalNormal,
    });

    AssertEqual("mandatory-first,mandatory-early,mandatory-late,optional-fast,optional-normal", JoinIds(sorted), "sort order");
    return Task.CompletedTask;
}

Task SortTriggersIsDeterministic()
{
    var resolver = new DefaultTimingWindowResolver(new InMemoryEffectQueryService());
    TimingWindowTrigger[] triggers =
    {
        CreateTrigger("effect-c", TimingWindowTriggerKind.Optional, priority: 1, sequence: 3),
        CreateTrigger("effect-a", TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 1),
        CreateTrigger("effect-b", TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 2),
        CreateTrigger("effect-d", TimingWindowTriggerKind.Optional, priority: 1, sequence: 4),
    };

    string first = JoinIds(resolver.SortTriggers(triggers));
    string second = JoinIds(resolver.SortTriggers(triggers));

    AssertEqual(first, second, "repeated sort order");
    AssertEqual("effect-a,effect-b,effect-c,effect-d", first, "expected order");
    return Task.CompletedTask;
}

Task OpenWindowReturnsSortedPendingEffects()
{
    var effects = new InMemoryEffectQueryService();
    effects.Register(CreateRequest("effect-1", "Main"));
    effects.Register(CreateRequest("effect-2", "Attack"));
    effects.Register(CreateRequest("effect-3", "Main"));

    var resolver = new DefaultTimingWindowResolver(effects);
    IReadOnlyList<PendingEffect> pendingEffects = resolver.OpenWindow(new TimingWindow("Main"));

    AssertEqual(2, pendingEffects.Count, "pending effect count");
    AssertEqual("effect-1", pendingEffects[0].Request.EffectId.Value, "first pending");
    AssertEqual("effect-3", pendingEffects[1].Request.EffectId.Value, "second pending");
    AssertEqual(EffectResolutionMode.MainStack, pendingEffects[0].Mode, "pending mode");

    var scheduler = new EffectScheduler();
    foreach (PendingEffect pendingEffect in pendingEffects)
    {
        scheduler.Enqueue(pendingEffect.Request, pendingEffect.Mode);
    }

    AssertEqual(2, scheduler.PendingCount, "scheduler pending count");
    return Task.CompletedTask;
}

Task TimingTriggerValidatesInputs()
{
    EffectRequest request = CreateRequest("effect-validation", "Main");

    ExpectThrows<ArgumentNullException>(() => new TimingWindowTrigger(null!, EffectResolutionMode.MainStack, TimingWindowTriggerKind.Mandatory, 0, 0));
    ExpectThrows<ArgumentOutOfRangeException>(() => new TimingWindowTrigger(request, (EffectResolutionMode)999, TimingWindowTriggerKind.Mandatory, 0, 0));
    ExpectThrows<ArgumentOutOfRangeException>(() => new TimingWindowTrigger(request, EffectResolutionMode.MainStack, (TimingWindowTriggerKind)999, 0, 0));
    ExpectThrows<ArgumentOutOfRangeException>(() => new TimingWindowTrigger(request, EffectResolutionMode.MainStack, TimingWindowTriggerKind.Mandatory, 0, -1));
    ExpectThrows<ArgumentNullException>(() => new DefaultTimingWindowResolver(null!));
    return Task.CompletedTask;
}

Task EmptyWindowHasNoPendingEffects()
{
    var resolver = new DefaultTimingWindowResolver(new InMemoryEffectQueryService());

    AssertEqual(0, resolver.CollectTriggers(new TimingWindow("Main")).Count, "empty collect");
    AssertEqual(0, resolver.OpenWindow(new TimingWindow("Main")).Count, "empty pending");
    return Task.CompletedTask;
}

Task AsIsTimingReferencesRemainReadOnlyInputs()
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

Task TimingWindowResolverSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Rules", "TimingWindowResolver.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("TimingWindowResolver.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "TimingWindowResolver must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "TimingWindowResolver must not reference MonoBehaviour");
    AssertTrue(text.Contains("CollectTriggers", StringComparison.Ordinal), "collect triggers contract");
    AssertTrue(text.Contains("SortTriggers", StringComparison.Ordinal), "sort triggers contract");
    AssertTrue(text.Contains("OpenWindow", StringComparison.Ordinal), "open window contract");
    return Task.CompletedTask;
}

static TimingWindowTrigger CreateTrigger(
    string effectId,
    TimingWindowTriggerKind kind,
    int priority,
    long sequence)
{
    return new TimingWindowTrigger(
        CreateRequest(effectId, "Main"),
        EffectResolutionMode.MainStack,
        kind,
        priority,
        sequence);
}

static EffectRequest CreateRequest(string effectId, string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        timing,
        new EffectContext(
            player,
            player,
            new HeadlessEntityId($"source-{effectId}"),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static string JoinIds(IEnumerable<TimingWindowTrigger> triggers)
{
    return string.Join(",", triggers.Select(trigger => trigger.Request.EffectId.Value));
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

static void AssertSame<T>(T expected, T? actual, string label)
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
