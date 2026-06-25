using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2A-005 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS end turn cleanup references are recorded", AsIsEndTurnCleanupReferencesAreRecorded),
    ("End turn cleanup removes turn scoped field metadata", EndTurnCleanupRemovesTurnScopedFieldMetadata),
    ("End turn cleanup resets attack count and pending attack state", EndTurnCleanupResetsAttackState),
    ("End turn cleanup preserves persistent and out of scope metadata", EndTurnCleanupPreservesPersistentMetadata),
    ("End turn cleanup keeps hand card turn metadata untouched", EndTurnCleanupKeepsHandCardMetadataUntouched),
    ("Turn scoped metadata remains before end turn", TurnScopedMetadataRemainsBeforeEndTurn),
    ("Memory pass end turn also applies cleanup", MemoryPassEndTurnAlsoAppliesCleanup),
    ("G2A-005 source files contain no placeholder TODOs", EndTurnCleanupFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-005")
        ?? throw new InvalidOperationException("G2A-005 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("end turn cleanup", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "end turn flag reset", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2A-005_end_turn_cleanup_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-004", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-004_main_phase_memory_pass_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-004 completion marker");
    return Task.CompletedTask;
}

Task AsIsEndTurnCleanupReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));

    AssertContains(turnStateMachine, "IEnumerator EndPhase()", "AS-IS EndPhase");
    AssertContains(turnStateMachine, "Reset status until end of turn", "AS-IS end turn cleanup region");
    AssertContains(turnStateMachine, "AttackCount = 0", "AS-IS attack count reset");
    AssertContains(turnStateMachine, "UntilEachTurnEndEffects", "AS-IS each turn cleanup");
    AssertContains(turnStateMachine, "UntilOwnerTurnEndEffects", "AS-IS owner turn cleanup");
    AssertContains(turnStateMachine, "UntilOpponentTurnEndEffects", "AS-IS opponent turn cleanup");
    AssertContains(turnStateMachine, "InitUseCountThisTurn", "AS-IS once/use-count cleanup");
    AssertContains(autoProcessing, "EndTurnProcess()", "AS-IS end turn process");
    AssertContains(attackProcess, "UntilEndAttackEffects", "AS-IS attack cleanup distinction");
    return Task.CompletedTask;
}

async Task EndTurnCleanupRemovesTurnScopedFieldMetadata()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPlayerId second = new(2);
    await AdvanceToMainAsync(match, first);
    await AddBattleCardAsync(match, first, "turn-card", first, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "all",
        ["untilOwnerTurnEndEffects"] = "owner",
        ["oncePerTurnUsed"] = true,
        ["persistentKeyword"] = "Blocker"
    });
    await AddBattleCardAsync(match, second, "opponent-card", second, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "all",
        ["untilOpponentTurnEndEffects"] = "opponent",
        ["persistentKeyword"] = "Reboot"
    });

    StepResult endTurn = await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));
    ActionProcessResult result = LastActionResult(endTurn);

    AssertTrue(result.IsSuccess, "end turn result");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.EndTurnCleanupApplied), "cleanup applied");
    AssertEqual(5, ReadInt(result.Metadata, HeadlessActionParameterKeys.EndTurnCleanupRemovedKeyCount), "removed key count");
    AssertStringSet(
        new[] { "turn-card", "opponent-card" },
        ReadStringArray(result.Metadata, HeadlessActionParameterKeys.EndTurnCleanupCardIds),
        "cleaned card ids");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("untilEachTurnEndEffects"), "turn each effect removed");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("untilOwnerTurnEndEffects"), "owner effect removed");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("oncePerTurnUsed"), "once flag removed");
    AssertFalse(CardMetadata(match, "opponent-card").ContainsKey("untilOpponentTurnEndEffects"), "opponent effect removed");
    AssertEqual("Blocker", CardMetadata(match, "turn-card")["persistentKeyword"], "persistent keyword");
    AssertEqual("Reboot", CardMetadata(match, "opponent-card")["persistentKeyword"], "opponent persistent keyword");
}

async Task EndTurnCleanupResetsAttackState()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    HeadlessPlayerId second = new(2);
    await AdvanceToMainAsync(match, first);
    await AddBattleCardAsync(match, first, "attacker", first, new Dictionary<string, object?>());

    // Establish a pending attack directly on the controller. Routing the declaration through the
    // game loop would let the common loop (G3.5-005) auto-advance and clear the attack before the
    // end-turn cleanup runs; this test isolates the end-turn attack-state reset.
    match.Context.AttackController.DeclareAttack(first, new HeadlessEntityId("attacker"), second);

    StepResult endTurn = await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));
    ActionProcessResult result = LastActionResult(endTurn);

    AssertEqual(1, ReadInt(result.Metadata, HeadlessActionParameterKeys.EndTurnCleanupResetAttackCount), "reset attack count metadata");
    AssertEqual(0, endTurn.Observation.Attack.AttackCount, "attack count");
    AssertFalse(endTurn.Observation.Attack.IsPending, "attack pending");
    AssertFalse(endTurn.Observation.Attack.IsResolved, "attack resolved");
}

async Task EndTurnCleanupPreservesPersistentMetadata()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    await AdvanceToMainAsync(match, first);
    await AddBattleCardAsync(match, first, "persistent-card", first, new Dictionary<string, object?>
    {
        ["isSuspended"] = true,
        ["persistentKeyword"] = "SecurityAttackPlus",
        ["untilEndTurnEffects"] = "temporary"
    });

    await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));
    IReadOnlyDictionary<string, object?> metadata = CardMetadata(match, "persistent-card");

    AssertEqual(true, metadata["isSuspended"], "suspended preserved");
    AssertEqual("SecurityAttackPlus", metadata["persistentKeyword"], "persistent keyword preserved");
    AssertFalse(metadata.ContainsKey("untilEndTurnEffects"), "turn scoped effect removed");
}

async Task EndTurnCleanupKeepsHandCardMetadataUntouched()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    await AdvanceToMainAsync(match, first);
    await AddHandCardAsync(match, first, "hand-card", first, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "hand-selection-marker",
        ["persistentKeyword"] = "HandOnly"
    });

    StepResult endTurn = await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));

    AssertEqual(0, ReadStringArray(LastActionResult(endTurn).Metadata, HeadlessActionParameterKeys.EndTurnCleanupCardIds).Length, "cleaned card count");
    AssertEqual("hand-selection-marker", CardMetadata(match, "hand-card")["untilEachTurnEndEffects"], "hand metadata remains");
    AssertEqual("HandOnly", CardMetadata(match, "hand-card")["persistentKeyword"], "hand persistent metadata");
}

async Task TurnScopedMetadataRemainsBeforeEndTurn()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    await AdvanceToMainAsync(match, first);
    await AddBattleCardAsync(match, first, "pre-end-card", first, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "still-active"
    });

    await ApplyActionAsync(match, HeadlessActionFactory.AddMemory(first, 1));

    AssertEqual("still-active", CardMetadata(match, "pre-end-card")["untilEachTurnEndEffects"], "metadata before end turn");
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "phase before end turn");
}

async Task MemoryPassEndTurnAlsoAppliesCleanup()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    HeadlessPlayerId first = new(1);
    await AdvanceToMainAsync(match, first);
    await AddBattleCardAsync(match, first, "memory-pass-card", first, new Dictionary<string, object?>
    {
        ["untilOwnerTurnEndEffects"] = "owner",
        ["oncePerTurnUsed"] = true
    });
    await ApplyActionAsync(match, HeadlessActionFactory.Pass(first));

    StepResult endTurn = await ApplyActionAsync(match, HeadlessActionFactory.EndTurn(first));
    ActionProcessResult result = LastActionResult(endTurn);

    AssertEqual(HeadlessPhase.Active, endTurn.Observation.Turn.Phase, "phase after memory pass end turn");
    AssertEqual(3, endTurn.Observation.Memory.Current, "memory after memory pass end turn");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.EndTurnCleanupApplied), "cleanup applied");
    AssertEqual(true, ReadBool(result.Metadata, HeadlessActionParameterKeys.MemoryPassCompleted), "memory pass completed");
    AssertFalse(CardMetadata(match, "memory-pass-card").ContainsKey("untilOwnerTurnEndEffects"), "owner effect removed");
    AssertFalse(CardMetadata(match, "memory-pass-card").ContainsKey("oncePerTurnUsed"), "once flag removed");
}

Task EndTurnCleanupFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessEndTurnCleanupFlow.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MetadataActionProcessor.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionParameterKeys.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "IHeadlessAttackController.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "InMemoryHeadlessAttackController.cs")
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
        await ApplyActionAsync(match, HeadlessActionFactory.AdvancePhase(playerId));
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static async Task AddBattleCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    IReadOnlyDictionary<string, object?> metadata)
{
    await AddCardAsync(match, zonePlayer, cardId, owner, ChoiceZone.BattleArea, metadata);
}

static async Task AddHandCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    IReadOnlyDictionary<string, object?> metadata)
{
    await AddCardAsync(match, zonePlayer, cardId, owner, ChoiceZone.Hand, metadata);
}

static async Task AddCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    ChoiceZone zone,
    IReadOnlyDictionary<string, object?> metadata)
{
    HeadlessEntityId id = new(cardId);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(
        id,
        new HeadlessEntityId($"{cardId}-def"),
        owner,
        Metadata: metadata));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(zonePlayer, id, ChoiceZone.None, zone));
}

static IReadOnlyDictionary<string, object?> CardMetadata(DcgoMatch match, string cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(cardId), out CardInstanceRecord? record) ||
        record is null)
    {
        throw new InvalidOperationException($"Card instance '{cardId}' was not found.");
    }

    return record.Metadata;
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
