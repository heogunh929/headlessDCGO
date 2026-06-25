using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
var fingerprintService = StateFingerprintService.Default;

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1B-006 goal row keeps the state fingerprint contract", GoalRowKeepsExpectedContract),
    ("Same logical state has same canonical snapshot and fingerprint", SameLogicalStateHasSameFingerprint),
    ("Fingerprint changes when zone order or card mutable state changes", FingerprintChangesForStateDifferences),
    ("Snapshot fingerprint includes player flags and event metadata", FingerprintIncludesFlagsAndEvents),
    ("MatchState ComputeFingerprint delegates to snapshot fingerprint service", MatchStateDelegatesToFingerprintService),
    ("State fingerprint service output is stable SHA256 lowercase hex", FingerprintFormatIsStable),
    ("State snapshot fingerprint source files no longer contain placeholder TODO contracts", FingerprintFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1B-006")
        ?? throw new InvalidOperationException("G1B-006 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("State", Value(row, "area"), "area");
    AssertEqual("Snapshot Fingerprint service", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("same state same fingerprint", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1B-006_state_snapshot_fingerprint_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-005", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("state"), "completion gate");
    return Task.CompletedTask;
}

Task SameLogicalStateHasSameFingerprint()
{
    MatchState first = CreateEquivalentState(orderVariant: false);
    MatchState second = CreateEquivalentState(orderVariant: true);

    AssertEqual(
        fingerprintService.BuildCanonicalSnapshot(first),
        fingerprintService.BuildCanonicalSnapshot(second),
        "canonical snapshot");
    AssertEqual(
        fingerprintService.ComputeFingerprint(first),
        fingerprintService.ComputeFingerprint(second),
        "same state fingerprint");
    return Task.CompletedTask;
}

Task FingerprintChangesForStateDifferences()
{
    MatchState baseline = CreateEquivalentState(orderVariant: false);
    MatchState zoneOrderChanged = ReplacePlayer(
        baseline,
        baseline.GetPlayer(new HeadlessPlayerId(1)).WithZone(
            ChoiceZone.Hand,
            new[] { new HeadlessEntityId("card-2"), new HeadlessEntityId("card-1") }));
    MatchState cardFlagChanged = baseline.WithCardInstance(
        baseline.GetCardInstance(new HeadlessEntityId("card-1")).SetFlag("piercing", false));
    MatchState sourceChanged = baseline.WithCardInstance(
        baseline.GetCardInstance(new HeadlessEntityId("card-1")).AttachSource(new HeadlessEntityId("source-extra")));
    MatchState modifierChanged = baseline.WithCardInstance(
        baseline.GetCardInstance(new HeadlessEntityId("card-1")).AddModifier("dp", 4000));

    string baselineFingerprint = fingerprintService.ComputeFingerprint(baseline);
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(zoneOrderChanged), "zone order change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(cardFlagChanged), "card flag change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(sourceChanged), "source change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(modifierChanged), "modifier change");
    return Task.CompletedTask;
}

Task FingerprintIncludesFlagsAndEvents()
{
    MatchState baseline = CreateEquivalentState(orderVariant: false);
    PlayerState player = baseline.GetPlayer(new HeadlessPlayerId(1));
    MatchState flagChanged = ReplacePlayer(baseline, player.SetFlag("hasDrawn", false));
    MatchState versionChanged = baseline with { Version = baseline.Version + 1 };
    MatchState terminalChanged = baseline with { IsTerminal = true };
    MatchState eventMetadataChanged = baseline with
    {
        Events = new[]
        {
            CreateEvent(sequence: 1, cardId: "card-1", metadataOrderVariant: false, faceUp: false),
            CreateEvent(sequence: 2, cardId: "card-2", metadataOrderVariant: false, faceUp: true)
        }
    };

    string baselineFingerprint = fingerprintService.ComputeFingerprint(baseline);
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(flagChanged), "player flag change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(versionChanged), "version change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(terminalChanged), "terminal change");
    AssertNotEqual(baselineFingerprint, fingerprintService.ComputeFingerprint(eventMetadataChanged), "event metadata change");

    string canonical = fingerprintService.BuildCanonicalSnapshot(baseline);
    AssertTrue(canonical.Contains("playerFlag|player=1|key=hasDrawn|value=True", StringComparison.Ordinal), "canonical player flag");
    AssertTrue(canonical.Contains("faceUp=True", StringComparison.Ordinal), "canonical event metadata");
    return Task.CompletedTask;
}

Task MatchStateDelegatesToFingerprintService()
{
    MatchState state = CreateEquivalentState(orderVariant: false);
    MatchStateSnapshot snapshot = state.Snapshot();

    AssertEqual(fingerprintService.ComputeFingerprint(state), state.ComputeFingerprint(), "match state delegation");
    AssertEqual(fingerprintService.ComputeFingerprint(snapshot), state.ComputeFingerprint(), "snapshot overload");
    AssertEqual(
        fingerprintService.BuildCanonicalSnapshot(state),
        fingerprintService.BuildCanonicalSnapshot(snapshot),
        "snapshot canonical");
    return Task.CompletedTask;
}

Task FingerprintFormatIsStable()
{
    string fingerprint = fingerprintService.ComputeFingerprint(CreateEquivalentState(orderVariant: false));
    AssertEqual(64, fingerprint.Length, "sha256 hex length");
    AssertTrue(fingerprint.All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')), "lowercase hex");
    return Task.CompletedTask;
}

Task FingerprintFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IStateFingerprintService.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "StateFingerprintService.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "State", "MatchState.cs"),
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

static MatchState CreateEquivalentState(bool orderVariant)
{
    var playerOne = new HeadlessPlayerId(1);
    var playerTwo = new HeadlessPlayerId(2);
    var cardOne = new HeadlessEntityId("card-1");
    var cardTwo = new HeadlessEntityId("card-2");

    PlayerState firstPlayer = new(
        playerOne,
        Memory: 3,
        Zones: orderVariant
            ? new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>
            {
                [ChoiceZone.Trash] = new[] { new HeadlessEntityId("trash-1") },
                [ChoiceZone.Hand] = new[] { cardOne, cardTwo },
            }
            : new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>
            {
                [ChoiceZone.Hand] = new[] { cardOne, cardTwo },
                [ChoiceZone.Trash] = new[] { new HeadlessEntityId("trash-1") },
            },
        Flags: orderVariant
            ? new Dictionary<string, bool> { ["turnStarted"] = false, ["hasDrawn"] = true }
            : new Dictionary<string, bool> { ["hasDrawn"] = true, ["turnStarted"] = false });
    PlayerState secondPlayer = new(playerTwo, Memory: 0);

    CardInstanceState firstCard = new(
        cardOne,
        new HeadlessEntityId("BT1-001"),
        playerOne,
        IsSuspended: true,
        IsFaceUp: true,
        SourceIds: new[] { new HeadlessEntityId("source-1") },
        Modifiers: orderVariant
            ? new Dictionary<string, object?> { ["keyword"] = "blocker", ["dp"] = 3000 }
            : new Dictionary<string, object?> { ["dp"] = 3000, ["keyword"] = "blocker" },
        Flags: orderVariant
            ? new Dictionary<string, bool> { ["retaliation"] = false, ["piercing"] = true }
            : new Dictionary<string, bool> { ["piercing"] = true, ["retaliation"] = false });
    CardInstanceState secondCard = new(cardTwo, new HeadlessEntityId("BT1-002"), playerOne);

    IReadOnlyList<PlayerState> players = orderVariant
        ? new[] { secondPlayer, firstPlayer }
        : new[] { firstPlayer, secondPlayer };
    IReadOnlyDictionary<HeadlessEntityId, CardInstanceState> cardInstances = orderVariant
        ? new Dictionary<HeadlessEntityId, CardInstanceState>
        {
            [cardTwo] = secondCard,
            [cardOne] = firstCard,
        }
        : new Dictionary<HeadlessEntityId, CardInstanceState>
        {
            [cardOne] = firstCard,
            [cardTwo] = secondCard,
        };
    IReadOnlyList<GameEvent> events = orderVariant
        ? new[]
        {
            CreateEvent(sequence: 1, cardId: "card-1", metadataOrderVariant: true, faceUp: true),
            CreateEvent(sequence: 2, cardId: "card-2", metadataOrderVariant: true, faceUp: false)
        }
        : new[]
        {
            CreateEvent(sequence: 1, cardId: "card-1", metadataOrderVariant: false, faceUp: true),
            CreateEvent(sequence: 2, cardId: "card-2", metadataOrderVariant: false, faceUp: false)
        };

    return new MatchState(
        players,
        cardInstances,
        Version: 7,
        IsTerminal: false,
        Events: events);
}

static GameEvent CreateEvent(long sequence, string cardId, bool metadataOrderVariant, bool faceUp)
{
    IReadOnlyDictionary<string, object?> metadata = metadataOrderVariant
        ? new Dictionary<string, object?>
        {
            ["toZone"] = ChoiceZone.Hand.ToString(),
            ["faceUp"] = faceUp,
            ["cardId"] = cardId,
            ["fromZone"] = ChoiceZone.Library.ToString(),
            ["playerId"] = 1
        }
        : new Dictionary<string, object?>
        {
            ["playerId"] = 1,
            ["cardId"] = cardId,
            ["fromZone"] = ChoiceZone.Library.ToString(),
            ["toZone"] = ChoiceZone.Hand.ToString(),
            ["faceUp"] = faceUp
        };

    return new GameEvent(
        sequence,
        GameEventType.CardMoved,
        $"Card moved: {cardId}",
        metadata);
}

static MatchState ReplacePlayer(MatchState state, PlayerState replacement)
{
    return state with
    {
        Players = state.Players
            .Select(player => player.PlayerId == replacement.PlayerId ? replacement : player)
            .ToArray()
    };
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
