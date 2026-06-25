using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2A-006 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS legal action dispatch references are recorded", AsIsLegalActionDispatchReferencesAreRecorded),
    ("Setup phase dispatch exposes advance phase for turn player only", SetupPhaseDispatchExposesAdvanceForTurnPlayerOnly),
    ("Early phase dispatch follows advance phase sequence", EarlyPhaseDispatchFollowsAdvancePhaseSequence),
    ("Main phase dispatch exposes pass and memory pass exposes end turn", MainAndMemoryPassDispatchExposeExpectedActions),
    ("Action mask merges dispatched phase action with seeded legal action", ActionMaskMergesDispatchedAndSeededActions),
    ("Terminal state suppresses dispatched legal actions", TerminalStateSuppressesDispatchedLegalActions),
    ("Pending effect suppresses dispatched legal actions until resolved", PendingEffectSuppressesDispatchedLegalActions),
    ("Setup-less empty match preserves legacy empty legal action contract", SetupLessEmptyMatchPreservesLegacyEmptyLegalActions),
    ("G2A-006 source files contain no placeholder TODOs", LegalDispatchFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-006")
        ?? throw new InvalidOperationException("G2A-006 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("legal action dispatcher", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "phase legal action", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2A-006_legal_action_dispatch_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-005", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-005_end_turn_cleanup_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-005 completion marker");
    return Task.CompletedTask;
}

Task AsIsLegalActionDispatchReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));

    AssertContains(turnStateMachine, "CanSelect()", "AS-IS main phase can select");
    AssertContains(turnStateMachine, "CanPlayFromHandDuringMainPhase", "AS-IS hand legal play check");
    AssertContains(turnStateMachine, "CanAttack(null)", "AS-IS attack legal check");
    AssertContains(turnStateMachine, "QueueMainPhaseAction", "AS-IS action dispatch queue");
    AssertContains(turnStateMachine, "PassTurn()", "AS-IS pass action");
    AssertContains(player, "Queue<MainPhaseAction>", "AS-IS player main action queue");
    AssertContains(player, "HasMainPhaseAction()", "AS-IS queued action availability");
    return Task.CompletedTask;
}

async Task SetupPhaseDispatchExposesAdvanceForTurnPlayerOnly()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPlayerId second = new(2);

    AssertActionTypes(new[] { HeadlessActionTypes.AdvancePhase }, match.GetLegalActions(first), "first legal actions");
    AssertEqual(0, match.GetLegalActions(second).Count, "second legal actions");
    AssertEqual(1, match.GetActionMask().Count, "action mask count");
}

async Task EarlyPhaseDispatchFollowsAdvancePhaseSequence()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPhase[] expectedAfterAdvance =
    {
        HeadlessPhase.Active,
        HeadlessPhase.Unsuspend,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main
    };

    foreach (HeadlessPhase expectedPhase in expectedAfterAdvance)
    {
        LegalAction advance = SingleLegalAction(match, first, HeadlessActionTypes.AdvancePhase);
        StepResult step = await ApplyActionAsync(match, advance);
        AssertEqual(expectedPhase, step.Observation.Turn.Phase, $"phase after {expectedPhase}");
    }
}

async Task MainAndMemoryPassDispatchExposeExpectedActions()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    await AdvanceToMainAsync(match, first);

    LegalAction pass = SingleLegalAction(match, first, HeadlessActionTypes.Pass);
    StepResult memoryPass = await ApplyActionAsync(match, pass);
    AssertEqual(HeadlessPhase.MemoryPass, memoryPass.Observation.Turn.Phase, "memory pass phase");

    LegalAction endTurn = SingleLegalAction(match, first, HeadlessActionTypes.EndTurn);
    StepResult nextTurn = await ApplyActionAsync(match, endTurn);
    AssertEqual(new HeadlessPlayerId(2), nextTurn.Observation.Turn.TurnPlayerId, "next turn player");
    AssertEqual(HeadlessPhase.Active, nextTurn.Observation.Turn.Phase, "next turn phase");
}

async Task ActionMaskMergesDispatchedAndSeededActions()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    var seeded = HeadlessActionFactory.NoOp(first, "seeded-legal-action");
    ((IHeadlessLegalActionController)match.Context.RuleQueryService).AddLegalActions(new[] { seeded });

    ActionMask mask = match.GetActionMask();

    AssertEqual(2, mask.Count, "mask count");
    AssertTrue(mask.LegalActions.Any(action => action.ActionType == HeadlessActionTypes.AdvancePhase), "has dispatched advance");
    AssertTrue(mask.ContainsAction(seeded), "has seeded action");
}

async Task TerminalStateSuppressesDispatchedLegalActions()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    ((ITerminalStateController)match.Context.RuleQueryService).SetTerminal(true);

    AssertEqual(0, match.GetLegalActions(first).Count, "terminal legal actions");
    AssertEqual(0, match.GetActionMask().Count, "terminal action mask");
}

async Task PendingEffectSuppressesDispatchedLegalActions()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    match.Context.EffectScheduler.Enqueue(new EffectRequest(
        new HeadlessEntityId("pending-effect"),
        first,
        "TestTiming",
        new EffectContext(first, new HeadlessEntityId("effect-source"))));

    AssertEqual(0, match.GetLegalActions(first).Count, "pending effect legal actions");
    await match.StepAsync();
    AssertActionTypes(new[] { HeadlessActionTypes.AdvancePhase }, match.GetLegalActions(first), "post effect legal actions");
}

async Task SetupLessEmptyMatchPreservesLegacyEmptyLegalActions()
{
    DcgoMatch match = new();
    HeadlessPlayerId first = new(1);
    await match.InitializeAsync(MatchConfig.Create(new[] { first, new HeadlessPlayerId(2) }));

    AssertEqual(0, match.GetLegalActions(first).Count, "setup-less legal actions");
    AssertFalse(match.GetActionMask().HasAnyLegalAction, "setup-less action mask");
}

Task LegalDispatchFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessLegalActionDispatcher.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessGameLoop.cs")
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
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, playerId, HeadlessActionTypes.AdvancePhase);
        await ApplyActionAsync(match, advance);
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static LegalAction SingleLegalAction(
    DcgoMatch match,
    HeadlessPlayerId playerId,
    string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

static async Task<StepResult> ApplyActionAsync(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

static void AssertActionTypes(
    IReadOnlyList<string> expectedActionTypes,
    IReadOnlyList<LegalAction> actions,
    string label)
{
    AssertSequence(
        expectedActionTypes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        actions.Select(action => action.ActionType).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        label);
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
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
