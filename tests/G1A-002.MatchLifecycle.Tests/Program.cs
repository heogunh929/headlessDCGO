using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1A-002 goal row keeps the match lifecycle contract", GoalRowKeepsExpectedContract),
    ("DcgoMatch rejects lifecycle APIs before initialize", RejectsLifecycleApisBeforeInitialize),
    ("Initialize establishes first step snapshot and drains initialize event once", InitializeEstablishesFirstStepSnapshot),
    ("ApplyAction then Step transitions to terminal result from action metadata", ApplyActionThenStepTransitionsToTerminalResult),
    ("Reset reuses config and returns to non-terminal lifecycle state", ResetReturnsToNonTerminalLifecycleState),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-002")
        ?? throw new InvalidOperationException("G1A-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Runtime", Value(row, "area"), "area");
    AssertEqual("Initialize Reset Step Result 계약 고정", Value(row, "scope"), "scope");
    AssertEqual("DcgoMatch lifecycle API", Value(row, "deliverables"), "deliverables");
    AssertEqual("lifecycle 호출 순서와 상태 전이 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G1A-002_match_lifecycle_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("lifecycle 테스트 통과", Value(row, "completion_gate"), "completion_gate");
    return Task.CompletedTask;
}

async Task RejectsLifecycleApisBeforeInitialize()
{
    var match = new DcgoMatch();

    AssertFalse(match.IsInitialized, "new match initialized flag");
    await ExpectThrowsAsync<InvalidOperationException>(() => match.ResetAsync());
    await ExpectThrowsAsync<InvalidOperationException>(() => match.StepAsync());
    await ExpectThrowsAsync<InvalidOperationException>(() => match.ApplyActionAsync(CreateAction(new HeadlessPlayerId(1))));
    ExpectThrows<InvalidOperationException>(() => match.GetResult());
    ExpectThrows<InvalidOperationException>(() => match.GetObservation());
    ExpectThrows<InvalidOperationException>(() => match.GetActionMask());
    ExpectThrows<InvalidOperationException>(() => match.IsTerminal());
}

async Task InitializeEstablishesFirstStepSnapshot()
{
    var players = CreatePlayers();
    var match = new DcgoMatch();

    await match.InitializeAsync(CreateConfig(players));
    AssertTrue(match.IsInitialized, "initialized flag after InitializeAsync");
    AssertFalse(match.IsTerminal(), "terminal flag after InitializeAsync");
    AssertEqual(2, match.GetObservation().PlayerCount, "player count after InitializeAsync");

    StepResult firstStep = await match.StepAsync();
    AssertFalse(firstStep.IsTerminal, "first step terminal flag");
    AssertEqual(1, firstStep.Events.Count, "first step event count");
    AssertEqual(GameEventType.StateChanged, firstStep.Events[0].Type, "initialize event type");
    AssertEqual("Match initialized.", firstStep.Events[0].Message, "initialize event message");
    AssertEqual(2, firstStep.Observation.PlayerCount, "first step player count");
    AssertEqual(1L, firstStep.Observation.StepIndex, "first step index");

    StepResult secondStep = await match.StepAsync();
    AssertEqual(0, secondStep.Events.Count, "second step drains no initialize event");
    AssertEqual(2L, secondStep.Observation.StepIndex, "second step index");
}

async Task ApplyActionThenStepTransitionsToTerminalResult()
{
    var players = CreatePlayers();
    var winner = players[0];
    var context = EngineContext.CreateDefault();
    var match = new DcgoMatch(context, actionProcessor: new TerminalActionProcessor(winner));
    var action = CreateAction(winner);

    await match.InitializeAsync(CreateConfig(players));
    await match.StepAsync();
    ((IHeadlessLegalActionController)context.RuleQueryService).AddLegalActions(new[] { action });

    StepResult queued = await match.ApplyActionAsync(action);
    AssertFalse(queued.IsTerminal, "queued result terminal flag");
    AssertEqual(1, queued.Events.Count, "queued event count");
    AssertEqual(GameEventType.ActionQueued, queued.Events[0].Type, "queued event type");
    AssertEqual(1, match.PendingActions().Count, "pending action count after apply");

    StepResult terminal = await match.StepAsync();
    AssertTrue(terminal.IsTerminal, "terminal step flag");
    AssertTrue(match.IsTerminal(), "match terminal flag");
    AssertEqual(0, match.PendingActions().Count, "pending action count after step");
    AssertTrue(terminal.Events.Any(gameEvent => gameEvent.Type == GameEventType.ActionProcessed), "action processed event");
    AssertTrue(terminal.Events.Any(gameEvent => gameEvent.Type == GameEventType.GameEnded), "game ended event");

    MatchResult result = match.GetResult();
    AssertEqual(winner, result.WinnerId, "winner id");
    AssertFalse(result.IsDraw, "draw flag");
    AssertFalse(result.IsSurrender, "surrender flag");
    AssertEqual("terminal action", result.Reason, "terminal reason");

    StepResult afterTerminal = await match.StepAsync();
    AssertTrue(afterTerminal.IsTerminal, "step after terminal remains terminal");
    AssertEqual(0, afterTerminal.Events.Count, "step after terminal has no new events");
    await ExpectThrowsAsync<InvalidOperationException>(() => match.ApplyActionAsync(action));
}

async Task ResetReturnsToNonTerminalLifecycleState()
{
    var players = CreatePlayers();
    var winner = players[0];
    var context = EngineContext.CreateDefault();
    var match = new DcgoMatch(context, actionProcessor: new TerminalActionProcessor(winner));
    var action = CreateAction(winner);

    await match.InitializeAsync(CreateConfig(players));
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    AssertTrue(match.IsTerminal(), "terminal before reset");

    await match.ResetAsync();
    AssertTrue(match.IsInitialized, "initialized flag after reset");
    AssertFalse(match.IsTerminal(), "terminal flag after reset");
    AssertEqual(null, match.GetResult().WinnerId, "winner after reset");
    AssertEqual(string.Empty, match.GetResult().Reason, "reason after reset");
    AssertEqual(2, match.GetObservation().PlayerCount, "player count after reset");
    AssertEqual(0L, match.GetObservation().StepIndex, "step index after reset before stepping");

    StepResult resetStep = await match.StepAsync();
    AssertEqual(1, resetStep.Events.Count, "reset event count");
    AssertEqual("Match reset.", resetStep.Events[0].Message, "reset event message");
    AssertFalse(resetStep.IsTerminal, "reset step terminal flag");
    AssertEqual(1L, resetStep.Observation.StepIndex, "reset step index");
}

static HeadlessPlayerId[] CreatePlayers()
{
    return new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) };
}

static MatchConfig CreateConfig(IReadOnlyList<HeadlessPlayerId> players)
{
    return MatchConfig.Create(players, randomSeed: 17, initialMemory: 0, minimumMemory: -10, maximumMemory: 10);
}

static LegalAction CreateAction(HeadlessPlayerId playerId)
{
    return new LegalAction(
        new HeadlessEntityId("action-1"),
        playerId,
        "end-match",
        new Dictionary<string, object?>());
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

static async Task ExpectThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
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

sealed class TerminalActionProcessor(HeadlessPlayerId winner) : IActionProcessor
{
    public Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ActionProcessResult.Success(
            "terminal action",
            new Dictionary<string, object?>
            {
                [HeadlessActionParameterKeys.IsTerminal] = true,
                [HeadlessActionParameterKeys.WinnerPlayerId] = winner.Value,
                [HeadlessActionParameterKeys.Reason] = "terminal action"
            }));
    }
}
