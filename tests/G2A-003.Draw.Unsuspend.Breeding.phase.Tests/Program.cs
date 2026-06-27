using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2A-003 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS early phase source references are recorded", AsIsEarlyPhaseSourceReferencesAreRecorded),
    ("Setup advances through active unsuspend draw breeding main", SetupAdvancesThroughEarlyPhaseOrder),
    ("Unsuspend phase clears turn player and reboot suspended cards", UnsuspendPhaseClearsEligibleCards),
    ("First turn draw phase skips draw", FirstTurnDrawPhaseSkipsDraw),
    ("Second turn draw phase draws one card", SecondTurnDrawPhaseDrawsOneCard),
    ("Draw phase deck out marks terminal", DrawPhaseDeckOutMarksTerminal),
    ("Breeding phase hatches digitama when breeding area is empty", BreedingPhaseHatchesDigitama),
    ("Breeding phase moves breeding card when occupied", BreedingPhaseMovesOccupiedBreedingArea),
    ("Non turn player cannot advance early phase", NonTurnPlayerCannotAdvanceEarlyPhase),
    ("Early phase source files contain no placeholder TODOs", EarlyPhaseFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-003")
        ?? throw new InvalidOperationException("G2A-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("phase transition implementation", Value(row, "deliverables"), "deliverables");
    AssertEqual("draw unsuspend breeding 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2A-003_early_phase_flow_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-002", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-002_setup_first_player_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-002 completion marker");
    return Task.CompletedTask;
}

Task AsIsEarlyPhaseSourceReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));
    string cardObjectController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"));

    AssertContains(turnStateMachine, "IEnumerator ActivePhase()", "AS-IS ActivePhase");
    AssertContains(turnStateMachine, "IUnsuspendPermanents", "AS-IS unsuspend");
    AssertContains(turnStateMachine, "IEnumerator DrawPhase()", "AS-IS DrawPhase");
    AssertContains(turnStateMachine, "if (TurnCount != 1)", "AS-IS first turn draw skip");
    AssertContains(turnStateMachine, "IEnumerator BreedingPhase()", "AS-IS BreedingPhase");
    AssertContains(turnStateMachine, "HatchDigiEggClass", "AS-IS hatch");
    AssertContains(turnStateMachine, "MovePermanent", "AS-IS breeding move");
    AssertContains(cardController, "public class DrawClass", "AS-IS DrawClass");
    AssertContains(cardController, "public class IAddSecurityFromLibrary", "AS-IS setup security");
    AssertContains(cardObjectController, "Shuffle", "AS-IS shuffle");
    return Task.CompletedTask;
}

async Task SetupAdvancesThroughEarlyPhaseOrder()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);

    AssertEqual(HeadlessPhase.Setup, match.GetObservation().Turn.Phase, "initial phase");
    AssertEqual(HeadlessPhase.Active, await AdvancePhaseAsync(match, player), "active");
    AssertEqual(HeadlessPhase.Unsuspend, await AdvancePhaseAsync(match, player), "unsuspend");
    AssertEqual(HeadlessPhase.Draw, await AdvancePhaseAsync(match, player), "draw");
    AssertEqual(HeadlessPhase.Breeding, await AdvancePhaseAsync(match, player), "breeding");
    AssertEqual(HeadlessPhase.Main, await AdvancePhaseAsync(match, player), "main");
}

async Task UnsuspendPhaseClearsEligibleCards()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId turnPlayer = new(1);
    HeadlessPlayerId opponent = new(2);

    await AddSuspendedBattleCardAsync(match, turnPlayer, "turn-suspended", owner: turnPlayer);
    await AddSuspendedBattleCardAsync(match, opponent, "opponent-reboot", owner: opponent, hasReboot: true);
    await AddSuspendedBattleCardAsync(match, opponent, "opponent-no-reboot", owner: opponent);
    await AddSuspendedBreedingCardAsync(match, turnPlayer, "breeding-suspended", owner: turnPlayer);

    await AdvancePhaseAsync(match, turnPlayer);
    StepResult unsuspend = await ApplyAdvanceAsync(match, turnPlayer);
    ActionProcessResult result = LastActionResult(unsuspend);

    AssertEqual(HeadlessPhase.Unsuspend.ToString(), result.Metadata[HeadlessActionParameterKeys.Phase], "phase metadata");
    AssertStringSet(
        new[] { "turn-suspended", "opponent-reboot", "breeding-suspended" },
        ReadStringArray(result.Metadata, HeadlessActionParameterKeys.UnsuspendedCardIds),
        "unsuspended ids");
    AssertFalse(IsSuspended(match, "turn-suspended"), "turn player card suspended");
    AssertFalse(IsSuspended(match, "opponent-reboot"), "opponent reboot card suspended");
    AssertTrue(IsSuspended(match, "opponent-no-reboot"), "opponent no reboot remains suspended");
    AssertFalse(IsSuspended(match, "breeding-suspended"), "breeding card suspended");
}

async Task FirstTurnDrawPhaseSkipsDraw()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);

    await AdvancePhaseAsync(match, player);
    await AdvancePhaseAsync(match, player);
    StepResult draw = await ApplyAdvanceAsync(match, player);
    ActionProcessResult result = LastActionResult(draw);

    AssertEqual(HeadlessPhase.Draw, draw.Observation.Turn.Phase, "draw phase");
    AssertEqual(true, result.Metadata[HeadlessActionParameterKeys.DrawSkipped], "draw skipped");
    AssertEqual(0, ReadStringArray(result.Metadata, HeadlessActionParameterKeys.DrawnCardIds).Length, "drawn ids");
    AssertEqual(5, ZoneCount(draw.Observation, player, ChoiceZone.Hand), "hand count");
    AssertEqual(2, ZoneCount(draw.Observation, player, ChoiceZone.Library), "library count");
}

async Task SecondTurnDrawPhaseDrawsOneCard()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId firstPlayer = new(1);
    HeadlessPlayerId secondPlayer = new(2);

    match.Context.TurnController.SetPhase(HeadlessPhase.End);
    await ApplyEndTurnAsync(match, firstPlayer);
    await AdvancePhaseAsync(match, secondPlayer);
    StepResult draw = await ApplyAdvanceAsync(match, secondPlayer);

    AssertEqual(HeadlessPhase.Draw, draw.Observation.Turn.Phase, "draw phase");
    AssertEqual(false, LastActionResult(draw).Metadata[HeadlessActionParameterKeys.DrawSkipped], "draw skipped");
    AssertEqual(1, ReadStringArray(LastActionResult(draw).Metadata, HeadlessActionParameterKeys.DrawnCardIds).Length, "drawn ids");
    AssertEqual(6, ZoneCount(draw.Observation, secondPlayer, ChoiceZone.Hand), "hand count");
    AssertEqual(1, ZoneCount(draw.Observation, secondPlayer, ChoiceZone.Library), "library count");
}

async Task DrawPhaseDeckOutMarksTerminal()
{
    DcgoMatch match = await CreateInitializedMatchAsync(mainDeckCount: 10);
    HeadlessPlayerId firstPlayer = new(1);
    HeadlessPlayerId secondPlayer = new(2);

    match.Context.TurnController.SetPhase(HeadlessPhase.End);
    await ApplyEndTurnAsync(match, firstPlayer);
    await AdvancePhaseAsync(match, secondPlayer);
    StepResult draw = await ApplyAdvanceAsync(match, secondPlayer);

    AssertTrue(draw.IsTerminal, "deck out terminal");
    AssertEqual(true, LastActionResult(draw).Metadata[HeadlessActionParameterKeys.DeckOut], "deck out metadata");
    AssertEqual(0, ZoneCount(draw.Observation, secondPlayer, ChoiceZone.Library), "library count");
}

async Task BreedingPhaseHatchesDigitama()
{
    // D-6: breeding is now a player DECISION (not auto-resolved). Advancing into the breeding phase
    // hatches nothing; the turn player hatches explicitly via the dispatched HatchDigitama action.
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);

    StepResult enter = await AdvanceToPhaseAsync(match, player, HeadlessPhase.Breeding);
    AssertEqual(HeadlessPhase.Breeding, enter.Observation.Turn.Phase, "breeding phase");
    AssertEqual("None", LastActionResult(enter).Metadata[HeadlessActionParameterKeys.BreedingAction], "no auto breeding action");
    AssertTrue(
        match.GetLegalActions(player).Any(a => a.ActionType == HeadlessActionTypes.HatchDigitama),
        "hatch is offered as a legal action");

    StepResult hatch = await ApplyActionAsync(match, HeadlessActionFactory.HatchDigitama(player));
    AssertTrue(LastActionResult(hatch).IsSuccess, "hatch success");
    AssertEqual(1, ZoneCount(hatch.Observation, player, ChoiceZone.BreedingArea), "breeding count");
    AssertEqual(2, ZoneCount(hatch.Observation, player, ChoiceZone.DigitamaLibrary), "digitama count");
}

async Task BreedingPhaseMovesOccupiedBreedingArea()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId player = new(1);
    await match.Context.ZoneMover.HatchDigitamaAsync(player);
    match.Context.TurnController.SetPhase(HeadlessPhase.Draw);

    StepResult enter = await ApplyAdvanceAsync(match, player); // Draw -> Breeding
    AssertEqual(HeadlessPhase.Breeding, enter.Observation.Turn.Phase, "breeding phase");
    AssertTrue(
        match.GetLegalActions(player).Any(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle),
        "move is offered when the breeding area is occupied");

    StepResult move = await ApplyActionAsync(match, HeadlessActionFactory.MoveBreedingToBattle(player, count: 1));
    AssertTrue(LastActionResult(move).IsSuccess, "move success");
    AssertEqual(0, ZoneCount(move.Observation, player, ChoiceZone.BreedingArea), "breeding count");
    AssertEqual(1, ZoneCount(move.Observation, player, ChoiceZone.BattleArea), "battle count");
}

async Task NonTurnPlayerCannotAdvanceEarlyPhase()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    StepResult step = await ApplyAdvanceAsync(match, new HeadlessPlayerId(2));
    ActionProcessResult result = LastActionResult(step);

    AssertFalse(result.IsSuccess, "illegal advance success");
    AssertContains(result.Message, "Only the current turn player", "illegal advance message");
    AssertEqual(HeadlessPhase.Setup, step.Observation.Turn.Phase, "phase unchanged");
}

Task EarlyPhaseFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessEarlyPhaseFlow.cs"),
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

static async Task<HeadlessPhase> AdvancePhaseAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    return (await ApplyAdvanceAsync(match, playerId)).Observation.Turn.Phase;
}

static async Task<StepResult> ApplyAdvanceAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await match.ApplyActionAsync(HeadlessActionFactory.AdvancePhase(playerId));
    return await match.StepAsync();
}

static async Task<StepResult> ApplyActionAsync(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

static async Task<StepResult> AdvanceToPhaseAsync(DcgoMatch match, HeadlessPlayerId playerId, HeadlessPhase target)
{
    StepResult step = await ApplyAdvanceAsync(match, playerId);
    for (var attempt = 0; attempt < 8 && step.Observation.Turn.Phase != target; attempt++)
    {
        step = await ApplyAdvanceAsync(match, playerId);
    }

    return step;
}

static async Task<StepResult> ApplyEndTurnAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(playerId));
    return await match.StepAsync();
}

static ActionProcessResult LastActionResult(StepResult step)
{
    GameEvent processed = step.Events.LastOrDefault(e => e.Type == GameEventType.ActionProcessed)
        ?? throw new InvalidOperationException("ActionProcessed event was not emitted.");
    bool success = processed.Metadata.TryGetValue("success", out object? rawSuccess) && rawSuccess is bool value && value;
    return new ActionProcessResult(success, processed.Message, processed.Metadata);
}

static async Task AddSuspendedBattleCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    bool hasReboot = false)
{
    HeadlessEntityId id = new(cardId);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(
        id,
        new HeadlessEntityId($"{cardId}-def"),
        owner,
        Metadata: new Dictionary<string, object?>
        {
            ["isSuspended"] = true,
            ["canUnsuspend"] = true,
            ["hasReboot"] = hasReboot
        }));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(zonePlayer, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

static async Task AddSuspendedBreedingCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner)
{
    HeadlessEntityId id = new(cardId);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(
        id,
        new HeadlessEntityId($"{cardId}-def"),
        owner,
        Metadata: new Dictionary<string, object?>
        {
            ["isSuspended"] = true,
            ["canUnsuspend"] = true
        }));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(zonePlayer, id, ChoiceZone.None, ChoiceZone.BreedingArea));
}

static bool IsSuspended(DcgoMatch match, string cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(cardId), out CardInstanceRecord? record) ||
        record is null ||
        !record.Metadata.TryGetValue("isSuspended", out object? rawValue))
    {
        return false;
    }

    return rawValue is bool value && value;
}

static int ZoneCount(
    ObservationSnapshot observation,
    HeadlessPlayerId playerId,
    ChoiceZone zone)
{
    return observation.Players
        .Single(player => player.PlayerId == playerId)
        .FindZone(zone)
        ?.Count ?? 0;
}

static string[] ReadStringArray(
    IReadOnlyDictionary<string, object?> metadata,
    string key)
{
    if (!metadata.TryGetValue(key, out object? value) || value is null)
    {
        return Array.Empty<string>();
    }

    return value switch
    {
        string[] strings => strings,
        IEnumerable<string> strings => strings.ToArray(),
        object[] objects => objects.Select(item => item?.ToString() ?? string.Empty).ToArray(),
        _ => Array.Empty<string>()
    };
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

static void AssertStringSet(
    IReadOnlyList<string> expected,
    IReadOnlyList<string> actual,
    string label)
{
    AssertSequence(
        expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        actual.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        label);
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
