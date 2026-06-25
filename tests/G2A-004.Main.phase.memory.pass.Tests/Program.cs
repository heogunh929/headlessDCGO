using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2A-004 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS main phase and memory pass references are recorded", AsIsMainPhaseSourceReferencesAreRecorded),
    ("Advance phase enters main without memory pass", AdvancePhaseEntersMainWithoutMemoryPass),
    ("Explicit pass in main phase enters memory pass with fixed memory", ExplicitPassEntersMemoryPass),
    ("End turn after memory pass hands memory to next player", EndTurnAfterMemoryPassHandsMemoryToNextPlayer),
    ("Pay memory crossing threshold in main triggers memory pass", PayMemoryCrossingThresholdTriggersMemoryPass),
    ("Set memory below threshold in main triggers memory pass", SetMemoryBelowThresholdTriggersMemoryPass),
    ("Add memory positive in main stays in main phase", AddMemoryPositiveStaysInMainPhase),
    ("Pass outside main phase is illegal", PassOutsideMainPhaseIsIllegal),
    ("Non turn player cannot pass main phase", NonTurnPlayerCannotPassMainPhase),
    ("Main phase memory pass files contain no placeholder TODOs", MainPhaseMemoryPassFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-004")
        ?? throw new InvalidOperationException("G2A-004 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("main phase memory logic", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "memory pass", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2A-004_main_phase_memory_pass_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-003", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-003_early_phase_flow_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-003 completion marker");
    return Task.CompletedTask;
}

Task AsIsMainPhaseSourceReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));

    AssertContains(turnStateMachine, "IEnumerator MainPhase()", "AS-IS MainPhase");
    AssertContains(turnStateMachine, "TurnPhase = GameContext.phase.Main", "AS-IS main phase set");
    AssertContains(turnStateMachine, "PassTurn()", "AS-IS PassTurn");
    AssertContains(autoProcessing, "EndTurnCheck()", "AS-IS EndTurnCheck");
    AssertContains(autoProcessing, "TurnEndMinMemory", "AS-IS turn end threshold");
    AssertContains(autoProcessing, "EndTurnProcess()", "AS-IS EndTurnProcess");
    AssertContains(autoProcessing, "gameContext.Memory = 3", "AS-IS pass memory for player 0");
    AssertContains(autoProcessing, "gameContext.Memory = -3", "AS-IS pass memory for player 1");
    AssertContains(player, "MemoryForPlayer", "AS-IS player-relative memory");
    return Task.CompletedTask;
}

async Task AdvancePhaseEntersMainWithoutMemoryPass()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);

    await AdvanceToPhaseAsync(match, player, HeadlessPhase.Breeding);
    StepResult main = await ApplyAdvanceAsync(match, player);
    ActionProcessResult result = LastActionResult(main);

    AssertEqual(HeadlessPhase.Main, main.Observation.Turn.Phase, "phase");
    AssertEqual(0, main.Observation.Memory.Current, "memory");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MainPhaseEntered), "main entered");
    AssertEqual(false, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassTriggered), "memory pass triggered");
    AssertEqual("MainPhaseEntry", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
}

async Task ExplicitPassEntersMemoryPass()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    StepResult pass = await ApplyActionAsync(match, HeadlessActionFactory.Pass(player));
    ActionProcessResult result = LastActionResult(pass);

    AssertTrue(result.IsSuccess, "pass result");
    AssertEqual(HeadlessPhase.MemoryPass, pass.Observation.Turn.Phase, "phase");
    AssertEqual(-3, pass.Observation.Memory.Current, "memory");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassTriggered), "memory pass triggered");
    AssertEqual("ExplicitPass", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
    AssertEqual(3, ReadInt(result.Metadata, HeadlessActionParameterKeys.PassedMemory), "passed memory");
}

async Task EndTurnAfterMemoryPassHandsMemoryToNextPlayer()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPlayerId second = new(2);
    await AdvanceToMainAsync(match, first);
    await ApplyActionAsync(match, HeadlessActionFactory.Pass(first));

    StepResult endTurn = await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));
    ActionProcessResult result = LastActionResult(endTurn);

    AssertTrue(result.IsSuccess, "end turn result");
    AssertEqual(2, endTurn.Observation.Turn.TurnNumber, "turn number");
    AssertEqual(second, endTurn.Observation.Turn.TurnPlayerId, "turn player");
    AssertEqual(HeadlessPhase.Active, endTurn.Observation.Turn.Phase, "phase");
    AssertEqual(3, endTurn.Observation.Memory.Current, "memory");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassCompleted), "memory pass completed");
    AssertEqual("MemoryPassEndTurn", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
}

async Task PayMemoryCrossingThresholdTriggersMemoryPass()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    StepResult pay = await ApplyActionAsync(match, HeadlessActionFactory.PayMemory(player, 1));
    ActionProcessResult result = LastActionResult(pay);

    AssertTrue(result.IsSuccess, "pay result");
    AssertEqual(HeadlessPhase.MemoryPass, pay.Observation.Turn.Phase, "phase");
    AssertEqual(-1, pay.Observation.Memory.Current, "memory");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassTriggered), "memory pass triggered");
    AssertEqual("MemoryThreshold", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
    AssertEqual(1, ReadInt(result.Metadata, HeadlessActionParameterKeys.MemoryPassThreshold), "threshold");
}

async Task SetMemoryBelowThresholdTriggersMemoryPass()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    StepResult setMemory = await ApplyActionAsync(match, HeadlessActionFactory.SetMemory(player, -2));
    ActionProcessResult result = LastActionResult(setMemory);

    AssertTrue(result.IsSuccess, "set memory result");
    AssertEqual(HeadlessPhase.MemoryPass, setMemory.Observation.Turn.Phase, "phase");
    AssertEqual(-2, setMemory.Observation.Memory.Current, "memory");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassTriggered), "memory pass triggered");
    AssertEqual("MemoryThreshold", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
}

async Task AddMemoryPositiveStaysInMainPhase()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    StepResult addMemory = await ApplyActionAsync(match, HeadlessActionFactory.AddMemory(player, 2));
    ActionProcessResult result = LastActionResult(addMemory);

    AssertTrue(result.IsSuccess, "add memory result");
    AssertEqual(HeadlessPhase.Main, addMemory.Observation.Turn.Phase, "phase");
    AssertEqual(2, addMemory.Observation.Memory.Current, "memory");
    AssertEqual(false, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassTriggered), "memory pass triggered");
    AssertEqual("AddMemory", ReadString(result.Metadata, HeadlessActionParameterKeys.MemoryPassReason), "reason");
}

async Task PassOutsideMainPhaseIsIllegal()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);

    StepResult step = await ApplyActionAsync(match, HeadlessActionFactory.Pass(player));
    ActionProcessResult result = LastActionResult(step);

    AssertFalse(result.IsSuccess, "pass result");
    AssertContains(result.Message, "Main phase", "illegal message");
    AssertEqual(HeadlessPhase.Setup, step.Observation.Turn.Phase, "phase");
    AssertEqual(0, step.Observation.Memory.Current, "memory");
}

async Task NonTurnPlayerCannotPassMainPhase()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPlayerId second = new(2);
    await AdvanceToMainAsync(match, first);

    StepResult step = await ApplyActionAsync(match, HeadlessActionFactory.Pass(second));
    ActionProcessResult result = LastActionResult(step);

    AssertFalse(result.IsSuccess, "pass result");
    AssertContains(result.Message, "Only the current turn player", "illegal message");
    AssertEqual(HeadlessPhase.Main, step.Observation.Turn.Phase, "phase");
    AssertEqual(0, step.Observation.Memory.Current, "memory");
}

Task MainPhaseMemoryPassFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessMainPhaseFlow.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MetadataActionProcessor.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionParameterKeys.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static async Task<DcgoMatch> CreateInitializedMatchAsync(int mainDeckCount = 12)
{
    DcgoMatch match = new();
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1", mainDeckCount), BuildDeck(new HeadlessPlayerId(2), "P2", mainDeckCount) },
        firstPlayerId: new HeadlessPlayerId(1));
    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 17, setup: setup));
    return match;
}

static PlayerDeckSetup BuildDeck(
    HeadlessPlayerId playerId,
    string prefix,
    int mainCount = 12,
    int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount)
            .Select(index => new HeadlessEntityId($"{prefix}-M{index:D2}"))
            .ToArray(),
        Enumerable.Range(1, digitamaCount)
            .Select(index => new HeadlessEntityId($"{prefix}-D{index:D2}"))
            .ToArray());
}

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await AdvanceToPhaseAsync(match, playerId, HeadlessPhase.Main);
}

static async Task AdvanceToPhaseAsync(
    DcgoMatch match,
    HeadlessPlayerId playerId,
    HeadlessPhase target)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != target; attempt++)
    {
        await ApplyAdvanceAsync(match, playerId);
    }

    AssertEqual(target, match.GetObservation().Turn.Phase, $"advance to {target}");
}

static async Task<StepResult> ApplyAdvanceAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    return await ApplyActionAsync(match, HeadlessActionFactory.AdvancePhase(playerId));
}

static async Task<StepResult> ApplyActionAsync(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

static ActionProcessResult LastActionResult(StepResult step)
{
    GameEvent processed = step.Events.LastOrDefault(e => e.Type == GameEventType.ActionProcessed)
        ?? throw new InvalidOperationException("ActionProcessed event was not emitted.");
    bool success = processed.Metadata.TryGetValue("success", out object? rawSuccess) && rawSuccess is bool value && value;
    return new ActionProcessResult(success, processed.Message, processed.Metadata);
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

static bool ReadBool(
    IReadOnlyDictionary<string, object?> metadata,
    string key)
{
    if (!metadata.TryGetValue(key, out object? value) || value is null)
    {
        throw new InvalidOperationException($"Metadata key '{key}' is missing.");
    }

    return value switch
    {
        bool boolValue => boolValue,
        string stringValue when bool.TryParse(stringValue, out bool parsed) => parsed,
        _ => throw new InvalidOperationException($"Metadata key '{key}' is not a bool.")
    };
}

static int ReadInt(
    IReadOnlyDictionary<string, object?> metadata,
    string key)
{
    if (!metadata.TryGetValue(key, out object? value) || value is null)
    {
        throw new InvalidOperationException($"Metadata key '{key}' is missing.");
    }

    return value switch
    {
        int intValue => intValue,
        long longValue => (int)longValue,
        string stringValue when int.TryParse(stringValue, out int parsed) => parsed,
        _ => throw new InvalidOperationException($"Metadata key '{key}' is not an int.")
    };
}

static string ReadString(
    IReadOnlyDictionary<string, object?> metadata,
    string key)
{
    if (!metadata.TryGetValue(key, out object? value) || value is null)
    {
        throw new InvalidOperationException($"Metadata key '{key}' is missing.");
    }

    return value.ToString() ?? string.Empty;
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
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
