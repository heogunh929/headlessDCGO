using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2F-003 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS optional prompt references are recorded", AsIsOptionalPromptReferencesAreRecorded),
    ("Optional prompt queue requests a skippable choice from optional triggers", QueueRequestsSkippableChoice),
    ("Skipping optional prompt leaves scheduler unchanged and dequeues prompt", SkippingPromptLeavesSchedulerUnchanged),
    ("Selecting optional prompt enqueues selected effect only", SelectingPromptEnqueuesSelectedEffectOnly),
    ("Prompt queue preserves multiple prompts in FIFO order", PromptQueuePreservesFifoOrder),
    ("Pending choice prevents optional prompt overwrite", PendingChoicePreventsPromptOverwrite),
    ("Invalid optional prompt input returns explicit failure", InvalidPromptInputReturnsFailure),
    ("Optional prompt queue is deterministic for repeated equivalent input", OptionalPromptQueueIsDeterministic),
    ("G2F-003 source files contain no placeholder or Unity dependency", OptionalPromptQueueSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2F-003")
        ?? throw new InvalidOperationException("G2F-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AutoProcessing", Value(row, "area"), "area");
    AssertEqual("Optional prompt queue 포팅", Value(row, "goal"), "goal");
    AssertContains(Value(row, "scope"), "optional effect", "scope");
    AssertEqual("optional prompt queue", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "optional choice", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2F-003_optional_prompt_queue_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2F-002; G1E-005", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2F-002_mandatory_effect_order_unit_test_results.md");
    AssertComplete("G1E-005_choice_pause_resume_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsOptionalPromptReferencesAreRecorded()
{
    string multipleSkills = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MultipleSkills.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string selectCardEffect = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"));

    AssertContains(multipleSkills, "IsOnlyOptionalEffectStacked", "AS-IS optional stack check");
    AssertContains(multipleSkills, "OpenSelectCardPanel", "AS-IS select prompt");
    AssertContains(multipleSkills, "Don't activate these effects", "AS-IS skip prompt text");
    AssertContains(multipleSkills, "_CanNoSelect", "AS-IS no select condition");
    AssertContains(multipleSkills, "SelectedIndex", "AS-IS selected effect index");
    AssertContains(multipleSkills, "Activate_Optional_Effect_Execute", "AS-IS optional execution hook");
    AssertContains(autoProcessing, "TriggeredSkillProcess", "AS-IS trigger process");
    AssertContains(selectCardEffect, "SelectCardEffect", "AS-IS selection model");
    return Task.CompletedTask;
}

Task QueueRequestsSkippableChoice()
{
    var queue = new OptionalPromptQueue();
    var choice = new InMemoryHeadlessChoiceController();
    TimingWindowTrigger first = CreateOptionalTrigger("effect-a");
    TimingWindowTrigger second = CreateOptionalTrigger("effect-b");

    OptionalPromptQueueResult enqueue = queue.EnqueuePrompt(new[] { first, second }, Player);
    OptionalPromptQueueResult requested = queue.RequestNextChoice(choice);

    AssertTrue(enqueue.IsSuccess, "enqueue success");
    AssertTrue(requested.IsSuccess, "request success");
    AssertEqual(1, queue.Count, "queue count while pending");
    AssertTrue(choice.Current.IsPending, "choice pending");
    AssertEqual(ChoiceType.OptionalEffect, choice.Current.Type, "choice type");
    AssertEqual(Player, choice.Current.PlayerId!.Value, "choice player");
    AssertEqual(0, choice.Current.MinCount, "min count");
    AssertEqual(1, choice.Current.MaxCount, "max count");
    AssertTrue(choice.Current.CanSkip, "can skip");
    AssertEqual(2, choice.Current.CandidateCount, "candidate count");
    AssertEqual("effect-a,effect-b", string.Join(",", choice.PendingRequest!.Candidates.Select(candidate => candidate.Id.Value)), "candidate order");
    return Task.CompletedTask;
}

Task SkippingPromptLeavesSchedulerUnchanged()
{
    var queue = new OptionalPromptQueue();
    var choice = new InMemoryHeadlessChoiceController();
    var scheduler = new EffectScheduler();
    queue.EnqueuePrompt(new[] { CreateOptionalTrigger("effect-a") }, Player);
    queue.RequestNextChoice(choice);

    OptionalPromptQueueResult result = queue.ResolveChoice(ChoiceResult.Skip(), choice, scheduler);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.IsSkipped, "skipped");
    AssertEqual(0, scheduler.PendingCount, "scheduler pending count");
    AssertEqual(0, result.EnqueuedCount, "enqueued count");
    AssertEqual(0, queue.Count, "queue count");
    AssertFalse(choice.Current.IsPending, "choice no longer pending");
    return Task.CompletedTask;
}

Task SelectingPromptEnqueuesSelectedEffectOnly()
{
    var queue = new OptionalPromptQueue();
    var choice = new InMemoryHeadlessChoiceController();
    var scheduler = new EffectScheduler();
    queue.EnqueuePrompt(new[] { CreateOptionalTrigger("effect-a"), CreateOptionalTrigger("effect-b") }, Player);
    queue.RequestNextChoice(choice);

    OptionalPromptQueueResult result = queue.ResolveChoice(
        ChoiceResult.Select(new HeadlessEntityId("effect-b")),
        choice,
        scheduler);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertFalse(result.IsSkipped, "not skipped");
    AssertEqual(1, result.EnqueuedCount, "enqueued count");
    AssertEqual(1, scheduler.PendingCount, "scheduler pending count");
    AssertEqual("effect-b", string.Join(",", SchedulerSnapshotIds(scheduler)), "scheduled effect");
    AssertEqual(0, queue.Count, "queue count");
    AssertEqual("effect-b", string.Join(",", choice.Current.SelectedIds.Select(id => id.Value)), "choice selected ids");
    return Task.CompletedTask;
}

Task PromptQueuePreservesFifoOrder()
{
    var queue = new OptionalPromptQueue();
    var choice = new InMemoryHeadlessChoiceController();
    var scheduler = new EffectScheduler();
    queue.EnqueuePrompt(new[] { CreateOptionalTrigger("first") }, Player, "First prompt");
    queue.EnqueuePrompt(new[] { CreateOptionalTrigger("second") }, Player, "Second prompt");

    OptionalPromptQueueResult firstPrompt = queue.RequestNextChoice(choice);
    queue.ResolveChoice(ChoiceResult.Select(new HeadlessEntityId("first")), choice, scheduler);
    OptionalPromptQueueResult secondPrompt = queue.RequestNextChoice(choice);

    AssertTrue(firstPrompt.IsSuccess, "first request success");
    AssertContains(firstPrompt.Prompt!.Message, "First", "first prompt message");
    AssertTrue(secondPrompt.IsSuccess, "second request success");
    AssertContains(secondPrompt.Prompt!.Message, "Second", "second prompt message");
    AssertEqual(1, queue.Count, "second prompt still pending in queue");
    AssertEqual("first", string.Join(",", SchedulerSnapshotIds(scheduler)), "first scheduled");
    return Task.CompletedTask;
}

Task PendingChoicePreventsPromptOverwrite()
{
    var queue = new OptionalPromptQueue();
    var choice = new InMemoryHeadlessChoiceController();
    queue.EnqueuePrompt(new[] { CreateOptionalTrigger("effect-a") }, Player);
    queue.RequestNextChoice(choice);

    OptionalPromptQueueResult secondRequest = queue.RequestNextChoice(choice);

    AssertFalse(secondRequest.IsSuccess, "second request failure");
    AssertContains(secondRequest.FailureReason, "another choice is pending", "failure reason");
    AssertTrue(choice.Current.IsPending, "original choice remains pending");
    AssertEqual("effect-a", choice.PendingRequest!.Candidates[0].Id.Value, "original candidate");
    return Task.CompletedTask;
}

Task InvalidPromptInputReturnsFailure()
{
    var queue = new OptionalPromptQueue();
    OptionalPromptQueueResult emptyPlayer = queue.EnqueuePrompt(new[] { CreateOptionalTrigger("effect-a") }, default);
    OptionalPromptQueueResult nullTriggers = queue.EnqueuePrompt(null!, Player);
    OptionalPromptQueueResult mandatoryTrigger = queue.EnqueuePrompt(new[] { CreateMandatoryTrigger("mandatory") }, Player);
    OptionalPromptQueueResult wrongPlayer = queue.EnqueuePrompt(new[] { CreateOptionalTrigger("wrong-player", player: 2) }, Player);

    AssertFalse(emptyPlayer.IsSuccess, "empty player failure");
    AssertContains(emptyPlayer.FailureReason, "player id", "empty player reason");
    AssertFalse(nullTriggers.IsSuccess, "null triggers failure");
    AssertContains(nullTriggers.FailureReason, "must not be null", "null triggers reason");
    AssertFalse(mandatoryTrigger.IsSuccess, "mandatory failure");
    AssertContains(mandatoryTrigger.FailureReason, "only optional", "mandatory reason");
    AssertFalse(wrongPlayer.IsSuccess, "wrong player failure");
    AssertContains(wrongPlayer.FailureReason, "controller", "wrong player reason");
    AssertEqual(0, queue.Count, "queue remains empty");
    return Task.CompletedTask;
}

Task OptionalPromptQueueIsDeterministic()
{
    var firstQueue = new OptionalPromptQueue();
    var secondQueue = new OptionalPromptQueue();
    TimingWindowTrigger[] triggers =
    {
        CreateOptionalTrigger("effect-a"),
        CreateOptionalTrigger("effect-b"),
        CreateOptionalTrigger("effect-c"),
    };

    firstQueue.EnqueuePrompt(triggers, Player);
    secondQueue.EnqueuePrompt(triggers, Player);
    string first = string.Join(",", firstQueue.Snapshot()[0].ToChoiceRequest().Candidates.Select(candidate => candidate.Id.Value));
    string second = string.Join(",", secondQueue.Snapshot()[0].ToChoiceRequest().Candidates.Select(candidate => candidate.Id.Value));

    AssertEqual(first, second, "repeated candidate order");
    AssertEqual("effect-a,effect-b,effect-c", first, "expected order");
    return Task.CompletedTask;
}

Task OptionalPromptQueueSourceHasNoPlaceholderOrUnityDependency()
{
    string queuePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "OptionalPromptQueue.cs");
    string choiceTypePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceType.cs");
    string queueText = File.ReadAllText(queuePath);
    string choiceText = File.ReadAllText(choiceTypePath);

    AssertFalse(queueText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "queue must not contain TODO");
    AssertFalse(queueText.Contains("UnityEngine", StringComparison.Ordinal), "queue must not reference UnityEngine");
    AssertFalse(queueText.Contains("MonoBehaviour", StringComparison.Ordinal), "queue must not reference MonoBehaviour");
    AssertContains(queueText, "OptionalPromptQueue", "queue type");
    AssertContains(queueText, "RequestNextChoice", "choice request API");
    AssertContains(queueText, "ResolveChoice", "choice resolve API");
    AssertContains(choiceText, "OptionalEffect", "choice type enum");
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

static TimingWindowTrigger CreateOptionalTrigger(string effectId, int player = 1)
{
    return CreateTrigger(effectId, player, TimingWindowTriggerKind.Optional);
}

static TimingWindowTrigger CreateMandatoryTrigger(string effectId, int player = 1)
{
    return CreateTrigger(effectId, player, TimingWindowTriggerKind.Mandatory);
}

static TimingWindowTrigger CreateTrigger(
    string effectId,
    int player,
    TimingWindowTriggerKind kind)
{
    return new TimingWindowTrigger(
        CreateRequest(effectId, player),
        EffectResolutionMode.MainStack,
        kind,
        priority: 0,
        sequence: 0);
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

static IEnumerable<string> SchedulerSnapshotIds(EffectScheduler scheduler)
{
    var queueField = typeof(EffectScheduler)
        .GetField("_queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("EffectScheduler queue field was not found.");
    var queue = (EffectResolutionQueue)queueField.GetValue(scheduler)!;
    return queue.Snapshot().Select(effect => effect.Request.EffectId.Value);
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
