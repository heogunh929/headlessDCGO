using System.Collections;
using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId TurnPlayer = new(1);
HeadlessPlayerId NonTurnPlayer = new(2);
HeadlessPlayerId UnknownPlayer = new(3);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3K-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS timing priority references are recorded", AsIsTimingPriorityReferencesAreRecorded),
    ("Timing priority orders mandatory before optional", TimingPriorityOrdersMandatoryBeforeOptional),
    ("Timing priority orders turn player before non-turn player", TimingPriorityOrdersTurnPlayerFirst),
    ("Timing priority orders optional effects by player priority and sequence", OptionalEffectsUsePlayerPriorityAndSequence),
    ("Timing priority separates unknown players from ordered output", UnknownPlayersAreSeparated),
    ("Timing priority is deterministic for repeated equivalent input", TimingPriorityIsDeterministic),
    ("Timing priority returns explicit failure results for invalid inputs", InvalidInputsReturnFailures),
    ("Timing priority enqueues mandatory effects only in ordered sequence", EnqueuesMandatoryOnly),
    ("Assets facade delegates and source files stay inside G3K scope", AssetsFacadeAndSourceScope),
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
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3K-002")
        ?? throw new InvalidOperationException("G3K-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Timing", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "turn player ordering", "scope");
    AssertEqual("timing priority helpers", Value(row, "deliverables"), "deliverables");
    AssertEqual("timing priority 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G3K-002_timing_priority_helpers_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G3K-001", Value(row, "blocked_until"), "predecessor");
    AssertContains(Value(row, "completion_gate"), "timing priority", "completion gate");
    AssertComplete("G3K-001_effect_selection_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsTimingPriorityReferencesAreRecorded()
{
    string autoProcessing = ReadAsIs("AutoProcessing.cs");
    string multipleSkills = ReadAsIs("MultipleSkills.cs");
    string continuousController = ReadAsIs("ContinuousController.cs");

    AssertContains(autoProcessing, "StackedSkillInfos", "AS-IS stacked trigger list");
    AssertContains(autoProcessing, "Players_ForTurnPlayer", "AS-IS turn player iteration");
    AssertContains(autoProcessing, "TriggeredSkillProcess", "AS-IS trigger process entry");
    AssertContains(autoProcessing, "ActivateEffectProcess", "AS-IS activation process");
    AssertContains(multipleSkills, "TurnPlayerSkillInfos", "AS-IS turn player skill group");
    AssertContains(multipleSkills, "NonTurnPlayerSkillInfos", "AS-IS non-turn player skill group");
    AssertContains(multipleSkills, "AutomaticOrder.GetSkillIndexAutomaticOrder", "AS-IS automatic effect order hook");
    AssertContains(continuousController, "autoEffectOrder", "AS-IS auto order setting");
    return Task.CompletedTask;
}

Task TimingPriorityOrdersMandatoryBeforeOptional()
{
    TimingWindowTrigger optionalFast = Trigger("optional-fast", player: 1, TimingWindowTriggerKind.Optional, priority: -100, sequence: 0);
    TimingWindowTrigger mandatorySlow = Trigger("mandatory-slow", player: 2, TimingWindowTriggerKind.Mandatory, priority: 100, sequence: 9);
    TimingWindowTrigger mandatoryFast = Trigger("mandatory-fast", player: 1, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 1);

    TimingPriorityOrderResult result = TimingPriorityHelpers.Order(
        new[] { optionalFast, mandatorySlow, mandatoryFast },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success");
    AssertEqual("mandatory-fast,mandatory-slow,optional-fast", JoinIds(result.OrderedTriggers), "ordered triggers");
    AssertEqual("mandatory-fast,mandatory-slow", JoinIds(result.MandatoryTriggers), "mandatory triggers");
    AssertEqual("optional-fast", JoinIds(result.OptionalTriggers), "optional triggers");
    return Task.CompletedTask;
}

Task TimingPriorityOrdersTurnPlayerFirst()
{
    TimingWindowTrigger nonTurnHighPriority = Trigger("non-turn-high", player: 2, TimingWindowTriggerKind.Mandatory, priority: -100, sequence: 0);
    TimingWindowTrigger turnLate = Trigger("turn-late", player: 1, TimingWindowTriggerKind.Mandatory, priority: 5, sequence: 3);
    TimingWindowTrigger turnEarly = Trigger("turn-early", player: 1, TimingWindowTriggerKind.Mandatory, priority: 1, sequence: 1);

    TimingPriorityOrderResult result = TimingPriorityHelpers.Order(
        new[] { nonTurnHighPriority, turnLate, turnEarly },
        TurnPlayer,
        NonTurnPlayer);

    AssertEqual("turn-early,turn-late,non-turn-high", JoinIds(result.OrderedTriggers), "turn player grouped first");
    AssertEqual("turn-early,turn-late", JoinIds(result.TurnPlayerTriggers), "turn player triggers");
    AssertEqual("non-turn-high", JoinIds(result.NonTurnPlayerTriggers), "non-turn triggers");
    return Task.CompletedTask;
}

Task OptionalEffectsUsePlayerPriorityAndSequence()
{
    TimingWindowTrigger nonTurnOptionalEarly = Trigger("non-turn-optional", player: 2, TimingWindowTriggerKind.Optional, priority: -50, sequence: 0);
    TimingWindowTrigger turnOptionalLate = Trigger("turn-optional-late", player: 1, TimingWindowTriggerKind.Optional, priority: 5, sequence: 0);
    TimingWindowTrigger turnOptionalEarly = Trigger("turn-optional-early", player: 1, TimingWindowTriggerKind.Optional, priority: 0, sequence: 2);
    TimingWindowTrigger turnOptionalFirst = Trigger("turn-optional-first", player: 1, TimingWindowTriggerKind.Optional, priority: 0, sequence: 1);

    TimingPriorityOrderResult result = TimingPriorityHelpers.Order(
        new[] { nonTurnOptionalEarly, turnOptionalLate, turnOptionalEarly, turnOptionalFirst },
        TurnPlayer,
        NonTurnPlayer);

    AssertEqual("turn-optional-first,turn-optional-early,turn-optional-late,non-turn-optional", JoinIds(result.OptionalTriggers), "optional order");
    return Task.CompletedTask;
}

Task UnknownPlayersAreSeparated()
{
    TimingWindowTrigger known = Trigger("known", player: 1, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0);
    TimingWindowTrigger unknown = Trigger("unknown", player: 3, TimingWindowTriggerKind.Mandatory, priority: -100, sequence: 0);

    TimingPriorityOrderResult result = TimingPriorityHelpers.Order(
        new[] { unknown, known },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success");
    AssertEqual("known", JoinIds(result.OrderedTriggers), "ordered excludes unknown");
    AssertEqual("unknown", JoinIds(result.UnknownPlayerTriggers), "unknown list");
    AssertSequence(new[] { "unknown" }, Strings(result.Values[TimingPriorityHelpers.UnknownPlayerEffectIdsKey]), "unknown value");
    return Task.CompletedTask;
}

Task TimingPriorityIsDeterministic()
{
    TimingWindowTrigger[] triggers =
    {
        Trigger("optional-z", player: 2, TimingWindowTriggerKind.Optional, priority: 1, sequence: 5),
        Trigger("mandatory-b", player: 1, TimingWindowTriggerKind.Mandatory, priority: 1, sequence: 2),
        Trigger("mandatory-a", player: 1, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 2),
        Trigger("optional-a", player: 1, TimingWindowTriggerKind.Optional, priority: 0, sequence: 1),
        Trigger("mandatory-c", player: 2, TimingWindowTriggerKind.Mandatory, priority: -100, sequence: 0),
    };

    string first = Signature(TimingPriorityHelpers.Order(triggers, TurnPlayer, NonTurnPlayer));
    string second = Signature(TimingPriorityHelpers.Order(triggers, TurnPlayer, NonTurnPlayer));

    AssertEqual(first, second, "repeated signature");
    AssertContains(first, "ordered=mandatory-a,mandatory-b,mandatory-c,optional-a,optional-z", "expected order");
    return Task.CompletedTask;
}

Task InvalidInputsReturnFailures()
{
    TimingPriorityOrderResult emptyTurn = TimingPriorityHelpers.Order(Array.Empty<TimingWindowTrigger>(), default, NonTurnPlayer);
    TimingPriorityOrderResult emptyNonTurn = TimingPriorityHelpers.Order(Array.Empty<TimingWindowTrigger>(), TurnPlayer, default(HeadlessPlayerId));
    TimingPriorityOrderResult samePlayers = TimingPriorityHelpers.Order(Array.Empty<TimingWindowTrigger>(), TurnPlayer, TurnPlayer);
    TimingPriorityOrderResult nullInput = TimingPriorityHelpers.Order(null!, TurnPlayer, NonTurnPlayer);
    TimingPriorityOrderResult nullTrigger = TimingPriorityHelpers.Order(new TimingWindowTrigger?[] { null! }!, TurnPlayer, NonTurnPlayer);

    AssertFalse(emptyTurn.IsSuccess, "empty turn");
    AssertContains(emptyTurn.FailureReason, "Turn player", "empty turn reason");
    AssertFalse(emptyNonTurn.IsSuccess, "empty non-turn");
    AssertContains(emptyNonTurn.FailureReason, "Non-turn player", "empty non-turn reason");
    AssertFalse(samePlayers.IsSuccess, "same players");
    AssertContains(samePlayers.FailureReason, "different", "same player reason");
    AssertFalse(nullInput.IsSuccess, "null input");
    AssertContains(nullInput.FailureReason, "must not be null", "null input reason");
    AssertFalse(nullTrigger.IsSuccess, "null trigger");
    AssertContains(nullTrigger.FailureReason, "must not contain null", "null trigger reason");
    return Task.CompletedTask;
}

Task EnqueuesMandatoryOnly()
{
    var scheduler = new EffectScheduler();
    TimingWindowTrigger optional = Trigger("optional", player: 1, TimingWindowTriggerKind.Optional, priority: -100, sequence: 0);
    TimingWindowTrigger nonTurn = Trigger("non-turn", player: 2, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0);
    TimingWindowTrigger turn = Trigger("turn", player: 1, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0);

    TimingPriorityOrderResult result = TimingPriorityHelpers.OrderAndEnqueueMandatory(
        new[] { optional, nonTurn, turn },
        scheduler,
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(2, result.EnqueuedMandatoryCount, "enqueued mandatory count");
    AssertEqual(2, scheduler.PendingCount, "scheduler pending count");
    AssertEqual("turn,non-turn", string.Join(",", SchedulerSnapshotIds(scheduler)), "scheduler order");
    return Task.CompletedTask;
}

Task AssetsFacadeAndSourceScope()
{
    TimingPriorityOrderResult result = TimingPriorityHelperFactory.Order(
        new[]
        {
            Trigger("facade-optional", player: 2, TimingWindowTriggerKind.Optional, priority: 0, sequence: 0),
            Trigger("facade-mandatory", player: 1, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0),
        },
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "facade result");
    AssertEqual("facade-mandatory,facade-optional", JoinIds(result.OrderedTriggers), "facade order");

    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "TimingPriorityHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "TimingPriorityHelpers.cs");
    string testPath = Path.Combine(root, "tests", "G3K-002.Timing.priority.helper.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless helper exists");
    AssertTrue(File.Exists(facadePath), "facade helper exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

TimingWindowTrigger Trigger(
    string effectId,
    int player,
    TimingWindowTriggerKind kind,
    int priority,
    long sequence)
{
    var playerId = new HeadlessPlayerId(player);
    return new TimingWindowTrigger(
        new EffectRequest(
            new HeadlessEntityId(effectId),
            playerId,
            "Main",
            new EffectContext(
                playerId,
                playerId,
                new HeadlessEntityId($"source-{effectId}"),
                triggerEntityId: null,
                targetEntityIds: Array.Empty<HeadlessEntityId>())),
        EffectResolutionMode.MainStack,
        kind,
        priority,
        sequence);
}

string JoinIds(IEnumerable<TimingWindowTrigger> triggers)
{
    return string.Join(",", triggers.Select(trigger => trigger.Request.EffectId.Value));
}

string Signature(TimingPriorityOrderResult result)
{
    return string.Join(
        "|",
        $"success={result.IsSuccess}",
        $"ordered={JoinIds(result.OrderedTriggers)}",
        $"mandatory={JoinIds(result.MandatoryTriggers)}",
        $"optional={JoinIds(result.OptionalTriggers)}",
        $"unknown={JoinIds(result.UnknownPlayerTriggers)}",
        $"values={string.Join(";", result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={ValueToString(pair.Value)}"))}");
}

IEnumerable<string> SchedulerSnapshotIds(EffectScheduler scheduler)
{
    System.Reflection.FieldInfo queueField = typeof(EffectScheduler)
        .GetField("_queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("EffectScheduler queue field was not found.");
    var queue = (EffectResolutionQueue)queueField.GetValue(scheduler)!;
    return queue.Snapshot().Select(effect => effect.Request.EffectId.Value);
}

string[] Strings(object? value)
{
    if (value is null)
    {
        return Array.Empty<string>();
    }

    if (value is string text)
    {
        return new[] { text };
    }

    if (value is IEnumerable enumerable)
    {
        return enumerable.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray();
    }

    return new[] { value.ToString() ?? string.Empty };
}

string ValueToString(object? value)
{
    return string.Join(",", Strings(value));
}

string ReadAsIs(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", relativePath));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    AssertTrue(File.Exists(path), $"predecessor result document {fileName}");
    AssertContains(File.ReadAllText(path), "COMPLETE", $"predecessor completion {fileName}");
}

List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] values = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < values.Length ? values[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

IEnumerable<string> SplitCsvLine(string line)
{
    var value = new StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }

            continue;
        }

        if (ch == ',' && !inQuotes)
        {
            yield return value.ToString();
            value.Clear();
            continue;
        }

        value.Append(ch);
    }

    yield return value.ToString();
}

string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(Environment.CurrentDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}

void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
    }
}

void AssertDoesNotContain(string text, string unexpected, string label)
{
    if (text.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: text must not contain '{unexpected}'.");
    }
}

void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}
