using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2E-005 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS PassAction and CheatAction references are recorded", AsIsPassAndCheatReferencesAreRecorded),
    ("Main phase dispatch exposes pass and excludes cheat/debug actions", MainPhaseDispatchExposesPassAndExcludesCheats),
    ("Legal pass action moves main phase to memory pass", LegalPassMovesToMemoryPass),
    ("Pass processor rejects non-turn player without mutation", PassRejectsNonTurnPlayerWithoutMutation),
    ("Pass processor rejects non-main phase without mutation", PassRejectsNonMainPhaseWithoutMutation),
    ("Cheat action is explicitly rejected without mutation", CheatActionIsRejectedWithoutMutation),
    ("Seeded cheat and debug actions are filtered from legal actions", SeededCheatAndDebugActionsAreFiltered),
    ("Action mask excludes cheat and debug actions", ActionMaskExcludesCheatAndDebugActions),
    ("G2E-005 source files contain no placeholder markers", PassCheatGuardFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2E-005")
        ?? throw new InvalidOperationException("G2E-005 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("MainPhaseAction", Value(row, "area"), "area");
    AssertEqual("PassAction CheatAction guard", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "pass action", "scope");
    AssertContains(Value(row, "unit_test_scope"), "pass cheat guard", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2E-005_pass_cheat_guard_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2E-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2E-001_play_card_action_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2E-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsPassAndCheatReferencesAreRecorded()
{
    string passAction = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MainPhaseAction", "PassAction.cs"));
    string cheatAction = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MainPhaseAction", "CheatAction.cs"));
    string gManager = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GManager.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string nextPhaseButton = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "NextPhaseButton.cs"));

    AssertContains(passAction, "class PassAction : MainPhaseAction", "AS-IS pass action class");
    AssertContains(passAction, "stateMachine.PassTurn()", "AS-IS pass execution target");
    AssertContains(turnStateMachine, "public void PassTurn()", "AS-IS pass turn method");
    AssertContains(turnStateMachine, "EndTurnProcess", "AS-IS pass enters end turn process");
    AssertContains(nextPhaseButton, "new PassAction()", "AS-IS pass queued from next phase");
    AssertContains(cheatAction, "class CheatAction : MainPhaseAction", "AS-IS cheat action class");
    AssertContains(cheatAction, "if (gameManager.AllowCheats())", "AS-IS cheat guard");
    AssertContains(gManager, "public bool AllowCheats()", "AS-IS allow cheats");
    AssertContains(gManager, "new CheatAction", "AS-IS cheat queued by shortcut");
    return Task.CompletedTask;
}

async Task MainPhaseDispatchExposesPassAndExcludesCheats()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync();
    IReadOnlyList<LegalAction> actions = match.GetLegalActions(Player);

    AssertEqual(1, actions.Count(action => action.ActionType == HeadlessActionTypes.Pass), "pass action count");
    AssertFalse(actions.Any(action => action.ActionType == HeadlessActionTypes.Cheat), "cheat action excluded");
    AssertFalse(actions.Any(action => CheatActionGuard.IsCheatOrDebugAction(action.ActionType)), "debug actions excluded");
    AssertEqual(0, match.GetLegalActions(Opponent).Count, "opponent legal action count");
}

async Task LegalPassMovesToMemoryPass()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync(initialMemory: 2);
    LegalAction pass = SingleLegalAction(match, Player, HeadlessActionTypes.Pass);
    int previousMemory = match.Context.MemoryController.Current.Current;

    await match.ApplyActionAsync(pass);
    StepResult step = await match.StepAsync();

    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "phase after pass");
    AssertEqual(-HeadlessMainPhaseFlow.DefaultMemoryPassValue, match.Context.MemoryController.Current.Current, "memory after pass");
    GameEvent processed = step.Events.Last(e => e.Type == GameEventType.ActionProcessed);
    AssertMetadata(processed.Metadata, "success", true);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.Pass);
    AssertMetadata(processed.Metadata, "passIntent", "PassAction");
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.PreviousMemory, previousMemory);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.Memory, -HeadlessMainPhaseFlow.DefaultMemoryPassValue);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.MemoryPassReason, "ExplicitPass");
}

async Task PassRejectsNonTurnPlayerWithoutMutation()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync();
    LegalAction pass = HeadlessActionFactory.Pass(Opponent);
    string before = SnapshotTurnAndMemory(match);

    ActionProcessResult result = new PassAction().Process(pass, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "current turn player", "illegal reason");
    AssertEqual(before, SnapshotTurnAndMemory(match), "state unchanged");
}

async Task PassRejectsNonMainPhaseWithoutMutation()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    LegalAction pass = HeadlessActionFactory.Pass(Player);
    string before = SnapshotTurnAndMemory(match);

    ActionProcessResult result = new PassAction().Process(pass, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "Main phase", "illegal reason");
    AssertEqual(before, SnapshotTurnAndMemory(match), "state unchanged");
}

async Task CheatActionIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync(initialMemory: 2);
    LegalAction cheat = HeadlessActionFactory.Cheat(Player, "GainMemory");
    string before = SnapshotTurnAndMemory(match);

    ActionProcessResult result = await new MetadataActionProcessor().ProcessAsync(cheat, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "excluded", "illegal reason");
    AssertMetadata(result.Metadata, "cheatGuard", "Rejected");
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.CheatType, "GainMemory");
    AssertEqual(before, SnapshotTurnAndMemory(match), "state unchanged");
}

async Task SeededCheatAndDebugActionsAreFiltered()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync();
    SeedCheatAndDebugActions(match);

    IReadOnlyList<LegalAction> actions = match.GetLegalActions(Player);

    AssertFalse(actions.Any(action => action.ActionType == HeadlessActionTypes.Cheat), "seeded cheat filtered");
    AssertFalse(actions.Any(action => action.ActionType == HeadlessActionTypes.DrawCards), "seeded draw filtered");
    AssertFalse(actions.Any(action => action.ActionType == HeadlessActionTypes.SetMemory), "seeded set memory filtered");
    AssertTrue(actions.Any(action => action.ActionType == HeadlessActionTypes.Pass), "pass preserved");
}

async Task ActionMaskExcludesCheatAndDebugActions()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync();
    SeedCheatAndDebugActions(match);

    ActionMask mask = match.GetActionMask();

    AssertFalse(mask.LegalActions.Any(action => action.ActionType == HeadlessActionTypes.Cheat), "mask cheat filtered");
    AssertFalse(mask.LegalActions.Any(action => CheatActionGuard.IsCheatOrDebugAction(action.ActionType)), "mask debug filtered");
    AssertTrue(mask.LegalActions.Any(action => action.ActionType == HeadlessActionTypes.Pass), "mask pass preserved");
}

Task PassCheatGuardFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "PassAction.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionTypes.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionParameterKeys.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionFactory.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessGameLoop.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessLegalActionDispatcher.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MetadataActionProcessor.cs")
    };

    foreach (string path in scopedFiles)
    {
        string text = File.ReadAllText(path);
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
        AssertFalse(text.Contains("NotImplementedException", StringComparison.Ordinal), path);
    }

    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateMainPhaseMatchAsync(int initialMemory = 0)
{
    DcgoMatch match = await CreateInitializedMatchAsync(initialMemory);
    await AdvanceToMainAsync(match, Player);
    return match;
}

async Task<DcgoMatch> CreateInitializedMatchAsync(int initialMemory = 0)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 45);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P1-M{index:D2}"),
            $"P1-M{index:D2}",
            $"P1 card {index}",
            new Dictionary<string, object?>(),
            CardType: "Unknown"));
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P2-M{index:D2}"),
            $"P2-M{index:D2}",
            $"P2 card {index}",
            new Dictionary<string, object?>(),
            CardType: "Unknown"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player);

    await match.InitializeAsync(MatchConfig.Create(
        new[] { Player, Opponent },
        randomSeed: 45,
        initialMemory: initialMemory,
        setup: setup));
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
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
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

void SeedCheatAndDebugActions(DcgoMatch match)
{
    if (match.Context.RuleQueryService is not IHeadlessLegalActionController legalActionController)
    {
        throw new InvalidOperationException("Rule query service does not support seeded legal actions.");
    }

    legalActionController.AddLegalActions(new[]
    {
        HeadlessActionFactory.Cheat(Player, "Draw"),
        HeadlessActionFactory.DrawCards(Player, 1),
        HeadlessActionFactory.SetMemory(Player, 9),
    });
}

static string SnapshotTurnAndMemory(DcgoMatch match)
{
    HeadlessTurnState turn = match.Context.TurnController.Current;
    HeadlessMemoryState memory = match.Context.MemoryController.Current;
    return $"{turn.TurnNumber}:{turn.Phase}:{turn.TurnPlayerId?.Value}:{turn.NonTurnPlayerId?.Value}:{memory.Current}";
}

static void AssertMetadata(IReadOnlyDictionary<string, object?> metadata, string key, object? expected)
{
    if (!metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"metadata: missing key '{key}'.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
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
