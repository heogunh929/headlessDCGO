using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2B-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS GameContext accessor references are recorded", AsIsGameContextAccessorReferencesAreRecorded),
    ("Accessor reads AS-IS GameContext player and turn views", AccessorReadsPlayerAndTurnViews),
    ("Accessor writes memory phase active cards and security flag", AccessorWritesStateFields),
    ("Accessor switch turn follows DoSwitchTurnPlayer contract", AccessorSwitchTurnFollowsDoSwitchContract),
    ("Accessor writes MatchState and preserves deterministic zone movement", AccessorWritesMatchState),
    ("Accessor invalid write leaves previous state unchanged", AccessorInvalidWriteLeavesStateUnchanged),
    ("Accessor snapshots are deterministic for identical inputs", AccessorSnapshotsAreDeterministic),
    ("G2B-001 source files contain no placeholder TODOs", GameContextAccessorFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2B-001")
        ?? throw new InvalidOperationException("G2B-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("GameContext", Value(row, "area"), "area");
    AssertEqual("GameContext adapter", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "state read write", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2B-001_gamecontext_state_accessor_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-002", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-002_setup_first_player_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-002 completion marker");
    return Task.CompletedTask;
}

Task AsIsGameContextAccessorReferencesAreRecorded()
{
    string gameContext = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameContext.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));

    AssertContains(gameContext, "public int Memory", "AS-IS memory");
    AssertContains(gameContext, "ActiveCardList", "AS-IS active card list");
    AssertContains(gameContext, "Players_ForTurnPlayer", "AS-IS players for turn player");
    AssertContains(gameContext, "Players_ForNonTurnPlayer", "AS-IS players for non-turn player");
    AssertContains(gameContext, "PlayerFromID", "AS-IS player lookup");
    AssertContains(gameContext, "SwitchTurnPlayer()", "AS-IS switch turn");
    AssertContains(gameContext, "DoSwitchTurnPlayer", "AS-IS switch guard");
    AssertContains(gameContext, "IsSecurityLooking", "AS-IS security looking flag");
    AssertContains(turnStateMachine, "gameContext.TurnPhase", "AS-IS turn phase access");
    return Task.CompletedTask;
}

Task AccessorReadsPlayerAndTurnViews()
{
    GameContextStateAccessor accessor = CreateAccessor();
    GameContextStateSnapshot snapshot = accessor.ReadState();

    AssertEqual(2, snapshot.Players.Count, "players count");
    AssertEqual(new HeadlessPlayerId(1), snapshot.TurnPlayerId, "turn player id");
    AssertEqual(new HeadlessPlayerId(2), snapshot.NonTurnPlayerId, "non-turn player id");
    AssertEqual(new HeadlessPlayerId(1), snapshot.FirstPlayerId, "first player id");
    AssertEqual(HeadlessPhase.Main, snapshot.TurnPhase, "turn phase");
    AssertEqual(3, snapshot.Memory, "memory");
    AssertEqual(new HeadlessPlayerId(1), snapshot.PlayersForTurnPlayer[0].PlayerId, "turn ordered player[0]");
    AssertEqual(new HeadlessPlayerId(2), snapshot.PlayersForTurnPlayer[1].PlayerId, "turn ordered player[1]");
    AssertEqual(new HeadlessPlayerId(2), snapshot.PlayersForNonTurnPlayer[0].PlayerId, "non-turn ordered player[0]");
    AssertEqual(new HeadlessPlayerId(1), snapshot.PlayersForNonTurnPlayer[1].PlayerId, "non-turn ordered player[1]");
    AssertEqual(new HeadlessPlayerId(2), accessor.PlayerFromId(new HeadlessPlayerId(2)).PlayerId, "player from id");
    AssertSequence(new[] { "p1-battle", "p2-battle" }, snapshot.PermanentsForTurnPlayer.Select(id => id.Value).ToArray(), "permanents");
    AssertSequence(new[] { "p1-battle", "p2-battle" }, snapshot.ActiveCardIds.Select(id => id.Value).ToArray(), "active cards");
    return Task.CompletedTask;
}

Task AccessorWritesStateFields()
{
    GameContextStateAccessor accessor = CreateAccessor();

    accessor.WriteState(new GameContextStateWrite(
        Memory: -2,
        TurnPhase: HeadlessPhase.Draw,
        IsSecurityLooking: true,
        ActiveCardIds: new[] { new HeadlessEntityId("p2-battle") }));
    GameContextStateSnapshot snapshot = accessor.ReadState();

    AssertEqual(-2, snapshot.Memory, "memory");
    AssertEqual(HeadlessPhase.Draw, snapshot.TurnPhase, "turn phase");
    AssertTrue(snapshot.IsSecurityLooking, "security looking");
    AssertSequence(new[] { "p2-battle" }, snapshot.ActiveCardIds.Select(id => id.Value).ToArray(), "active cards");
    return Task.CompletedTask;
}

Task AccessorSwitchTurnFollowsDoSwitchContract()
{
    GameContextStateAccessor accessor = CreateAccessor();

    accessor.WriteState(new GameContextStateWrite(DoSwitchTurnPlayer: false));
    accessor.SwitchTurnPlayer();
    AssertEqual(new HeadlessPlayerId(1), accessor.TurnPlayerId, "turn player after blocked switch");
    AssertTrue(accessor.DoSwitchTurnPlayer, "switch guard reset");

    accessor.SwitchTurnPlayer();
    AssertEqual(new HeadlessPlayerId(2), accessor.TurnPlayerId, "turn player after switch");
    AssertEqual(new HeadlessPlayerId(1), accessor.ReadState().NonTurnPlayerId, "non-turn player after switch");
    return Task.CompletedTask;
}

Task AccessorWritesMatchState()
{
    GameContextStateAccessor accessor = CreateAccessor();
    HeadlessPlayerId player = new(1);
    HeadlessEntityId card = new("p1-library");
    MatchState moved = accessor.State.MoveCard(new ZoneMoveRequest(player, card, ChoiceZone.Library, ChoiceZone.Hand));

    accessor.WriteState(new GameContextStateWrite(State: moved));
    GameContextStateSnapshot snapshot = accessor.ReadState();

    AssertEqual(1L, snapshot.State.Version, "state version");
    AssertSequence(Array.Empty<string>(), snapshot.State.GetPlayer(player).GetZone(ChoiceZone.Library).Select(id => id.Value).ToArray(), "library");
    AssertSequence(new[] { "p1-library" }, snapshot.State.GetPlayer(player).GetZone(ChoiceZone.Hand).Select(id => id.Value).ToArray(), "hand");
    AssertEqual(GameEventType.CardMoved, snapshot.State.Events.Single().Type, "event type");
    return Task.CompletedTask;
}

Task AccessorInvalidWriteLeavesStateUnchanged()
{
    GameContextStateAccessor accessor = CreateAccessor();
    long versionBefore = accessor.State.Version;
    int memoryBefore = accessor.Memory;
    HeadlessPlayerId turnBefore = accessor.TurnPlayerId;

    ExpectThrows<InvalidOperationException>(() => accessor.WriteState(new GameContextStateWrite(TurnPlayerId: new HeadlessPlayerId(99))));
    ExpectThrows<InvalidOperationException>(() => accessor.WriteState(new GameContextStateWrite(ActiveCardIds: new[] { new HeadlessEntityId("dup"), new HeadlessEntityId("dup") })));

    AssertEqual(versionBefore, accessor.State.Version, "state version unchanged");
    AssertEqual(memoryBefore, accessor.Memory, "memory unchanged");
    AssertEqual(turnBefore, accessor.TurnPlayerId, "turn player unchanged");
    return Task.CompletedTask;
}

Task AccessorSnapshotsAreDeterministic()
{
    GameContextStateAccessor first = CreateAccessor();
    GameContextStateAccessor second = CreateAccessor();

    AssertEqual(first.ReadState().State.ComputeFingerprint(), second.ReadState().State.ComputeFingerprint(), "fingerprint");
    AssertSequence(
        first.ReadState().ToMetadata().OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={FormatValue(pair.Value)}").ToArray(),
        second.ReadState().ToMetadata().OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={FormatValue(pair.Value)}").ToArray(),
        "metadata");
    return Task.CompletedTask;
}

Task GameContextAccessorFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "GameContextStateAccessor.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static GameContextStateAccessor CreateAccessor()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);
    MatchState state = MatchState.CreateInitial(new[] { playerOne, playerTwo })
        .WithCardInstance(new CardInstanceState(new HeadlessEntityId("p1-battle"), new HeadlessEntityId("def-p1-battle"), playerOne))
        .PlaceCard(new HeadlessEntityId("p1-battle"), ChoiceZone.BattleArea)
        .WithCardInstance(new CardInstanceState(new HeadlessEntityId("p2-battle"), new HeadlessEntityId("def-p2-battle"), playerTwo))
        .PlaceCard(new HeadlessEntityId("p2-battle"), ChoiceZone.BattleArea)
        .WithCardInstance(new CardInstanceState(new HeadlessEntityId("p1-library"), new HeadlessEntityId("def-p1-library"), playerOne))
        .PlaceCard(new HeadlessEntityId("p1-library"), ChoiceZone.Library);

    return new GameContextStateAccessor(
        state,
        playerOne,
        HeadlessPhase.Main,
        memory: 3,
        firstPlayerId: playerOne,
        activeCardIds: new[] { new HeadlessEntityId("p1-battle"), new HeadlessEntityId("p2-battle") });
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

static string FormatValue(object? value)
{
    return value switch
    {
        null => "<null>",
        string[] strings => string.Join("|", strings),
        IEnumerable<string> strings => string.Join("|", strings),
        _ => value.ToString() ?? string.Empty
    };
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}
