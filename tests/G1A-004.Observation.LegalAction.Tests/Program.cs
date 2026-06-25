using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1A-004 goal row keeps the observation legal action contract", GoalRowKeepsExpectedContract),
    ("Empty observation and action mask expose stable empty contracts", EmptyObservationAndActionMaskExposeStableContracts),
    ("Observation snapshots preserve immutable player and zone snapshots", ObservationSnapshotsPreserveImmutableSnapshots),
    ("Observation models reject invalid empty state contract values", ObservationModelsRejectInvalidContractValues),
    ("ActionMask preserves immutable legal action snapshots and lookup contracts", ActionMaskPreservesImmutableLookupContracts),
    ("DcgoMatch returns empty observation and legal action contracts", DcgoMatchReturnsObservationAndLegalActionContracts),
    ("Observation legal action source files no longer contain placeholder TODO contracts", ObservationLegalActionFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-004")
        ?? throw new InvalidOperationException("G1A-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Runtime", Value(row, "area"), "area");
    AssertTrue(Value(row, "goal").StartsWith("Observation LegalAction", StringComparison.Ordinal), "goal");
    AssertEqual("ObservationSnapshot LegalAction ActionMask contract", Value(row, "deliverables"), "deliverables");
    AssertEqual("docs/test-results/goals/G1A-004_observation_legal_action_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    return Task.CompletedTask;
}

Task EmptyObservationAndActionMaskExposeStableContracts()
{
    ObservationSnapshot observation = ObservationSnapshot.Empty;
    AssertEqual(0L, observation.StepIndex, "empty step index");
    AssertFalse(observation.IsTerminal, "empty terminal flag");
    AssertEqual(0, observation.PendingActionCount, "empty pending action count");
    AssertFalse(observation.HasPendingEffects, "empty pending effects");
    AssertEqual(0, observation.CardInstanceCount, "empty card instance count");
    AssertEqual(0, observation.PlayerCount, "empty player count");
    AssertEqual(0, observation.Players.Count, "empty players");
    AssertSame(HeadlessTurnState.Empty, observation.Turn, "empty turn");
    AssertSame(HeadlessChoiceState.Empty, observation.Choice, "empty choice");
    AssertSame(HeadlessAttackState.Empty, observation.Attack, "empty attack");
    AssertSame(HeadlessEffectState.Empty, observation.Effects, "empty effects");
    AssertSame(HeadlessMemoryState.Default, observation.Memory, "empty memory");

    ActionMask mask = ActionMask.Empty;
    AssertFalse(mask.HasAnyLegalAction, "empty mask has no actions");
    AssertEqual(0, mask.Count, "empty mask count");
    AssertEqual(0, mask.LegalActions.Count, "empty mask action list");
    AssertFalse(mask.ContainsActionId(new HeadlessEntityId("missing")), "empty mask contains id");
    AssertNull(mask.FindById(new HeadlessEntityId("missing")), "empty mask find");
    return Task.CompletedTask;
}

Task ObservationSnapshotsPreserveImmutableSnapshots()
{
    var cardIds = new List<HeadlessEntityId>
    {
        new("card-1")
    };
    var zones = new List<ZoneObservation>
    {
        new(ChoiceZone.Hand, 1, cardIds)
    };
    var players = new List<PlayerObservation>
    {
        new(new HeadlessPlayerId(1), zones)
    };

    var snapshot = CreateSnapshot(players);

    cardIds.Add(new HeadlessEntityId("card-2"));
    zones.Add(new ZoneObservation(ChoiceZone.Trash, 1, new[] { new HeadlessEntityId("trash-1") }));
    players.Add(new PlayerObservation(new HeadlessPlayerId(2), Array.Empty<ZoneObservation>()));

    AssertEqual(1, snapshot.PlayerCount, "snapshot player count");
    PlayerObservation player = snapshot.Players.Single();
    AssertEqual(1, player.Zones.Count, "player zone count");
    AssertEqual(1, player.TotalCardCount, "player total card count");
    ZoneObservation? hand = player.FindZone(ChoiceZone.Hand);
    AssertNotNull(hand, "hand zone");
    AssertEqual(1, hand!.CardIds.Count, "hand card ids");
    AssertEqual(new HeadlessEntityId("card-1"), hand.CardIds[0], "hand card id snapshot");
    return Task.CompletedTask;
}

Task ObservationModelsRejectInvalidContractValues()
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
    {
        var _ = CreateSnapshot(Array.Empty<PlayerObservation>()) with { StepIndex = -1 };
    });
    ExpectThrows<ArgumentOutOfRangeException>(() =>
    {
        var _ = CreateSnapshot(Array.Empty<PlayerObservation>()) with { PendingActionCount = -1 };
    });
    ExpectThrows<ArgumentOutOfRangeException>(() =>
    {
        var _ = CreateSnapshot(Array.Empty<PlayerObservation>()) with { CardInstanceCount = -1 };
    });
    ExpectThrows<ArgumentNullException>(() => CreateSnapshot(null!));
    ExpectThrows<ArgumentNullException>(() => new PlayerObservation(new HeadlessPlayerId(1), null!));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ZoneObservation(ChoiceZone.Hand, -1, Array.Empty<HeadlessEntityId>()));
    ExpectThrows<ArgumentException>(() => new ZoneObservation(
        ChoiceZone.Hand,
        0,
        new[] { new HeadlessEntityId("visible-card") }));
    return Task.CompletedTask;
}

Task ActionMaskPreservesImmutableLookupContracts()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var actionOne = HeadlessActionFactory.NoOp(playerOne, "action-1");
    var actionTwo = HeadlessActionFactory.NoOp(playerTwo, "action-2");
    var legalActions = new List<LegalAction>
    {
        actionOne
    };

    var mask = new ActionMask(legalActions);
    legalActions.Add(actionTwo);

    AssertTrue(mask.HasAnyLegalAction, "mask has legal action");
    AssertEqual(1, mask.Count, "mask count snapshot");
    AssertTrue(mask.ContainsAction(actionOne), "contains action");
    AssertTrue(mask.ContainsActionId(actionOne.Id), "contains action id");
    LegalAction? foundAction = mask.FindById(actionOne.Id);
    AssertNotNull(foundAction, "find action");
    AssertSame(actionOne, foundAction!, "find action instance");
    AssertFalse(mask.ContainsActionId(actionTwo.Id), "does not contain later action");
    AssertNull(mask.FindById(actionTwo.Id), "find missing later action");
    ExpectThrows<ArgumentNullException>(() => new ActionMask(null!));
    ExpectThrows<ArgumentNullException>(() => mask.ContainsAction(null!));
    return Task.CompletedTask;
}

async Task DcgoMatchReturnsObservationAndLegalActionContracts()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var match = new DcgoMatch();

    await match.InitializeAsync(MatchConfig.Create(new[] { playerOne, playerTwo }));

    ObservationSnapshot observation = match.GetObservation();
    AssertEqual(0L, observation.StepIndex, "match initial step index");
    AssertFalse(observation.IsTerminal, "match initial terminal");
    AssertEqual(2, observation.PlayerCount, "match player count");
    AssertEqual(12, observation.Players[0].Zones.Count, "player one observable zones");
    AssertEqual(0, observation.Players[0].TotalCardCount, "player one empty cards");
    AssertEqual(0, observation.Players[1].TotalCardCount, "player two empty cards");

    ActionMask emptyMask = match.GetActionMask();
    AssertFalse(emptyMask.HasAnyLegalAction, "initial mask has no action");

    var action = HeadlessActionFactory.NoOp(playerOne, "legal-action-1");
    var controller = (IHeadlessLegalActionController)match.Context.RuleQueryService;
    controller.AddLegalActions(new[] { action });

    IReadOnlyList<LegalAction> playerActions = match.GetLegalActions(playerOne);
    AssertEqual(1, playerActions.Count, "player legal action count");
    AssertSame(action, playerActions[0], "player legal action");

    ActionMask mask = match.GetActionMask();
    AssertTrue(mask.HasAnyLegalAction, "mask has seeded action");
    AssertEqual(1, mask.Count, "mask action count");
    AssertTrue(mask.ContainsAction(action), "mask contains seeded action");
    LegalAction? foundSeededAction = mask.FindById(action.Id);
    AssertNotNull(foundSeededAction, "mask finds seeded action");
    AssertSame(action, foundSeededAction!, "mask finds seeded action instance");

    controller.ClearLegalActions();
    AssertEqual(1, mask.Count, "mask snapshot survives legal action clear");
}

Task ObservationLegalActionFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "ObservationSnapshot.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "ActionMask.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "LegalAction.cs"),
    };

    foreach (var relativeFile in relativeFiles)
    {
        var path = Path.Combine(root, relativeFile);
        var text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{relativeFile} still contains a TODO placeholder.");
        }
    }

    return Task.CompletedTask;
}

static ObservationSnapshot CreateSnapshot(IReadOnlyList<PlayerObservation> players)
{
    return new ObservationSnapshot(
        StepIndex: 0,
        IsTerminal: false,
        PendingActionCount: 0,
        HasPendingEffects: false,
        CardInstanceCount: 0,
        RandomSeed: null,
        LastActionType: null,
        LastActionSucceeded: null,
        LastActionMessage: null,
        Turn: HeadlessTurnState.Empty,
        Choice: HeadlessChoiceState.Empty,
        Attack: HeadlessAttackState.Empty,
        Effects: HeadlessEffectState.Empty,
        Memory: HeadlessMemoryState.Default,
        Players: players);
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

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
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

static void AssertSame<T>(T expected, T actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected the same instance.");
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

static void AssertNull(object? value, string label)
{
    if (value is not null)
    {
        throw new InvalidOperationException($"{label}: expected null.");
    }
}

static void AssertNotNull(object? value, string label)
{
    if (value is null)
    {
        throw new InvalidOperationException($"{label}: expected a value.");
    }
}
