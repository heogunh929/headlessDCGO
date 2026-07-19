using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
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
    AssertEqual(2, match.Context.MemoryController.Current.Current, "P1 holds +2 before the pass");

    // Pump: applying the explicit Pass runs AS-IS PassTurn -> memory pass -> EndTurnProcess. The OLD
    // single-step model paused at the intermediate IsMemoryPassPhase (StepCursor==AwaitingMemoryPassEnd)
    // before a second EndTurn step completed the flip; under the faithful pump the memory pass and the
    // turn flip are one continuous flow. Drive until the turn hands to the opponent, collecting the
    // emitted step events so the ActionProcessed assertions observe the real processed Pass.
    var driveEvents = new List<GameEvent>();
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        await match.ApplyActionAsync(pass);
        for (int i = 0; i < 12 && match.Context.TurnController.Current.TurnPlayerId != Opponent; i++)
        {
            StepResult sr = await match.StepAsync();
            driveEvents.AddRange(sr.Events);
        }
    }

    // The explicit pass transferred DefaultMemoryPassValue (3) of memory to the opponent and ended P1's
    // turn: the gauge now sits at +3 on the opponent's side and the opponent is the turn player. The OLD
    // assertion read the same magnitude as -3 from P1's still-active turn (the intermediate memory-pass
    // phase); the pump reads it as +3 once the turn has flipped, the same "3 passed to the opponent".
    AssertEqual(Opponent, match.Context.TurnController.Current.TurnPlayerId, "pass ended P1's turn (memory pass concluded)");
    AssertEqual(MetadataActionProcessor.DefaultMemoryPassValue, match.Context.MemoryController.Current.Current, "memory after pass (opponent received 3)");

    // The pump records the pass as a queued main-phase action; its ActionProcessed carries success +
    // actionType + the queued marker + an actionId embedding the Pass. The rich previous/after memory +
    // MemoryPassReason + passIntent metadata was an OLD synchronous-processor surface — the memory delta
    // is verified live above (opponent +3), and the explicit-pass nature is verified by the agent-issued
    // Pass action being the queued main-phase action here (not an auto EndTurnCheck memory pass).
    GameEvent processed = driveEvents.Last(e => e.Type == GameEventType.ActionProcessed
        && e.Metadata.TryGetValue(HeadlessActionParameterKeys.ActionType, out object? at)
        && Equals(at, HeadlessActionTypes.Pass));
    AssertMetadata(processed.Metadata, "success", true);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.Pass);
    AssertMetadata(processed.Metadata, "mainPhaseActionQueued", HeadlessActionTypes.Pass);
    AssertTrue(
        processed.Metadata.TryGetValue("actionId", out object? aid) && aid is string aidStr && aidStr.Contains(HeadlessActionTypes.Pass, StringComparison.Ordinal),
        "the queued Pass action references the Pass action type");
}

// (4b B6 re-pin) The OLD Runtime PassAction processor is retired with the OLD step driver; the pass GUARD
// rules (only the turn player, only during their Main wait) are enforced at the pump's authoritative
// legality boundary — an out-of-set Pass is rejected at apply time and mutates nothing.

async Task PassRejectsNonTurnPlayerWithoutMutation()
{
    DcgoMatch match = await CreateMainPhaseMatchAsync();
    LegalAction pass = HeadlessActionFactory.Pass(Opponent);
    string before = SnapshotTurnAndMemory(match);

    StepResult step = await match.ApplyActionAsync(pass);

    AssertTrue(step.Events.Any(e => e.Type == GameEventType.InvalidAction), "non-turn-player pass rejected at the legality boundary");
    AssertEqual(before, SnapshotTurnAndMemory(match), "state unchanged");
}

async Task PassRejectsNonMainPhaseWithoutMutation()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    LegalAction pass = HeadlessActionFactory.Pass(Player);
    string before = SnapshotTurnAndMemory(match);

    StepResult step = await match.ApplyActionAsync(pass);

    AssertTrue(step.Events.Any(e => e.Type == GameEventType.InvalidAction), "pass outside the Main wait rejected at the legality boundary");
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
    // Port-completeness guard: the Pass/Cheat-guard engine surface carries no placeholder markers. The
    // sniff was re-pointed off HeadlessGameLoop.cs (4b B5-c2): that whole file is a confirmed B6 physical
    // delete target (design doc §1.3 item 1 / §3.1 B6-Db "OLD 스텝 루프+RunToStableAsync"), so a source
    // sniff pinned to it would read a doomed file and red at B6. The game-loop shell is not part of the
    // Pass/Cheat guard verification. The survivors stay: MetadataActionProcessor.cs (hosts the cheat guard;
    // B6 removes only its AdvancePhase/EndTurn method bodies, §1.3 item 2, the file survives) and
    // HeadlessLegalActionDispatcher.cs (B6 removes only the OLD phase-table arm, §1.3 item 11, file survives).
    // (4b B6) The OLD Runtime/PassAction.cs is physically deleted (the pass seat is the mirror
    // MainPhaseAction/PassAction.cs via TurnFlowDriver); the cheat guard was rehomed to CheatActionGuard.cs.
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "MainPhaseAction", "PassAction.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "CheatActionGuard.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionTypes.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionParameterKeys.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionFactory.cs"),
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

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
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

// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired (G2G-001/F62/EXEMPLAR-T1 precedent). Breeding/Mulligan decisions
// are declined; assertion strength unchanged.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
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
    return $"{turn.TurnNumber}:{turn.Phase}:{turn.StepCursor}:{turn.TurnPlayerId?.Value}:{turn.NonTurnPlayerId?.Value}:{memory.Current}";
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
