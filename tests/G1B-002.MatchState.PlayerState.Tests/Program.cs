using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-002 goal row keeps the match player state contract", GoalRowKeepsExpectedContract),
    ("MatchState creates deterministic initial two-player snapshots", MatchStateCreatesInitialSnapshots),
    ("PlayerState preserves zone order memory flags and immutable snapshots", PlayerStatePreservesStateSnapshots),
    ("Opponent view hides hidden zone card identities while preserving counts", OpponentViewHidesHiddenZoneIdentities),
    ("MatchState move records card moved event and deterministic fingerprint", MatchStateMoveRecordsEventAndFingerprint),
    ("MatchState rejects invalid owner missing card and duplicate player state", MatchStateRejectsInvalidMutations),
    ("Match player state source files no longer contain placeholder TODO contracts", MatchPlayerStateFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-002")
        ?? throw new InvalidOperationException("G1B-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("MatchState PlayerState", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("initial state snapshot", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-002_match_player_state_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("state model", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task MatchStateCreatesInitialSnapshots()
{
    var players = new List<HeadlessPlayerId>
    {
        new(2),
        new(1)
    };

    MatchState state = MatchState.CreateInitial(players, initialMemory: 3);
    players.Add(new HeadlessPlayerId(3));

    AssertEqual(0L, state.Version, "initial version");
    AssertFalse(state.IsTerminal, "initial terminal");
    AssertEqual(2, state.Players.Count, "initial player count");
    AssertEqual(new HeadlessPlayerId(1), state.Players[0].PlayerId, "deterministic player order 0");
    AssertEqual(new HeadlessPlayerId(2), state.Players[1].PlayerId, "deterministic player order 1");
    AssertEqual(3, state.GetPlayer(new HeadlessPlayerId(1)).Memory, "initial memory");
    AssertEqual(0, state.CardInstances.Count, "initial card instance count");
    AssertEqual(0, state.Events.Count, "initial event count");

    MatchStateSnapshot snapshot = state.Snapshot();
    AssertEqual(0L, snapshot.Version, "snapshot version");
    AssertEqual(2, snapshot.Players.Count, "snapshot player count");

    ExpectThrows<ArgumentException>(() => MatchState.CreateInitial(Array.Empty<HeadlessPlayerId>()));
    ExpectThrows<InvalidOperationException>(() => MatchState.CreateInitial(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(1) }));
    return Task.CompletedTask;
}

Task PlayerStatePreservesStateSnapshots()
{
    var hand = new List<HeadlessEntityId>
    {
        new("hand-1")
    };
    var zones = new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>
    {
        [ChoiceZone.Hand] = hand
    };
    var flags = new Dictionary<string, bool>
    {
        ["hasDrawn"] = true
    };

    PlayerState player = new(new HeadlessPlayerId(1), Memory: 2, Zones: zones, Flags: flags);
    hand.Add(new HeadlessEntityId("hand-2"));
    zones[ChoiceZone.Trash] = new[] { new HeadlessEntityId("trash-1") };
    flags["hasDrawn"] = false;

    AssertEqual(1, player.GetZone(ChoiceZone.Hand).Count, "hand snapshot count");
    AssertEqual(new HeadlessEntityId("hand-1"), player.GetZone(ChoiceZone.Hand)[0], "hand snapshot card");
    AssertFalse(player.Zones.ContainsKey(ChoiceZone.Trash), "zone dictionary snapshot");
    AssertTrue(player.Flags["hasDrawn"], "flag snapshot");

    PlayerState updated = player
        .SetMemory(5)
        .SetFlag("turnStarted", true)
        .WithZone(ChoiceZone.Library, new[] { new HeadlessEntityId("deck-1"), new HeadlessEntityId("deck-2") });

    AssertEqual(2, player.Memory, "original memory unchanged");
    AssertEqual(5, updated.Memory, "updated memory");
    AssertTrue(updated.Flags["turnStarted"], "updated flag");
    AssertSequence(
        new[] { new HeadlessEntityId("deck-1"), new HeadlessEntityId("deck-2") },
        updated.GetZone(ChoiceZone.Library),
        "library order");
    return Task.CompletedTask;
}

Task OpponentViewHidesHiddenZoneIdentities()
{
    PlayerState player = new(new HeadlessPlayerId(1));
    player = player
        .WithZone(ChoiceZone.Hand, new[] { new HeadlessEntityId("secret-hand") })
        .WithZone(ChoiceZone.Library, new[] { new HeadlessEntityId("secret-deck") })
        .WithZone(ChoiceZone.Trash, new[] { new HeadlessEntityId("public-trash") });

    PlayerStateView ownerView = player.ToView(new HeadlessPlayerId(1));
    PlayerStateView opponentView = player.ToView(new HeadlessPlayerId(2));

    AssertEqual(1, ownerView.FindZone(ChoiceZone.Hand)!.CardIds.Count, "owner hand identity visible");
    PlayerZoneView opponentHand = opponentView.FindZone(ChoiceZone.Hand)!;
    AssertTrue(opponentHand.IsHidden, "opponent hand hidden");
    AssertEqual(1, opponentHand.Count, "opponent hand count preserved");
    AssertEqual(0, opponentHand.CardIds.Count, "opponent hand identity hidden");

    PlayerZoneView opponentTrash = opponentView.FindZone(ChoiceZone.Trash)!;
    AssertFalse(opponentTrash.IsHidden, "opponent trash public");
    AssertSequence(new[] { new HeadlessEntityId("public-trash") }, opponentTrash.CardIds, "opponent trash ids");
    return Task.CompletedTask;
}

Task MatchStateMoveRecordsEventAndFingerprint()
{
    var player = new HeadlessPlayerId(1);
    var card = new HeadlessEntityId("p1-card-1");
    var definition = new HeadlessEntityId("BT1-001");
    CardInstanceState instance = new(card, definition, player);

    MatchState initial = MatchState.CreateInitial(new[] { player })
        .WithCardInstance(instance)
        .PlaceCard(card, ChoiceZone.Library);

    MatchState moved = initial.MoveCard(new ZoneMoveRequest(player, card, ChoiceZone.Library, ChoiceZone.Hand));
    MatchState repeated = MatchState.CreateInitial(new[] { player })
        .WithCardInstance(instance)
        .PlaceCard(card, ChoiceZone.Library)
        .MoveCard(new ZoneMoveRequest(player, card, ChoiceZone.Library, ChoiceZone.Hand));

    AssertEqual(0L, initial.Version, "initial version remains unchanged");
    AssertEqual(1L, moved.Version, "moved version");
    AssertEqual(0, moved.GetPlayer(player).GetZone(ChoiceZone.Library).Count, "library after move");
    AssertSequence(new[] { card }, moved.GetPlayer(player).GetZone(ChoiceZone.Hand), "hand after move");
    AssertEqual(1, moved.Events.Count, "move event count");
    AssertEqual(GameEventType.CardMoved, moved.Events[0].Type, "move event type");
    AssertEqual(card.Value, moved.Events[0].Metadata["cardId"], "move event card id");
    AssertEqual(moved.ComputeFingerprint(), repeated.ComputeFingerprint(), "deterministic fingerprint");
    AssertNotEqual(initial.ComputeFingerprint(), moved.ComputeFingerprint(), "fingerprint changes after move");
    return Task.CompletedTask;
}

Task MatchStateRejectsInvalidMutations()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var card = new HeadlessEntityId("p1-card-1");
    var definition = new HeadlessEntityId("BT1-001");
    MatchState state = MatchState.CreateInitial(new[] { playerOne })
        .WithCardInstance(new CardInstanceState(card, definition, playerOne));

    ExpectThrows<InvalidOperationException>(() => state.GetPlayer(playerTwo));
    ExpectThrows<InvalidOperationException>(() => state.GetCardInstance(new HeadlessEntityId("missing-card")));
    ExpectThrows<InvalidOperationException>(() => state.WithCardInstance(new CardInstanceState(
        new HeadlessEntityId("p2-card-1"),
        definition,
        playerTwo)));
    ExpectThrows<InvalidOperationException>(() => state.MoveCard(new ZoneMoveRequest(playerTwo, card, ChoiceZone.Library, ChoiceZone.Hand)));
    ExpectThrows<InvalidOperationException>(() => state.MoveCard(new ZoneMoveRequest(playerOne, card, ChoiceZone.Library, ChoiceZone.Hand)));
    ExpectThrows<InvalidOperationException>(() => new MatchState(new[]
    {
        new PlayerState(playerOne),
        new PlayerState(playerOne)
    }));
    ExpectThrows<ArgumentException>(() => new MatchState(
        new[] { new PlayerState(playerOne) },
        new Dictionary<HeadlessEntityId, CardInstanceState>
        {
            [new HeadlessEntityId("wrong-key")] = new(card, definition, playerOne)
        }));
    return Task.CompletedTask;
}

Task MatchPlayerStateFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "MatchState.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "PlayerState.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "CardInstanceState.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string path = Path.Combine(root, relativeFile);
        string text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{relativeFile} still contains a TODO placeholder.");
        }
    }

    return Task.CompletedTask;
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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertNotEqual<T>(T unexpected, T actual, string label)
{
    if (EqualityComparer<T>.Default.Equals(unexpected, actual))
    {
        throw new InvalidOperationException($"{label}: value should not equal '{unexpected}'.");
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
