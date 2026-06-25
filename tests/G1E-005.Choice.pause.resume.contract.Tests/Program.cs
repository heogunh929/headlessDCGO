using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1E-005 goal row keeps the choice pause resume contract", GoalRowKeepsExpectedContract),
    ("Predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("RequestChoice step pauses match with pending choice state", RequestChoiceStepPausesMatchWithPendingChoiceState),
    ("ResolveChoice step resumes match with provider result", ResolveChoiceStepResumesMatchWithProviderResult),
    ("Invalid resolve result keeps pending choice and reports failure", InvalidResolveResultKeepsPendingChoiceAndReportsFailure),
    ("Duplicate RequestChoice while pending is rejected without replacing pending state", DuplicateRequestChoiceWhilePendingIsRejected),
    ("ClearChoice explicitly releases pending choice", ClearChoiceReleasesPendingChoice),
    ("Choice controller rejects direct duplicate request and preserves pending request", ChoiceControllerRejectsDirectDuplicateRequestAndPreservesPendingRequest),
    ("AS-IS pause resume choice references remain read-only inputs", AsIsPauseResumeChoiceReferencesRemainReadOnlyInputs),
    ("Pending choice runtime integration files have no placeholder or Unity dependency", PendingChoiceRuntimeIntegrationFilesHaveNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1E-005")
        ?? throw new InvalidOperationException("G1E-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Choices", Value(row, "area"), "area");
    AssertEqual("Choice pause resume contract", Value(row, "goal"), "goal");
    AssertEqual("choice pending과 resume 계약 확정", Value(row, "scope"), "scope");
    AssertEqual("PendingChoiceState runtime integration contract", Value(row, "deliverables"), "deliverables");
    AssertEqual("pause resume result 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1E-005_choice_pause_resume_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1E-003; G1E-004", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("choice pause resume 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1E-003_scripted_choice_provider_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1E-004_policy_choice_provider_unit_test_results.md"),
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

async Task RequestChoiceStepPausesMatchWithPendingChoiceState()
{
    var player = new HeadlessPlayerId(1);
    var match = new DcgoMatch(EngineContext.CreateDefault());
    await match.InitializeAsync(CreateConfig(player));
    await match.StepAsync();

    await match.ApplyActionAsync(CreateRequestChoice(player, actionId: "choice-request-1"));
    StepResult step = await match.StepAsync();

    AssertTrue(step.HasPendingChoice, "step pending choice");
    AssertTrue(match.HasPendingChoice(), "match pending choice");
    AssertEqual(GameEventType.ChoiceRequested, FindEvent(step, GameEventType.ChoiceRequested).Type, "choice requested event");
    AssertEqual("choice-request-1", step.Observation.Choice.RequestId?.Value, "observation request id");
    AssertEqual(ChoiceType.Card, step.Observation.Choice.Type, "choice type");
    AssertEqual(player, step.Observation.Choice.PlayerId, "choice player");
    AssertEqual("Pick one", step.Observation.Choice.Message, "choice message");
    AssertEqual(1, step.Observation.Choice.MinCount, "min count");
    AssertEqual(1, step.Observation.Choice.MaxCount, "max count");
    AssertFalse(step.Observation.Choice.CanSkip, "can skip");
    AssertEqual(ChoiceZone.Hand, step.Observation.Choice.SourceZone, "source zone");
    AssertEqual(2, step.Observation.Choice.CandidateCount, "candidate count");
}

async Task ResolveChoiceStepResumesMatchWithProviderResult()
{
    var player = new HeadlessPlayerId(1);
    var match = new DcgoMatch(CreateContext(new ScriptedChoiceProvider(new[]
    {
        ChoiceResult.Select(new HeadlessEntityId("card-b")),
    })));

    await match.InitializeAsync(CreateConfig(player));
    await match.ApplyActionAsync(CreateRequestChoice(player, actionId: "choice-request-2"));
    await match.StepAsync();

    await match.ApplyActionAsync(HeadlessActionFactory.ResolveChoice(player, actionId: "choice-resolve-2"));
    StepResult step = await match.StepAsync();

    AssertFalse(step.HasPendingChoice, "step pending choice");
    AssertFalse(match.HasPendingChoice(), "match pending choice");
    AssertEqual(GameEventType.ChoiceResolved, FindEvent(step, GameEventType.ChoiceResolved).Type, "choice resolved event");
    AssertEqual("choice-request-2", step.Observation.Choice.RequestId?.Value, "observation request id");
    AssertTrue(step.Observation.Choice.IsResolved, "resolved");
    AssertFalse(step.Observation.Choice.IsPending, "pending");
    AssertEqual(1, step.Observation.Choice.SelectedIds.Count, "selected id count");
    AssertEqual(new HeadlessEntityId("card-b"), step.Observation.Choice.SelectedIds[0], "selected id");
}

async Task InvalidResolveResultKeepsPendingChoiceAndReportsFailure()
{
    var player = new HeadlessPlayerId(1);
    var match = new DcgoMatch(CreateContext(new ScriptedChoiceProvider(new[]
    {
        ChoiceResult.Skip(),
    })));

    await match.InitializeAsync(CreateConfig(player));
    await match.ApplyActionAsync(CreateRequestChoice(player, actionId: "choice-request-3"));
    await match.StepAsync();

    await match.ApplyActionAsync(HeadlessActionFactory.ResolveChoice(player, actionId: "choice-resolve-3"));
    StepResult step = await match.StepAsync();

    AssertTrue(step.HasPendingChoice, "step pending choice");
    AssertTrue(match.HasPendingChoice(), "match pending choice");
    GameEvent processed = FindEvent(step, GameEventType.ActionProcessed);
    AssertEqual("Choice resolve failed.", processed.Message, "failure message");
    AssertEqual(false, ReadMetadata<bool>(processed, "success"), "success metadata");
    AssertEqual("choice-request-3", step.Observation.Choice.RequestId?.Value, "request id retained");
    AssertTrue(step.Observation.Choice.IsPending, "pending retained");
    AssertFalse(step.Events.Any(gameEvent => gameEvent.Type == GameEventType.ChoiceResolved), "no resolved event");
}

async Task DuplicateRequestChoiceWhilePendingIsRejected()
{
    var player = new HeadlessPlayerId(1);
    var match = new DcgoMatch(EngineContext.CreateDefault());

    await match.InitializeAsync(CreateConfig(player));
    await match.ApplyActionAsync(CreateRequestChoice(player, actionId: "choice-request-original"));
    await match.StepAsync();

    await match.ApplyActionAsync(CreateRequestChoice(player, message: "Replacement", actionId: "choice-request-replacement"));
    StepResult step = await match.StepAsync();

    AssertTrue(step.HasPendingChoice, "still pending");
    GameEvent processed = FindEvent(step, GameEventType.ActionProcessed);
    AssertEqual("RequestChoice action was received while another choice is pending.", processed.Message, "duplicate request message");
    AssertEqual(false, ReadMetadata<bool>(processed, "success"), "success metadata");
    AssertEqual("choice-request-original", step.Observation.Choice.RequestId?.Value, "original request id");
    AssertEqual("Pick one", step.Observation.Choice.Message, "original message");
    AssertFalse(step.Events.Any(gameEvent => gameEvent.Type == GameEventType.ChoiceRequested), "no duplicate requested event");
}

async Task ClearChoiceReleasesPendingChoice()
{
    var player = new HeadlessPlayerId(1);
    var match = new DcgoMatch(EngineContext.CreateDefault());

    await match.InitializeAsync(CreateConfig(player));
    await match.ApplyActionAsync(CreateRequestChoice(player, actionId: "choice-request-clear"));
    await match.StepAsync();
    AssertTrue(match.HasPendingChoice(), "pending before clear");

    await match.ApplyActionAsync(HeadlessActionFactory.ClearChoice(player, actionId: "choice-clear"));
    StepResult step = await match.StepAsync();

    AssertFalse(step.HasPendingChoice, "pending after clear");
    AssertFalse(match.HasPendingChoice(), "match pending after clear");
    AssertEqual(GameEventType.ChoiceCleared, FindEvent(step, GameEventType.ChoiceCleared).Type, "choice cleared event");
    AssertEqual(ChoiceType.Unknown, step.Observation.Choice.Type, "empty choice type");
    AssertEqual(null, step.Observation.Choice.RequestId, "empty request id");
}

Task ChoiceControllerRejectsDirectDuplicateRequestAndPreservesPendingRequest()
{
    var player = new HeadlessPlayerId(1);
    var controller = new InMemoryHeadlessChoiceController();
    ChoiceRequest original = CardRequest(player, "Original");
    ChoiceRequest replacement = CardRequest(player, "Replacement");

    controller.RequestChoice(original, new HeadlessEntityId("choice-original"));

    InvalidOperationException ex = ExpectThrows<InvalidOperationException>(
        () => controller.RequestChoice(replacement, new HeadlessEntityId("choice-replacement")));

    AssertTrue(ex.Message.Contains("another choice is pending", StringComparison.Ordinal), "duplicate message");
    AssertSame(original, controller.PendingRequest, "pending request");
    AssertEqual("choice-original", controller.Current.RequestId?.Value, "request id");
    AssertEqual("Original", controller.Current.Message, "message");
    return Task.CompletedTask;
}

Task AsIsPauseResumeChoiceReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"),
            new[] { "SelectCardEffect", "SelectedList", "_canEndSelectCondition", "_canNoSelect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectPermanentEffect.cs"),
            new[] { "SelectPermanentEffect", "PermanentSelection", "_canNoSelect", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCountEffect.cs"),
            new[] { "SelectCountEffect", "ValueSelection", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectHandEffect.cs"),
            new[] { "SelectHandEffect", "CardSelection" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectAttackEffect.cs"),
            new[] { "SelectAttackEffect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "CardSelection.cs"),
            new[] { "CardSelection", "CardIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "PermanentSelection.cs"),
            new[] { "PermanentSelection", "PermanentIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "ValueSelection.cs"),
            new[] { "ValueSelection", "ValueAsInt" }),
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

Task PendingChoiceRuntimeIntegrationFilesHaveNoPlaceholderOrUnityDependency()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessChoiceState.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "InMemoryHeadlessChoiceController.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MetadataActionProcessor.cs"),
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

    string controller = File.ReadAllText(paths[1]);
    AssertTrue(controller.Contains("Cannot request a new choice while another choice is pending.", StringComparison.Ordinal), "duplicate pending guard");

    string processor = File.ReadAllText(paths[2]);
    AssertTrue(processor.Contains("Choice resolve failed.", StringComparison.Ordinal), "resolve failure contract");
    return Task.CompletedTask;
}

static MatchConfig CreateConfig(params HeadlessPlayerId[] players)
{
    HeadlessPlayerId[] matchPlayers = players.Length > 0
        ? players
        : new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) };

    return MatchConfig.Create(matchPlayers, randomSeed: 23, initialMemory: 0, minimumMemory: -10, maximumMemory: 10);
}

static LegalAction CreateRequestChoice(
    HeadlessPlayerId player,
    string message = "Pick one",
    string actionId = "choice-request")
{
    return HeadlessActionFactory.RequestChoice(
        player,
        ChoiceType.Card,
        message,
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        ChoiceZone.Hand,
        new[]
        {
            new HeadlessEntityId("card-a"),
            new HeadlessEntityId("card-b"),
        },
        actionId);
}

static ChoiceRequest CardRequest(HeadlessPlayerId player, string message)
{
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        message,
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        ChoiceZone.Hand,
        new[]
        {
            new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Hand, IsSelectable: true, player),
        });
}

static EngineContext CreateContext(IChoiceProvider choiceProvider)
{
    GameRandomSource randomSource = new(23);

    return new EngineContext(
        choiceProvider,
        randomSource,
        new CardDatabase(),
        new InMemoryCardInstanceRepository(),
        new InMemoryZoneMover(randomSource),
        new InMemoryRuleQueryService(),
        new InMemoryHeadlessTurnController(),
        new InMemoryHeadlessChoiceController(),
        new InMemoryHeadlessAttackController(),
        new InMemoryHeadlessMemoryController(),
        new NullLogSink(),
        new EngineTaskRunner(),
        new EffectScheduler());
}

static GameEvent FindEvent(StepResult result, GameEventType type)
{
    return result.Events.FirstOrDefault(gameEvent => gameEvent.Type == type)
        ?? throw new InvalidOperationException($"Expected event type {type}.");
}

static T ReadMetadata<T>(GameEvent gameEvent, string key)
{
    if (!gameEvent.Metadata.TryGetValue(key, out object? value) || value is not T typedValue)
    {
        throw new InvalidOperationException($"Expected metadata '{key}' with type {typeof(T).Name}.");
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
