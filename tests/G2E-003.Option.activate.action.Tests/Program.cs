using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessEntityId OptionCardId = new("p1:main:001:P1-OPT01");
HeadlessEntityId NonOptionCardId = new("p1:main:002:P1-M02");
HeadlessEntityId OptionEffectId = new("effect:p1-option");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2E-003 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS option activate references are recorded", AsIsOptionActivateReferencesAreRecorded),
    ("Main phase legal actions expose usable option activation", MainPhaseLegalActionsExposeUsableOptionActivation),
    ("Option activate processor pays memory moves card and enqueues effect", OptionActivatePaysMemoryMovesCardAndEnqueuesEffect),
    ("Match loop resolves enqueued option effect after processing", MatchLoopResolvesEnqueuedOptionEffect),
    ("Option activate rejects non-option card without mutation", OptionActivateRejectsNonOptionWithoutMutation),
    ("Option activate rejects wrong cost without mutation", OptionActivateRejectsWrongCostWithoutMutation),
    ("Option activate legal query and apply share locked option condition", LegalQueryAndApplyShareLockedOptionCondition),
    ("Option activate legal query and apply share memory condition", LegalQueryAndApplyShareMemoryCondition),
    ("G2E-003 source files contain no placeholder markers", OptionActivateFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2E-003")
        ?? throw new InvalidOperationException("G2E-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("MainPhaseAction", Value(row, "area"), "area");
    AssertEqual("ActivateCardAction option flow", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "option use", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2E-003_option_activate_action_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2E-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2E-001_play_card_action_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2E-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsOptionActivateReferencesAreRecorded()
{
    string activateCardAction = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MainPhaseAction", "ActivateCardAction.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));

    AssertContains(activateCardAction, "class ActivateCardAction : MainPhaseAction", "AS-IS option action class");
    AssertContains(activateCardAction, "CardIndex", "AS-IS card index payload");
    AssertContains(activateCardAction, "SkillIndex", "AS-IS skill index payload");
    AssertContains(activateCardAction, "SetActCardSkill", "AS-IS execution target");
    AssertContains(turnStateMachine, "UseCardEffect.CanUse(null)", "AS-IS option use condition");
    AssertContains(turnStateMachine, "SetIsDeclarative(true)", "AS-IS declarative marker");
    AssertContains(turnStateMachine, "new ActivateCardAction(handCard.cardSource.CardIndex", "AS-IS queued option action");
    AssertContains(turnStateMachine, "CanNotPlayThisOption", "AS-IS option lock");
    AssertContains(cardSource, "ActivateICardEffect && cardEffect.CanUse(null)", "AS-IS option effect query");
    return Task.CompletedTask;
}

async Task MainPhaseLegalActionsExposeUsableOptionActivation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await AdvanceToMainAsync(match, Player);

    IReadOnlyList<LegalAction> legalActions = match.GetLegalActions(Player);
    LegalAction activate = SingleLegalAction(match, Player, HeadlessActionTypes.ActivateOption);

    AssertEqual(OptionCardId, ReadEntityId(activate.Parameters, HeadlessActionParameterKeys.CardId), "option card id");
    AssertEqual(OptionEffectId, ReadEntityId(activate.Parameters, HeadlessActionParameterKeys.EffectId), "option effect id");
    AssertEqual(3, ReadInt(activate.Parameters, HeadlessActionParameterKeys.MemoryCost), "option cost");
    AssertEqual(0, ReadInt(activate.Parameters, HeadlessActionParameterKeys.SkillIndex), "skill index");
    AssertFalse(legalActions.Any(action =>
        action.ActionType == HeadlessActionTypes.PlayCard &&
        ReadEntityId(action.Parameters, HeadlessActionParameterKeys.CardId) == OptionCardId), "option must not be exposed as PlayCard");
    AssertTrue(legalActions.Any(action => action.ActionType == HeadlessActionTypes.Pass), "pass still exposed");
}

async Task OptionActivatePaysMemoryMovesCardAndEnqueuesEffect()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await AdvanceToMainAsync(match, Player);
    LegalAction activate = SingleLegalAction(match, Player, HeadlessActionTypes.ActivateOption);
    string beforeZones = SnapshotZones(match, Player);
    int beforeMemory = match.Context.MemoryController.Current.Current;
    int beforeMoveEvents = match.Context.ZoneMover.Events.Count(e => e.Type == GameEventType.CardMoved);

    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(activate, match.Context);

    AssertTrue(result.IsSuccess, "result success");
    AssertEqual(beforeMemory - 3, match.Context.MemoryController.Current.Current, "memory after option");
    AssertFalse(ZoneReader(match).GetCards(Player, ChoiceZone.Hand).Contains(OptionCardId), "option removed from hand");
    AssertTrue(ZoneReader(match).GetCards(Player, ChoiceZone.Trash).Contains(OptionCardId), "option moved to trash");
    AssertFalse(SnapshotZones(match, Player) == beforeZones, "zones changed");
    AssertEqual(beforeMoveEvents + 1, match.Context.ZoneMover.Events.Count(e => e.Type == GameEventType.CardMoved), "movement event count");
    AssertEqual(1, match.Context.EffectScheduler.PendingCount, "pending effect count");
    AssertEqual(1, match.Context.EffectScheduler.TotalEnqueuedCount, "total enqueued effect count");
    AssertEqual(0, match.Context.EffectScheduler.TotalResolvedCount, "total resolved effect count");
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.ActivateOption);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.CardId, OptionCardId.Value);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.EffectId, OptionEffectId.Value);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.PreviousMemory, beforeMemory);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.Memory, beforeMemory - 3);
}

async Task MatchLoopResolvesEnqueuedOptionEffect()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await AdvanceToMainAsync(match, Player);
    LegalAction activate = SingleLegalAction(match, Player, HeadlessActionTypes.ActivateOption);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    await match.ApplyActionAsync(activate);
    StepResult step = await match.StepAsync();

    AssertEqual(beforeMemory - 3, match.Context.MemoryController.Current.Current, "memory after option");
    AssertTrue(ZoneReader(match).GetCards(Player, ChoiceZone.Trash).Contains(OptionCardId), "option moved to trash");
    AssertEqual(0, match.Context.EffectScheduler.PendingCount, "pending effect count");
    AssertEqual(1, match.Context.EffectScheduler.TotalEnqueuedCount, "total enqueued effect count");
    AssertEqual(1, match.Context.EffectScheduler.TotalResolvedCount, "total resolved effect count");
    AssertTrue(step.Events.Any(e => e.Type == GameEventType.EffectResolved), "effect resolved event");
    GameEvent processed = step.Events.Last(e => e.Type == GameEventType.ActionProcessed);
    AssertMetadata(processed.Metadata, "success", true);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.ActivateOption);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.CardId, OptionCardId.Value);
    AssertMetadata(processed.Metadata, HeadlessActionParameterKeys.EffectId, OptionEffectId.Value);
}

async Task OptionActivateRejectsNonOptionWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await AdvanceToMainAsync(match, Player);
    var action = HeadlessActionFactory.ActivateOption(Player, NonOptionCardId, new HeadlessEntityId("effect:p1-non-option"), memoryCost: 0);
    string beforeZones = SnapshotZones(match, Player);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "not an Option", "illegal reason");
    AssertEqual(beforeMemory, match.Context.MemoryController.Current.Current, "memory unchanged");
    AssertEqual(beforeZones, SnapshotZones(match, Player), "zones unchanged");
    AssertEqual(0, match.Context.EffectScheduler.TotalEnqueuedCount, "effect enqueue unchanged");
}

async Task OptionActivateRejectsWrongCostWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await AdvanceToMainAsync(match, Player);
    var action = HeadlessActionFactory.ActivateOption(Player, OptionCardId, OptionEffectId, memoryCost: 4);
    string beforeZones = SnapshotZones(match, Player);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "does not match", "illegal reason");
    AssertEqual(beforeMemory, match.Context.MemoryController.Current.Current, "memory unchanged");
    AssertEqual(beforeZones, SnapshotZones(match, Player), "zones unchanged");
    AssertEqual(0, match.Context.EffectScheduler.TotalEnqueuedCount, "effect enqueue unchanged");
}

async Task LegalQueryAndApplyShareLockedOptionCondition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(optionMetadata: new Dictionary<string, object?> { ["canNotPlayThisOption"] = true });
    await AdvanceToMainAsync(match, Player);
    var action = HeadlessActionFactory.ActivateOption(Player, OptionCardId, OptionEffectId, memoryCost: 3);
    string beforeZones = SnapshotZones(match, Player);

    AssertEqual(0, match.GetLegalActions(Player).Count(a => a.ActionType == HeadlessActionTypes.ActivateOption), "legal activate count");

    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(action, match.Context);
    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "cannot be activated", "illegal reason");
    AssertEqual(beforeZones, SnapshotZones(match, Player), "zones unchanged");
    AssertEqual(0, match.Context.EffectScheduler.TotalEnqueuedCount, "effect enqueue unchanged");
}

async Task LegalQueryAndApplyShareMemoryCondition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(initialMemory: 0, minimumMemory: -2);
    await AdvanceToMainAsync(match, Player);
    var action = HeadlessActionFactory.ActivateOption(Player, OptionCardId, OptionEffectId, memoryCost: 3);
    string beforeZones = SnapshotZones(match, Player);

    AssertEqual(0, match.GetLegalActions(Player).Count(a => a.ActionType == HeadlessActionTypes.ActivateOption), "legal activate count");

    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(action, match.Context);
    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "Cannot pay", "illegal reason");
    AssertEqual(beforeZones, SnapshotZones(match, Player), "zones unchanged");
    AssertEqual(0, match.Context.EffectScheduler.TotalEnqueuedCount, "effect enqueue unchanged");
}

Task OptionActivateFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionTypes.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionFactory.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessLegalActionDispatcher.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MetadataActionProcessor.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "OptionActivateAction.cs")
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
    IReadOnlyDictionary<string, object?>? optionMetadata = null,
    int initialMemory = 0,
    int minimumMemory = -5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 43);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-OPT01"),
        "P1-OPT01",
        "Usable Option",
        optionMetadata ?? new Dictionary<string, object?>(),
        CardType: "Option",
        PlayCost: 3,
        EffectBindingKey: OptionEffectId.Value));
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M02"),
        "P1-M02",
        "Non Option",
        new Dictionary<string, object?>(),
        CardType: "Digimon"));

    for (int index = 3; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P1-M{index:D2}"),
            $"P1-M{index:D2}",
            $"P1 filler {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
    }

    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P2-M{index:D2}"),
            $"P2-M{index:D2}",
            $"P2 filler {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
    }

    DcgoMatch match = new(context);
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            BuildPlayerOneDeck(),
            BuildDeck(new HeadlessPlayerId(2), "P2")
        },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(
        players,
        randomSeed: 43,
        initialMemory: initialMemory,
        minimumMemory: minimumMemory,
        maximumMemory: 10,
        setup: setup));

    return match;
}

static PlayerDeckSetup BuildPlayerOneDeck()
{
    return new PlayerDeckSetup(
        new HeadlessPlayerId(1),
        new[]
        {
            new HeadlessEntityId("P1-OPT01"),
            new HeadlessEntityId("P1-M02")
        }.Concat(Enumerable.Range(3, 10).Select(index => new HeadlessEntityId($"P1-M{index:D2}"))).ToArray(),
        Enumerable.Range(1, 3)
            .Select(index => new HeadlessEntityId($"P1-D{index:D2}"))
            .ToArray());
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
