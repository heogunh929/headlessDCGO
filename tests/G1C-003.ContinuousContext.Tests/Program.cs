using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1C-003 goal row keeps the ContinuousContext contract", GoalRowKeepsExpectedContract),
    ("ContinuousContext maps seed options session memory and decks", ContextMapsSeedOptionsSessionMemoryAndDecks),
    ("ContinuousContext round trips MatchConfig fields", ContextRoundTripsMatchConfigFields),
    ("ContinuousContext deck access is scoped to configured players", DeckAccessIsScopedToConfiguredPlayers),
    ("ContinuousContext rejects invalid seed player deck and memory config", InvalidConfigFailsClearly),
    ("EngineContext default exposes ContinuousContext as a registered service", EngineContextRegistersContinuousContext),
    ("ContinuousContext source file no longer contains placeholder TODO contracts", ContinuousContextFileHasNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1C-003")
        ?? throw new InvalidOperationException("G1C-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Bridge", Value(row, "area"), "area");
    AssertEqual("ContinuousContext", Value(row, "goal"), "goal");
    AssertEqual("ContinuousContext MatchConfig mapping", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("seed option deck session config", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1C-003_continuous_context_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1C-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("ContinuousContext", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task ContextMapsSeedOptionsSessionMemoryAndDecks()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);
    var players = new List<HeadlessPlayerId> { playerOne, playerTwo };
    var deckOne = CreateDeck("alpha");
    var deckTwo = CreateDeck("beta");
    var decks = new Dictionary<HeadlessPlayerId, DeckList>
    {
        [playerOne] = deckOne
    };

    ContinuousContext context = ContinuousContext.Create(
        players,
        randomSeed: 123456,
        useDeterministicChoices: false,
        isAiSimulation: true,
        canSetRandom: true,
        sessionId: "  session-42  ",
        decks: decks,
        initialMemory: 1,
        minimumMemory: -3,
        maximumMemory: 7);

    players.Add(new HeadlessPlayerId(3));
    decks[playerTwo] = deckTwo;

    AssertSequence(new[] { playerOne, playerTwo }, context.PlayerIds, "player ids");
    AssertEqual(123456L, context.RandomSeed, "random seed");
    AssertTrue(context.IsHeadless, "headless flag");
    AssertTrue(context.IsAiSimulation, "ai simulation");
    AssertTrue(context.CanSetRandom, "can set random");
    AssertFalse(context.UseDeterministicChoices, "deterministic choices");
    AssertEqual("session-42", context.SessionId, "session id");
    AssertEqual(1, context.InitialMemory, "initial memory");
    AssertEqual(-3, context.MinimumMemory, "minimum memory");
    AssertEqual(7, context.MaximumMemory, "maximum memory");
    AssertSame(deckOne, context.GetDeck(playerOne), "deck one");
    AssertFalse(context.TryGetDeck(playerTwo, out _), "defensive deck copy");
    AssertEqual("local", ContinuousContext.Create(Array.Empty<HeadlessPlayerId>(), sessionId: "   ").SessionId, "blank session default");
    return Task.CompletedTask;
}

Task ContextRoundTripsMatchConfigFields()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);
    MatchConfig config = MatchConfig.Create(
        new[] { playerOne, playerTwo },
        randomSeed: 77,
        useDeterministicChoices: false,
        initialMemory: -1,
        minimumMemory: -4,
        maximumMemory: 6);
    var decks = new Dictionary<HeadlessPlayerId, DeckList>
    {
        [playerOne] = CreateDeck("round-trip")
    };

    ContinuousContext context = ContinuousContext.FromMatchConfig(
        config,
        sessionId: "session-roundtrip",
        isAiSimulation: true,
        canSetRandom: true,
        decks: decks);
    MatchConfig roundTrip = context.ToMatchConfig();

    AssertSequence(config.PlayerIds, context.PlayerIds, "context players");
    AssertEqual(77L, context.RandomSeed, "context seed");
    AssertFalse(context.UseDeterministicChoices, "context deterministic");
    AssertTrue(context.IsAiSimulation, "context ai");
    AssertTrue(context.CanSetRandom, "context can set random");
    AssertEqual("session-roundtrip", context.SessionId, "context session");
    AssertSame(decks[playerOne], context.GetDeck(playerOne), "context deck");
    AssertSequence(config.PlayerIds, roundTrip.PlayerIds, "round trip players");
    AssertEqual(config.RandomSeed, roundTrip.RandomSeed, "round trip seed");
    AssertEqual(config.UseDeterministicChoices, roundTrip.UseDeterministicChoices, "round trip deterministic");
    AssertEqual(config.InitialMemory, roundTrip.InitialMemory, "round trip initial memory");
    AssertEqual(config.MinimumMemory, roundTrip.MinimumMemory, "round trip minimum memory");
    AssertEqual(config.MaximumMemory, roundTrip.MaximumMemory, "round trip maximum memory");
    return Task.CompletedTask;
}

Task DeckAccessIsScopedToConfiguredPlayers()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);
    HeadlessPlayerId outsider = new(9);
    ContinuousContext context = ContinuousContext.Create(new[] { playerOne, playerTwo });
    DeckList deck = CreateDeck("scoped");

    ContinuousContext withDeck = context.WithDeck(playerTwo, deck);

    AssertSame(deck, withDeck.GetDeck(playerTwo), "configured deck");
    AssertTrue(withDeck.TryGetDeck(playerTwo, out DeckList? found), "try deck");
    AssertSame(deck, found!, "try deck instance");
    ExpectThrows<InvalidOperationException>(() => withDeck.GetDeck(playerOne));
    ExpectThrows<InvalidOperationException>(() => context.WithDeck(outsider, deck));
    ExpectThrows<InvalidOperationException>(() => (context with
    {
        Decks = new Dictionary<HeadlessPlayerId, DeckList>
        {
            [outsider] = deck
        }
    }).Validate());
    return Task.CompletedTask;
}

Task InvalidConfigFailsClearly()
{
    HeadlessPlayerId playerOne = new(1);
    HeadlessPlayerId playerTwo = new(2);

    ExpectThrows<ArgumentNullException>(() => ContinuousContext.Create(null!));
    ExpectThrows<ArgumentNullException>(() => ContinuousContext.FromMatchConfig(null!));
    ExpectThrows<ArgumentException>(() => ContinuousContext.Create(new[] { default(HeadlessPlayerId) }));
    ExpectThrows<InvalidOperationException>(() => ContinuousContext.Create(new[] { playerOne, playerOne }));
    ExpectThrows<ArgumentOutOfRangeException>(() => ContinuousContext.Create(new[] { playerOne }, randomSeed: (long)int.MaxValue + 1));
    ExpectThrows<ArgumentOutOfRangeException>(() => ContinuousContext.Create(new[] { playerOne }, initialMemory: 11));
    ExpectThrows<ArgumentOutOfRangeException>(() => ContinuousContext.Create(new[] { playerOne }, initialMemory: 0, minimumMemory: 1));
    ExpectThrows<ArgumentOutOfRangeException>(() => ContinuousContext.Create(new[] { playerOne }, initialMemory: 0, maximumMemory: -1));
    ExpectThrows<ArgumentException>(() => (ContinuousContext.Create(new[] { playerOne }) with
    {
        Decks = new Dictionary<HeadlessPlayerId, DeckList>
        {
            [default] = CreateDeck("empty-owner")
        }
    }).Validate());
    ExpectThrows<ArgumentNullException>(() => (ContinuousContext.Create(new[] { playerOne, playerTwo }) with
    {
        Decks = new Dictionary<HeadlessPlayerId, DeckList>
        {
            [playerTwo] = null!
        }
    }).Validate());
    ExpectThrows<ArgumentException>(() => ContinuousContext.Create(new[] { playerOne }).WithDeck(default, CreateDeck("bad")));
    ExpectThrows<ArgumentNullException>(() => ContinuousContext.Create(new[] { playerOne }).WithDeck(playerOne, null!));
    return Task.CompletedTask;
}

Task EngineContextRegistersContinuousContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 42);
    ContinuousContext continuous = context.ContinuousContext;

    AssertSame(continuous, context.GetService<ContinuousContext>(), "continuous context service");
    AssertEqual(42L, continuous.RandomSeed, "default seed");
    AssertTrue(continuous.IsHeadless, "default headless");
    AssertTrue(continuous.UseDeterministicChoices, "default deterministic");
    AssertEqual("local", continuous.SessionId, "default session");
    AssertEqual(0, continuous.PlayerIds.Count, "default player ids");
    AssertEqual(0, continuous.Decks.Count, "default decks");
    AssertEqual(42, continuous.ToMatchConfig().RandomSeed, "default match config seed");
    return Task.CompletedTask;
}

Task ContinuousContextFileHasNoTodoContracts()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Bridge", "ContinuousContext.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ContinuousContext.cs still contains a TODO placeholder.");
    }

    return Task.CompletedTask;
}

static DeckList CreateDeck(string name)
{
    return new DeckList(
        name,
        new[]
        {
            new DeckListEntry(new HeadlessEntityId($"{name}-main-001"), 2),
            new DeckListEntry(new HeadlessEntityId($"{name}-main-002"), 1),
        },
        new[]
        {
            new DeckListEntry(new HeadlessEntityId($"{name}-egg-001"), 1),
        });
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
