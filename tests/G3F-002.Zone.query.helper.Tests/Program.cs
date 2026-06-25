using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId P1LibraryTop = new("p1-library-top");
HeadlessEntityId P1LibraryBottom = new("p1-library-bottom");
HeadlessEntityId P1Trash = new("p1-trash");
HeadlessEntityId P1SecurityHidden = new("p1-security-hidden");
HeadlessEntityId P1SecurityFaceUp = new("p1-security-faceup");
HeadlessEntityId P1BattleRoot = new("p1-battle-root");
HeadlessEntityId P1SourceBottom = new("p1-source-bottom");
HeadlessEntityId P1SourceTop = new("p1-source-top");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3F-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS zone query roots are recorded", AsIsZoneQueryRootsAreRecorded),
    ("Library trash and security queries preserve zone order", LibraryTrashSecurityQueriesPreserveZoneOrder),
    ("Opponent library and security hide definitions by default", OpponentLibraryAndSecurityHideDefinitions),
    ("IncludeHidden exposes private zone definitions for engine use", IncludeHiddenExposesPrivateDefinitions),
    ("Trash query is public for opponent viewer", TrashQueryIsPublic),
    ("Digivolution source query uses root source order", DigivolutionSourceQueryUsesRootSourceOrder),
    ("Missing player viewer root or card returns failure", MissingInputsReturnFailure),
    ("Invalid zones return failure without throwing", InvalidZonesReturnFailure),
    ("G3F-002 source files stay inside zone query scope", SourceFilesStayInsideGoalScope),
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
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3F-002")
        ?? throw new InvalidOperationException("G3F-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Targeting", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "library trash security source zone query", "scope");
    AssertEqual("zone query helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "zone query", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3F-002_zone_query_helpers_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3F-001", Value(row, "blocked_until"), "prerequisite");
    AssertComplete("G3F-001_target_filtering_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsZoneQueryRootsAreRecorded()
{
    string selectCard = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"));
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));

    AssertContains(selectCard, "RootCardList()", "AS-IS root query");
    AssertContains(selectCard, "LibraryCards", "AS-IS library root");
    AssertContains(selectCard, "TrashCards", "AS-IS trash root");
    AssertContains(selectCard, "SecurityCards", "AS-IS security root");
    AssertContains(selectCard, "DigivolutionCards", "AS-IS source root enum");
    AssertContains(player, "LibraryCards", "AS-IS player library");
    AssertContains(player, "TrashCards", "AS-IS player trash");
    AssertContains(player, "SecurityCards", "AS-IS player security");
    AssertContains(cardSource, "DigivolutionCards.Contains(this)", "AS-IS source containment");
    return Task.CompletedTask;
}

Task LibraryTrashSecurityQueriesPreserveZoneOrder()
{
    MatchState state = CreateState();

    ZoneQueryResult library = ZoneQueryHelpers.Library(state, PlayerOne, PlayerOne);
    ZoneQueryResult trash = ZoneQueryHelpers.Trash(state, PlayerOne, PlayerOne);
    ZoneQueryResult security = ZoneQueryHelpers.Security(state, PlayerOne, PlayerOne);

    AssertTrue(library.IsSuccess, "library success");
    AssertTrue(trash.IsSuccess, "trash success");
    AssertTrue(security.IsSuccess, "security success");
    AssertSequence(new[] { P1LibraryTop.Value, P1LibraryBottom.Value }, CardIds(library), "library order");
    AssertSequence(new[] { P1Trash.Value }, CardIds(trash), "trash order");
    AssertSequence(new[] { P1SecurityHidden.Value, P1SecurityFaceUp.Value }, CardIds(security), "security order");
    AssertSequence(new[] { "0", "1" }, library.Cards.Select(card => card.Index.ToString()).ToArray(), "library indices");
    return Task.CompletedTask;
}

Task OpponentLibraryAndSecurityHideDefinitions()
{
    MatchState state = CreateState();

    ZoneQueryResult library = ZoneQueryHelpers.Library(state, PlayerOne, PlayerTwo);
    ZoneQueryResult security = ZoneQueryHelpers.Security(state, PlayerOne, PlayerTwo);

    AssertTrue(library.IsSuccess, "library success");
    AssertTrue(security.IsSuccess, "security success");
    AssertEqual(2, library.Cards.Count, "library count remains visible");
    AssertTrue(library.Cards.All(card => !card.IsVisible && !card.DefinitionId.HasValue), "opponent library definitions hidden");
    AssertEqual(P1SecurityHidden, security.Cards[0].CardId, "hidden security card id");
    AssertFalse(security.Cards[0].IsVisible, "face-down security hidden");
    AssertNull(security.Cards[0].DefinitionId, "face-down security definition hidden");
    AssertTrue(security.Cards[1].IsVisible, "face-up security visible");
    AssertEqual(new HeadlessEntityId("DEF-p1-security-faceup"), security.Cards[1].DefinitionId, "face-up security definition");
    return Task.CompletedTask;
}

Task IncludeHiddenExposesPrivateDefinitions()
{
    MatchState state = CreateState();

    ZoneQueryResult library = ZoneQueryHelpers.Library(state, PlayerOne, PlayerTwo, includeHidden: true);
    ZoneQueryResult security = ZoneQueryHelpers.Security(state, PlayerOne, PlayerTwo, includeHidden: true);

    AssertTrue(library.IsSuccess, "library success");
    AssertTrue(security.IsSuccess, "security success");
    AssertTrue(library.Cards.All(card => card.IsVisible && card.DefinitionId.HasValue), "include hidden library definitions");
    AssertTrue(security.Cards.All(card => card.IsVisible && card.DefinitionId.HasValue), "include hidden security definitions");
    AssertEqual(2, ValueAsInt(library.Values, "visibleCardCount"), "visible count value");
    return Task.CompletedTask;
}

Task TrashQueryIsPublic()
{
    MatchState state = CreateState();

    ZoneQueryResult trash = ZoneQueryHelpers.Trash(state, PlayerOne, PlayerTwo);

    AssertTrue(trash.IsSuccess, "trash success");
    AssertEqual(P1Trash, trash.Cards.Single().CardId, "trash card id");
    AssertTrue(trash.Cards.Single().IsVisible, "trash is public");
    AssertEqual(new HeadlessEntityId("DEF-p1-trash"), trash.Cards.Single().DefinitionId, "trash definition visible");
    return Task.CompletedTask;
}

Task DigivolutionSourceQueryUsesRootSourceOrder()
{
    MatchState state = CreateState();

    ZoneQueryResult result = ZoneQueryHelpers.Sources(state, P1BattleRoot, PlayerTwo);

    AssertTrue(result.IsSuccess, "sources success");
    AssertSequence(new[] { P1SourceBottom.Value, P1SourceTop.Value }, CardIds(result), "source order");
    AssertTrue(result.Cards.All(card => card.Zone == ChoiceZone.DigivolutionCards), "source zone");
    AssertTrue(result.Cards.All(card => card.IsVisible && card.DefinitionId.HasValue), "sources public");
    AssertEqual(P1BattleRoot.Value, ValueAsString(result.Values, "rootCardId"), "root card id value");
    return Task.CompletedTask;
}

Task MissingInputsReturnFailure()
{
    MatchState state = CreateState();
    HeadlessPlayerId missingPlayer = new(99);
    MatchState missingCardState = state.GetPlayer(PlayerOne)
        .WithZone(ChoiceZone.Library, new[] { new HeadlessEntityId("missing-card") }) is PlayerState player
        ? new MatchState(new[] { player, state.GetPlayer(PlayerTwo) }, state.CardInstances)
        : state;

    AssertFalse(ZoneQueryHelpers.Library(state, missingPlayer, PlayerOne).IsSuccess, "missing player");
    AssertFalse(ZoneQueryHelpers.Library(state, PlayerOne, missingPlayer).IsSuccess, "missing viewer");
    AssertFalse(ZoneQueryHelpers.Sources(state, new HeadlessEntityId("missing-root"), PlayerOne).IsSuccess, "missing root");
    AssertFalse(ZoneQueryHelpers.Library(missingCardState, PlayerOne, PlayerOne).IsSuccess, "missing referenced card");
    return Task.CompletedTask;
}

Task InvalidZonesReturnFailure()
{
    MatchState state = CreateState();

    ZoneQueryResult none = ZoneQueryHelpers.Query(new ZoneQueryRequest(state, PlayerOne, PlayerOne, ChoiceZone.None));
    ZoneQueryResult custom = ZoneQueryHelpers.Query(new ZoneQueryRequest(state, PlayerOne, PlayerOne, ChoiceZone.Custom));
    ZoneQueryResult sourceWithoutRoot = ZoneQueryHelpers.Query(new ZoneQueryRequest(state, PlayerOne, PlayerOne, ChoiceZone.DigivolutionCards));

    AssertFalse(none.IsSuccess, "none zone");
    AssertFalse(custom.IsSuccess, "custom zone");
    AssertFalse(sourceWithoutRoot.IsSuccess, "source without root");
    AssertContains(none.Reason, "not a concrete", "none reason");
    AssertContains(sourceWithoutRoot.Reason, "requires a root card id", "source reason");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helperPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "ZoneQueryHelpers.cs");
    string testPath = Path.Combine(root, "tests", "G3F-002.Zone.query.helper.Tests", "Program.cs");
    string helper = File.ReadAllText(helperPath);
    string test = File.ReadAllText(testPath);

    AssertContains(helper, "public static class ZoneQueryHelpers", "helper public API");
    AssertContains(helper, "Library(", "library API");
    AssertContains(helper, "Trash(", "trash API");
    AssertContains(helper, "Security(", "security API");
    AssertContains(helper, "Sources(", "source API");
    AssertDoesNotContain(helper, "TODO", "no todo placeholder");
    AssertDoesNotContain(helper, "UnityEngine", "no Unity dependency");
    AssertDoesNotContain(helper, "DCGO.Assets", "no original asset dependency");
    AssertDoesNotContain(helper, "TargetFilterHelpers.", "G3F-002 helper does not depend on target filtering internals");
    AssertContains(test, "ZoneQueryHelpers.", "G3F-002 tests call the zone query API");
    return Task.CompletedTask;
}

MatchState CreateState()
{
    PlayerState playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.Library, new[] { P1LibraryTop, P1LibraryBottom })
        .WithZone(ChoiceZone.Trash, new[] { P1Trash })
        .WithZone(ChoiceZone.Security, new[] { P1SecurityHidden, P1SecurityFaceUp })
        .WithZone(ChoiceZone.BattleArea, new[] { P1BattleRoot });
    PlayerState playerTwo = new(PlayerTwo);

    var instances = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [P1LibraryTop] = Instance(P1LibraryTop, PlayerOne),
        [P1LibraryBottom] = Instance(P1LibraryBottom, PlayerOne),
        [P1Trash] = Instance(P1Trash, PlayerOne, isFaceUp: true),
        [P1SecurityHidden] = Instance(P1SecurityHidden, PlayerOne),
        [P1SecurityFaceUp] = Instance(P1SecurityFaceUp, PlayerOne, isFaceUp: true),
        [P1BattleRoot] = Instance(P1BattleRoot, PlayerOne, isFaceUp: true, sources: new[] { P1SourceBottom, P1SourceTop }),
        [P1SourceBottom] = Instance(P1SourceBottom, PlayerOne, isFaceUp: true),
        [P1SourceTop] = Instance(P1SourceTop, PlayerOne, isFaceUp: true),
    };

    return new MatchState(new[] { playerOne, playerTwo }, instances);
}

CardInstanceState Instance(
    HeadlessEntityId id,
    HeadlessPlayerId owner,
    bool isFaceUp = false,
    IReadOnlyList<HeadlessEntityId>? sources = null)
{
    return new CardInstanceState(
        id,
        new HeadlessEntityId($"DEF-{id.Value}"),
        owner,
        IsFaceUp: isFaceUp,
        SourceIds: sources);
}

static string[] CardIds(ZoneQueryResult result)
{
    return result.Cards.Select(card => card.CardId.Value).ToArray();
}

static string ValueAsString(IReadOnlyDictionary<string, object?> values, string key)
{
    return values.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
}

static int ValueAsInt(IReadOnlyDictionary<string, object?> values, string key)
{
    return values.TryGetValue(key, out object? value) && value is int intValue
        ? intValue
        : throw new InvalidOperationException($"Value '{key}' was not an int.");
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]);
    var rows = new List<Dictionary<string, string>>();
    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] cells = SplitCsvLine(line);
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length && index < cells.Length; index++)
        {
            row[headers[index]] = cells[index];
        }

        rows.Add(row);
    }

    return rows;
}

static string[] SplitCsvLine(string line)
{
    var cells = new List<string>();
    var current = new System.Text.StringBuilder();
    bool quoted = false;
    for (int index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (quoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                quoted = !quoted;
            }
        }
        else if (ch == ',' && !quoted)
        {
            cells.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    cells.Add(current.ToString());
    return cells.ToArray();
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Required predecessor result document is missing: {fileName}");
    }

    string text = File.ReadAllText(path);
    AssertContains(text, "COMPLETE", $"predecessor complete: {fileName}");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected true: {message}");
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException($"Expected false: {message}");
    }
}

static void AssertNull(object? value, string message)
{
    if (value is not null)
    {
        throw new InvalidOperationException($"Expected null for {message}, actual '{value}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}' but got '{actual}' for {message}.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"Sequence mismatch for {message}. Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text to contain '{expected}' for {message}.");
    }
}

static void AssertDoesNotContain(string text, string forbidden, string message)
{
    if (text.Contains(forbidden, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text not to contain '{forbidden}' for {message}.");
    }
}

static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        string marker = Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv");
        if (File.Exists(marker))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate repository root.");
}
