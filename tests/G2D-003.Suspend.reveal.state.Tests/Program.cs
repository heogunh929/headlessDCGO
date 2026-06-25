using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2D-003 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS suspend reveal references are recorded", AsIsSuspendRevealReferencesAreRecorded),
    ("Card state mutation port suspends and unsuspends without moving card", StateMutationSuspendsAndUnsuspends),
    ("Card state mutation port reveals and hides face-up state", StateMutationRevealsAndHides),
    ("Card state mutation port emits StateChanged event payload", StateMutationEmitsEventPayload),
    ("Card state mutation port records trace and effect context", StateMutationRecordsTraceAndEffectContext),
    ("Card state mutation port treats repeated same state as no-op", StateMutationNoOpDoesNotAppendEvents),
    ("Card state mutation port returns failure without mutating state for invalid card", StateMutationRejectsInvalidCardWithoutMutation),
    ("Card state mutation port emits deterministic payload for repeated input", StateMutationPayloadIsDeterministic),
    ("G2D-003 source files contain no placeholder markers", CardStateMutationPortFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2D-003")
        ?? throw new InvalidOperationException("G2D-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("CardController", Value(row, "area"), "area");
    AssertEqual("card state mutation", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "suspend unsuspend reveal face-up", "scope");
    AssertContains(Value(row, "unit_test_scope"), "suspend reveal", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2D-003_card_suspend_reveal_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2D-002", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2D-002_card_zone_movement_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2D-002 completion marker");
    return Task.CompletedTask;
}

Task AsIsSuspendRevealReferencesAreRecorded()
{
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));

    AssertContains(cardSource, "public bool IsFlipped", "AS-IS face-down flag");
    AssertContains(cardSource, "public bool IsFaceUp", "AS-IS face-up property");
    AssertContains(cardSource, "public void SetFace", "AS-IS reveal");
    AssertContains(cardSource, "public void SetReverse", "AS-IS hide");
    AssertContains(cardSource, "IsBeingRevealed", "AS-IS temporary reveal flag");
    AssertContains(cardController, "public class SuspendPermanentsClass", "AS-IS suspend class");
    AssertContains(cardController, "permanent.IsSuspended = true", "AS-IS suspend mutation");
    AssertContains(cardController, "public class IUnsuspendPermanents", "AS-IS unsuspend class");
    AssertContains(cardController, "permanent.IsSuspended = false", "AS-IS unsuspend mutation");
    AssertContains(cardController, "public IEnumerator FlipFaceUp", "AS-IS security reveal flow");
    AssertContains(turnStateMachine, "permanent.IsSuspended", "AS-IS unsuspend phase state");
    return Task.CompletedTask;
}

Task StateMutationSuspendsAndUnsuspends()
{
    MatchState state = CreateState();
    CardStateMutationPort port = new(state);

    CardStateMutationProcessResult suspended = port.Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-battle"),
        CardStateMutation.Suspend,
        "attack"));

    AssertTrue(suspended.IsSuccess, "suspend success");
    AssertTrue(suspended.DidMutate, "suspend mutated");
    AssertFalse(suspended.Before!.IsSuspended, "before suspended");
    AssertTrue(suspended.After!.IsSuspended, "after suspended");
    AssertEqual(ChoiceZone.BattleArea, suspended.After.Zone, "zone preserved");
    AssertSequence(new[] { "p1-source" }, suspended.After.SourceIds.Select(id => id.Value).ToArray(), "sources preserved");

    CardStateMutationProcessResult unsuspended = new CardStateMutationPort(suspended.State).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-battle"),
        CardStateMutation.Unsuspend,
        "unsuspend-phase"));

    AssertTrue(unsuspended.IsSuccess, "unsuspend success");
    AssertTrue(unsuspended.DidMutate, "unsuspend mutated");
    AssertTrue(unsuspended.Before!.IsSuspended, "unsuspend before suspended");
    AssertFalse(unsuspended.After!.IsSuspended, "unsuspend after suspended");
    AssertEqual(2L, unsuspended.State.Version, "version increments twice");
    AssertEqual(2, unsuspended.State.Events.Count, "two state events");
    return Task.CompletedTask;
}

Task StateMutationRevealsAndHides()
{
    MatchState state = CreateState();
    CardStateMutationProcessResult revealed = new CardStateMutationPort(state).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-security"),
        CardStateMutation.Reveal,
        "security-reveal"));

    AssertTrue(revealed.IsSuccess, "reveal success");
    AssertTrue(revealed.DidMutate, "reveal mutated");
    AssertFalse(revealed.Before!.IsFaceUp, "before face up");
    AssertTrue(revealed.After!.IsFaceUp, "after face up");
    AssertEqual(ChoiceZone.Security, revealed.After.Zone, "security zone preserved");

    CardStateMutationProcessResult hidden = new CardStateMutationPort(revealed.State).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-security"),
        CardStateMutation.Hide,
        "security-hide"));

    AssertTrue(hidden.IsSuccess, "hide success");
    AssertTrue(hidden.DidMutate, "hide mutated");
    AssertTrue(hidden.Before!.IsFaceUp, "hide before face up");
    AssertFalse(hidden.After!.IsFaceUp, "hide after face up");
    AssertEqual(ChoiceZone.Security, hidden.After.Zone, "zone preserved after hide");
    return Task.CompletedTask;
}

Task StateMutationEmitsEventPayload()
{
    CardStateMutationProcessResult result = new CardStateMutationPort(CreateState()).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-battle"),
        CardStateMutation.Suspend,
        "attack"));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(1L, result.State.Version, "state version");
    AssertEqual(result.State.Events.Single(), result.StateEvent, "state event appended");
    AssertEqual(GameEventType.StateChanged, result.StateEvent!.Type, "event type");
    AssertEqual("Card state changed: p1-battle Suspend", result.StateEvent.Message, "message");
    AssertMetadata(result.StateEvent, "mutation", CardStateMutation.Suspend.ToString());
    AssertMetadata(result.StateEvent, "cardId", "p1-battle");
    AssertMetadata(result.StateEvent, "cardDefinitionId", "def-p1-battle");
    AssertMetadata(result.StateEvent, "cardOwnerId", 1);
    AssertMetadata(result.StateEvent, "zone", ChoiceZone.BattleArea.ToString());
    AssertMetadata(result.StateEvent, "zoneOwnerId", 1);
    AssertMetadata(result.StateEvent, "zoneIndex", 0);
    AssertMetadata(result.StateEvent, "beforeSuspended", false);
    AssertMetadata(result.StateEvent, "afterSuspended", true);
    AssertMetadata(result.StateEvent, "beforeFaceUp", false);
    AssertMetadata(result.StateEvent, "afterFaceUp", false);
    AssertMetadata(result.StateEvent, "reason", "attack");
    return Task.CompletedTask;
}

Task StateMutationRecordsTraceAndEffectContext()
{
    EngineTrace trace = new();
    CardStateMutationProcessResult result = new CardStateMutationPort(CreateState(), trace).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-security"),
        CardStateMutation.Reveal,
        "security-check"));

    AssertTrue(result.IsSuccess, "success");
    TraceEvent traceEvent = trace.Snapshot().Single();
    AssertEqual("card.state", traceEvent.Category, "trace category");
    AssertEqual(result.StateEvent!.Message, traceEvent.Message, "trace message");
    AssertEqual(result.StateEvent.Metadata["cardId"], traceEvent.Metadata["cardId"], "trace card id");
    AssertEqual(result.StateEvent.Metadata["afterFaceUp"], traceEvent.Metadata["afterFaceUp"], "trace face up");

    EffectContext context = result.EffectContext!;
    AssertEqual(new HeadlessPlayerId(1), context.SourcePlayerId, "context source player");
    AssertEqual(new HeadlessPlayerId(1), context.OwnerPlayerId, "context owner player");
    AssertEqual(new HeadlessEntityId("p1-security"), context.SourceEntityId, "context source entity");
    AssertEqual(new HeadlessEntityId("p1-security"), context.TriggerEntityId, "context trigger entity");
    AssertSequence(new[] { "p1-security" }, context.TargetEntityIds.Select(id => id.Value).ToArray(), "context targets");
    AssertEqual(result.StateEvent.Metadata["mutation"], context.Values["mutation"], "context mutation");
    AssertEqual("security-check", context.Values["reason"], "context reason");
    return Task.CompletedTask;
}

Task StateMutationNoOpDoesNotAppendEvents()
{
    CardStateMutationProcessResult suspended = new CardStateMutationPort(CreateState()).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-battle"),
        CardStateMutation.Suspend));
    string before = suspended.State.ComputeFingerprint();

    CardStateMutationProcessResult repeated = new CardStateMutationPort(suspended.State).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("p1-battle"),
        CardStateMutation.Suspend,
        "repeat"));

    AssertTrue(repeated.IsSuccess, "no-op success");
    AssertFalse(repeated.DidMutate, "no-op did not mutate");
    AssertEqual(null, repeated.StateEvent, "no event");
    AssertEqual(null, repeated.EffectContext, "no context");
    AssertEqual(before, repeated.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertEqual(1, repeated.State.Events.Count, "event count unchanged");
    return Task.CompletedTask;
}

Task StateMutationRejectsInvalidCardWithoutMutation()
{
    MatchState state = CreateState();
    string before = state.ComputeFingerprint();

    CardStateMutationProcessResult result = new CardStateMutationPort(state).Mutate(new CardStateMutationRequest(
        new HeadlessEntityId("missing"),
        CardStateMutation.Reveal));

    AssertFalse(result.IsSuccess, "failure");
    AssertFalse(result.DidMutate, "failure did not mutate");
    AssertContains(result.FailureReason, "not in the match state", "failure reason");
    AssertEqual(before, result.State.ComputeFingerprint(), "fingerprint unchanged");
    AssertEqual(0, result.State.Events.Count, "events unchanged");
    return Task.CompletedTask;
}

Task StateMutationPayloadIsDeterministic()
{
    CardStateMutationRequest request = new(
        new HeadlessEntityId("p1-security"),
        CardStateMutation.Reveal,
        "security-reveal");
    CardStateMutationProcessResult first = new CardStateMutationPort(CreateState()).Mutate(request);
    CardStateMutationProcessResult second = new CardStateMutationPort(CreateState()).Mutate(request);

    AssertTrue(first.IsSuccess, "first success");
    AssertTrue(second.IsSuccess, "second success");
    AssertEqual(first.State.ComputeFingerprint(), second.State.ComputeFingerprint(), "state fingerprint");
    AssertEqual(FlattenEvent(first.StateEvent!), FlattenEvent(second.StateEvent!), "event payload");
    AssertEqual(first.EffectContext!.Values["afterFaceUp"], second.EffectContext!.Values["afterFaceUp"], "context face up");
    return Task.CompletedTask;
}

Task CardStateMutationPortFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "CardStateMutationPort.cs")
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
        ("p1-battle", p1, ChoiceZone.BattleArea),
        ("p1-source", p1, ChoiceZone.BattleArea),
        ("p1-security", p1, ChoiceZone.Security),
        ("p1-hand", p1, ChoiceZone.Hand),
        ("p2-battle", p2, ChoiceZone.BattleArea)
    })
    {
        state = state
            .WithCardInstance(new CardInstanceState(new HeadlessEntityId(card.Item1), new HeadlessEntityId($"def-{card.Item1}"), card.Item2))
            .PlaceCard(new HeadlessEntityId(card.Item1), card.Item3);
    }

    CardIdentityAdapter adapter = new CardIdentityAdapter(state)
        .AttachSource(new HeadlessEntityId("p1-battle"), new HeadlessEntityId("p1-source"));
    return adapter.State;
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
