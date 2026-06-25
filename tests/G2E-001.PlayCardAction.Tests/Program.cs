using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2E-001 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS PlayCardAction references are recorded", AsIsPlayCardActionReferencesAreRecorded),
    ("Main phase legal actions expose playable hand cards only", MainPhaseLegalActionsExposePlayableHandCardsOnly),
    ("Legal PlayCard action pays memory and moves card to battle area", LegalPlayCardPaysMemoryAndMovesCard),
    ("PlayCard processor rejects wrong cost without mutation", PlayCardRejectsWrongCostWithoutMutation),
    ("PlayCard processor rejects non-hand card without mutation", PlayCardRejectsNonHandCardWithoutMutation),
    ("PlayCard processor rejects missing definition without legal action", PlayCardRejectsMissingDefinition),
    ("PlayCard legal query and apply share the same cost condition", LegalQueryAndApplyShareCostCondition),
    ("G2E-001 source files contain no placeholder markers", PlayCardActionFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2E-001")
        ?? throw new InvalidOperationException("G2E-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("MainPhaseAction", Value(row, "area"), "area");
    AssertEqual("PlayCardAction", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "play card legal apply", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2E-001_play_card_action_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-006; G2D-002", Value(row, "blocked_until"), "blocked_until");

    string g2a006 = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-006_legal_action_dispatch_unit_test_results.md"));
    string g2d002 = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2D-002_card_zone_movement_unit_test_results.md"));
    AssertContains(g2a006, "COMPLETE", "G2A-006 completion marker");
    AssertContains(g2d002, "COMPLETE", "G2D-002 completion marker");
    return Task.CompletedTask;
}

Task AsIsPlayCardActionReferencesAreRecorded()
{
    string playCardAction = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MainPhaseAction", "PlayCardAction.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));

    AssertContains(playCardAction, "class PlayCardAction : MainPhaseAction", "AS-IS action class");
    AssertContains(playCardAction, "CardIndex", "AS-IS card index payload");
    AssertContains(playCardAction, "TargetFrameID", "AS-IS target frame payload");
    AssertContains(playCardAction, "SetPlayCard", "AS-IS execution target");
    AssertContains(turnStateMachine, "CanPlayFromHandDuringMainPhase", "AS-IS legal play condition");
    AssertContains(turnStateMachine, "QueueMainPhaseAction(gameContext.TurnPlayer, new PlayCardAction", "AS-IS queued play action");
    AssertContains(turnStateMachine, "playCard.PlayCard()", "AS-IS play apply call");
    return Task.CompletedTask;
}

async Task MainPhaseLegalActionsExposePlayableHandCardsOnly()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    IReadOnlyList<LegalAction> legalActions = match.GetLegalActions(player);
    LegalAction[] playActions = legalActions
        .Where(action => action.ActionType == HeadlessActionTypes.PlayCard)
        .ToArray();

    AssertEqual(1, playActions.Length, "play action count");
    AssertEqual(HeadlessActionTypes.PlayCard, playActions[0].ActionType, "play action type");
    AssertEqual(3, ReadInt(playActions[0].Parameters, HeadlessActionParameterKeys.MemoryCost), "play cost");
    AssertEqual(ChoiceZone.Hand, ReadZone(playActions[0].Parameters, HeadlessActionParameterKeys.FromZone), "from zone");
    AssertEqual(ChoiceZone.BattleArea, ReadZone(playActions[0].Parameters, HeadlessActionParameterKeys.ToZone), "to zone");
    AssertTrue(legalActions.Any(action => action.ActionType == HeadlessActionTypes.Pass), "pass still exposed");
}

async Task LegalPlayCardPaysMemoryAndMovesCard()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    LegalAction play = SingleLegalAction(match, player, HeadlessActionTypes.PlayCard);
    HeadlessEntityId cardId = ReadEntityId(play.Parameters, HeadlessActionParameterKeys.CardId);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    await match.ApplyActionAsync(play);
    StepResult step = await match.StepAsync();

    AssertEqual(beforeMemory - 3, match.Context.MemoryController.Current.Current, "memory after play");
    AssertFalse(ZoneReader(match).GetCards(player, ChoiceZone.Hand).Contains(cardId), "card removed from hand");
    AssertTrue(ZoneReader(match).GetCards(player, ChoiceZone.BattleArea).Contains(cardId), "card moved to battle");

    GameEvent processed = step.Events.Last(e => e.Type == GameEventType.ActionProcessed);
    AssertMetadata(processed, "success", true);
    AssertMetadata(processed, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.PlayCard);
    AssertMetadata(processed, HeadlessActionParameterKeys.CardId, cardId.Value);
    AssertMetadata(processed, HeadlessActionParameterKeys.PreviousMemory, beforeMemory);
    AssertMetadata(processed, HeadlessActionParameterKeys.Memory, beforeMemory - 3);
    AssertTrue(
        match.Context.ZoneMover.Events.Any(e => e.Type == GameEventType.CardMoved &&
            Equals(e.Metadata[HeadlessActionParameterKeys.CardId], cardId.Value)),
        "zone mover emitted CardMoved");
}

async Task PlayCardRejectsWrongCostWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    LegalAction legal = SingleLegalAction(match, player, HeadlessActionTypes.PlayCard);
    HeadlessEntityId cardId = ReadEntityId(legal.Parameters, HeadlessActionParameterKeys.CardId);
    var action = HeadlessActionFactory.PlayCard(player, cardId, memoryCost: 4);
    string beforeZones = SnapshotZones(match, player);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "does not match", "illegal reason");
    AssertEqual(beforeMemory, match.Context.MemoryController.Current.Current, "memory unchanged");
    AssertEqual(beforeZones, SnapshotZones(match, player), "zones unchanged");
}

async Task PlayCardRejectsNonHandCardWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    HeadlessEntityId libraryCard = ZoneReader(match).GetCards(player, ChoiceZone.Library).First();
    var action = HeadlessActionFactory.PlayCard(player, libraryCard, memoryCost: 3);
    string beforeZones = SnapshotZones(match, player);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "hand", "illegal reason");
    AssertEqual(beforeMemory, match.Context.MemoryController.Current.Current, "memory unchanged");
    AssertEqual(beforeZones, SnapshotZones(match, player), "zones unchanged");
}

async Task PlayCardRejectsMissingDefinition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(includePlayableRecord: false);
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    LegalAction[] playActions = match.GetLegalActions(player)
        .Where(action => action.ActionType == HeadlessActionTypes.PlayCard)
        .ToArray();

    AssertEqual(0, playActions.Length, "legal play action count");
}

async Task LegalQueryAndApplyShareCostCondition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(initialMemory: 0, minimumMemory: -2);
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    IReadOnlyList<HeadlessEntityId> hand = ZoneReader(match).GetCards(player, ChoiceZone.Hand);
    HeadlessEntityId playableDefinition = new("P1-M01");
    HeadlessEntityId expensiveCard = hand.Single(cardId => match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) &&
        instance!.DefinitionId == playableDefinition);
    var action = HeadlessActionFactory.PlayCard(player, expensiveCard, memoryCost: 3);

    AssertEqual(0, match.GetLegalActions(player).Count(a => a.ActionType == HeadlessActionTypes.PlayCard), "legal play count");

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(action, match.Context);
    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "Cannot pay", "illegal reason");
}

Task PlayCardActionFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "PlayCardAction.cs")
    };

    foreach (string path in scopedFiles)
    {
        string text = File.ReadAllText(path);
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
        AssertFalse(text.Contains("NotImplementedException", StringComparison.Ordinal), path);
    }

    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(
    bool includePlayableRecord = true,
    int initialMemory = 0,
    int minimumMemory = -5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 31);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    if (includePlayableRecord)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId("P1-M01"),
            "P1-M01",
            "Playable Digimon",
            new Dictionary<string, object?>(),
            CardType: "Digimon",
            PlayCost: 3));
    }

    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M02"),
        "P1-M02",
        "Too Expensive Digimon",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 12));

    DcgoMatch match = new(context);
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            BuildDeck(new HeadlessPlayerId(1), "P1"),
            BuildDeck(new HeadlessPlayerId(2), "P2")
        },
        firstPlayerId: new HeadlessPlayerId(1));

    await match.InitializeAsync(MatchConfig.Create(
        players,
        randomSeed: 31,
        initialMemory: initialMemory,
        minimumMemory: minimumMemory,
        maximumMemory: 10,
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

static IZoneStateReader ZoneReader(DcgoMatch match)
{
    return match.Context.ZoneMover as IZoneStateReader
        ?? throw new InvalidOperationException("Zone mover is not readable.");
}

static string SnapshotZones(DcgoMatch match, HeadlessPlayerId playerId)
{
    return string.Join(
        "|",
        ZoneReader(match)
            .Snapshot(playerId)
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{string.Join(",", pair.Value.Select(id => id.Value))}"));
}

static int ReadInt(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing int parameter '{key}'.");
    }

    return raw switch
    {
        int value => value,
        long value => (int)value,
        string value when int.TryParse(value, out int parsed) => parsed,
        _ => throw new InvalidOperationException($"Invalid int parameter '{key}'.")
    };
}

static ChoiceZone ReadZone(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing zone parameter '{key}'.");
    }

    return raw switch
    {
        ChoiceZone zone => zone,
        string value when Enum.TryParse(value, out ChoiceZone zone) => zone,
        _ => throw new InvalidOperationException($"Invalid zone parameter '{key}'.")
    };
}

static HeadlessEntityId ReadEntityId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing entity id parameter '{key}'.");
    }

    return raw switch
    {
        HeadlessEntityId id => id,
        string value => new HeadlessEntityId(value),
        _ => throw new InvalidOperationException($"Invalid entity id parameter '{key}'.")
    };
}

static void AssertMetadata(GameEvent gameEvent, string key, object? expected)
{
    if (!gameEvent.Metadata.TryGetValue(key, out object? actual))
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
