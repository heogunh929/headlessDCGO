using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2D-004 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS digivolution source references are recorded", AsIsSourceStackReferencesAreRecorded),
    ("Source stack port attaches top sources in stable order", SourceStackAttachesTopInOrder),
    ("Source stack port attaches bottom sources after existing sources", SourceStackAttachesBottomInOrder),
    ("Source stack port detaches sources and preserves remaining order", SourceStackDetachesAndPreservesOrder),
    ("Source stack port repositions existing sources without duplicates", SourceStackRepositionsWithoutDuplicates),
    ("Source stack port moves source from another target stack", SourceStackMovesSourceFromAnotherTarget),
    ("Source stack port emits event trace and effect context", SourceStackEmitsEventTraceAndContext),
    ("Source stack port rejects invalid source mutations without changing state", SourceStackRejectsInvalidMutationsWithoutChangingState),
    ("Source stack port emits deterministic payload and source files contain no placeholder markers", SourceStackPayloadIsDeterministicAndFilesHaveNoMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2D-004")
        ?? throw new InvalidOperationException("G2D-004 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("CardController", Value(row, "area"), "area");
    AssertEqual("source stack mutation", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "진화원 attach detach", "scope");
    AssertContains(Value(row, "unit_test_scope"), "source attach detach", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2D-004_digivolution_source_attach_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2D-003", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2D-003_card_suspend_reveal_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2D-003 completion marker");
    return Task.CompletedTask;
}

Task AsIsSourceStackReferencesAreRecorded()
{
    string permanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Permanent.cs"));
    string cardObjectController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardObjectController.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(permanent, "public List<CardSource> cardSources", "AS-IS source stack");
    AssertContains(permanent, "DigivolutionCards => cardSources.Filter", "AS-IS digivolution cards");
    AssertContains(permanent, "public void AddCardSource", "AS-IS add source");
    AssertContains(permanent, "AddDigivolutionCardsTop", "AS-IS attach top");
    AssertContains(permanent, "this.cardSources.Insert(1, addedDigivolutionCard)", "AS-IS top source insertion");
    AssertContains(permanent, "AddDigivolutionCardsBottom", "AS-IS attach bottom");
    AssertContains(permanent, "cardSources.Add(addedDigivolutionCard)", "AS-IS bottom source insertion");
    AssertContains(cardObjectController, "permanent.RemoveCardSource(cardSource)", "AS-IS remove source from all areas");
    AssertContains(cardController, "ReturnToLibraryBottomDigivolutionCardsClass", "AS-IS detach source flow");
    return Task.CompletedTask;
}

Task SourceStackAttachesTopInOrder()
{
    SourceStackProcessResult result = new DigivolutionSourceStackPort(CreateState()).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("src-a", "src-b"),
        "evolve-top"));

    AssertTrue(result.IsSuccess, "success");
    AssertTrue(result.DidMutate, "mutated");
    AssertSequence(Array.Empty<string>(), result.Before!.SourceIds.Select(id => id.Value).ToArray(), "before sources");
    AssertSequence(new[] { "src-a", "src-b" }, result.After!.SourceIds.Select(id => id.Value).ToArray(), "after sources");
    AssertEqual(ChoiceZone.BattleArea, result.After.Zone, "target zone preserved");
    return Task.CompletedTask;
}

Task SourceStackAttachesBottomInOrder()
{
    MatchState state = Attach(CreateState(), SourceStackMutation.AttachTop, "p1-target", "src-a");
    SourceStackProcessResult result = new DigivolutionSourceStackPort(state).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachBottom,
        Ids("src-b", "src-c"),
        "evolve-bottom"));

    AssertTrue(result.IsSuccess, "success");
    AssertSequence(new[] { "src-a", "src-b", "src-c" }, result.After!.SourceIds.Select(id => id.Value).ToArray(), "after sources");
    return Task.CompletedTask;
}

Task SourceStackDetachesAndPreservesOrder()
{
    MatchState state = Attach(CreateState(), SourceStackMutation.AttachBottom, "p1-target", "src-a", "src-b", "src-c");
    SourceStackProcessResult result = new DigivolutionSourceStackPort(state).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.Detach,
        Ids("src-b"),
        "trash-source"));

    AssertTrue(result.IsSuccess, "success");
    AssertSequence(new[] { "src-a", "src-c" }, result.After!.SourceIds.Select(id => id.Value).ToArray(), "remaining sources");
    AssertSequence(new[] { "src-a", "src-b", "src-c" }, result.Before!.SourceIds.Select(id => id.Value).ToArray(), "before sources");
    return Task.CompletedTask;
}

Task SourceStackRepositionsWithoutDuplicates()
{
    MatchState state = Attach(CreateState(), SourceStackMutation.AttachBottom, "p1-target", "src-a", "src-b", "src-c");
    SourceStackProcessResult result = new DigivolutionSourceStackPort(state).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("src-c"),
        "move-existing-to-top"));

    AssertTrue(result.IsSuccess, "success");
    AssertSequence(new[] { "src-c", "src-a", "src-b" }, result.After!.SourceIds.Select(id => id.Value).ToArray(), "repositioned sources");
    AssertEqual(3, result.After.SourceIds.Distinct().Count(), "unique sources");
    return Task.CompletedTask;
}

Task SourceStackMovesSourceFromAnotherTarget()
{
    MatchState state = Attach(CreateState(), SourceStackMutation.AttachBottom, "p1-target", "src-a", "src-b");
    state = Attach(state, SourceStackMutation.AttachBottom, "p1-other", "src-c");
    SourceStackProcessResult result = new DigivolutionSourceStackPort(state).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("src-c"),
        "move-between-stacks"));

    AssertTrue(result.IsSuccess, "success");
    CardIdentityAdapter identity = new(result.State);
    AssertSequence(new[] { "src-c", "src-a", "src-b" }, identity.Bind(new HeadlessEntityId("p1-target")).SourceIds.Select(id => id.Value).ToArray(), "target sources");
    AssertSequence(Array.Empty<string>(), identity.Bind(new HeadlessEntityId("p1-other")).SourceIds.Select(id => id.Value).ToArray(), "other sources");
    return Task.CompletedTask;
}

Task SourceStackEmitsEventTraceAndContext()
{
    EngineTrace trace = new();
    SourceStackProcessResult result = new DigivolutionSourceStackPort(CreateState(), trace).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachBottom,
        Ids("src-a", "src-b"),
        "dna-stack"));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(1L, result.State.Version, "version");
    AssertEqual(GameEventType.StateChanged, result.SourceEvent!.Type, "event type");
    AssertMetadata(result.SourceEvent, "mutation", SourceStackMutation.AttachBottom.ToString());
    AssertMetadata(result.SourceEvent, "targetCardId", "p1-target");
    AssertMetadata(result.SourceEvent, "targetDefinitionId", "def-p1-target");
    AssertMetadata(result.SourceEvent, "ownerId", 1);
    AssertMetadata(result.SourceEvent, "sourceCount", 2);
    AssertMetadata(result.SourceEvent, "reason", "dna-stack");

    AssertSequence(new[] { "src-a", "src-b" }, ((string[])result.SourceEvent.Metadata["sourceIds"]!).ToArray(), "event source ids");
    AssertSequence(new[] { "src-a", "src-b" }, ((string[])result.SourceEvent.Metadata["afterSourceIds"]!).ToArray(), "event after source ids");

    TraceEvent traceEvent = trace.Snapshot().Single();
    AssertEqual("card.source", traceEvent.Category, "trace category");
    AssertEqual(result.SourceEvent.Message, traceEvent.Message, "trace message");
    AssertEqual(result.SourceEvent.Metadata["targetCardId"], traceEvent.Metadata["targetCardId"], "trace target");

    EffectContext context = result.EffectContext!;
    AssertEqual(new HeadlessEntityId("p1-target"), context.SourceEntityId, "context source entity");
    AssertSequence(new[] { "p1-target", "src-a", "src-b" }, context.TargetEntityIds.Select(id => id.Value).ToArray(), "context targets");
    AssertEqual(result.SourceEvent.Metadata["mutation"], context.Values["mutation"], "context mutation");
    return Task.CompletedTask;
}

Task SourceStackRejectsInvalidMutationsWithoutChangingState()
{
    MatchState state = CreateState();
    string before = state.ComputeFingerprint();
    DigivolutionSourceStackPort port = new(state);

    SourceStackProcessResult self = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("p1-target")));
    SourceStackProcessResult crossOwner = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("p2-src")));
    SourceStackProcessResult missing = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("missing")));
    SourceStackProcessResult duplicate = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachTop,
        Ids("src-a", "src-a")));
    SourceStackProcessResult token = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("token-target"),
        SourceStackMutation.AttachTop,
        Ids("src-a")));
    SourceStackProcessResult detachMissing = port.Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.Detach,
        Ids("src-a")));

    foreach (SourceStackProcessResult result in new[] { self, crossOwner, missing, duplicate, token, detachMissing })
    {
        AssertFalse(result.IsSuccess, "failure expected");
        AssertFalse(result.DidMutate, "failure did not mutate");
        AssertEqual(before, result.State.ComputeFingerprint(), "fingerprint unchanged");
        AssertEqual(0, result.State.Events.Count, "events unchanged");
    }

    AssertContains(self.FailureReason, "own digivolution source", "self failure");
    AssertContains(crossOwner.FailureReason, "owned by player", "cross owner failure");
    AssertContains(missing.FailureReason, "not in the match state", "missing failure");
    AssertContains(duplicate.FailureReason, "duplicates", "duplicate failure");
    AssertContains(token.FailureReason, "Token card", "token failure");
    AssertContains(detachMissing.FailureReason, "not attached", "detach failure");
    return Task.CompletedTask;
}

Task SourceStackPayloadIsDeterministicAndFilesHaveNoMarkers()
{
    SourceStackMutationRequest request = new(
        new HeadlessEntityId("p1-target"),
        SourceStackMutation.AttachBottom,
        Ids("src-a", "src-b"),
        "deterministic");
    SourceStackProcessResult first = new DigivolutionSourceStackPort(CreateState()).Mutate(request);
    SourceStackProcessResult second = new DigivolutionSourceStackPort(CreateState()).Mutate(request);

    AssertTrue(first.IsSuccess, "first success");
    AssertTrue(second.IsSuccess, "second success");
    AssertEqual(first.State.ComputeFingerprint(), second.State.ComputeFingerprint(), "state fingerprint");
    AssertEqual(FlattenEvent(first.SourceEvent!), FlattenEvent(second.SourceEvent!), "event payload");

    string sourceFile = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "DigivolutionSourceStackPort.cs");
    AssertFalse(File.ReadAllText(sourceFile).Contains("TODO", StringComparison.OrdinalIgnoreCase), "source stack port TODO");
    return Task.CompletedTask;
}

static MatchState CreateState()
{
    HeadlessPlayerId p1 = new(1);
    HeadlessPlayerId p2 = new(2);
    MatchState state = MatchState.CreateInitial(new[] { p1, p2 });

    foreach (var card in new[]
    {
        ("p1-target", p1, ChoiceZone.BattleArea, false),
        ("p1-other", p1, ChoiceZone.BattleArea, false),
        ("src-a", p1, ChoiceZone.Hand, false),
        ("src-b", p1, ChoiceZone.Hand, false),
        ("src-c", p1, ChoiceZone.Trash, false),
        ("p2-src", p2, ChoiceZone.Hand, false),
        ("token-target", p1, ChoiceZone.BattleArea, true)
    })
    {
        CardInstanceState instance = new(new HeadlessEntityId(card.Item1), new HeadlessEntityId($"def-{card.Item1}"), card.Item2);
        if (card.Item4)
        {
            instance = instance.SetFlag(CardIdentityAdapter.TokenFlagKey, true);
        }

        state = state
            .WithCardInstance(instance)
            .PlaceCard(new HeadlessEntityId(card.Item1), card.Item3);
    }

    return state;
}

static MatchState Attach(
    MatchState state,
    SourceStackMutation mutation,
    string targetId,
    params string[] sourceIds)
{
    SourceStackProcessResult result = new DigivolutionSourceStackPort(state).Mutate(new SourceStackMutationRequest(
        new HeadlessEntityId(targetId),
        mutation,
        Ids(sourceIds)));

    if (!result.IsSuccess)
    {
        throw new InvalidOperationException(result.FailureReason);
    }

    return result.State;
}

static IReadOnlyList<HeadlessEntityId> Ids(params string[] values)
{
    return values.Select(value => new HeadlessEntityId(value)).ToArray();
}

static string FlattenEvent(GameEvent gameEvent)
{
    return string.Join(
        "|",
        gameEvent.Sequence,
        gameEvent.Type,
        gameEvent.Message,
        string.Join(",", gameEvent.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={FormatValue(pair.Value)}")));
}

static string FormatValue(object? value)
{
    return value is IEnumerable<string> strings
        ? string.Join("/", strings)
        : value?.ToString() ?? "<null>";
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
