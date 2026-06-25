using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId TurnPlayer = new(1);
HeadlessPlayerId NonTurnPlayer = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2F-004 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS security delayed trigger references are recorded", AsIsSecurityDelayedReferencesAreRecorded),
    ("Security check event fixes AS-IS timing metadata", SecurityCheckEventFixesTimingMetadata),
    ("Security hook enqueues mandatory security check triggers", SecurityHookEnqueuesMandatorySecurityCheckTriggers),
    ("Security hook routes optional security skill triggers to prompt queue", SecurityHookRoutesOptionalSecuritySkillTriggers),
    ("Delayed trigger event enqueues delayed source trigger", DelayedTriggerEventEnqueuesDelayedSourceTrigger),
    ("Security hook preserves mandatory turn-player ordering", SecurityHookPreservesMandatoryTurnPlayerOrdering),
    ("Security hook rejects unsupported event without mutation", SecurityHookRejectsUnsupportedEvent),
    ("Security hook filters security triggers by player and card", SecurityHookFiltersByPlayerAndCard),
    ("G2F-004 source files contain no placeholder or Unity dependency", SecurityDelayedHookSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2F-004")
        ?? throw new InvalidOperationException("G2F-004 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AutoProcessing", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Security delayed trigger hook", "goal");
    AssertContains(Value(row, "scope"), "security", "scope");
    AssertContains(Value(row, "scope"), "delayed trigger", "scope");
    AssertEqual("security delayed hook", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "security delayed trigger", "unit_test_scope");
    AssertEqual(
        "docs/test-results/goals/G2F-004_security_delayed_trigger_hook_unit_test_results.md",
        Value(row, "result_document"),
        "result_document");
    AssertEqual("G2F-003", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2F-003_optional_prompt_queue_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsSecurityDelayedReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string cardEffectFactory = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory.cs"));

    AssertContains(attackProcess, "DoSecurityCheck", "AS-IS security check flag");
    AssertContains(attackProcess, "SecurityDigimon", "AS-IS security digimon bridge");
    AssertContains(cardController, "ISecurityCheck", "AS-IS security check task");
    AssertContains(cardController, "OnSecurityCheck", "AS-IS OnSecurityCheck timing");
    AssertContains(cardController, "SecuritySkill", "AS-IS SecuritySkill timing");
    AssertContains(autoProcessing, "StackSkillInfos", "AS-IS trigger stack hook");
    AssertContains(cardEffectFactory, "PlaceSelfDelayOptionSecurityEffect", "AS-IS delayed option security effect");
    return Task.CompletedTask;
}

Task SecurityCheckEventFixesTimingMetadata()
{
    HeadlessEntityId securityCard = new("security-card");
    HeadlessEntityId attacker = new("attacker");
    GameEvent gameEvent = SecurityDelayedTriggerHook.CreateSecurityCheckEvent(
        10,
        NonTurnPlayer,
        securityCard,
        attacker,
        priority: 7);

    AssertEqual(GameEventType.SecurityCheck, gameEvent.Type, "event type");
    AssertEqual(SecurityDelayedTriggerHook.SecurityCheckTiming, gameEvent.Metadata[AutoProcessingTriggerCollector.TriggerTimingKey], "timing");
    AssertEqual(NonTurnPlayer, gameEvent.Metadata[AutoProcessingTriggerCollector.PlayerIdKey], "player metadata");
    AssertEqual(securityCard, gameEvent.Metadata[AutoProcessingTriggerCollector.CardIdKey], "card metadata");
    AssertEqual(attacker, gameEvent.Metadata[AutoProcessingTriggerCollector.TargetEntityIdKey], "attacker metadata");
    AssertEqual("Mandatory", gameEvent.Metadata[AutoProcessingTriggerCollector.TriggerKindKey], "kind metadata");
    AssertEqual(7, gameEvent.Metadata[AutoProcessingTriggerCollector.PriorityKey], "priority metadata");
    return Task.CompletedTask;
}

Task SecurityHookEnqueuesMandatorySecurityCheckTriggers()
{
    var query = new InMemoryEffectQueryService();
    HeadlessEntityId securityCard = new("security-card");
    query.Register(CreateEffect("security-effect", NonTurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, securityCard));
    query.Register(CreateEffect("other-timing", NonTurnPlayer, SecurityDelayedTriggerHook.DelayedTriggerTiming, securityCard));
    var scheduler = new EffectScheduler();
    var hook = CreateHook(query);

    SecurityDelayedTriggerHookResult result = hook.Process(
        SecurityDelayedTriggerHook.CreateSecurityCheckEvent(1, NonTurnPlayer, securityCard),
        scheduler,
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(SecurityDelayedTriggerHook.SecurityCheckTiming, result.Timing, "timing");
    AssertEqual(1, result.CollectedCount, "collected count");
    AssertEqual(1, result.EnqueuedMandatoryCount, "mandatory enqueued count");
    AssertEqual(1, scheduler.PendingCount, "scheduler pending count");
    AssertEqual("security-effect", string.Join(",", SchedulerSnapshotIds(scheduler)), "scheduled ids");
    return Task.CompletedTask;
}

Task SecurityHookRoutesOptionalSecuritySkillTriggers()
{
    var query = new InMemoryEffectQueryService();
    HeadlessEntityId securityCard = new("security-option");
    query.Register(CreateEffect("security-optional-a", NonTurnPlayer, SecurityDelayedTriggerHook.SecuritySkillTiming, securityCard));
    query.Register(CreateEffect("security-optional-b", NonTurnPlayer, SecurityDelayedTriggerHook.SecuritySkillTiming, securityCard));
    var scheduler = new EffectScheduler();
    var optionalQueue = new OptionalPromptQueue();
    var hook = CreateHook(query);

    SecurityDelayedTriggerHookResult result = hook.Process(
        SecurityDelayedTriggerHook.CreateSecuritySkillEvent(
            2,
            NonTurnPlayer,
            securityCard,
            TimingWindowTriggerKind.Optional),
        scheduler,
        TurnPlayer,
        NonTurnPlayer,
        optionalQueue);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(2, result.CollectedCount, "collected count");
    AssertEqual(0, result.EnqueuedMandatoryCount, "mandatory enqueued count");
    AssertEqual(2, result.QueuedOptionalCount, "queued optional count");
    AssertEqual(0, scheduler.PendingCount, "scheduler remains empty");
    AssertEqual(1, optionalQueue.Count, "optional prompt count");
    AssertEqual(
        "security-optional-a,security-optional-b",
        string.Join(",", optionalQueue.Snapshot()[0].Triggers.Select(trigger => trigger.Request.EffectId.Value)),
        "optional trigger order");
    return Task.CompletedTask;
}

Task DelayedTriggerEventEnqueuesDelayedSourceTrigger()
{
    var query = new InMemoryEffectQueryService();
    HeadlessEntityId delayedSource = new("delay-source");
    query.Register(CreateEffect("delayed-effect", TurnPlayer, SecurityDelayedTriggerHook.DelayedTriggerTiming, delayedSource));
    query.Register(CreateEffect("wrong-source", TurnPlayer, SecurityDelayedTriggerHook.DelayedTriggerTiming, new HeadlessEntityId("other-source")));
    var scheduler = new EffectScheduler();
    var hook = CreateHook(query);

    SecurityDelayedTriggerHookResult result = hook.Process(
        SecurityDelayedTriggerHook.CreateDelayedTriggerEvent(3, TurnPlayer, delayedSource),
        scheduler,
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(GameEventType.DelayedTrigger, result.EventType, "event type");
    AssertEqual(SecurityDelayedTriggerHook.DelayedTriggerTiming, result.Timing, "timing");
    AssertEqual(1, result.CollectedCount, "collected count");
    AssertEqual("delayed-effect", string.Join(",", SchedulerSnapshotIds(scheduler)), "scheduled delayed effect");
    return Task.CompletedTask;
}

Task SecurityHookPreservesMandatoryTurnPlayerOrdering()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateEffect("non-turn-first-input", NonTurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, new HeadlessEntityId("security-a")));
    query.Register(CreateEffect("turn-second-input", TurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, new HeadlessEntityId("security-b")));
    var scheduler = new EffectScheduler();
    var hook = CreateHook(query);
    var gameEvent = new GameEvent(
        4,
        GameEventType.SecurityCheck,
        "Security ordering window.",
        new Dictionary<string, object?>
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = SecurityDelayedTriggerHook.SecurityCheckTiming,
        });

    SecurityDelayedTriggerHookResult result = hook.Process(gameEvent, scheduler, TurnPlayer, NonTurnPlayer);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(2, result.CollectedCount, "collected count");
    AssertEqual(
        "turn-second-input,non-turn-first-input",
        string.Join(",", SchedulerSnapshotIds(scheduler)),
        "turn-player mandatory order");
    return Task.CompletedTask;
}

Task SecurityHookRejectsUnsupportedEvent()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateEffect("attack-effect", TurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, new HeadlessEntityId("card")));
    var scheduler = new EffectScheduler();
    var hook = CreateHook(query);
    var gameEvent = new GameEvent(
        5,
        GameEventType.AttackDeclared,
        "Attack declared.",
        new Dictionary<string, object?>
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = SecurityDelayedTriggerHook.SecurityCheckTiming,
        });

    SecurityDelayedTriggerHookResult result = hook.Process(gameEvent, scheduler, TurnPlayer, NonTurnPlayer);

    AssertFalse(result.IsSuccess, "hook failure");
    AssertContains(result.FailureReason, "not a security or delayed", "failure reason");
    AssertEqual(0, scheduler.PendingCount, "scheduler remains empty");
    return Task.CompletedTask;
}

Task SecurityHookFiltersByPlayerAndCard()
{
    var query = new InMemoryEffectQueryService();
    HeadlessEntityId targetCard = new("target-security-card");
    query.Register(CreateEffect("matching", NonTurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, targetCard));
    query.Register(CreateEffect("wrong-player", TurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, targetCard));
    query.Register(CreateEffect("wrong-card", NonTurnPlayer, SecurityDelayedTriggerHook.SecurityCheckTiming, new HeadlessEntityId("other-card")));
    var scheduler = new EffectScheduler();
    var hook = CreateHook(query);

    SecurityDelayedTriggerHookResult result = hook.Process(
        SecurityDelayedTriggerHook.CreateSecurityCheckEvent(6, NonTurnPlayer, targetCard),
        scheduler,
        TurnPlayer,
        NonTurnPlayer);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(1, result.CollectedCount, "collected count");
    AssertEqual("matching", string.Join(",", SchedulerSnapshotIds(scheduler)), "filtered scheduled effect");
    return Task.CompletedTask;
}

Task SecurityDelayedHookSourceHasNoPlaceholderOrUnityDependency()
{
    string hookPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "SecurityDelayedTriggerHook.cs");
    string eventTypePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameEventType.cs");
    string hookText = File.ReadAllText(hookPath);
    string eventTypeText = File.ReadAllText(eventTypePath);

    AssertFalse(hookText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "hook must not contain TODO");
    AssertFalse(hookText.Contains("UnityEngine", StringComparison.Ordinal), "hook must not reference UnityEngine");
    AssertFalse(hookText.Contains("MonoBehaviour", StringComparison.Ordinal), "hook must not reference MonoBehaviour");
    AssertContains(hookText, "SecurityDelayedTriggerHook", "hook type");
    AssertContains(hookText, "CreateSecurityCheckEvent", "security event factory");
    AssertContains(hookText, "CreateDelayedTriggerEvent", "delayed event factory");
    AssertContains(hookText, "OptionalPromptQueue", "optional prompt connection");
    AssertContains(eventTypeText, "SecurityCheck", "security event type");
    AssertContains(eventTypeText, "DelayedTrigger", "delayed event type");
    return Task.CompletedTask;
}

SecurityDelayedTriggerHook CreateHook(InMemoryEffectQueryService query)
{
    return new SecurityDelayedTriggerHook(new AutoProcessingTriggerCollector(query));
}

EffectRequest CreateEffect(
    string effectId,
    HeadlessPlayerId player,
    string timing,
    HeadlessEntityId sourceEntityId)
{
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        timing,
        new EffectContext(
            player,
            player,
            sourceEntityId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

IEnumerable<string> SchedulerSnapshotIds(EffectScheduler scheduler)
{
    var queueField = typeof(EffectScheduler)
        .GetField("_queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("EffectScheduler queue field was not found.");
    var queue = (EffectResolutionQueue)queueField.GetValue(scheduler)!;
    return queue.Snapshot().Select(effect => effect.Request.EffectId.Value);
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
