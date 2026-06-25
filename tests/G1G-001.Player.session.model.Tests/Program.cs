using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1G-001 goal row keeps the player session model contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("SessionContext preserves session local player and immutable player order", SessionContextPreservesSessionLocalAndOrder),
    ("SessionContext resolves ownership and local ownership", SessionContextResolvesOwnership),
    ("SessionContext tracks turn identity and non-turn identity", SessionContextTracksTurnIdentity),
    ("SessionContext advances turn deterministically through player order", SessionContextAdvancesTurnDeterministically),
    ("SessionContext rejects invalid players and turn identity", SessionContextRejectsInvalidPlayers),
    ("SessionContext fingerprint is deterministic for identical input", SessionContextFingerprintIsDeterministic),
    ("HeadlessPlayerId value contract remains intact", HeadlessPlayerIdValueContractRemainsIntact),
    ("AS-IS player session references remain read-only inputs", AsIsPlayerSessionReferencesRemainReadOnlyInputs),
    ("SessionContext source has no placeholder or Unity dependency", SessionContextSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1G-001")
        ?? throw new InvalidOperationException("G1G-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Session", Value(row, "area"), "area");
    AssertEqual("Player session model", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("local player session", StringComparison.Ordinal), "scope");
    AssertEqual("HeadlessPlayerId SessionContext", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("player ownership turn identity", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1G-001_player_session_model_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("session model", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1B-001_stable_ids_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1B-001 COMPLETE");
    return Task.CompletedTask;
}

Task SessionContextPreservesSessionLocalAndOrder()
{
    var players = new List<HeadlessPlayerId> { new(2), new(1) };
    var session = new SessionContext(" session-a ", players, new HeadlessPlayerId(1));
    players.Add(new HeadlessPlayerId(3));

    AssertEqual("session-a", session.SessionId, "session id trim");
    AssertEqual(new HeadlessPlayerId(1), session.LocalPlayerId, "local player");
    AssertEqual(2, session.PlayerIds.Count, "player snapshot count");
    AssertEqual(new HeadlessPlayerId(2), session.PlayerIds[0], "first player order");
    AssertEqual(new HeadlessPlayerId(1), session.PlayerIds[1], "second player order");
    AssertEqual("local", new SessionContext(" ", new[] { new HeadlessPlayerId(1) }).SessionId, "blank session default");
    return Task.CompletedTask;
}

Task SessionContextResolvesOwnership()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var session = new SessionContext("session", new[] { playerOne, playerTwo }, playerOne);

    AssertTrue(session.IsOwner(playerOne, playerOne), "same owner viewer");
    AssertFalse(session.IsOwner(playerOne, playerTwo), "different owner viewer");
    AssertTrue(session.IsLocalOwner(playerOne), "local owner");
    AssertFalse(session.IsLocalOwner(playerTwo), "remote owner");
    ExpectThrows<InvalidOperationException>(() => session.IsOwner(new HeadlessPlayerId(3), playerOne));
    return Task.CompletedTask;
}

Task SessionContextTracksTurnIdentity()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var session = new SessionContext("session", new[] { playerOne, playerTwo }, playerOne, playerTwo, turnNumber: 4);

    AssertEqual(playerTwo, session.TurnPlayerId, "turn player");
    AssertEqual(playerOne, session.NonTurnPlayerId, "non-turn player");
    AssertEqual(4, session.TurnNumber, "turn number");
    AssertFalse(session.IsLocalPlayerTurn, "local player turn");

    SessionContext localTurn = session.WithTurn(playerOne, 5);
    AssertEqual(playerOne, localTurn.TurnPlayerId, "updated turn player");
    AssertEqual(playerTwo, localTurn.NonTurnPlayerId, "updated non-turn player");
    AssertTrue(localTurn.IsLocalPlayerTurn, "updated local player turn");
    AssertEqual(5, localTurn.TurnNumber, "updated turn number");
    return Task.CompletedTask;
}

Task SessionContextAdvancesTurnDeterministically()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var playerThree = new HeadlessPlayerId(3);
    var session = new SessionContext("session", new[] { playerOne, playerTwo, playerThree }, playerOne);

    SessionContext first = session.AdvanceTurn();
    SessionContext second = first.AdvanceTurn();
    SessionContext third = second.AdvanceTurn();
    SessionContext fourth = third.AdvanceTurn();

    AssertEqual(playerOne, first.TurnPlayerId, "first turn starts at first player");
    AssertEqual(1, first.TurnNumber, "first turn number");
    AssertEqual(playerTwo, second.TurnPlayerId, "second turn player");
    AssertEqual(playerThree, third.TurnPlayerId, "third turn player");
    AssertEqual(playerOne, fourth.TurnPlayerId, "wrap turn player");
    AssertEqual(4, fourth.TurnNumber, "wrap turn number");
    return Task.CompletedTask;
}

Task SessionContextRejectsInvalidPlayers()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);

    ExpectThrows<ArgumentNullException>(() => new SessionContext("session", null!));
    ExpectThrows<ArgumentException>(() => new SessionContext("session", Array.Empty<HeadlessPlayerId>()));
    ExpectThrows<ArgumentException>(() => new SessionContext("session", new[] { default(HeadlessPlayerId) }));
    ExpectThrows<InvalidOperationException>(() => new SessionContext("session", new[] { playerOne, playerOne }));
    ExpectThrows<InvalidOperationException>(() => new SessionContext("session", new[] { playerOne }, playerTwo));
    ExpectThrows<InvalidOperationException>(() => new SessionContext("session", new[] { playerOne }, playerOne, playerTwo));
    ExpectThrows<ArgumentOutOfRangeException>(() => new SessionContext("session", new[] { playerOne }, turnNumber: -1));
    ExpectThrows<InvalidOperationException>(() => new SessionContext("session", new[] { playerOne }).WithTurn(playerTwo, 1));
    ExpectThrows<ArgumentOutOfRangeException>(() => new SessionContext("session", new[] { playerOne }).WithTurn(playerOne, -1));
    return Task.CompletedTask;
}

Task SessionContextFingerprintIsDeterministic()
{
    var players = new[] { new HeadlessPlayerId(2), new HeadlessPlayerId(1) };
    var first = new SessionContext("session", players, new HeadlessPlayerId(1), new HeadlessPlayerId(2), turnNumber: 3);
    var second = new SessionContext("session", players, new HeadlessPlayerId(1), new HeadlessPlayerId(2), turnNumber: 3);
    var differentOrder = new SessionContext("session", players.Reverse(), new HeadlessPlayerId(1), new HeadlessPlayerId(2), turnNumber: 3);

    AssertEqual(first.Fingerprint(), second.Fingerprint(), "same input fingerprint");
    AssertFalse(first.Fingerprint() == differentOrder.Fingerprint(), "different order fingerprint");
    return Task.CompletedTask;
}

Task HeadlessPlayerIdValueContractRemainsIntact()
{
    var playerOne = new HeadlessPlayerId(1);
    var playerOneAgain = HeadlessPlayerId.Parse("1");

    AssertEqual(playerOne, playerOneAgain, "parse equality");
    AssertTrue(HeadlessPlayerId.TryParse("2", out HeadlessPlayerId parsed), "try parse");
    AssertEqual(new HeadlessPlayerId(2), parsed, "try parse value");
    AssertFalse(HeadlessPlayerId.TryParse("0", out _), "try parse zero");
    ExpectThrows<ArgumentOutOfRangeException>(() => new HeadlessPlayerId(0));
    ExpectThrows<FormatException>(() => HeadlessPlayerId.Parse("not-a-player"));
    return Task.CompletedTask;
}

Task AsIsPlayerSessionReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GManager.cs"),
            new[] { "GManager", "Photon", "Player" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameContext.cs"),
            new[] { "GameContext", "Player" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "TurnStateMachine", "Player" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"AS-IS reference file was not found: {path}");
        }

        string text = File.ReadAllText(path);
        foreach (string pattern in patterns)
        {
            AssertTrue(text.Contains(pattern, StringComparison.Ordinal), $"{Path.GetFileName(path)} contains {pattern}");
        }
    }

    return Task.CompletedTask;
}

Task SessionContextSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "SessionContext.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("SessionContext.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "SessionContext must not reference UnityEngine");
    AssertFalse(text.Contains("Photon", StringComparison.Ordinal), "SessionContext must not reference Photon");
    AssertTrue(text.Contains("IsOwner", StringComparison.Ordinal), "owner contract");
    AssertTrue(text.Contains("TurnPlayerId", StringComparison.Ordinal), "turn identity contract");
    AssertTrue(text.Contains("Fingerprint", StringComparison.Ordinal), "deterministic fingerprint contract");
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

static TException ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
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
