using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2D-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS zone movement event references are recorded", AsIsZoneMovementReferencesAreRecorded),
    ("Card movement port emits CardMoved event with before and after zones", MovementEmitsEventWithZonePayload),
    ("Card movement port records deterministic trace metadata", MovementRecordsTraceMetadata),
    ("Card movement port exposes EffectContext from movement event", MovementExposesEffectContext),
    ("Card movement port preserves face-up state in event and card identity", MovementPreservesFaceUpState),
    ("Card movement port returns failure without mutating state for invalid owner", MovementRejectsInvalidOwnerWithoutMutation),
    ("Card movement port returns failure without mutating state for invalid zone or card", MovementRejectsInvalidZoneAndMissingCardWithoutMutation),
    ("Card movement port emits deterministic payload for repeated input", MovementPayloadIsDeterministic),
    ("G2D-002 source files contain no placeholder markers", CardMovementPortFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2D-002")
        ?? throw new InvalidOperationException("G2D-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("CardController", Value(row, "area"), "area");
    AssertEqual("card movement port", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "카드 이동", "scope");
    AssertContains(Value(row, "unit_test_scope"), "move event", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2D-002_card_zone_movement_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2D-001", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2D-001_card_identity_binding_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2D-001 completion marker");
    return Task.CompletedTask;
}

Task AsIsZoneMovementReferencesAreRecorded()
{
    string cardObjectController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(cardObjectController, "RemoveFromAllArea", "AS-IS remove from old zone");
    AssertContains(cardObjectController, "AddTrashCard", "AS-IS trash movement");
    AssertContains(cardObjectController, "AddSecurityCard", "AS-IS security movement");
    AssertContains(cardObjectController, "MovePermanent", "AS-IS permanent movement");
    AssertContains(cardObjectController, "EffectTiming.OnMove", "AS-IS on move timing");
    AssertContains(cardController, "CardObjectController.AddTrashCard", "AS-IS CardController move call");
    AssertContains(cardController, "EffectTiming.OnLoseSecurity", "AS-IS security loss event");
    AssertContains(cardController, "EffectTiming.OnAddSecurity", "AS-IS security add event");
    return Task.CompletedTask;
}

Task MovementEmitsEventWithZonePayload()
{
    CardMovementPort port = new(CreateState());
    CardMovementProcessResult result = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash,
        reason: "discard"));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(string.Empty, result.FailureReason, "failure reason");
    AssertEqual(ChoiceZone.Hand, result.Before!.Zone, "before zone");
    AssertEqual(ChoiceZone.Trash, result.After!.Zone, "after zone");
    AssertEqual(1, result.After.ZoneIndex, "trash append index");
    AssertEqual(1, result.MovementEvent!.Sequence, "event sequence");
    AssertEqual(GameEventType.CardMoved, result.MovementEvent.Type, "event type");
    AssertEqual(result.MovementEvent, result.State.Events.Single(), "state event");
    AssertMetadata(result.MovementEvent, "cardId", "p1-hand");
    AssertMetadata(result.MovementEvent, "cardDefinitionId", "def-p1-hand");
    AssertMetadata(result.MovementEvent, "playerId", 1);
    AssertMetadata(result.MovementEvent, "cardOwnerId", 1);
    AssertMetadata(result.MovementEvent, "fromZone", ChoiceZone.Hand.ToString());
    AssertMetadata(result.MovementEvent, "toZone", ChoiceZone.Trash.ToString());
    AssertMetadata(result.MovementEvent, "fromZoneOwnerId", 1);
    AssertMetadata(result.MovementEvent, "toZoneOwnerId", 1);
    AssertMetadata(result.MovementEvent, "fromIndex", 0);
    AssertMetadata(result.MovementEvent, "toIndex", 1);
    AssertMetadata(result.MovementEvent, "reason", "discard");
    return Task.CompletedTask;
}

Task MovementRecordsTraceMetadata()
{
    EngineTrace trace = new();
    CardMovementPort port = new(CreateState(), trace);
    CardMovementProcessResult result = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-library"),
        ChoiceZone.Library,
        ChoiceZone.Hand,
        reason: "draw"));

    AssertTrue(result.IsSuccess, "success");
    TraceEvent traceEvent = trace.Snapshot().Single();
    AssertEqual(1L, traceEvent.Sequence, "trace sequence");
    AssertEqual("card.movement", traceEvent.Category, "trace category");
    AssertEqual(result.MovementEvent!.Message, traceEvent.Message, "trace message");
    AssertEqual(result.MovementEvent.Metadata["cardId"], traceEvent.Metadata["cardId"], "trace card id");
    AssertEqual(result.MovementEvent.Metadata["fromZone"], traceEvent.Metadata["fromZone"], "trace from zone");
    AssertEqual(result.MovementEvent.Metadata["toZone"], traceEvent.Metadata["toZone"], "trace to zone");
    AssertFalse(string.IsNullOrWhiteSpace(trace.Fingerprint()), "trace fingerprint");
    return Task.CompletedTask;
}

Task MovementExposesEffectContext()
{
    CardMovementPort port = new(CreateState());
    CardMovementProcessResult result = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-battle"),
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea,
        reason: "move-to-breeding"));

    AssertTrue(result.IsSuccess, "success");
    EffectContext context = result.EffectContext!;
    AssertEqual(new HeadlessPlayerId(1), context.SourcePlayerId, "context source player");
    AssertEqual(new HeadlessPlayerId(1), context.OwnerPlayerId, "context owner player");
    AssertEqual(new HeadlessEntityId("p1-battle"), context.SourceEntityId, "context source entity");
    AssertEqual(new HeadlessEntityId("p1-battle"), context.TriggerEntityId, "context trigger entity");
    AssertSequence(new[] { "p1-battle" }, context.TargetEntityIds.Select(id => id.Value).ToArray(), "context targets");
    AssertEqual(result.MovementEvent!.Metadata["toZone"], context.Values["toZone"], "context to zone");
    AssertEqual("move-to-breeding", context.Values["reason"], "context reason");
    return Task.CompletedTask;
}

Task MovementPreservesFaceUpState()
{
    CardMovementPort port = new(CreateState());
    CardMovementProcessResult result = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-security"),
        ChoiceZone.Security,
        ChoiceZone.Hand,
        faceUp: true,
        reason: "security-check"));

    AssertTrue(result.IsSuccess, "success");
    AssertTrue(result.After!.IsFaceUp, "after face up");
    AssertMetadata(result.MovementEvent!, "faceUp", true);
    AssertEqual(ChoiceZone.Hand, result.After.Zone, "after zone");
    CardIdentitySnapshot snapshot = new CardIdentityAdapter(result.State).Bind(new HeadlessEntityId("p1-security"));
    AssertTrue(snapshot.IsFaceUp, "state identity face up");
    return Task.CompletedTask;
}

Task MovementRejectsInvalidOwnerWithoutMutation()
{
    MatchState state = CreateState();
    CardMovementPort port = new(state);
    string before = state.ComputeFingerprint();
    int eventCount = state.Events.Count;

    CardMovementProcessResult result = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(2),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash));

    AssertFalse(result.IsSuccess, "failure");
    AssertContains(result.FailureReason, "owned by player", "failure reason");
    AssertEqual(before, result.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertEqual(eventCount, result.State.Events.Count, "events unchanged");
    AssertEqual(ChoiceZone.Hand, new CardIdentityAdapter(result.State).Bind(new HeadlessEntityId("p1-hand")).Zone, "zone unchanged");
    return Task.CompletedTask;
}

Task MovementRejectsInvalidZoneAndMissingCardWithoutMutation()
{
    MatchState state = CreateState();
    CardMovementPort port = new(state);
    string before = state.ComputeFingerprint();

    CardMovementProcessResult badZone = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Clock));
    CardMovementProcessResult missingCard = port.Move(new CardMovementRequest(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("missing"),
        ChoiceZone.Hand,
        ChoiceZone.Trash));

    AssertFalse(badZone.IsSuccess, "bad zone failure");
    AssertFalse(missingCard.IsSuccess, "missing card failure");
    AssertContains(badZone.FailureReason, "not a player-owned zone", "bad zone reason");
    AssertContains(missingCard.FailureReason, "not in the match state", "missing card reason");
    AssertEqual(before, badZone.State.ComputeFingerprint(), "bad zone unchanged");
    AssertEqual(before, missingCard.State.ComputeFingerprint(), "missing card unchanged");
    AssertEqual(0, badZone.State.Events.Count, "bad zone events unchanged");
    AssertEqual(0, missingCard.State.Events.Count, "missing card events unchanged");
    return Task.CompletedTask;
}

Task MovementPayloadIsDeterministic()
{
    CardMovementRequest request = new(
        new HeadlessPlayerId(1),
        new HeadlessEntityId("p1-hand"),
        ChoiceZone.Hand,
        ChoiceZone.Trash,
        reason: "discard");
    CardMovementProcessResult first = new CardMovementPort(CreateState()).Move(request);
    CardMovementProcessResult second = new CardMovementPort(CreateState()).Move(request);

    AssertTrue(first.IsSuccess, "first success");
    AssertTrue(second.IsSuccess, "second success");
    AssertEqual(first.State.ComputeFingerprint(), second.State.ComputeFingerprint(), "state fingerprint");
    AssertEqual(FlattenEvent(first.MovementEvent!), FlattenEvent(second.MovementEvent!), "event payload");
    AssertEqual(first.EffectContext!.Values["toIndex"], second.EffectContext!.Values["toIndex"], "context to index");
    return Task.CompletedTask;
}

Task CardMovementPortFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "CardMovementPort.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    return Task.CompletedTask;
}

static MatchState CreateState()
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    MatchState state = MatchState.CreateInitial(new[] { p1, p2 });

    foreach (var card in new[]
    {
        ("p1-library", p1, ChoiceZone.Library),
        ("p1-hand", p1, ChoiceZone.Hand),
        ("p1-battle", p1, ChoiceZone.BattleArea),
        ("p1-trash", p1, ChoiceZone.Trash),
        ("p1-security", p1, ChoiceZone.Security),
        ("p2-hand", p2, ChoiceZone.Hand)
    })
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.Item1), new HeadlessEntityId($"def-{card.Item1}"), card.Item2))
            .PlaceCard(new HeadlessEntityId(card.Item1), card.Item3);
    }

    return state;
}

static string FlattenEvent(GameEvent gameEvent)
{
    return string.Join(
        "|",
        gameEvent.Sequence,
        gameEvent.Type,
        gameEvent.Message,
        string.Join(",", gameEvent.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")));
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

static void AssertMetadata(GameEvent gameEvent, string key, object? expected)
{
    if (!gameEvent.Metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"metadata: missing key '{key}'.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
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
