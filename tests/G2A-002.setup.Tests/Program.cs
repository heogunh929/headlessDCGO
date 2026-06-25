using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2A-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS setup source references are recorded", AsIsSetupSourceReferencesAreRecorded),
    ("DeckList setup expands repeated definitions into unique instances", DeckListSetupExpandsRepeatedDefinitions),
    ("Initialize applies specified first player setup to turn and zones", InitializeAppliesSpecifiedFirstPlayerSetup),
    ("Initialize records setup metadata on first step event", InitializeRecordsSetupMetadata),
    ("Reset reapplies setup deterministically", ResetReappliesSetupDeterministically),
    ("Unspecified first player is deterministic for the same seed", RandomFirstPlayerIsDeterministicForSameSeed),
    ("Invalid setup inputs fail before mutation", InvalidSetupInputsFailBeforeMutation),
    ("Setup source files contain no placeholder TODOs", SetupFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-002")
        ?? throw new InvalidOperationException("G2A-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("setup flow", Value(row, "deliverables"), "deliverables");
    AssertEqual("first player setup 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2A-002_setup_first_player_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-001_phase_mapping_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsSetupSourceReferencesAreRecorded()
{
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string gameContext = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameContext.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));
    string cardObjectController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"));

    AssertContains(turnStateMachine, "StartGame", "AS-IS StartGame");
    AssertContains(turnStateMachine, "gameContext.FirstPlayer = gameContext.NonTurnPlayer", "AS-IS first player assignment");
    AssertContains(turnStateMachine, "new DrawClass(player, 5, null).Draw()", "AS-IS opening hand draw");
    AssertContains(turnStateMachine, "new IAddSecurityFromLibrary(player, 5).AddSecurity()", "AS-IS opening security setup");
    AssertContains(gameContext, "SwitchTurnPlayer", "AS-IS SwitchTurnPlayer");
    AssertContains(cardController, "public class DrawClass", "AS-IS DrawClass");
    AssertContains(cardController, "public class IAddSecurityFromLibrary", "AS-IS IAddSecurityFromLibrary");
    AssertContains(cardObjectController, "RandomUtility.ShuffledDeckCards", "AS-IS deck shuffle");
    return Task.CompletedTask;
}

Task DeckListSetupExpandsRepeatedDefinitions()
{
    var deckList = new DeckList(
        "fixture",
        new[]
        {
            new DeckListEntry(new HeadlessEntityId("BT1-001"), 2),
            new DeckListEntry(new HeadlessEntityId("BT1-002"), 1)
        },
        new[]
        {
            new DeckListEntry(new HeadlessEntityId("BT1-003"), 2)
        });

    PlayerDeckSetup setup = PlayerDeckSetup.FromDeckList(new HeadlessPlayerId(1), deckList);
    AssertSequence(
        new[] { "BT1-001", "BT1-001", "BT1-002" },
        setup.MainDeckDefinitionIds.Select(id => id.Value).ToArray(),
        "expanded main deck");
    AssertSequence(
        new[] { "BT1-003", "BT1-003" },
        setup.DigitamaDeckDefinitionIds.Select(id => id.Value).ToArray(),
        "expanded digitama deck");
    return Task.CompletedTask;
}

async Task InitializeAppliesSpecifiedFirstPlayerSetup()
{
    HeadlessPlayerId firstPlayer = new(2);
    DcgoMatch match = new();
    await match.InitializeAsync(CreateConfig(firstPlayer));

    ObservationSnapshot observation = match.GetObservation();
    AssertEqual(HeadlessPhase.Setup, observation.Turn.Phase, "setup phase after initialize");
    AssertEqual(firstPlayer, observation.Turn.TurnPlayerId, "turn player after setup");
    AssertEqual(new HeadlessPlayerId(1), observation.Turn.NonTurnPlayerId, "non-turn player after setup");
    AssertTrue(observation.Turn.IsFirstTurn, "first turn marker");

    AssertPlayerZones(observation, new HeadlessPlayerId(2), "P2");
    AssertPlayerZones(observation, new HeadlessPlayerId(1), "P1");
    AssertEqual(30, match.Context.CardInstanceRepository.Snapshot().Count, "card instance count");

    CardInstanceRecord firstHand = match.Context.CardInstanceRepository.Snapshot()
        .Single(instance => instance.InstanceId.Value == "p2:main:001:P2-M01");
    AssertEqual(new HeadlessEntityId("P2-M01"), firstHand.DefinitionId, "first hand definition id");
    AssertEqual(new HeadlessPlayerId(2), firstHand.OwnerId, "first hand owner id");
}

async Task InitializeRecordsSetupMetadata()
{
    DcgoMatch match = new();
    await match.InitializeAsync(CreateConfig(new HeadlessPlayerId(2)));

    StepResult firstStep = await match.StepAsync();
    GameEvent initialized = firstStep.Events.Single(e => e.Message == "Match initialized.");
    AssertEqual(true, initialized.Metadata["setupApplied"], "setup applied metadata");
    AssertEqual(2, initialized.Metadata["firstPlayerId"], "first player metadata");
    AssertEqual(2, firstStep.Observation.PlayerCount, "player observation count");
}

async Task ResetReappliesSetupDeterministically()
{
    HeadlessPlayerId firstPlayer = new(2);
    DcgoMatch match = new();
    await match.InitializeAsync(CreateConfig(firstPlayer));

    await match.Context.ZoneMover.DrawAsync(firstPlayer, 1);
    AssertEqual(6, GetZone(match, firstPlayer, ChoiceZone.Hand).Count, "mutated hand count before reset");

    await match.ResetAsync();
    AssertEqual(HeadlessPhase.Setup, match.GetObservation().Turn.Phase, "phase after reset");
    AssertEqual(firstPlayer, match.GetObservation().Turn.TurnPlayerId, "first player after reset");
    AssertEqual(5, GetZone(match, firstPlayer, ChoiceZone.Hand).Count, "hand after reset");
    AssertEqual(5, GetZone(match, firstPlayer, ChoiceZone.Security).Count, "security after reset");
    AssertEqual(2, GetZone(match, firstPlayer, ChoiceZone.Library).Count, "library after reset");
}

async Task RandomFirstPlayerIsDeterministicForSameSeed()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(new[]
    {
        BuildDeck(new HeadlessPlayerId(1), "P1"),
        BuildDeck(new HeadlessPlayerId(2), "P2")
    });

    DcgoMatch first = new();
    DcgoMatch second = new();
    await first.InitializeAsync(MatchConfig.Create(players, randomSeed: 77, setup: setup));
    await second.InitializeAsync(MatchConfig.Create(players, randomSeed: 77, setup: setup));

    AssertEqual(first.GetObservation().Turn.TurnPlayerId, second.GetObservation().Turn.TurnPlayerId, "same seed first player");
    AssertEqual(HeadlessPhase.Setup, first.GetObservation().Turn.Phase, "random setup phase");
}

Task InvalidSetupInputsFailBeforeMutation()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig missingDeck = MatchSetupConfig.Create(new[] { BuildDeck(new HeadlessPlayerId(1), "P1") });
    ExpectThrows<InvalidOperationException>(() => MatchConfig.Create(players, setup: missingDeck));

    MatchSetupConfig shortDeck = MatchSetupConfig.Create(new[]
    {
        BuildDeck(new HeadlessPlayerId(1), "P1", mainCount: 9),
        BuildDeck(new HeadlessPlayerId(2), "P2")
    });
    ExpectThrows<InvalidOperationException>(() => MatchConfig.Create(players, setup: shortDeck));

    MatchSetupConfig invalidFirst = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(3));
    ExpectThrows<InvalidOperationException>(() => MatchConfig.Create(players, setup: invalidFirst));
    return Task.CompletedTask;
}

Task SetupFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MatchSetupFlow.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "MatchConfig.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "DcgoMatch.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static MatchConfig CreateConfig(HeadlessPlayerId firstPlayer)
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: firstPlayer);

    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
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

static void AssertPlayerZones(
    ObservationSnapshot observation,
    HeadlessPlayerId playerId,
    string prefix)
{
    PlayerObservation player = observation.Players.Single(p => p.PlayerId == playerId);
    AssertZone(
        player,
        ChoiceZone.Hand,
        Enumerable.Range(1, 5).Select(index => $"p{playerId.Value}:main:{index:D3}:{prefix}-M{index:D2}").ToArray());
    AssertZone(
        player,
        ChoiceZone.Security,
        Enumerable.Range(6, 5).Select(index => $"p{playerId.Value}:main:{index:D3}:{prefix}-M{index:D2}").ToArray());
    AssertZone(
        player,
        ChoiceZone.Library,
        Enumerable.Range(11, 2).Select(index => $"p{playerId.Value}:main:{index:D3}:{prefix}-M{index:D2}").ToArray());
    AssertZone(
        player,
        ChoiceZone.DigitamaLibrary,
        Enumerable.Range(1, 3).Select(index => $"p{playerId.Value}:digitama:{index:D3}:{prefix}-D{index:D2}").ToArray());
}

static void AssertZone(
    PlayerObservation player,
    ChoiceZone zone,
    IReadOnlyList<string> expectedIds)
{
    ZoneObservation zoneObservation = player.FindZone(zone)
        ?? throw new InvalidOperationException($"Missing zone '{zone}' for player '{player.PlayerId}'.");
    AssertEqual(expectedIds.Count, zoneObservation.Count, $"{player.PlayerId} {zone} count");
    AssertSequence(expectedIds, zoneObservation.CardIds.Select(id => id.Value).ToArray(), $"{player.PlayerId} {zone} ids");
}

static IReadOnlyList<HeadlessEntityId> GetZone(
    DcgoMatch match,
    HeadlessPlayerId playerId,
    ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader reader
        ? reader.GetCards(playerId, zone)
        : throw new InvalidOperationException("Zone mover does not expose zone state.");
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
