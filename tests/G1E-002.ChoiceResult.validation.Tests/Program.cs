using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1E-002 goal row keeps the ChoiceResult validation contract", GoalRowKeepsExpectedContract),
    ("ChoiceOption preserves immutable option schema and rejects invalid values", ChoiceOptionPreservesSchemaAndRejectsInvalidValues),
    ("ChoiceResult preserves immutable selected id and count snapshots", ChoiceResultPreservesImmutableSnapshots),
    ("ChoiceResult validates selected ids against min max and selectable candidates", ChoiceResultValidatesSelectedIds),
    ("ChoiceResult rejects skip when request does not allow skipping", ChoiceResultRejectsIllegalSkip),
    ("ChoiceResult validates count selections against count request range", ChoiceResultValidatesCountSelections),
    ("ChoiceResult application rejects invalid result before resolving pending choice", ChoiceControllerRejectsInvalidResultBeforeResolve),
    ("AS-IS result validation references remain read-only inputs", AsIsResultValidationReferencesRemainReadOnlyInputs),
    ("ChoiceResult validation source files no longer contain placeholder TODO contracts", ChoiceResultValidationFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1E-002")
        ?? throw new InvalidOperationException("G1E-002 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Choices", Value(row, "area"), "area");
    AssertEqual("ChoiceResult validation", Value(row, "goal"), "goal");
    AssertEqual("선택 결과 검증 기준 확정", Value(row, "scope"), "scope");
    AssertEqual("ChoiceResult ChoiceOption", Value(row, "deliverables"), "deliverables");
    AssertEqual("select count skip invalid candidate 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1E-002_choice_result_validation_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1E-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("ChoiceResult 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task ChoiceOptionPreservesSchemaAndRejectsInvalidValues()
{
    var candidate = new ChoiceCandidate(
        new HeadlessEntityId("card-001"),
        "  BT1-001 Agumon  ",
        ChoiceZone.Hand,
        IsSelectable: true,
        new HeadlessPlayerId(1));

    ChoiceOption option = ChoiceOption.FromCandidate(candidate);
    AssertEqual(candidate.Id, option.Id, "id");
    AssertEqual("BT1-001 Agumon", option.Label, "label");
    AssertEqual(candidate.Zone, option.Zone, "zone");

    ExpectThrows<ArgumentNullException>(() => ChoiceOption.FromCandidate(null!));
    ExpectThrows<ArgumentException>(() => new ChoiceOption(default, "label", ChoiceZone.Hand));
    ExpectThrows<ArgumentNullException>(() => new ChoiceOption(new HeadlessEntityId("card"), null!, ChoiceZone.Hand));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceOption(new HeadlessEntityId("card"), "label", ChoiceZone.None));
    return Task.CompletedTask;
}

Task ChoiceResultPreservesImmutableSnapshots()
{
    var ids = new List<HeadlessEntityId>
    {
        new("card-a"),
    };

    ChoiceResult result = ChoiceResult.Select(ids);
    ids.Add(new HeadlessEntityId("card-b"));

    AssertFalse(result.IsSkipped, "selected result skipped");
    AssertEqual(1, result.SelectedIds.Count, "selected id snapshot");
    AssertEqual(new HeadlessEntityId("card-a"), result.SelectedIds[0], "selected id");
    AssertTrue(result.SelectedIds is System.Collections.ObjectModel.ReadOnlyCollection<HeadlessEntityId>, "read-only selected ids");
    AssertEqual(null, result.SelectedCount, "selected count");

    ChoiceResult count = ChoiceResult.SelectCount(2);
    AssertEqual(2, count.SelectedCount, "count");
    AssertEqual(0, count.SelectedIds.Count, "count selected ids");

    ChoiceResult skip = ChoiceResult.Skip();
    AssertTrue(skip.IsSkipped, "skip result");
    AssertEqual(0, skip.SelectedIds.Count, "skip ids");
    AssertEqual(null, skip.SelectedCount, "skip count");

    ExpectThrows<ArgumentNullException>(() => ChoiceResult.Select(null!));
    ExpectThrows<ArgumentException>(() => ChoiceResult.Select(default(HeadlessEntityId)));
    ExpectThrows<ArgumentOutOfRangeException>(() => ChoiceResult.SelectCount(-1));
    ExpectThrows<ArgumentException>(() => new ChoiceResult(isSkipped: true, new[] { new HeadlessEntityId("card-a") }));
    return Task.CompletedTask;
}

Task ChoiceResultValidatesSelectedIds()
{
    ChoiceRequest request = CardRequest(min: 1, max: 2, canSkip: false);

    AssertValid(ChoiceResult.Select(new HeadlessEntityId("card-a")).Validate(request), "single valid card");
    AssertValid(ChoiceResult.Select(new HeadlessEntityId("card-a"), new HeadlessEntityId("card-b")).Validate(request), "two valid cards");

    AssertInvalidContains(
        ChoiceResult.Select(Array.Empty<HeadlessEntityId>()).Validate(request),
        "outside the allowed range",
        "too few ids");
    AssertInvalidContains(
        ChoiceResult.Select(new HeadlessEntityId("card-a"), new HeadlessEntityId("card-b"), new HeadlessEntityId("card-c")).Validate(request),
        "outside the allowed range",
        "too many ids");
    AssertInvalidContains(
        ChoiceResult.Select(new HeadlessEntityId("card-c")).Validate(request),
        "not a selectable candidate",
        "not selectable candidate");
    AssertInvalidContains(
        ChoiceResult.Select(new HeadlessEntityId("missing")).Validate(request),
        "not a selectable candidate",
        "missing candidate");
    AssertInvalidContains(
        ChoiceResult.Select(new HeadlessEntityId("card-a"), new HeadlessEntityId("card-a")).Validate(request),
        "more than once",
        "duplicate candidate");
    return Task.CompletedTask;
}

Task ChoiceResultRejectsIllegalSkip()
{
    ChoiceRequest noSkip = CardRequest(min: 0, max: 1, canSkip: false);
    ChoiceRequest canSkip = CardRequest(min: 0, max: 1, canSkip: true);

    AssertInvalidContains(ChoiceResult.Skip().Validate(noSkip), "does not allow skipping", "illegal skip");
    AssertValid(ChoiceResult.Skip().Validate(canSkip), "legal skip");
    return Task.CompletedTask;
}

Task ChoiceResultValidatesCountSelections()
{
    ChoiceRequest countRequest = new(
        ChoiceType.Count,
        new HeadlessPlayerId(1),
        "Choose count",
        minCount: 1,
        maxCount: 3,
        canSkip: false,
        sourceZone: ChoiceZone.None,
        Array.Empty<ChoiceCandidate>());

    AssertValid(ChoiceResult.SelectCount(1).Validate(countRequest), "min count");
    AssertValid(ChoiceResult.SelectCount(3).Validate(countRequest), "max count");
    AssertInvalidContains(ChoiceResult.SelectCount(0).Validate(countRequest), "outside the allowed range", "below range");
    AssertInvalidContains(ChoiceResult.SelectCount(4).Validate(countRequest), "outside the allowed range", "above range");
    AssertInvalidContains(ChoiceResult.Select(new HeadlessEntityId("card-a")).Validate(countRequest), "require a selected count", "ids for count request");
    AssertInvalidContains(ChoiceResult.SelectCount(1).Validate(CardRequest(min: 0, max: 1, canSkip: false)), "only valid for count", "count for card request");
    return Task.CompletedTask;
}

Task ChoiceControllerRejectsInvalidResultBeforeResolve()
{
    ChoiceRequest request = CardRequest(min: 1, max: 1, canSkip: false);
    var controller = new InMemoryHeadlessChoiceController();
    controller.RequestChoice(request, new HeadlessEntityId("choice-1"));

    InvalidOperationException ex = ExpectThrows<InvalidOperationException>(
        () => controller.ResolveChoice(ChoiceResult.Skip()));

    AssertTrue(ex.Message.Contains("does not allow skipping", StringComparison.Ordinal), "error message");
    AssertTrue(controller.Current.IsPending, "still pending");
    AssertTrue(controller.PendingRequest is not null, "pending request retained");

    HeadlessChoiceState resolved = controller.ResolveChoice(ChoiceResult.Select(new HeadlessEntityId("card-a")));
    AssertTrue(resolved.IsResolved, "resolved");
    AssertFalse(resolved.IsSkipped, "not skipped");
    AssertEqual(1, resolved.SelectedIds.Count, "selected count");
    return Task.CompletedTask;
}

Task AsIsResultValidationReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"),
            new[] { "SelectedList", "CardSelection", "_canEndSelectCondition", "_canNoSelect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectPermanentEffect.cs"),
            new[] { "CanEndSelect", "PermanentSelection", "_canNoSelect", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCountEffect.cs"),
            new[] { "_candidates", "ValueSelection", "SetCount", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "CardSelection.cs"),
            new[] { "CardSelection", "CardIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "PermanentSelection.cs"),
            new[] { "PermanentSelection", "PermanentIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "ValueSelection.cs"),
            new[] { "ValueSelection", "ValueAsInt" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"AS-IS reference file was not found: {path}");
        }

        string text = File.ReadAllText(path);
        foreach (string pattern in patterns)
        {
            AssertTrue(text.Contains(pattern, StringComparison.Ordinal), $"{Path.GetFileName(path)} contains {pattern}");
        }
    }

    return Task.CompletedTask;
}

Task ChoiceResultValidationFilesHaveNoTodoContracts()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceResult.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceOption.cs"),
    };

    foreach (string path in paths)
    {
        string text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} still contains a TODO placeholder.");
        }

        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference UnityEngine");
        AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference MonoBehaviour");
    }

    return Task.CompletedTask;
}

static ChoiceRequest CardRequest(int min, int max, bool canSkip)
{
    var player = new HeadlessPlayerId(1);
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        "Select cards",
        min,
        max,
        canSkip,
        ChoiceZone.Hand,
        new[]
        {
            new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-c"), "Card C", ChoiceZone.Hand, IsSelectable: false, player),
        });
}

static void AssertValid(ChoiceResultValidation validation, string label)
{
    if (!validation.IsValid)
    {
        throw new InvalidOperationException($"{label}: expected valid result, actual failures: {validation}");
    }
}

static void AssertInvalidContains(ChoiceResultValidation validation, string expectedText, string label)
{
    if (validation.IsValid)
    {
        throw new InvalidOperationException($"{label}: expected invalid result.");
    }

    if (!validation.Failures.Any(failure => failure.Contains(expectedText, StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException($"{label}: expected failure containing '{expectedText}', actual: {validation}");
    }
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

static TException ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
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
