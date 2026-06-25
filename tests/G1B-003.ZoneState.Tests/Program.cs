using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-003 goal row keeps the zone state contract", GoalRowKeepsExpectedContract),
    ("ZoneId rejects non-gameplay zones and preserves concrete ids", ZoneIdPreservesConcreteIds),
    ("ZoneState preserves deck hand security and source order", ZoneStatePreservesRequiredOrder),
    ("ZoneState hides hidden zone identities from opponent views", ZoneStateHidesOpponentIdentities),
    ("ZoneState moves cards between zones without mutating originals", ZoneStateMovesCardsWithoutMutation),
    ("ZoneState rejects invalid duplicate and missing card mutations", ZoneStateRejectsInvalidMutations),
    ("ZoneState shuffle and fingerprint are deterministic for equal seeds", ZoneStateShuffleAndFingerprintAreDeterministic),
    ("PlayerState can expose and accept ZoneState models", PlayerStateExposesZoneStateModels),
    ("Zone state source files no longer contain placeholder TODO contracts", ZoneStateFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-003")
        ?? throw new InvalidOperationException("G1B-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("ZoneState ZoneId Visibility model", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("deck hand security source order", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-003_zone_state_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("zone state", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task ZoneIdPreservesConcreteIds()
{
    var hand = new ZoneId(ChoiceZone.Hand);
    AssertEqual(ChoiceZone.Hand, hand.Value, "zone id value");
    AssertEqual("Hand", hand.ToString(), "zone id string");
    AssertEqual(hand, ZoneId.FromChoiceZone(ChoiceZone.Hand), "zone id equality");

    ExpectThrows<ArgumentException>(() => new ZoneId(ChoiceZone.None));
    ExpectThrows<ArgumentException>(() => new ZoneId(ChoiceZone.Custom));
    return Task.CompletedTask;
}

Task ZoneStatePreservesRequiredOrder()
{
    ZoneState deck = ZoneState
        .Create(ChoiceZone.Library)
        .InsertBottom(new HeadlessEntityId("deck-1"))
        .InsertBottom(new HeadlessEntityId("deck-2"))
        .InsertTop(new HeadlessEntityId("deck-0"));
    AssertSequence(
        new[] { new HeadlessEntityId("deck-0"), new HeadlessEntityId("deck-1"), new HeadlessEntityId("deck-2") },
        deck.CardIds,
        "deck order");

    ZoneState hand = ZoneState
        .Create(ChoiceZone.Hand)
        .InsertBottom(new HeadlessEntityId("hand-1"))
        .InsertAt(0, new HeadlessEntityId("hand-0"));
    AssertSequence(
        new[] { new HeadlessEntityId("hand-0"), new HeadlessEntityId("hand-1") },
        hand.CardIds,
        "hand order");

    ZoneState security = ZoneState
        .Create(ChoiceZone.Security)
        .InsertBottom(new HeadlessEntityId("security-1"))
        .InsertBottom(new HeadlessEntityId("security-2"));
    AssertSequence(
        new[] { new HeadlessEntityId("security-1"), new HeadlessEntityId("security-2") },
        security.CardIds,
        "security order");

    ZoneState source = ZoneState
        .Create(ChoiceZone.DigivolutionCards)
        .InsertBottom(new HeadlessEntityId("source-1"))
        .InsertBottom(new HeadlessEntityId("source-2"));
    AssertEqual(ZoneVisibility.Public, source.Visibility, "source visibility");
    AssertSequence(
        new[] { new HeadlessEntityId("source-1"), new HeadlessEntityId("source-2") },
        source.CardIds,
        "source order");
    return Task.CompletedTask;
}

Task ZoneStateHidesOpponentIdentities()
{
    ZoneState hand = ZoneState
        .Create(ChoiceZone.Hand)
        .InsertBottom(new HeadlessEntityId("secret-hand"));
    ZoneState trash = ZoneState
        .Create(ChoiceZone.Trash)
        .InsertBottom(new HeadlessEntityId("public-trash"));

    AssertEqual(ZoneVisibility.Hidden, hand.Visibility, "hand default hidden");
    ZoneStateView ownerHand = hand.ToView(isOwnerView: true);
    ZoneStateView opponentHand = hand.ToView(isOwnerView: false);
    AssertFalse(ownerHand.IsHidden, "owner hand visible");
    AssertEqual(1, ownerHand.CardIds.Count, "owner hand id count");
    AssertTrue(opponentHand.IsHidden, "opponent hand hidden");
    AssertEqual(1, opponentHand.Count, "opponent hand count");
    AssertEqual(0, opponentHand.CardIds.Count, "opponent hand hidden ids");

    ZoneStateView opponentTrash = trash.ToView(isOwnerView: false);
    AssertFalse(opponentTrash.IsHidden, "opponent trash public");
    AssertSequence(new[] { new HeadlessEntityId("public-trash") }, opponentTrash.CardIds, "opponent trash ids");

    ZoneStateView revealedHand = hand.Reveal().ToView(isOwnerView: false);
    AssertFalse(revealedHand.IsHidden, "revealed hand public");
    AssertEqual(1, revealedHand.CardIds.Count, "revealed hand ids");
    return Task.CompletedTask;
}

Task ZoneStateMovesCardsWithoutMutation()
{
    ZoneState deck = ZoneState
        .Create(ChoiceZone.Library)
        .InsertBottom(new HeadlessEntityId("deck-1"))
        .InsertBottom(new HeadlessEntityId("deck-2"));
    ZoneState hand = ZoneState.Create(ChoiceZone.Hand);

    ZoneMoveStateResult result = deck.MoveCardTo(hand, new HeadlessEntityId("deck-1"));
    AssertSequence(
        new[] { new HeadlessEntityId("deck-1"), new HeadlessEntityId("deck-2") },
        deck.CardIds,
        "original deck unchanged");
    AssertSequence(new[] { new HeadlessEntityId("deck-2") }, result.Source.CardIds, "source after move");
    AssertSequence(new[] { new HeadlessEntityId("deck-1") }, result.Destination.CardIds, "destination after move");

    ZoneState movedToBottom = result.Source.InsertTop(new HeadlessEntityId("deck-0")).MoveToBottom(new HeadlessEntityId("deck-0"));
    AssertSequence(
        new[] { new HeadlessEntityId("deck-2"), new HeadlessEntityId("deck-0") },
        movedToBottom.CardIds,
        "move to bottom order");
    return Task.CompletedTask;
}

Task ZoneStateRejectsInvalidMutations()
{
    ZoneState deck = ZoneState
        .Create(ChoiceZone.Library)
        .InsertBottom(new HeadlessEntityId("deck-1"));

    ExpectThrows<ArgumentException>(() => ZoneState.Create(ChoiceZone.None));
    ExpectThrows<ArgumentException>(() => deck.InsertBottom(default));
    ExpectThrows<InvalidOperationException>(() => deck.InsertBottom(new HeadlessEntityId("deck-1")));
    ExpectThrows<InvalidOperationException>(() => deck.Remove(new HeadlessEntityId("missing-card")));
    ExpectThrows<ArgumentOutOfRangeException>(() => deck.InsertAt(2, new HeadlessEntityId("deck-2")));
    ExpectThrows<InvalidOperationException>(() => deck.MoveCardTo(ZoneState.Create(ChoiceZone.Library), new HeadlessEntityId("deck-1")));
    ExpectThrows<InvalidOperationException>(() => new ZoneState(
        new ZoneId(ChoiceZone.Hand),
        ZoneVisibility.Hidden,
        new[] { new HeadlessEntityId("same-card"), new HeadlessEntityId("same-card") }));
    return Task.CompletedTask;
}

Task ZoneStateShuffleAndFingerprintAreDeterministic()
{
    ZoneState first = ZoneState
        .Create(ChoiceZone.Library)
        .InsertBottom(new HeadlessEntityId("a"))
        .InsertBottom(new HeadlessEntityId("b"))
        .InsertBottom(new HeadlessEntityId("c"))
        .InsertBottom(new HeadlessEntityId("d"));
    ZoneState second = ZoneState.Create(ChoiceZone.Library, first.CardIds);

    ZoneState shuffledA = first.Shuffle(new GameRandomSource(seed: 17));
    ZoneState shuffledB = second.Shuffle(new GameRandomSource(seed: 17));
    ZoneState shuffledC = second.Shuffle(new GameRandomSource(seed: 18));

    AssertSequence(shuffledA.CardIds, shuffledB.CardIds, "same seed shuffle");
    AssertEqual(shuffledA.FingerprintSegment(), shuffledB.FingerprintSegment(), "same seed fingerprint");
    AssertNotEqual(shuffledA.FingerprintSegment(), shuffledC.FingerprintSegment(), "different seed fingerprint");
    AssertSequence(
        new[] { new HeadlessEntityId("a"), new HeadlessEntityId("b"), new HeadlessEntityId("c"), new HeadlessEntityId("d") },
        first.CardIds,
        "original shuffle input unchanged");
    return Task.CompletedTask;
}

Task PlayerStateExposesZoneStateModels()
{
    PlayerState player = new(new HeadlessPlayerId(1));
    ZoneState hand = ZoneState
        .Create(ChoiceZone.Hand)
        .InsertBottom(new HeadlessEntityId("hand-1"));

    PlayerState updated = player.WithZone(hand);
    ZoneState roundTrip = updated.GetZoneState(ChoiceZone.Hand);

    AssertEqual(new ZoneId(ChoiceZone.Hand), roundTrip.Id, "roundtrip zone id");
    AssertEqual(ZoneVisibility.Hidden, roundTrip.Visibility, "roundtrip default visibility");
    AssertSequence(hand.CardIds, roundTrip.CardIds, "roundtrip card ids");

    PlayerStateView opponentView = updated.ToView(new HeadlessPlayerId(2));
    PlayerZoneView opponentHand = opponentView.FindZone(ChoiceZone.Hand)!;
    AssertTrue(opponentHand.IsHidden, "player view uses zone visibility");
    AssertEqual(1, opponentHand.Count, "player view preserves hidden count");
    AssertEqual(0, opponentHand.CardIds.Count, "player view hides identity");
    return Task.CompletedTask;
}

Task ZoneStateFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "ZoneState.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "PlayerState.cs"),
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
