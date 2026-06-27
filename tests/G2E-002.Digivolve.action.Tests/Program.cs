using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessEntityId EvolveCardId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetCardId = new("p1:main:002:P1-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2E-002 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS Digivolve action references are recorded", AsIsDigivolveReferencesAreRecorded),
    ("Main phase legal actions expose valid digivolve pairs only", MainPhaseLegalActionsExposeValidDigivolvePairsOnly),
    ("Legal Digivolve action pays memory moves card and attaches source", LegalDigivolvePaysMemoryMovesCardAndAttachesSource),
    ("Digivolve processor rejects wrong evolution cost without mutation", DigivolveRejectsWrongCostWithoutMutation),
    ("Digivolve processor rejects invalid evolution condition without mutation", DigivolveRejectsInvalidConditionWithoutMutation),
    ("Digivolve processor rejects non-hand card without mutation", DigivolveRejectsNonHandCardWithoutMutation),
    ("Digivolve legal query and apply share the same memory condition", LegalQueryAndApplyShareMemoryCondition),
    ("G2E-002 source files contain no placeholder markers", DigivolveActionFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2E-002")
        ?? throw new InvalidOperationException("G2E-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("MainPhaseAction", Value(row, "area"), "area");
    AssertEqual("DigivolveAction", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "digivolve legal apply", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2E-002_digivolve_action_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2E-001; G2D-004", Value(row, "blocked_until"), "blocked_until");

    string g2e001 = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2E-001_play_card_action_unit_test_results.md"));
    string g2d004 = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2D-004_digivolution_source_attach_unit_test_results.md"));
    AssertContains(g2e001, "COMPLETE", "G2E-001 completion marker");
    AssertContains(g2d004, "COMPLETE", "G2D-004 completion marker");
    return Task.CompletedTask;
}

Task AsIsDigivolveReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));
    string permanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Permanent.cs"));

    AssertContains(turnStateMachine, "CanEvolve(targetPermanent, true)", "AS-IS digivolve legal condition");
    AssertContains(turnStateMachine, "void Digivolution()", "AS-IS digivolve action branch");
    AssertContains(turnStateMachine, "new PlayCardAction(handCard.cardSource.CardIndex", "AS-IS queued packet");
    AssertContains(cardController, "isEvolution = true", "AS-IS evolution apply marker");
    AssertContains(cardController, "DigivolveFieldPermanentCardEffect", "AS-IS digivolve visual apply");
    AssertContains(permanent, "AddDigivolutionCardsTop", "AS-IS source attachment");
    return Task.CompletedTask;
}

async Task MainPhaseLegalActionsExposeValidDigivolvePairsOnly()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    LegalAction[] digivolves = match.GetLegalActions(player)
        .Where(action => action.ActionType == HeadlessActionTypes.Digivolve)
        .ToArray();

    AssertEqual(1, digivolves.Length, "digivolve action count");
    AssertEqual(EvolveCardId, ReadEntityId(digivolves[0].Parameters, HeadlessActionParameterKeys.CardId), "evolve card id");
    AssertEqual(TargetCardId, ReadEntityId(digivolves[0].Parameters, HeadlessActionParameterKeys.TargetCardId), "target card id");
    AssertEqual(2, ReadInt(digivolves[0].Parameters, HeadlessActionParameterKeys.MemoryCost), "evolution cost");
}

async Task LegalDigivolvePaysMemoryMovesCardAndAttachesSource()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    LegalAction digivolve = SingleLegalAction(match, player, HeadlessActionTypes.Digivolve);
    int beforeMemory = match.Context.MemoryController.Current.Current;
    int beforeMoveEvents = match.Context.ZoneMover.Events.Count(e => e.Type == GameEventType.CardMoved);

    await match.ApplyActionAsync(digivolve);
    StepResult step = await match.StepAsync();

    AssertEqual(beforeMemory - 2, match.Context.MemoryController.Current.Current, "memory after digivolve");
    AssertFalse(ZoneReader(match).GetCards(player, ChoiceZone.Hand).Contains(EvolveCardId), "evolve card removed from hand");
    AssertFalse(ZoneReader(match).GetCards(player, ChoiceZone.BattleArea).Contains(TargetCardId), "target removed from battle");
    AssertTrue(ZoneReader(match).GetCards(player, ChoiceZone.BattleArea).Contains(EvolveCardId), "evolve card moved to battle");
    AssertSequence(new[] { TargetCardId.Value }, ReadSourceIds(match, EvolveCardId).Select(id => id.Value).ToArray(), "source ids");

    GameEvent processed = step.Events.Last(e => e.Type == GameEventType.ActionProcessed);
    AssertMetadata(processed, "success", true);
    AssertMetadata(processed, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.Digivolve);
    AssertMetadata(processed, HeadlessActionParameterKeys.CardId, EvolveCardId.Value);
    AssertMetadata(processed, HeadlessActionParameterKeys.TargetCardId, TargetCardId.Value);
    AssertMetadata(processed, HeadlessActionParameterKeys.PreviousMemory, beforeMemory);
    AssertMetadata(processed, HeadlessActionParameterKeys.Memory, beforeMemory - 2);
    int afterMoveEvents = match.Context.ZoneMover.Events.Count(e => e.Type == GameEventType.CardMoved);
    AssertEqual(beforeMoveEvents + 2, afterMoveEvents, "digivolve movement events");
}

async Task DigivolveRejectsWrongCostWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    var action = HeadlessActionFactory.Digivolve(player, EvolveCardId, TargetCardId, memoryCost: 3);
    string beforeZones = SnapshotZones(match, player);
    string beforeSources = SnapshotSources(match, EvolveCardId);
    int beforeMemory = match.Context.MemoryController.Current.Current;

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "does not match", "illegal reason");
    AssertEqual(beforeMemory, match.Context.MemoryController.Current.Current, "memory unchanged");
    AssertEqual(beforeZones, SnapshotZones(match, player), "zones unchanged");
    AssertEqual(beforeSources, SnapshotSources(match, EvolveCardId), "sources unchanged");
}

async Task DigivolveRejectsInvalidConditionWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(condition: "definition:OTHER");
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    var action = HeadlessActionFactory.Digivolve(player, EvolveCardId, TargetCardId, memoryCost: 2);
    string beforeZones = SnapshotZones(match, player);

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "does not satisfy", "illegal reason");
    AssertEqual(beforeZones, SnapshotZones(match, player), "zones unchanged");
    AssertEqual(string.Empty, SnapshotSources(match, EvolveCardId), "sources unchanged");
}

async Task DigivolveRejectsNonHandCardWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, EvolveCardId, ChoiceZone.Hand, ChoiceZone.Trash));
    var action = HeadlessActionFactory.Digivolve(player, EvolveCardId, TargetCardId, memoryCost: 2);
    string beforeZones = SnapshotZones(match, player);

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "hand", "illegal reason");
    AssertEqual(beforeZones, SnapshotZones(match, player), "zones unchanged");
    AssertEqual(string.Empty, SnapshotSources(match, EvolveCardId), "sources unchanged");
}

async Task LegalQueryAndApplyShareMemoryCondition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(initialMemory: 0, minimumMemory: -1);
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);
    var action = HeadlessActionFactory.Digivolve(player, EvolveCardId, TargetCardId, memoryCost: 2);

    AssertEqual(0, match.GetLegalActions(player).Count(a => a.ActionType == HeadlessActionTypes.Digivolve), "legal digivolve count");

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, match.Context);
    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "Cannot pay", "illegal reason");
}

Task DigivolveActionFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "DigivolveAction.cs")
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
    string condition = "definition:P1-M02",
    int initialMemory = 0,
    int minimumMemory = -5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M01"),
        "P1-M01",
        "Evolving Digimon",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        EvolutionCost: 2,
        EvolutionCondition: condition));
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M02"),
        "P1-M02",
        "Base Digimon",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 3));

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
            BuildDeck(new HeadlessPlayerId(1), "P1"),
            BuildDeck(new HeadlessPlayerId(2), "P2")
        },
        firstPlayerId: new HeadlessPlayerId(1), shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(
        players,
        randomSeed: 41,
        initialMemory: initialMemory,
        minimumMemory: minimumMemory,
        maximumMemory: 10,
        setup: setup));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(new HeadlessPlayerId(1), TargetCardId, ChoiceZone.Hand, ChoiceZone.BattleArea));
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

static IReadOnlyList<HeadlessEntityId> ReadSourceIds(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
        record is null ||
        !record.Metadata.TryGetValue("sourceIds", out object? rawValue) ||
        rawValue is null)
    {
        return Array.Empty<HeadlessEntityId>();
    }

    return rawValue switch
    {
        IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
        IEnumerable<string> ids => ids.Select(id => new HeadlessEntityId(id)).ToArray(),
        string value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => new HeadlessEntityId(id))
            .ToArray(),
        _ => Array.Empty<HeadlessEntityId>()
    };
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

static string SnapshotSources(DcgoMatch match, HeadlessEntityId cardId)
{
    return string.Join(",", ReadSourceIds(match, cardId).Select(id => id.Value));
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
