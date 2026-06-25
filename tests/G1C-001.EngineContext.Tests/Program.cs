using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1C-001 goal row keeps the EngineContext contract", GoalRowKeepsExpectedContract),
    ("EngineContext default registers core services by interface and concrete type", DefaultRegistersCoreServices),
    ("EngineContext supports explicit registration lookup and isolated service snapshots", ExplicitRegistrationAndSnapshotAreStable),
    ("EngineContext rejects null invalid and missing service access", InvalidServiceAccessFailsClearly),
    ("EngineContext tracks current match and current state through lifecycle steps", CurrentMatchAndStateAreTracked),
    ("EngineContext ResetMatchState resets scoped services and current state", ResetMatchStateResetsScopedServices),
    ("EngineContext source files no longer contain placeholder TODO contracts", EngineContextFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1C-001")
        ?? throw new InvalidOperationException("G1C-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Bridge", Value(row, "area"), "area");
    AssertEqual("EngineContext", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("service registration lookup", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1C-001_engine_context_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("EngineContext", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task DefaultRegistersCoreServices()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 77);

    AssertSame(context.ChoiceProvider, context.GetService<IChoiceProvider>(), "choice provider");
    AssertSame(context.RandomSource, context.GetService<IRandomSource>(), "random source interface");
    AssertSame(context.RandomSource, context.GetService<GameRandomSource>(), "random source concrete");
    AssertSame(context.CardRepository, context.GetService<ICardRepository>(), "card repository");
    AssertSame(context.CardInstanceRepository, context.GetService<ICardInstanceRepository>(), "card instance repository");
    AssertSame(context.ZoneMover, context.GetService<IZoneMover>(), "zone mover interface");
    AssertSame(context.ZoneMover, context.GetService<InMemoryZoneMover>(), "zone mover concrete");
    AssertSame(context.RuleQueryService, context.GetService<IRuleQueryService>(), "rule query");
    AssertSame(context.TurnController, context.GetService<IHeadlessTurnController>(), "turn controller");
    AssertSame(context.ChoiceController, context.GetService<IHeadlessChoiceController>(), "choice controller");
    AssertSame(context.AttackController, context.GetService<IHeadlessAttackController>(), "attack controller");
    AssertSame(context.MemoryController, context.GetService<IHeadlessMemoryController>(), "memory controller");
    AssertSame(context.LogSink, context.GetService<ILogSink>(), "log sink");
    AssertSame(context.TaskRunner, context.GetService<EngineTaskRunner>(), "task runner");
    AssertSame(context.EffectScheduler, context.GetService<EffectScheduler>(), "effect scheduler");
    AssertTrue(context.Services.ContainsKey(typeof(IZoneMover)), "services contains zone mover");
    AssertTrue(context.Services.ContainsKey(typeof(InMemoryZoneMover)), "services contains concrete zone mover");
    return Task.CompletedTask;
}

Task ExplicitRegistrationAndSnapshotAreStable()
{
    EngineContext context = EngineContext.CreateDefault();
    IReadOnlyDictionary<Type, object> before = context.Services;
    var logSink = new RecordingLogSink();

    context.RegisterService<ILogSink>(logSink);
    context.RegisterService(typeof(RecordingLogSink), logSink);

    AssertSame(logSink, context.GetService<ILogSink>(), "registered log sink interface");
    AssertSame(logSink, context.GetService<RecordingLogSink>(), "registered log sink concrete");
    AssertFalse(before.ContainsKey(typeof(RecordingLogSink)), "previous snapshot isolation");
    AssertTrue(context.Services.ContainsKey(typeof(RecordingLogSink)), "new snapshot contains registered type");

    context.GetService<ILogSink>().Info("registered");
    AssertSequence(new[] { "INFO:registered" }, logSink.Messages, "registered sink messages");
    return Task.CompletedTask;
}

Task InvalidServiceAccessFailsClearly()
{
    EngineContext context = EngineContext.CreateDefault();

    AssertFalse(context.TryGetService<RecordingLogSink>(out _), "missing try get");
    ExpectThrows<InvalidOperationException>(() => context.GetService<RecordingLogSink>());
    ExpectThrows<ArgumentNullException>(() => context.GetService(null!));
    ExpectThrows<ArgumentNullException>(() => context.TryGetService(null!, out _));
    ExpectThrows<ArgumentNullException>(() => context.RegisterService<ILogSink>(null!));
    ExpectThrows<ArgumentNullException>(() => context.RegisterService(null!, context.LogSink));
    ExpectThrows<ArgumentNullException>(() => context.RegisterService(typeof(ILogSink), null!));
    ExpectThrows<ArgumentException>(() => context.RegisterService(typeof(IRandomSource), context.LogSink));
    ExpectThrows<ArgumentNullException>(() => new DcgoMatch(null!));
    return Task.CompletedTask;
}

async Task CurrentMatchAndStateAreTracked()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    AssertEqual(null, context.CurrentMatch, "initial current match");
    AssertSame(ObservationSnapshot.Empty, context.CurrentState, "initial current state");

    var match = new DcgoMatch(context);
    AssertSame(match, context.CurrentMatch!, "attached current match");
    AssertSame(match, context.GetService<DcgoMatch>(), "current match registered");

    await match.InitializeAsync(MatchConfig.Create(
        new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) },
        randomSeed: 12));
    StepResult step = await match.StepAsync();

    AssertSame(step.Observation, context.CurrentState, "current state updated from step");
    AssertEqual(2, context.CurrentState.PlayerCount, "current state player count");
    AssertEqual(12, context.CurrentState.RandomSeed, "current state random seed");

    context.ClearCurrentMatch();
    AssertEqual(null, context.CurrentMatch, "cleared current match");
    AssertSame(ObservationSnapshot.Empty, context.CurrentState, "cleared current state");
    AssertFalse(context.TryGetService<DcgoMatch>(out _), "cleared match registration");
}

async Task ResetMatchStateResetsScopedServices()
{
    EngineContext context = EngineContext.CreateDefault();
    var player = new HeadlessPlayerId(1);
    var card = new HeadlessEntityId("card-1");

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.None, ChoiceZone.Hand));
    context.UpdateCurrentState(new ObservationSnapshot(
        StepIndex: 1,
        IsTerminal: false,
        PendingActionCount: 0,
        HasPendingEffects: false,
        CardInstanceCount: 0,
        RandomSeed: 3,
        LastActionType: null,
        LastActionSucceeded: null,
        LastActionMessage: null,
        Turn: HeadlessTurnState.Empty,
        Choice: HeadlessChoiceState.Empty,
        Attack: HeadlessAttackState.Empty,
        Effects: HeadlessEffectState.Empty,
        Memory: HeadlessMemoryState.Default,
        Players: Array.Empty<PlayerObservation>()));

    AssertEqual(1, context.ZoneMover.Events.Count, "zone mover event before reset");
    AssertEqual(1, ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Hand).Count, "zone count before reset");

    context.ResetMatchState();

    AssertSame(ObservationSnapshot.Empty, context.CurrentState, "current state after reset");
    AssertEqual(0, context.ZoneMover.Events.Count, "zone mover events after reset");
    AssertEqual(0, ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Hand).Count, "zone count after reset");
}

Task EngineContextFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Bridge", "EngineContext.cs"),
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

static void AssertSame(object expected, object actual, string label)
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same instance.");
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

sealed class RecordingLogSink : ILogSink
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public void Info(string message)
    {
        _messages.Add($"INFO:{message}");
    }

    public void Warn(string message)
    {
        _messages.Add($"WARN:{message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        _messages.Add($"ERROR:{message}");
    }
}
