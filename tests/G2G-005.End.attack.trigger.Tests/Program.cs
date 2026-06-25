using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("attacker-001");
HeadlessEntityId TargetId = new("target-001");
HeadlessEntityId BlockerId = new("blocker-001");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-005 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS end attack trigger references are recorded", AsIsEndAttackReferencesAreRecorded),
    ("End attack event records attack metadata and timing", EndAttackEventRecordsMetadata),
    ("End attack hook collects and enqueues mandatory triggers", EndAttackHookEnqueuesMandatoryTriggers),
    ("End attack hook defers optional triggers without enqueueing", EndAttackHookDefersOptionalTriggers),
    ("End attack hook rejects unsupported events", EndAttackHookRejectsUnsupportedEvents),
    ("End attack hook rejects unresolved attack without scheduler mutation", EndAttackHookRejectsUnresolvedAttack),
    ("End attack hook is deterministic and source scoped", EndAttackHookIsDeterministicAndSourceScoped),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-005")
        ?? throw new InvalidOperationException("G2G-005 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "End attack trigger", "goal");
    AssertContains(Value(row, "scope"), "end attack event", "scope event");
    AssertContains(Value(row, "scope"), "trigger", "scope trigger");
    AssertEqual("end attack trigger hook", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "end attack trigger", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-005_end_attack_trigger_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-004; G2F-001", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-004_security_check_unit_test_results.md");
    AssertComplete("G2F-001_trigger_collection_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsEndAttackReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string onEndAttack = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "OnEndAttack.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));

    AssertContains(attackProcess, "public IEnumerator EndAttack()", "AS-IS EndAttack method");
    AssertContains(attackProcess, "IsEndAttack = true", "AS-IS end attack flag");
    AssertContains(attackProcess, "StackSkillInfos(EffectHashtable, EffectTiming.OnEndAttack)", "AS-IS OnEndAttack stack");
    AssertContains(attackProcess, "permanent.UntilEndAttackEffects", "AS-IS cleanup until end attack effects");
    AssertContains(onEndAttack, "CanTriggerOnEndAttack", "AS-IS OnEndAttack predicate");
    AssertContains(autoProcessing, "IsEndAttack = true", "AS-IS forced end attack from auto processing");
    return Task.CompletedTask;
}

Task EndAttackEventRecordsMetadata()
{
    HeadlessAttackState attack = ResolvedAttack(isBlocked: true);

    GameEvent gameEvent = EndAttackTriggerHook.CreateEndAttackEvent(
        sequence: 12,
        attack,
        kind: TimingWindowTriggerKind.Mandatory,
        mode: EffectResolutionMode.CutIn,
        priority: -3);

    AssertEqual(GameEventType.AttackResolved, gameEvent.Type, "event type");
    AssertEqual(12L, gameEvent.Sequence, "sequence");
    AssertMetadata(gameEvent, AutoProcessingTriggerCollector.TriggerTimingKey, EndAttackTriggerHook.OnEndAttackTiming);
    AssertMetadata(gameEvent, AutoProcessingTriggerCollector.TriggerKindKey, TimingWindowTriggerKind.Mandatory.ToString());
    AssertMetadata(gameEvent, AutoProcessingTriggerCollector.ResolutionModeKey, EffectResolutionMode.CutIn.ToString());
    AssertMetadata(gameEvent, AutoProcessingTriggerCollector.PriorityKey, -3);
    AssertMetadata(gameEvent, EndAttackTriggerHook.AttackingPlayerIdKey, Player);
    AssertMetadata(gameEvent, EndAttackTriggerHook.DefendingPlayerIdKey, Opponent);
    AssertMetadata(gameEvent, EndAttackTriggerHook.AttackerIdKey, AttackerId);
    AssertMetadata(gameEvent, EndAttackTriggerHook.AttackTargetIdKey, BlockerId);
    AssertMetadata(gameEvent, EndAttackTriggerHook.BlockerIdKey, BlockerId);
    AssertMetadata(gameEvent, EndAttackTriggerHook.AttackBlockedKey, true);
    return Task.CompletedTask;
}

Task EndAttackHookEnqueuesMandatoryTriggers()
{
    var query = new InMemoryEffectQueryService();
    EffectRequest turnPlayerEffect = CreateRequest("effect-turn", EndAttackTriggerHook.OnEndAttackTiming, Player, "turn-card");
    EffectRequest opponentEffect = CreateRequest("effect-opponent", EndAttackTriggerHook.OnEndAttackTiming, Opponent, "opponent-card");
    EffectRequest ignoredTiming = CreateRequest("effect-ignored", "OnSecurityCheck", Player, "turn-card");
    query.Register(opponentEffect);
    query.Register(ignoredTiming);
    query.Register(turnPlayerEffect);
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(query));

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 15,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(EndAttackTriggerHook.OnEndAttackTiming, result.Timing, "timing");
    AssertEqual(2, result.CollectedCount, "collected count");
    AssertEqual(2, result.EnqueuedMandatoryCount, "enqueued mandatory count");
    AssertEqual(0, result.DeferredOptionalCount, "deferred optional count");
    AssertEqual(2, scheduler.PendingCount, "scheduler pending");
    AssertEqual(2, scheduler.TotalEnqueuedCount, "scheduler total enqueued");
    AssertEqual("effect-turn,effect-opponent", string.Join(",", result.MandatoryOrder!.OrderedMandatoryTriggers.Select(trigger => trigger.Request.EffectId.Value)), "mandatory order");
    return Task.CompletedTask;
}

Task EndAttackHookDefersOptionalTriggers()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateRequest("effect-optional-a", EndAttackTriggerHook.OnEndAttackTiming, Player, "turn-card"));
    query.Register(CreateRequest("effect-optional-b", EndAttackTriggerHook.OnEndAttackTiming, Opponent, "opponent-card"));
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(query));

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 16,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent,
        kind: TimingWindowTriggerKind.Optional);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(2, result.CollectedCount, "collected count");
    AssertEqual(0, result.EnqueuedMandatoryCount, "mandatory enqueue");
    AssertEqual(2, result.DeferredOptionalCount, "deferred optional count");
    AssertEqual(0, scheduler.PendingCount, "scheduler pending");
    return Task.CompletedTask;
}

Task EndAttackHookRejectsUnsupportedEvents()
{
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(new InMemoryEffectQueryService()));
    var scheduler = new EffectScheduler();
    var unsupportedEvent = new GameEvent(
        4,
        GameEventType.AttackResolved,
        "Plain attack resolved event.",
        new Dictionary<string, object?>());

    EndAttackTriggerHookResult result = hook.Process(unsupportedEvent, scheduler, Player, Opponent);

    AssertFalse(result.IsSuccess, "unsupported failure");
    AssertContains(result.FailureReason, "not an end attack trigger event", "failure reason");
    AssertEqual(0, scheduler.PendingCount, "scheduler unchanged");
    return Task.CompletedTask;
}

Task EndAttackHookRejectsUnresolvedAttack()
{
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(new InMemoryEffectQueryService()));
    var scheduler = new EffectScheduler();
    HeadlessAttackState pending = PendingAttack();

    EndAttackTriggerHookResult result = hook.Process(
        pending,
        sequence: 5,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent);

    AssertFalse(result.IsSuccess, "pending failure");
    AssertContains(result.FailureReason, "resolved non-pending attack", "failure reason");
    AssertEqual(0, scheduler.PendingCount, "scheduler unchanged");
    return Task.CompletedTask;
}

Task EndAttackHookIsDeterministicAndSourceScoped()
{
    var query = new InMemoryEffectQueryService();
    query.Register(CreateRequest("effect-a", EndAttackTriggerHook.OnEndAttackTiming, Player, "card-a"));
    query.Register(CreateRequest("effect-b", EndAttackTriggerHook.OnEndAttackTiming, Opponent, "card-b"));
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(query));

    string first = Snapshot(hook.Process(ResolvedAttack(), 21, new EffectScheduler(), Player, Opponent));
    string second = Snapshot(hook.Process(ResolvedAttack(), 21, new EffectScheduler(), Player, Opponent));

    AssertEqual(first, second, "repeated hook result");

    string hookPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EndAttackTriggerHook.cs");
    string hookText = File.ReadAllText(hookPath);
    AssertFalse(hookText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "EndAttackTriggerHook must not contain TODO");
    AssertFalse(hookText.Contains("UnityEngine", StringComparison.Ordinal), "EndAttackTriggerHook must not reference UnityEngine");
    AssertFalse(hookText.Contains("MonoBehaviour", StringComparison.Ordinal), "EndAttackTriggerHook must not reference MonoBehaviour");
    AssertContains(hookText, "CreateEndAttackEvent", "event API");
    AssertContains(hookText, "OnEndAttack", "timing contract");
    AssertContains(hookText, "OrderAndEnqueue", "trigger enqueue contract");
    return Task.CompletedTask;
}

HeadlessAttackState PendingAttack(bool isBlocked = false)
{
    HeadlessAttackState attack = new InMemoryHeadlessAttackController()
        .DeclareAttack(Player, AttackerId, Opponent, isBlocked ? TargetId : null, isDirectAttack: !isBlocked);
    return isBlocked ? attack with { BlockerId = BlockerId, TargetId = BlockerId, IsBlocked = true, IsDirectAttack = false } : attack;
}

HeadlessAttackState ResolvedAttack(bool isBlocked = false)
{
    var controller = new InMemoryHeadlessAttackController();
    controller.DeclareAttack(Player, AttackerId, Opponent, isBlocked ? TargetId : null, isDirectAttack: !isBlocked);
    if (isBlocked)
    {
        controller.SelectBlocker(BlockerId);
    }

    return controller.ResolveAttack("Attack flow completed.");
}

static EffectRequest CreateRequest(
    string effectId,
    string timing,
    HeadlessPlayerId controllerId,
    string source)
{
    var sourceId = new HeadlessEntityId(source);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        controllerId,
        timing,
        new EffectContext(
            controllerId,
            controllerId,
            sourceId,
            triggerEntityId: sourceId,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static string Snapshot(EndAttackTriggerHookResult result)
{
    return string.Join(
        ":",
        result.IsSuccess,
        result.EventSequence,
        result.Timing,
        result.CollectedCount,
        result.EnqueuedMandatoryCount,
        result.DeferredOptionalCount,
        string.Join(",", result.Collection?.Triggers.Select(trigger => trigger.Request.EffectId.Value) ?? Array.Empty<string>()));
}

static void AssertMetadata(GameEvent gameEvent, string key, object expected)
{
    if (!gameEvent.Metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"Metadata '{key}' was not found.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
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

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = ParseCsvLine(lines[0]).ToArray();
    return lines.Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line =>
        {
            string[] values = ParseCsvLine(line).ToArray();
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = index < values.Length ? values[index] : string.Empty;
            }

            return row;
        })
        .ToArray();
}

static IEnumerable<string> ParseCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    values.Add(current.ToString());
    return values;
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    string directory = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(directory, "docs", "headless_complete_goal_breakdown.csv")))
    {
        directory = Directory.GetParent(directory)?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    return directory;
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}
