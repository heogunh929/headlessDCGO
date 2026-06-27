using System.Collections;
using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId SourceId = new("source-effect");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3K-001 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS selection effect references are recorded", AsIsSelectionEffectReferencesAreRecorded),
    ("Card choice request preserves effect selection contract", CardChoiceRequestPreservesContract),
    ("Permanent choice request uses battle area selectable candidates", PermanentChoiceUsesBattleArea),
    ("Count choice resolves through scripted provider into effect context", CountChoiceResolvesIntoContext),
    ("Selected card ids are written into a new effect context", SelectedIdsAreWrittenIntoContext),
    ("Allowed skip is recorded without selected ids", AllowedSkipIsRecorded),
    ("Illegal skip fails without mutating effect context", IllegalSkipFailsWithoutContextMutation),
    ("Provider validation failure returns explicit effect choice failure", ProviderValidationFailureReturnsExplicitFailure),
    ("Assets facade delegates and source files stay inside G3K scope", AssetsFacadeAndSourceScope),
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

Task GoalRowAndPredecessorsAreSatisfied()
{
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3K-001")
        ?? throw new InvalidOperationException("G3K-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Selection", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "effect", "scope");
    AssertEqual("effect choice helpers", Value(row, "deliverables"), "deliverables");
    AssertEqual("effect choice 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3K-001_effect_selection_helpers_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertContains(Value(row, "blocked_until"), "G3J-002", "predecessor G3J-002");
    AssertContains(Value(row, "blocked_until"), "G1E-005", "predecessor G1E-005");
    AssertComplete("G3J-002_permanent_effect_factory_binding_unit_test_results.md");
    AssertComplete("G1E-005_choice_pause_resume_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsSelectionEffectReferencesAreRecorded()
{
    string selectCard = ReadAsIs("SelectCardEffect.cs");
    string selectPermanent = ReadAsIs("SelectPermanentEffect.cs");
    string selectCount = ReadAsIs("SelectCountEffect.cs");

    AssertContains(selectCard, "public void SetUp", "AS-IS SelectCard SetUp");
    AssertContains(selectCard, "Mode mode", "AS-IS card select mode");
    AssertContains(selectCard, "_canNoSelect", "AS-IS card no-select");
    AssertContains(selectPermanent, "Func<Permanent, bool> canTargetCondition", "AS-IS permanent target condition");
    AssertContains(selectPermanent, "_targetPermanents", "AS-IS permanent target list");
    AssertContains(selectCount, "SetCandidates", "AS-IS count candidate override");
    AssertContains(selectCount, "_maxCount", "AS-IS count max");
    return Task.CompletedTask;
}

Task CardChoiceRequestPreservesContract()
{
    ChoiceRequest request = EffectChoiceHelpers.CreateCardRequest(
        PlayerOne,
        "Choose one card",
        minCount: 1,
        maxCount: 2,
        canSkip: false,
        ChoiceZone.Trash,
        new[]
        {
            EffectChoiceHelpers.Candidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Trash, ownerId: PlayerOne),
            EffectChoiceHelpers.Candidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Trash, ownerId: PlayerOne),
            EffectChoiceHelpers.Candidate(new HeadlessEntityId("card-c"), "Card C", ChoiceZone.Trash, isSelectable: false, ownerId: PlayerTwo),
        });

    AssertEqual(ChoiceType.Card, request.Type, "type");
    AssertEqual(PlayerOne, request.PlayerId, "player");
    AssertEqual("Choose one card", request.Message, "message");
    AssertEqual(1, request.MinCount, "min count");
    AssertEqual(2, request.MaxCount, "max count");
    AssertFalse(request.CanSkip, "can skip");
    AssertEqual(ChoiceZone.Trash, request.SourceZone, "source zone");
    AssertSequence(new[] { "card-a", "card-b", "card-c" }, request.Candidates.Select(candidate => candidate.Id.Value).ToArray(), "candidate ids");
    AssertSequence(new[] { "card-a", "card-b" }, request.SelectableCandidates.Select(candidate => candidate.Id.Value).ToArray(), "selectable ids");
    return Task.CompletedTask;
}

Task PermanentChoiceUsesBattleArea()
{
    ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(
        PlayerTwo,
        "Choose a Digimon",
        minCount: 0,
        maxCount: 1,
        canSkip: true,
        EffectChoiceHelpers.CandidatesFromIds(
            new[] { new HeadlessEntityId("perm-a"), new HeadlessEntityId("perm-b") },
            ChoiceZone.BattleArea,
            PlayerTwo));

    AssertEqual(ChoiceType.Permanent, request.Type, "permanent type");
    AssertEqual(ChoiceZone.BattleArea, request.SourceZone, "permanent source zone");
    AssertTrue(request.CanSkip, "permanent can skip");
    AssertEqual(PlayerTwo, request.Candidates[0].OwnerId, "owner id");
    return Task.CompletedTask;
}

async Task CountChoiceResolvesIntoContext()
{
    ChoiceRequest request = EffectChoiceHelpers.CreateCountRequest(
        PlayerOne,
        "Select count",
        minCount: 0,
        maxCount: 3,
        canSkip: false);
    EffectContext context = CreateContext();
    var provider = new ScriptedChoiceProvider(new[] { ChoiceResult.SelectCount(2) });

    EffectChoiceResolution resolution = await EffectChoiceHelpers.ResolveAsync(context, request, provider);

    AssertTrue(resolution.IsSuccess, "count resolution");
    AssertEqual(2, resolution.Context.GetRequiredValue<int?>(EffectChoiceKey(EffectChoiceHelpers.SelectedCountKey)), "selected count");
    AssertEqual("Count", resolution.Context.GetRequiredValue<string>(EffectChoiceKey(EffectChoiceHelpers.TypeKey)), "choice type value");
    AssertSequence(new[] { "count:0", "count:1", "count:2", "count:3" }, Strings(resolution.Context.Values[EffectChoiceKey(EffectChoiceHelpers.CandidateIdsKey)]), "count candidates");
}

Task SelectedIdsAreWrittenIntoContext()
{
    ChoiceRequest request = EffectChoiceHelpers.CreateCardRequest(
        PlayerOne,
        "Choose cards",
        minCount: 1,
        maxCount: 2,
        canSkip: false,
        ChoiceZone.Hand,
        EffectChoiceHelpers.CandidatesFromIds(
            new[] { new HeadlessEntityId("hand-a"), new HeadlessEntityId("hand-b") },
            ChoiceZone.Hand,
            PlayerOne));
    EffectContext context = CreateContext();

    EffectChoiceResolution resolution = EffectChoiceHelpers.ApplyResult(
        context,
        request,
        ChoiceResult.Select(new HeadlessEntityId("hand-b")));

    AssertTrue(resolution.IsSuccess, "selected id result");
    AssertFalse(ReferenceEquals(context, resolution.Context), "new context instance");
    AssertEqual("kept", resolution.Context.GetRequiredValue<string>("existing"), "existing value retained");
    AssertSequence(new[] { "hand-b" }, Strings(resolution.Context.Values[EffectChoiceKey(EffectChoiceHelpers.SelectedIdsKey)]), "selected ids");
    AssertEqual(false, resolution.Context.Values[EffectChoiceKey(EffectChoiceHelpers.IsSkippedKey)], "skip value");
    return Task.CompletedTask;
}

Task AllowedSkipIsRecorded()
{
    ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(
        PlayerOne,
        "Optional target",
        minCount: 0,
        maxCount: 1,
        canSkip: true,
        EffectChoiceHelpers.CandidatesFromIds(new[] { new HeadlessEntityId("perm-a") }, ChoiceZone.BattleArea));

    EffectChoiceResolution resolution = EffectChoiceHelpers.ApplyResult(CreateContext(), request, ChoiceResult.Skip(), "effectChoice");

    AssertTrue(resolution.IsSuccess, "skip result");
    AssertEqual(true, resolution.Context.Values["effectChoice.isSkipped"], "skip value");
    AssertSequence(Array.Empty<string>(), Strings(resolution.Context.Values["effectChoice.selectedIds"]), "skip selected ids");
    return Task.CompletedTask;
}

Task IllegalSkipFailsWithoutContextMutation()
{
    ChoiceRequest request = EffectChoiceHelpers.CreatePermanentRequest(
        PlayerOne,
        "Mandatory target",
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        EffectChoiceHelpers.CandidatesFromIds(new[] { new HeadlessEntityId("perm-a") }, ChoiceZone.BattleArea));
    EffectContext context = CreateContext();

    EffectChoiceResolution resolution = EffectChoiceHelpers.ApplyResult(context, request, ChoiceResult.Skip());

    AssertFalse(resolution.IsSuccess, "illegal skip success");
    AssertEqual("invalid_effect_choice_result", resolution.ErrorCode, "error code");
    AssertFalse(resolution.Context.HasValue(EffectChoiceKey(EffectChoiceHelpers.IsSkippedKey)), "context not mutated");
    AssertSequence(new[] { "Choice result skipped a request that does not allow skipping." }, Strings(resolution.Values[EffectChoiceKey(EffectChoiceHelpers.ValidationFailuresKey)]), "validation failure");
    return Task.CompletedTask;
}

async Task ProviderValidationFailureReturnsExplicitFailure()
{
    ChoiceRequest request = EffectChoiceHelpers.CreateCardRequest(
        PlayerOne,
        "Choose one",
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        ChoiceZone.Hand,
        EffectChoiceHelpers.CandidatesFromIds(new[] { new HeadlessEntityId("hand-a") }, ChoiceZone.Hand));
    var provider = new ScriptedChoiceProvider(new[] { ChoiceResult.Skip() });

    EffectChoiceResolution resolution = await EffectChoiceHelpers.ResolveAsync(CreateContext(), request, provider);

    AssertFalse(resolution.IsSuccess, "provider failure");
    AssertEqual("effect_choice_provider_failed", resolution.ErrorCode, "provider error code");
    AssertContains(resolution.Message ?? string.Empty, "does not allow skipping", "provider failure message");
    AssertSequence(new[] { "hand-a" }, Strings(resolution.Values[EffectChoiceKey(EffectChoiceHelpers.CandidateIdsKey)]), "request values preserved");
}

Task AssetsFacadeAndSourceScope()
{
    ChoiceRequest request = EffectChoiceHelperFactory.CreateCountRequest(PlayerOne, "Facade count", 1, 2, canSkip: false);
    EffectChoiceResolution resolution = EffectChoiceHelperFactory.ApplyResult(
        CreateContext(),
        request,
        ChoiceResult.SelectCount(1));

    AssertTrue(resolution.IsSuccess, "facade resolution");
    AssertEqual(1, resolution.Context.GetRequiredValue<int?>(EffectChoiceKey(EffectChoiceHelpers.SelectedCountKey)), "facade selected count");

    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "EffectChoiceHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "EffectChoiceHelpers.cs");
    string testPath = Path.Combine(root, "tests", "G3K-001.Effect.selection.helper.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless helper exists");
    AssertTrue(File.Exists(facadePath), "facade helper exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

EffectContext CreateContext()
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        SourceId,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        values: new Dictionary<string, object?> { ["existing"] = "kept" });
}

string EffectChoiceKey(string suffix)
{
    return $"{EffectChoiceHelpers.DefaultKeyPrefix}.{suffix}";
}

string[] Strings(object? value)
{
    if (value is null)
    {
        return Array.Empty<string>();
    }

    if (value is string text)
    {
        return new[] { text };
    }

    if (value is IEnumerable enumerable)
    {
        return enumerable.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray();
    }

    return new[] { value.ToString() ?? string.Empty };
}

string ReadAsIs(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", relativePath));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    AssertTrue(File.Exists(path), $"predecessor result document {fileName}");
    AssertContains(File.ReadAllText(path), "COMPLETE", $"predecessor completion {fileName}");
}

List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] values = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < values.Length ? values[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

IEnumerable<string> SplitCsvLine(string line)
{
    var value = new StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }

            continue;
        }

        if (ch == ',' && !inQuotes)
        {
            yield return value.ToString();
            value.Clear();
            continue;
        }

        value.Append(ch);
    }

    yield return value.ToString();
}

string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(Environment.CurrentDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}

void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
    }
}

void AssertDoesNotContain(string text, string unexpected, string label)
{
    if (text.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: text must not contain '{unexpected}'.");
    }
}

void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}
