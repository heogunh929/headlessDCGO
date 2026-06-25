using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2C-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS memory security deck loss references are recorded", AsIsPlayerRuleReferencesAreRecorded),
    ("Player rule adapter calculates player relative memory cost checks", AdapterCalculatesPlayerRelativeMemory),
    ("Player rule adapter blocks security mutation while security is being looked at", AdapterBlocksSecurityMutationWhileLooking),
    ("Player rule adapter reports deck loss on failed draw", AdapterReportsDeckLossOnFailedDraw),
    ("Player rule adapter reports security loss on direct attack with empty security", AdapterReportsSecurityLossOnEmptySecurityAttack),
    ("Player rule adapter reports explicit player lose flag", AdapterReportsPlayerLoseFlag),
    ("Player rule adapter rejects invalid inputs without changing state", AdapterRejectsInvalidInputsWithoutChangingState),
    ("Player rule adapter terminal checks are deterministic", AdapterTerminalChecksAreDeterministic),
    ("G2C-002 source files contain no placeholder TODOs", PlayerRuleAdapterFilesHaveNoPlaceholderTodos),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2C-002")
        ?? throw new InvalidOperationException("G2C-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("Player", Value(row, "area"), "area");
    AssertEqual("Player rule adapter", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "memory security deck loss", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2C-002_player_terminal_checks_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2C-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2C-001_player_zone_ownership_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2C-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsPlayerRuleReferencesAreRecorded()
{
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));

    AssertContains(player, "public bool IsLose", "AS-IS lose flag");
    AssertContains(player, "MaxMemoryCost", "AS-IS max memory");
    AssertContains(player, "ExpectedMemory", "AS-IS expected memory");
    AssertContains(player, "CanAddSecurity", "AS-IS can add security");
    AssertContains(player, "CanReduceSecurity", "AS-IS can reduce security");
    AssertContains(attackProcess, "SecurityCards.Count == 0", "AS-IS direct attack security loss");
    AssertContains(turnStateMachine, "LibraryCards.Count == 0", "AS-IS draw deck loss");
    AssertContains(autoProcessing, "player.IsLose", "AS-IS lose flag terminal process");
    return Task.CompletedTask;
}

Task AdapterCalculatesPlayerRelativeMemory()
{
    PlayerRuleAdapter adapter = CreateAdapter(memory: 2);

    AssertEqual(new HeadlessPlayerId(1), adapter.PositiveMemoryPlayerId, "positive memory player");
    AssertEqual(8, adapter.MaxMemoryCost(new HeadlessPlayerId(1)), "p1 max memory cost");
    AssertEqual(12, adapter.MaxMemoryCost(new HeadlessPlayerId(2)), "p2 max memory cost");
    AssertEqual(5, adapter.ExpectedMemory(new HeadlessPlayerId(1), 3), "p1 expected memory");
    AssertEqual(-1, adapter.ExpectedMemory(new HeadlessPlayerId(2), 3), "p2 expected memory");
    AssertTrue(adapter.CanPayMemoryCost(new HeadlessPlayerId(1), 8), "p1 can pay boundary");
    AssertFalse(adapter.CanPayMemoryCost(new HeadlessPlayerId(1), 9), "p1 cannot overpay");
    AssertFalse(adapter.CanPayMemoryCost(new HeadlessPlayerId(2), -1), "negative cost rejected");
    return Task.CompletedTask;
}

Task AdapterBlocksSecurityMutationWhileLooking()
{
    PlayerRuleAdapter open = CreateAdapter(memory: 0, isSecurityLooking: false);
    PlayerRuleAdapter looking = CreateAdapter(memory: 0, isSecurityLooking: true);

    AssertTrue(open.CanAddSecurity(new HeadlessPlayerId(1)), "add security while open");
    AssertTrue(open.CanReduceSecurity(new HeadlessPlayerId(1)), "reduce security while open");
    AssertFalse(looking.CanAddSecurity(new HeadlessPlayerId(1)), "add security while looking");
    AssertFalse(looking.CanReduceSecurity(new HeadlessPlayerId(1)), "reduce security while looking");
    AssertFalse(open.CanReduceSecurity(new HeadlessPlayerId(2), count: 2), "cannot reduce more security than present");
    return Task.CompletedTask;
}

Task AdapterReportsDeckLossOnFailedDraw()
{
    PlayerRuleAdapter adapter = CreateAdapter(memory: 0, emptyP2Library: true);

    PlayerTerminalCheck safe = adapter.EvaluateDeckLossOnDraw(new HeadlessPlayerId(1), drawCount: 1);
    PlayerTerminalCheck loss = adapter.EvaluateDeckLossOnDraw(new HeadlessPlayerId(2), drawCount: 1);

    AssertFalse(safe.IsTerminal, "p1 draw safe");
    AssertTrue(loss.IsTerminal, "p2 deck loss");
    AssertEqual(PlayerTerminalReason.DeckLoss, loss.Reason, "deck loss reason");
    AssertEqual(new HeadlessPlayerId(1), loss.WinnerPlayerId, "deck loss winner");
    AssertEqual(new HeadlessPlayerId(2), loss.LosingPlayerId, "deck loss loser");
    AssertContains(loss.Message, "cannot draw", "deck loss message");
    return Task.CompletedTask;
}

Task AdapterReportsSecurityLossOnEmptySecurityAttack()
{
    PlayerRuleAdapter adapter = CreateAdapter(memory: 0, emptyP2Security: true);

    PlayerTerminalCheck safe = adapter.EvaluateSecurityAttack(new HeadlessPlayerId(1), new HeadlessPlayerId(1));
    PlayerTerminalCheck loss = adapter.EvaluateSecurityAttack(new HeadlessPlayerId(1), new HeadlessPlayerId(2));

    AssertFalse(safe.IsTerminal, "own security still present");
    AssertTrue(loss.IsTerminal, "security loss");
    AssertEqual(PlayerTerminalReason.SecurityLoss, loss.Reason, "security loss reason");
    AssertEqual(new HeadlessPlayerId(1), loss.WinnerPlayerId, "security loss winner");
    AssertEqual(new HeadlessPlayerId(2), loss.LosingPlayerId, "security loss loser");
    AssertContains(loss.Message, "no security", "security loss message");
    return Task.CompletedTask;
}

Task AdapterReportsPlayerLoseFlag()
{
    PlayerRuleAdapter adapter = CreateAdapter(memory: 0, p2LoseFlag: true);

    PlayerTerminalCheck check = adapter.EvaluateLoseFlag(new HeadlessPlayerId(2));

    AssertTrue(check.IsTerminal, "lose flag terminal");
    AssertEqual(PlayerTerminalReason.PlayerLoseFlag, check.Reason, "lose flag reason");
    AssertEqual(new HeadlessPlayerId(1), check.WinnerPlayerId, "lose flag winner");
    AssertEqual(new HeadlessPlayerId(2), check.LosingPlayerId, "lose flag loser");
    AssertEqual(true, check.ToMetadata()["isTerminal"], "metadata terminal");
    AssertEqual("PlayerLoseFlag", check.ToMetadata()["reason"], "metadata reason");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidInputsWithoutChangingState()
{
    PlayerRuleAdapter adapter = CreateAdapter(memory: 0);
    string fingerprint = adapter.Zones.State.ComputeFingerprint();

    ExpectThrows<InvalidOperationException>(() => adapter.MaxMemoryCost(new HeadlessPlayerId(99)));
    ExpectThrows<ArgumentOutOfRangeException>(() => adapter.ExpectedMemory(new HeadlessPlayerId(1), -1));
    ExpectThrows<ArgumentOutOfRangeException>(() => adapter.CanDraw(new HeadlessPlayerId(1), -1));
    ExpectThrows<ArgumentOutOfRangeException>(() => adapter.CanReduceSecurity(new HeadlessPlayerId(1), -1));
    ExpectThrows<ArgumentOutOfRangeException>(() => adapter.EvaluateSecurityAttack(new HeadlessPlayerId(1), new HeadlessPlayerId(2), -1));

    AssertEqual(fingerprint, adapter.Zones.State.ComputeFingerprint(), "state fingerprint unchanged");
    return Task.CompletedTask;
}

Task AdapterTerminalChecksAreDeterministic()
{
    PlayerRuleAdapter first = CreateAdapter(memory: -1, emptyP2Library: true, emptyP2Security: true);
    PlayerRuleAdapter second = CreateAdapter(memory: -1, emptyP2Library: true, emptyP2Security: true);

    AssertSequence(Flatten(first), Flatten(second), "deterministic checks");
    return Task.CompletedTask;
}

Task PlayerRuleAdapterFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "State", "PlayerRuleAdapter.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static PlayerRuleAdapter CreateAdapter(
    int memory,
    bool isSecurityLooking = false,
    bool emptyP2Library = false,
    bool emptyP2Security = false,
    bool p2LoseFlag = false)
{
    MatchState state = CreateState(emptyP2Library, emptyP2Security, p2LoseFlag);
    GameContextStateAccessor accessor = new(
        state,
        turnPlayerId: new HeadlessPlayerId(1),
        HeadlessPhase.Main,
        memory,
        firstPlayerId: new HeadlessPlayerId(1),
        isSecurityLooking: isSecurityLooking);
    return new PlayerRuleAdapter(accessor.ReadState());
}

static MatchState CreateState(
    bool emptyP2Library,
    bool emptyP2Security,
    bool p2LoseFlag)
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    PlayerState playerOne = new(p1, Memory: 0);
    PlayerState playerTwo = new(p2, Memory: 0);
    if (p2LoseFlag)
    {
        playerTwo = playerTwo.SetFlag(PlayerRuleAdapter.LoseFlagKey, true);
    }

    MatchState state = new(new[] { playerOne, playerTwo });
    foreach (var card in CardFixture(p1, p2, emptyP2Library, emptyP2Security))
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.Id), new HeadlessEntityId($"def-{card.Id}"), card.Owner))
            .PlaceCard(new HeadlessEntityId(card.Id), card.Zone);
    }

    return state;
}

static IReadOnlyList<(string Id, HeadlessPlayerId Owner, ChoiceZone Zone)> CardFixture(
    HeadlessPlayerId p1,
    HeadlessPlayerId p2,
    bool emptyP2Library,
    bool emptyP2Security)
{
    var cards = new List<(string Id, HeadlessPlayerId Owner, ChoiceZone Zone)>
    {
        ("p1-library", p1, ChoiceZone.Library),
        ("p1-security", p1, ChoiceZone.Security),
        ("p1-hand", p1, ChoiceZone.Hand),
        ("p2-hand", p2, ChoiceZone.Hand),
    };

    if (!emptyP2Library)
    {
        cards.Add(("p2-library", p2, ChoiceZone.Library));
    }

    if (!emptyP2Security)
    {
        cards.Add(("p2-security", p2, ChoiceZone.Security));
    }

    return cards;
}

static IReadOnlyList<string> Flatten(PlayerRuleAdapter adapter)
{
    return new[]
    {
        $"p1:max={adapter.MaxMemoryCost(new HeadlessPlayerId(1))}",
        $"p2:max={adapter.MaxMemoryCost(new HeadlessPlayerId(2))}",
        $"p1:expected={adapter.ExpectedMemory(new HeadlessPlayerId(1), 2)}",
        $"p2:expected={adapter.ExpectedMemory(new HeadlessPlayerId(2), 2)}",
        $"p2:deck={Format(adapter.EvaluateDeckLossOnDraw(new HeadlessPlayerId(2), 1))}",
        $"p2:security={Format(adapter.EvaluateSecurityAttack(new HeadlessPlayerId(1), new HeadlessPlayerId(2)))}",
        $"p1:addSecurity={adapter.CanAddSecurity(new HeadlessPlayerId(1))}",
        $"p1:reduceSecurity={adapter.CanReduceSecurity(new HeadlessPlayerId(1))}",
    };
}

static string Format(PlayerTerminalCheck check)
{
    return $"{check.IsTerminal}:{check.Reason}:{check.WinnerPlayerId?.Value}:{check.LosingPlayerId?.Value}:{check.Message}";
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
