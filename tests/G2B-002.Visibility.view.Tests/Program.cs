using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2B-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS GameContext visibility references are recorded", AsIsGameContextVisibilityReferencesAreRecorded),
    ("Player visibility view hides opponent hidden zones and keeps counts", PlayerViewHidesOpponentHiddenZones),
    ("Player visibility view reveals own hidden zones and public opponent zones", PlayerViewRevealsOwnHiddenAndPublicOpponentZones),
    ("Debug full visibility view reveals all hidden card identities", DebugFullViewRevealsAllHiddenCardIdentities),
    ("Visibility view filters active cards by viewer information", PlayerViewFiltersActiveCards),
    ("Visibility view rejects invalid viewers without mutating state", InvalidViewerLeavesStateUnchanged),
    ("Visibility views are deterministic for identical inputs", VisibilityViewsAreDeterministic),
    ("G2B-002 source files contain no placeholder TODOs", VisibilityViewFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2B-002")
        ?? throw new InvalidOperationException("G2B-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("GameContext", Value(row, "area"), "area");
    AssertEqual("visibility view", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "hidden information view", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2B-002_visibility_view_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2B-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2B-001_gamecontext_state_accessor_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2B-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsGameContextVisibilityReferencesAreRecorded()
{
    string gameContext = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameContext.cs"));

    AssertContains(gameContext, "public Player You", "AS-IS local player");
    AssertContains(gameContext, "public Player Opponent", "AS-IS opponent player");
    AssertContains(gameContext, "public List<CardSource> ActiveCardList", "AS-IS active card list");
    AssertContains(gameContext, "Players_ForTurnPlayer", "AS-IS turn-player ordered view");
    AssertContains(gameContext, "Players_ForNonTurnPlayer", "AS-IS non-turn-player ordered view");
    AssertContains(gameContext, "PlayerFromID", "AS-IS player lookup");
    return Task.CompletedTask;
}

Task PlayerViewHidesOpponentHiddenZones()
{
    GameContextStateAccessor accessor = CreateAccessor();
    VisibilityViewSnapshot view = VisibilityView.ForPlayer(accessor, new HeadlessPlayerId(1));
    PlayerStateView opponent = view.Player(new HeadlessPlayerId(2));

    AssertTrue(view.IsPlayerView, "player view marker");
    AssertEqual(new HeadlessPlayerId(1), view.ViewerId, "viewer id");

    AssertHiddenZone(opponent, ChoiceZone.Hand, 1, "opponent hand");
    AssertHiddenZone(opponent, ChoiceZone.Library, 1, "opponent library");
    AssertHiddenZone(opponent, ChoiceZone.Security, 1, "opponent security");
    AssertHiddenZone(opponent, ChoiceZone.DigitamaLibrary, 1, "opponent digitama");
    return Task.CompletedTask;
}

Task PlayerViewRevealsOwnHiddenAndPublicOpponentZones()
{
    GameContextStateAccessor accessor = CreateAccessor();
    VisibilityViewSnapshot view = VisibilityView.ForPlayer(accessor, new HeadlessPlayerId(1));

    PlayerStateView owner = view.Player(new HeadlessPlayerId(1));
    PlayerStateView opponent = view.Player(new HeadlessPlayerId(2));

    AssertVisibleZone(owner, ChoiceZone.Hand, new[] { "p1-hand" }, "owner hand");
    AssertVisibleZone(owner, ChoiceZone.Library, new[] { "p1-library" }, "owner library");
    AssertVisibleZone(owner, ChoiceZone.Security, new[] { "p1-security" }, "owner security");
    AssertVisibleZone(owner, ChoiceZone.DigitamaLibrary, new[] { "p1-digitama" }, "owner digitama");
    AssertVisibleZone(opponent, ChoiceZone.BattleArea, new[] { "p2-battle" }, "opponent battle");
    AssertVisibleZone(opponent, ChoiceZone.Trash, new[] { "p2-trash" }, "opponent trash");
    return Task.CompletedTask;
}

Task DebugFullViewRevealsAllHiddenCardIdentities()
{
    GameContextStateAccessor accessor = CreateAccessor();
    VisibilityViewSnapshot view = VisibilityView.ForDebugFull(accessor);
    PlayerStateView opponent = view.Player(new HeadlessPlayerId(2));

    AssertTrue(view.IsDebugFullView, "debug marker");
    AssertEqual(null, view.ViewerId, "debug viewer id");
    AssertVisibleZone(opponent, ChoiceZone.Hand, new[] { "p2-hand" }, "debug opponent hand");
    AssertVisibleZone(opponent, ChoiceZone.Library, new[] { "p2-library" }, "debug opponent library");
    AssertVisibleZone(opponent, ChoiceZone.Security, new[] { "p2-security" }, "debug opponent security");
    AssertVisibleZone(opponent, ChoiceZone.DigitamaLibrary, new[] { "p2-digitama" }, "debug opponent digitama");
    return Task.CompletedTask;
}

Task PlayerViewFiltersActiveCards()
{
    GameContextStateAccessor accessor = CreateAccessor();

    VisibilityViewSnapshot p1View = VisibilityView.ForPlayer(accessor, new HeadlessPlayerId(1));
    VisibilityViewSnapshot p2View = VisibilityView.ForPlayer(accessor, new HeadlessPlayerId(2));
    VisibilityViewSnapshot debug = VisibilityView.ForDebugFull(accessor);

    AssertSequence(new[] { "p1-hand", "p1-battle", "p2-battle" }, p1View.ActiveCardIds.Select(id => id.Value).ToArray(), "p1 visible active cards");
    AssertSequence(new[] { "p1-battle", "p2-hand", "p2-battle" }, p2View.ActiveCardIds.Select(id => id.Value).ToArray(), "p2 visible active cards");
    AssertSequence(new[] { "p1-hand", "p1-battle", "p2-hand", "p2-battle" }, debug.ActiveCardIds.Select(id => id.Value).ToArray(), "debug active cards");
    return Task.CompletedTask;
}

Task InvalidViewerLeavesStateUnchanged()
{
    GameContextStateAccessor accessor = CreateAccessor();
    GameContextStateSnapshot before = accessor.ReadState();
    string fingerprintBefore = before.State.ComputeFingerprint();
    IReadOnlyList<HeadlessEntityId> activeBefore = before.ActiveCardIds;

    ExpectThrows<ArgumentException>(() => VisibilityView.ForPlayer(accessor, default));
    ExpectThrows<InvalidOperationException>(() => VisibilityView.ForPlayer(accessor, new HeadlessPlayerId(99)));
    ExpectThrows<ArgumentException>(() => VisibilityView.IsCardVisibleToPlayer(before, default, new HeadlessPlayerId(1)));

    GameContextStateSnapshot after = accessor.ReadState();
    AssertEqual(fingerprintBefore, after.State.ComputeFingerprint(), "state fingerprint unchanged");
    AssertSequence(activeBefore.Select(id => id.Value).ToArray(), after.ActiveCardIds.Select(id => id.Value).ToArray(), "active cards unchanged");
    return Task.CompletedTask;
}

Task VisibilityViewsAreDeterministic()
{
    GameContextStateAccessor first = CreateAccessor();
    GameContextStateAccessor second = CreateAccessor();

    VisibilityViewSnapshot firstPlayerView = VisibilityView.ForPlayer(first, new HeadlessPlayerId(1));
    VisibilityViewSnapshot secondPlayerView = VisibilityView.ForPlayer(second, new HeadlessPlayerId(1));
    VisibilityViewSnapshot firstDebug = VisibilityView.ForDebugFull(first);
    VisibilityViewSnapshot secondDebug = VisibilityView.ForDebugFull(second);

    AssertSequence(Flatten(firstPlayerView), Flatten(secondPlayerView), "player view");
    AssertSequence(Flatten(firstDebug), Flatten(secondDebug), "debug view");
    return Task.CompletedTask;
}

Task VisibilityViewFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "VisibilityView.cs")
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
    MatchState state = MatchState.CreateInitial(new[] { playerOne, playerTwo });

    foreach (var card in CardFixture())
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.InstanceId), new HeadlessEntityId($"def-{card.InstanceId}"), card.OwnerId))
            .PlaceCard(new HeadlessEntityId(card.InstanceId), card.Zone);
    }

    return new GameContextStateAccessor(
        state,
        playerOne,
        HeadlessPhase.Main,
        memory: 3,
        firstPlayerId: playerOne,
        isSecurityLooking: true,
        activeCardIds: new[]
        {
            new HeadlessEntityId("p1-hand"),
            new HeadlessEntityId("p1-battle"),
            new HeadlessEntityId("p2-hand"),
            new HeadlessEntityId("p2-battle")
        });
}

static IReadOnlyList<(string InstanceId, HeadlessPlayerId OwnerId, ChoiceZone Zone)> CardFixture()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);
    return new[]
    {
        ("p1-hand", playerOne, ChoiceZone.Hand),
        ("p1-library", playerOne, ChoiceZone.Library),
        ("p1-security", playerOne, ChoiceZone.Security),
        ("p1-digitama", playerOne, ChoiceZone.DigitamaLibrary),
        ("p1-battle", playerOne, ChoiceZone.BattleArea),
        ("p1-trash", playerOne, ChoiceZone.Trash),
        ("p2-hand", playerTwo, ChoiceZone.Hand),
        ("p2-library", playerTwo, ChoiceZone.Library),
        ("p2-security", playerTwo, ChoiceZone.Security),
        ("p2-digitama", playerTwo, ChoiceZone.DigitamaLibrary),
        ("p2-battle", playerTwo, ChoiceZone.BattleArea),
        ("p2-trash", playerTwo, ChoiceZone.Trash),
    };
}

static void AssertHiddenZone(PlayerStateView player, ChoiceZone zone, int count, string label)
{
    PlayerZoneView view = player.FindZone(zone) ?? throw new InvalidOperationException($"{label}: missing zone {zone}.");
    AssertTrue(view.IsHidden, $"{label} hidden");
    AssertEqual(count, view.Count, $"{label} count");
    AssertEqual(0, view.CardIds.Count, $"{label} visible ids");
}

static void AssertVisibleZone(PlayerStateView player, ChoiceZone zone, IReadOnlyList<string> expectedIds, string label)
{
    PlayerZoneView view = player.FindZone(zone) ?? throw new InvalidOperationException($"{label}: missing zone {zone}.");
    AssertFalse(view.IsHidden, $"{label} hidden");
    AssertEqual(expectedIds.Count, view.Count, $"{label} count");
    AssertSequence(expectedIds, view.CardIds.Select(id => id.Value).ToArray(), $"{label} ids");
}

static IReadOnlyList<string> Flatten(VisibilityViewSnapshot view)
{
    return view.ToMetadata()
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}")
        .Concat(view.Players.SelectMany(player => player.Zones.Select(zone =>
            $"{player.PlayerId.Value}:{player.IsOwnerView}:{zone.Zone}:{zone.Count}:{zone.IsHidden}:{string.Join("|", zone.CardIds.Select(id => id.Value))}")))
        .ToArray();
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
