using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId TurnPlayer = new(1);
HeadlessPlayerId NonTurnPlayer = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2F-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS mandatory ordering references are recorded", AsIsMandatoryOrderingReferencesAreRecorded),
    ("Ordering places turn player mandatory effects before non-turn player effects", OrderingPlacesTurnPlayerFirst),
    ("Ordering defers optional triggers without enqueueing them", OrderingDefersOptionalTriggers),
    ("Ordering sorts mandatory effects by priority sequence and stable input order", OrderingSortsByPrioritySequenceAndInput),
    ("Ordering keeps deterministic results for repeated identical input", OrderingIsDeterministic),
    ("Ordering reports unknown player mandatory triggers separately", OrderingReportsUnknownPlayers),
    ("Ordering enqueues mandatory effects into scheduler in sorted order", OrderingEnqueuesIntoScheduler),
    ("Ordering returns explicit failure results for invalid input", OrderingReturnsFailureForInvalidInput),
    ("G2F-002 source files contain no placeholder or Unity dependency", MandatoryOrderingSourceHasNoPlaceholderOrUnityDependency),
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

Task GoalRowAndPredecessorAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2F-002")
        ?? throw new InvalidOperationException("G2F-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AutoProcessing", Value(row, "area"), "area");
    AssertEqual("Mandatory effect ordering 포팅", Value(row, "goal"), "goal");
    AssertContains(Value(row, "scope"), "mandatory effect", "scope");
    AssertEqual("mandatory effect ordering", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "mandatory order", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2F-002_mandatory_effect_order_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2F-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2F-001_trigger_collection_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2F-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsMandatoryOrderingReferencesAreRecorded()
{
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string multipleSkills = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MultipleSkills.cs"));
    string continuousController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ContinuousController.cs"));

    AssertContains(autoProcessing, "TriggeredSkillProcess", "AS-IS trigger processing entry");
    AssertContains(autoProcessing, "StackedSkillInfos.Remove(skillInfo)", "AS-IS removes triggered skills");
    AssertContains(multipleSkills, "TurnPlayerSkillInfos", "AS-IS turn player group");
    AssertContains(multipleSkills, "NonTurnPlayerSkillInfos", "AS-IS non-turn player group");
    AssertContains(multipleSkills, "ActivateMultipleSkills_OnePlayer(TurnPlayerSkillInfos", "AS-IS turn player first");
    AssertContains(multipleSkills, "ActivateMultipleSkills_OnePlayer(NonTurnPlayerSkillInfos", "AS-IS non-turn player second");
    AssertContains(multipleSkills, "AutomaticOrder.GetSkillIndexAutomaticOrder", "AS-IS auto order hook");
    AssertContains(continuousController, "autoEffectOrder", "AS-IS auto effect order setting");
    return Task.CompletedTask;
}

Task OrderingPlacesTurnPlayerFirst()
{
    var ordering = new MandatoryEffectOrdering();
    TimingWindowTrigger nonTurnEarly = CreateTrigger("non-turn", player: 2, priority: -100, sequence: 0);
    TimingWindowTrigger turnLate = CreateTrigger("turn-late", player: 1, priority: 10, sequence: 3);
    TimingWindowTrigger turnEarly = CreateTrigger("turn-early", player: 1, priority: 0, sequence: 2);

    MandatoryEffectOrderResult result = ordering.Order(
        new[] { nonTurnEarly, turnLate, turnEarly },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual("turn-early,turn-late,non-turn", JoinIds(result.OrderedMandatoryTriggers), "player grouped order");
    return Task.CompletedTask;
}

Task OrderingDefersOptionalTriggers()
{
    var ordering = new MandatoryEffectOrdering();
    TimingWindowTrigger mandatory = CreateTrigger("mandatory", player: 1, priority: 0, sequence: 0);
    TimingWindowTrigger optional = CreateTrigger("optional", player: 1, priority: -100, sequence: 0, TimingWindowTriggerKind.Optional);

    MandatoryEffectOrderResult result = ordering.Order(
        new[] { optional, mandatory },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual("mandatory", JoinIds(result.OrderedMandatoryTriggers), "mandatory order");
    AssertEqual("optional", JoinIds(result.DeferredOptionalTriggers), "deferred optional order");
    AssertEqual(0, result.EnqueuedCount, "not enqueued by pure order");
    return Task.CompletedTask;
}

Task OrderingSortsByPrioritySequenceAndInput()
{
    var ordering = new MandatoryEffectOrdering();
    TimingWindowTrigger laterInput = CreateTrigger("later-input", player: 1, priority: 1, sequence: 5);
    TimingWindowTrigger lowPriority = CreateTrigger("low-priority", player: 1, priority: -1, sequence: 99);
    TimingWindowTrigger firstSame = CreateTrigger("first-same", player: 1, priority: 1, sequence: 1);
    TimingWindowTrigger secondSame = CreateTrigger("second-same", player: 1, priority: 1, sequence: 1);

    MandatoryEffectOrderResult result = ordering.Order(
        new[] { laterInput, lowPriority, firstSame, secondSame },
        TurnPlayer);

    AssertEqual("low-priority,first-same,second-same,later-input", JoinIds(result.OrderedMandatoryTriggers), "priority/sequence/stable order");
    return Task.CompletedTask;
}

Task OrderingIsDeterministic()
{
    var ordering = new MandatoryEffectOrdering();
    TimingWindowTrigger[] triggers =
    {
        CreateTrigger("non-turn-a", player: 2, priority: 0, sequence: 1),
        CreateTrigger("turn-a", player: 1, priority: 1, sequence: 2),
        CreateTrigger("turn-b", player: 1, priority: 0, sequence: 3),
        CreateTrigger("non-turn-b", player: 2, priority: 0, sequence: 0),
    };

    string first = JoinIds(ordering.Order(triggers, TurnPlayer, NonTurnPlayer).OrderedMandatoryTriggers);
    string second = JoinIds(ordering.Order(triggers, TurnPlayer, NonTurnPlayer).OrderedMandatoryTriggers);

    AssertEqual(first, second, "repeated order");
    AssertEqual("turn-b,turn-a,non-turn-b,non-turn-a", first, "expected order");
    return Task.CompletedTask;
}

Task OrderingReportsUnknownPlayers()
{
    var ordering = new MandatoryEffectOrdering();
    TimingWindowTrigger known = CreateTrigger("known", player: 1, priority: 0, sequence: 0);
    TimingWindowTrigger unknown = CreateTrigger("unknown", player: 3, priority: -100, sequence: 0);

    MandatoryEffectOrderResult result = ordering.Order(
        new[] { unknown, known },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual("known", JoinIds(result.OrderedMandatoryTriggers), "known player order");
    AssertEqual("unknown", JoinIds(result.UnknownPlayerTriggers), "unknown player list");
    return Task.CompletedTask;
}

Task OrderingEnqueuesIntoScheduler()
{
    var ordering = new MandatoryEffectOrdering();
    var scheduler = new EffectScheduler();
    TimingWindowTrigger optional = CreateTrigger("optional", player: 1, priority: -100, sequence: 0, TimingWindowTriggerKind.Optional);
    TimingWindowTrigger nonTurn = CreateTrigger("non-turn", player: 2, priority: 0, sequence: 0);
    TimingWindowTrigger turn = CreateTrigger("turn", player: 1, priority: 0, sequence: 0);

    MandatoryEffectOrderResult result = ordering.OrderAndEnqueue(
        new[] { optional, nonTurn, turn },
        scheduler,
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success result");
    AssertEqual(2, result.EnqueuedCount, "enqueued count");
    AssertEqual(2, scheduler.PendingCount, "scheduler pending count");
    AssertEqual(2, scheduler.TotalEnqueuedCount, "scheduler total enqueued count");
    AssertEqual("turn,non-turn", string.Join(",", schedulerSnapshotIds(scheduler)), "scheduler FIFO order");
    return Task.CompletedTask;

    static IEnumerable<string> schedulerSnapshotIds(EffectScheduler scheduler)
    {
        var queueField = typeof(EffectScheduler)
            .GetField("_queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("EffectScheduler queue field was not found.");
        var queue = (EffectResolutionQueue)queueField.GetValue(scheduler)!;
        return queue.Snapshot().Select(effect => effect.Request.EffectId.Value);
    }
}

Task OrderingReturnsFailureForInvalidInput()
{
    var ordering = new MandatoryEffectOrdering();

    MandatoryEffectOrderResult emptyTurn = ordering.Order(Array.Empty<TimingWindowTrigger>(), default, NonTurnPlayer);
    MandatoryEffectOrderResult nullInput = ordering.Order(null!, TurnPlayer, NonTurnPlayer);
    MandatoryEffectOrderResult emptyNonTurn = ordering.Order(Array.Empty<TimingWindowTrigger>(), TurnPlayer, default(HeadlessPlayerId));

    AssertFalse(emptyTurn.IsSuccess, "empty turn failure");
    AssertContains(emptyTurn.FailureReason, "Turn player id", "empty turn reason");
    AssertFalse(nullInput.IsSuccess, "null input failure");
    AssertContains(nullInput.FailureReason, "Trigger list", "null input reason");
    AssertFalse(emptyNonTurn.IsSuccess, "empty non-turn failure");
    AssertContains(emptyNonTurn.FailureReason, "Non-turn player id", "empty non-turn reason");
    return Task.CompletedTask;
}

Task MandatoryOrderingSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "MandatoryEffectOrdering.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "source must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "source must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "source must not reference MonoBehaviour");
    AssertContains(text, "MandatoryEffectOrdering", "ordering type");
    AssertContains(text, "OrderAndEnqueue", "scheduler API");
    AssertContains(text, "MandatoryEffectOrderResult", "result model");
    return Task.CompletedTask;
}

static TimingWindowTrigger CreateTrigger(
    string effectId,
    int player,
    int priority,
    long sequence,
    TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory)
{
    return new TimingWindowTrigger(
        CreateRequest(effectId, player),
        EffectResolutionMode.MainStack,
        kind,
        priority,
        sequence);
}

static EffectRequest CreateRequest(string effectId, int player)
{
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        new HeadlessPlayerId(player),
        "Main",
        new EffectContext(
            new HeadlessPlayerId(player),
            new HeadlessPlayerId(player),
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
