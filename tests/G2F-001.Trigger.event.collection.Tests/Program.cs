using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2F-001 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS AutoProcessing trigger collection references are recorded", AsIsAutoProcessingReferencesAreRecorded),
    ("Collector uses event timing metadata and keeps registration order", CollectorUsesTimingMetadataAndRegistrationOrder),
    ("Collector falls back to GameEventType when timing metadata is absent", CollectorFallsBackToEventTypeTiming),
    ("Collector filters candidates by source player and target metadata", CollectorFiltersByEventMetadata),
    ("Collector enqueues collected triggers into EffectScheduler", CollectorEnqueuesIntoEffectScheduler),
    ("Collector rejects unknown events with an explicit failure result", CollectorRejectsUnknownEvents),
    ("Collector returns deterministic candidates for repeated identical input", CollectorIsDeterministic),
    ("Collector maps event mode kind and priority metadata to triggers", CollectorMapsModeKindAndPriority),
    ("G2F-001 source files contain no placeholder or Unity dependency", TriggerCollectorSourceHasNoPlaceholderOrUnityDependency),
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

Task GoalRowAndPredecessorsAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2F-001")
        ?? throw new InvalidOperationException("G2F-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AutoProcessing", Value(row, "area"), "area");
    AssertEqual("Trigger event collection 포팅", Value(row, "goal"), "goal");
    AssertContains(Value(row, "scope"), "GameEvent", "scope");
    AssertEqual("AutoProcessing trigger collector", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "event trigger collection", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2F-001_trigger_collection_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-006; G1F-006", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "trigger collection", "completion_gate");

    AssertComplete("G2A-006_legal_action_dispatch_unit_test_results.md");
    AssertComplete("G1F-006_continuous_replacement_query_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsAutoProcessingReferencesAreRecorded()
{
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string multipleSkills = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MultipleSkills.cs"));
    string skillInfo = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SkillInfo.cs"));

    AssertContains(autoProcessing, "public List<SkillInfo> StackedSkillInfos", "AS-IS stacked skill list");
    AssertContains(autoProcessing, "public void PutStackedSkill(SkillInfo skillInfo)", "AS-IS PutStackedSkill");
    AssertContains(autoProcessing, "StackedSkillInfos.Add(skillInfo)", "AS-IS stack append");
    AssertContains(autoProcessing, "public static List<SkillInfo> GetSkillInfos", "AS-IS GetSkillInfos");
    AssertContains(autoProcessing, "cardEffect.CanTrigger(hashtable)", "AS-IS CanTrigger filter");
    AssertContains(autoProcessing, "public IEnumerator StackSkillInfos", "AS-IS StackSkillInfos");
    AssertContains(multipleSkills, "ActivateMultipleSkills", "AS-IS multiple skill activation");
    AssertContains(skillInfo, "class SkillInfo", "AS-IS SkillInfo model");
    return Task.CompletedTask;
}

Task CollectorUsesTimingMetadataAndRegistrationOrder()
{
    var query = new InMemoryEffectQueryService();
    EffectRequest first = CreateRequest("effect-1", "Main", player: 1, source: "card-a");
    EffectRequest ignored = CreateRequest("effect-ignored", "Attack", player: 1, source: "card-a");
    EffectRequest second = CreateRequest("effect-2", "Main", player: 2, source: "card-b");
    query.Register(first);
    query.Register(ignored);
    query.Register(second);
    var collector = new AutoProcessingTriggerCollector(query);
    GameEvent gameEvent = CreateEvent(
        GameEventType.StateChanged,
        7,
        new Dictionary<string, object?> { ["triggerTiming"] = " Main " });

    TriggerCollectionResult result = collector.Collect(gameEvent);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual("Main", result.Timing, "resolved timing");
    AssertEqual(2, result.Triggers.Count, "trigger count");
    AssertSame(first, result.Triggers[0].Request, "first request");
    AssertSame(second, result.Triggers[1].Request, "second request");
    AssertEqual(0L, result.Triggers[0].Sequence, "first sequence");
    AssertEqual(1L, result.Triggers[1].Sequence, "second sequence");
    return Task.CompletedTask;
}

Task CollectorFallsBackToEventTypeTiming()
{
    var query = new InMemoryEffectQueryService();
    EffectRequest attackEffect = CreateRequest("effect-attack", "AttackDeclared", player: 1, source: "attacker");
    query.Register(attackEffect);
    var collector = new AutoProcessingTriggerCollector(query);
    GameEvent gameEvent = CreateEvent(GameEventType.AttackDeclared, 3);

    TriggerCollectionResult result = collector.Collect(gameEvent);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual("AttackDeclared", result.Timing, "fallback timing");
    AssertEqual(1, result.Triggers.Count, "trigger count");
    AssertSame(attackEffect, result.Triggers[0].Request, "fallback request");
    return Task.CompletedTask;
}

Task CollectorFiltersByEventMetadata()
{
    var query = new InMemoryEffectQueryService();
    EffectRequest matching = CreateRequest(
        "effect-match",
        "CardMoved",
        player: 1,
        source: "card-a",
        trigger: "card-a",
        targets: new[] { "card-a" });
    EffectRequest wrongPlayer = CreateRequest("effect-wrong-player", "CardMoved", player: 2, source: "card-a", targets: new[] { "card-a" });
    EffectRequest wrongSource = CreateRequest("effect-wrong-source", "CardMoved", player: 1, source: "card-b", targets: new[] { "card-a" });
    EffectRequest wrongTarget = CreateRequest("effect-wrong-target", "CardMoved", player: 1, source: "card-a", targets: new[] { "card-c" });
    query.Register(matching);
    query.Register(wrongPlayer);
    query.Register(wrongSource);
    query.Register(wrongTarget);
    var collector = new AutoProcessingTriggerCollector(query);
    GameEvent gameEvent = CreateEvent(
        GameEventType.CardMoved,
        11,
        new Dictionary<string, object?>
        {
            ["playerId"] = 1,
            ["sourceEntityId"] = "card-a",
            ["targetEntityId"] = "card-a"
        });

    TriggerCollectionResult result = collector.Collect(gameEvent);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual(1, result.Triggers.Count, "filtered trigger count");
    AssertSame(matching, result.Triggers[0].Request, "matching request");
    return Task.CompletedTask;
}

Task CollectorEnqueuesIntoEffectScheduler()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateRequest("effect-1", "Main", player: 1, source: "card-a"));
    query.Register(CreateRequest("effect-2", "Main", player: 1, source: "card-b"));
    var collector = new AutoProcessingTriggerCollector(query);
    var scheduler = new EffectScheduler();
    GameEvent gameEvent = CreateEvent(
        GameEventType.StateChanged,
        4,
        new Dictionary<string, object?> { ["timing"] = "Main" });

    TriggerCollectionResult result = collector.CollectAndEnqueue(gameEvent, scheduler);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual(2, result.EnqueuedCount, "enqueued count");
    AssertEqual(2, scheduler.PendingCount, "scheduler pending count");
    AssertEqual(2, scheduler.TotalEnqueuedCount, "scheduler total enqueued count");
    return Task.CompletedTask;
}

Task CollectorRejectsUnknownEvents()
{
    var collector = new AutoProcessingTriggerCollector(new InMemoryEffectQueryService());
    GameEvent gameEvent = CreateEvent(GameEventType.Unknown, 0);

    TriggerCollectionResult result = collector.Collect(gameEvent);

    AssertFalse(result.IsSuccess, "unknown event success");
    AssertEqual(0, result.Triggers.Count, "unknown trigger count");
    AssertContains(result.FailureReason, "Unknown game events", "failure reason");
    return Task.CompletedTask;
}

Task CollectorIsDeterministic()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateRequest("effect-a", "Main", player: 1, source: "card-a"));
    query.Register(CreateRequest("effect-b", "Main", player: 1, source: "card-b"));
    query.Register(CreateRequest("effect-c", "Main", player: 1, source: "card-c"));
    var collector = new AutoProcessingTriggerCollector(query);
    GameEvent gameEvent = CreateEvent(
        GameEventType.StateChanged,
        8,
        new Dictionary<string, object?> { ["effectTiming"] = "Main" });

    string first = JoinEffectIds(collector.Collect(gameEvent).Triggers);
    string second = JoinEffectIds(collector.Collect(gameEvent).Triggers);

    AssertEqual(first, second, "repeated collection");
    AssertEqual("effect-a,effect-b,effect-c", first, "deterministic order");
    return Task.CompletedTask;
}

Task CollectorMapsModeKindAndPriority()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateRequest("effect-optional", "Main", player: 1, source: "card-a"));
    var collector = new AutoProcessingTriggerCollector(query);
    GameEvent gameEvent = CreateEvent(
        GameEventType.StateChanged,
        9,
        new Dictionary<string, object?>
        {
            ["triggerTiming"] = "Main",
            ["resolutionMode"] = "CutIn",
            ["triggerKind"] = "Optional",
            ["priority"] = -5
        });

    TimingWindowTrigger trigger = collector.Collect(gameEvent).Triggers.Single();

    AssertEqual(EffectResolutionMode.CutIn, trigger.Mode, "mode");
    AssertEqual(TimingWindowTriggerKind.Optional, trigger.Kind, "kind");
    AssertEqual(-5, trigger.Priority, "priority");
    return Task.CompletedTask;
}

Task TriggerCollectorSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "AutoProcessingTriggerCollector.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "collector must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "collector must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "collector must not reference MonoBehaviour");
    AssertContains(text, "Collect(GameEvent gameEvent)", "collect API");
    AssertContains(text, "CollectAndEnqueue", "scheduler enqueue API");
    AssertContains(text, "TriggerCollectionResult", "result model");
    return Task.CompletedTask;
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static EffectRequest CreateRequest(
    string effectId,
    string timing,
    int player,
    string source,
    string? trigger = null,
    IReadOnlyList<string>? targets = null)
{
    var targetIds = (targets ?? Array.Empty<string>())
        .Select(target => new HeadlessEntityId(target))
        .ToArray();

    return new EffectRequest(
        new HeadlessEntityId(effectId),
        new HeadlessPlayerId(player),
        timing,
        new EffectContext(
            new HeadlessPlayerId(player),
            new HeadlessPlayerId(player),
            new HeadlessEntityId(source),
            trigger is null ? null : new HeadlessEntityId(trigger),
            targetIds));
}

static GameEvent CreateEvent(
    GameEventType type,
    long sequence,
    IReadOnlyDictionary<string, object?>? metadata = null)
{
    return new GameEvent(
        sequence,
        type,
        $"Event {type}",
        metadata ?? new Dictionary<string, object?>());
}

static string JoinEffectIds(IEnumerable<TimingWindowTrigger> triggers)
{
    return string.Join(",", triggers.Select(trigger => trigger.Request.EffectId.Value));
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    List<List<string>> records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException("CSV file was empty.");
    }

    string[] headers = records[0].ToArray();
    var rows = new List<Dictionary<string, string>>();
    foreach (List<string> record in records.Skip(1))
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            row[headers[index]] = index < record.Count ? record[index] : string.Empty;
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
    bool inQuotes = false;

    for (int index = 0; index < text.Length; index++)
    {
        char current = text[index];
        if (inQuotes)
        {
            if (current == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(current);
            }

            continue;
        }

        if (current == '"')
        {
            inQuotes = true;
        }
        else if (current == ',')
        {
            record.Add(field.ToString());
            field.Clear();
        }
        else if (current == '\r')
        {
            if (index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            record.Add(field.ToString());
            field.Clear();
            records.Add(record);
            record = new List<string>();
        }
        else if (current == '\n')
        {
            record.Add(field.ToString());
            field.Clear();
            records.Add(record);
            record = new List<string>();
        }
        else
        {
            field.Append(current);
        }
    }

    if (field.Length > 0 || record.Count > 0)
    {
        record.Add(field.ToString());
        records.Add(record);
    }

    return records
        .Where(candidate => candidate.Any(value => value.Length > 0))
        .ToList();
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"CSV column was not found: {key}");
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to contain '{expected}'.");
    }
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
