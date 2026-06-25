using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-005 goal row keeps the zone mover contract", GoalRowKeepsExpectedContract),
    ("ZoneMover insert move and remove pass through one event boundary", InsertMoveAndRemoveRecordEvents),
    ("ZoneMover draw security and breeding helpers preserve ordered mutations", HelperMovesPreserveOrder),
    ("ZoneMover shuffle is deterministic for equal seeds and records an event", ShuffleIsDeterministicAndEvented),
    ("ZoneMover rejects invalid requests without mutating state", InvalidRequestsDoNotMutateState),
    ("ZoneMover snapshots events and zones are isolated from later mutations", SnapshotsAreIsolated),
    ("Zone mover source files no longer contain placeholder TODO contracts", ZoneMoverFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-005")
        ?? throw new InvalidOperationException("G1B-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("IZoneMover ZoneMoveRequest CardMoved event", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("move insert remove shuffle event", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-005_zone_mover_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-003; G1B-004", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("zone mover", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

async Task InsertMoveAndRemoveRecordEvents()
{
    var mover = new InMemoryZoneMover();
    var player = new HeadlessPlayerId(1);
    var card = new HeadlessEntityId("card-1");

    ZoneMoveResult inserted = await mover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.None, ChoiceZone.Library));
    AssertSequence(new[] { card }, inserted.DestinationZoneCards, "insert result destination");
    AssertEqual(GameEventType.CardMoved, inserted.Event.Type, "insert event type");
    AssertEqual(1L, inserted.Event.Sequence, "insert sequence");
    AssertEqual("Insert", inserted.Event.Metadata["operation"], "insert operation");
    AssertEqual(ChoiceZone.None.ToString(), inserted.Event.Metadata["fromZone"], "insert from zone");
    AssertEqual(ChoiceZone.Library.ToString(), inserted.Event.Metadata["toZone"], "insert to zone");

    ZoneMoveResult moved = await mover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Library, ChoiceZone.Hand));
    AssertEqual(0, moved.SourceZoneCards.Count, "move source empty");
    AssertSequence(new[] { card }, moved.DestinationZoneCards, "move destination");
    AssertEqual("Move", moved.Event.Metadata["operation"], "move operation");

    ZoneMoveResult removed = await mover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.None));
    AssertEqual(0, removed.SourceZoneCards.Count, "remove source empty");
    AssertEqual(0, removed.DestinationZoneCards.Count, "remove destination empty");
    AssertEqual("Remove", removed.Event.Metadata["operation"], "remove operation");
    AssertEqual(3, mover.Events.Count, "event count");
    AssertEqual(3L, mover.Events[2].Sequence, "remove sequence");
    AssertEqual(0, mover.GetCards(player, ChoiceZone.Hand).Count, "hand after remove");
}

async Task HelperMovesPreserveOrder()
{
    var mover = new InMemoryZoneMover();
    var player = new HeadlessPlayerId(1);
    var cardOne = new HeadlessEntityId("deck-1");
    var cardTwo = new HeadlessEntityId("deck-2");
    var securityOne = new HeadlessEntityId("security-1");
    var digitama = new HeadlessEntityId("digi-egg-1");

    await mover.MoveToDeckBottomAsync(player, cardOne);
    await mover.MoveToDeckBottomAsync(player, cardTwo);
    await mover.MoveToDeckTopAsync(player, new HeadlessEntityId("deck-0"));
    AssertSequence(
        new[] { new HeadlessEntityId("deck-0"), cardOne, cardTwo },
        mover.GetCards(player, ChoiceZone.Library),
        "deck top bottom order");

    IReadOnlyList<HeadlessEntityId> drawn = await mover.DrawAsync(player, 2);
    AssertSequence(new[] { new HeadlessEntityId("deck-0"), cardOne }, drawn, "draw order");
    AssertSequence(new[] { cardTwo }, mover.GetCards(player, ChoiceZone.Library), "library after draw");
    AssertSequence(new[] { new HeadlessEntityId("deck-0"), cardOne }, mover.GetCards(player, ChoiceZone.Hand), "hand after draw");

    await mover.AddToSecurityAsync(player, securityOne, faceUp: true);
    IReadOnlyList<HeadlessEntityId> trashed = await mover.TrashSecurityAsync(player, 1);
    AssertSequence(new[] { securityOne }, trashed, "trash security result");
    AssertSequence(new[] { securityOne }, mover.GetCards(player, ChoiceZone.Trash), "trash after security");
    GameEvent securityInsertEvent = mover.Events.Last(e =>
        Equals(e.Metadata["cardId"], securityOne.Value) &&
        Equals(e.Metadata["toZone"], ChoiceZone.Security.ToString()));
    AssertTrue((bool)securityInsertEvent.Metadata["faceUp"]!, "face-up event metadata");

    await mover.MoveAsync(new ZoneMoveRequest(player, digitama, ChoiceZone.None, ChoiceZone.DigitamaLibrary));
    HeadlessEntityId? hatched = await mover.HatchDigitamaAsync(player);
    AssertEqual(digitama, hatched!.Value, "hatched card");
    AssertSequence(new[] { digitama }, mover.GetCards(player, ChoiceZone.BreedingArea), "breeding after hatch");

    IReadOnlyList<HeadlessEntityId> promoted = await mover.MoveBreedingToBattleAsync(player);
    AssertSequence(new[] { digitama }, promoted, "promoted card");
    AssertSequence(new[] { digitama }, mover.GetCards(player, ChoiceZone.BattleArea), "battle after promote");
}

async Task ShuffleIsDeterministicAndEvented()
{
    var first = new InMemoryZoneMover(new GameRandomSource(seed: 17));
    var second = new InMemoryZoneMover(new GameRandomSource(seed: 17));
    var third = new InMemoryZoneMover(new GameRandomSource(seed: 18));
    var player = new HeadlessPlayerId(1);
    var cards = new[]
    {
        new HeadlessEntityId("a"),
        new HeadlessEntityId("b"),
        new HeadlessEntityId("c"),
        new HeadlessEntityId("d")
    };

    foreach (HeadlessEntityId card in cards)
    {
        await first.MoveToDeckBottomAsync(player, card);
        await second.MoveToDeckBottomAsync(player, card);
        await third.MoveToDeckBottomAsync(player, card);
    }

    await first.ShuffleAsync(player);
    await second.ShuffleAsync(player);
    await third.ShuffleAsync(player);

    AssertSequence(first.GetCards(player, ChoiceZone.Library), second.GetCards(player, ChoiceZone.Library), "same seed shuffle");
    AssertNotSequence(first.GetCards(player, ChoiceZone.Library), third.GetCards(player, ChoiceZone.Library), "different seed shuffle");
    GameEvent shuffleEvent = first.Events.Last();
    AssertEqual(GameEventType.StateChanged, shuffleEvent.Type, "shuffle event type");
    AssertEqual("Shuffle", shuffleEvent.Metadata["operation"], "shuffle operation");
    AssertEqual(ChoiceZone.Library.ToString(), shuffleEvent.Metadata["zone"], "shuffle zone");
}

async Task InvalidRequestsDoNotMutateState()
{
    var mover = new InMemoryZoneMover();
    var player = new HeadlessPlayerId(1);
    var card = new HeadlessEntityId("card-1");

    await mover.MoveToDeckBottomAsync(player, card);
    IReadOnlyList<HeadlessEntityId> before = mover.GetCards(player, ChoiceZone.Library);

    await ExpectThrowsAsync<InvalidOperationException>(() =>
        mover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.Trash)));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(default, card, ChoiceZone.None, ChoiceZone.Hand));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, default, ChoiceZone.None, ChoiceZone.Hand));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, card, ChoiceZone.Custom, ChoiceZone.Hand));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.Custom));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, card, ChoiceZone.None, ChoiceZone.None));
    ExpectThrows<ArgumentException>(() => new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.Hand));

    AssertSequence(before, mover.GetCards(player, ChoiceZone.Library), "library unchanged after failed move");
    AssertEqual(1, mover.Events.Count, "failed request did not add event");
}

async Task SnapshotsAreIsolated()
{
    var mover = new InMemoryZoneMover();
    var player = new HeadlessPlayerId(1);
    var cardOne = new HeadlessEntityId("card-1");
    var cardTwo = new HeadlessEntityId("card-2");

    await mover.MoveToDeckBottomAsync(player, cardOne);
    IReadOnlyList<GameEvent> eventSnapshot = mover.Events;
    IReadOnlyList<HeadlessEntityId> zoneSnapshot = mover.GetCards(player, ChoiceZone.Library);
    IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> playerSnapshot = mover.Snapshot(player);

    await mover.MoveToDeckBottomAsync(player, cardTwo);

    AssertEqual(1, eventSnapshot.Count, "event snapshot count");
    AssertSequence(new[] { cardOne }, zoneSnapshot, "zone snapshot isolation");
    AssertSequence(new[] { cardOne }, playerSnapshot[ChoiceZone.Library], "player snapshot isolation");

    mover.ResetMatchState();
    AssertEqual(0, mover.Events.Count, "events after reset");
    AssertEqual(0, mover.GetCards(player, ChoiceZone.Library).Count, "library after reset");
}

Task ZoneMoverFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IZoneMover.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ZoneMoveRequest.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ZoneMoveResult.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryZoneMover.cs"),
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
}

static void AssertNotSequence<T>(IReadOnlyList<T> unexpected, IReadOnlyList<T> actual, string label)
{
    if (unexpected.Count != actual.Count)
    {
        return;
    }

    for (int i = 0; i < unexpected.Count; i++)
    {
        if (!EqualityComparer<T>.Default.Equals(unexpected[i], actual[i]))
        {
            return;
        }
    }

    throw new InvalidOperationException($"{label}: sequences should differ.");
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
