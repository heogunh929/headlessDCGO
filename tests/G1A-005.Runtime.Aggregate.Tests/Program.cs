var root = FindRepositoryRoot();
var summaryRelativePath = Path.Combine("docs", "test-results", "goals", "G1A-005_runtime_test_summary.md");
var summaryPath = Path.Combine(root, summaryRelativePath);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1A-005 goal row keeps the runtime aggregate contract", GoalRowKeepsExpectedContract),
    ("Runtime summary document exists and links prerequisite result documents", RuntimeSummaryLinksPrerequisiteDocuments),
    ("Prerequisite runtime result documents are complete and passing", PrerequisiteRuntimeResultsAreCompleteAndPassing),
    ("Runtime summary aggregate counts match prerequisite documents", RuntimeSummaryCountsMatchPrerequisiteDocuments),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-005")
        ?? throw new InvalidOperationException("G1A-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Runtime", Value(row, "area"), "area");
    AssertEqual("Runtime test summary", Value(row, "deliverables"), "deliverables");
    AssertEqual("Runtime Goal 결과 문서 링크 검증", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1A-005_runtime_aggregate_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-002; G1A-003; G1A-004", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Runtime 영역 완료", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task RuntimeSummaryLinksPrerequisiteDocuments()
{
    string summary = ReadRequiredText(summaryPath);
    foreach (RuntimeGoalExpectation expectation in RuntimeGoals())
    {
        AssertContains(summary, expectation.GoalId, $"{expectation.GoalId} id in summary");
        AssertContains(summary, expectation.ResultDocumentFileName, $"{expectation.GoalId} result document link");
        AssertContains(summary, expectation.TestCommand, $"{expectation.GoalId} test command");

        string linkedPath = Path.Combine(root, "docs", "test-results", "goals", expectation.ResultDocumentFileName);
        AssertTrue(File.Exists(linkedPath), $"{expectation.GoalId} linked result document exists");
    }

    AssertContains(summary, "Runtime area decision: COMPLETE", "summary completion decision");
    return Task.CompletedTask;
}

Task PrerequisiteRuntimeResultsAreCompleteAndPassing()
{
    foreach (RuntimeGoalExpectation expectation in RuntimeGoals())
    {
        string document = ReadRequiredText(Path.Combine(root, "docs", "test-results", "goals", expectation.ResultDocumentFileName));
        AssertContains(document, $"Goal ID: {expectation.GoalId}", $"{expectation.GoalId} document goal id");
        AssertContains(document, "Status: PASS", $"{expectation.GoalId} pass status");
        AssertContains(document, "COMPLETE", $"{expectation.GoalId} complete decision");
        AssertContains(document, expectation.TestCommand, $"{expectation.GoalId} test command");
        AssertContains(document, $"- Total: {expectation.Total}", $"{expectation.GoalId} total count");
        AssertContains(document, $"- Passed: {expectation.Passed}", $"{expectation.GoalId} passed count");
        AssertContains(document, $"- Failed: {expectation.Failed}", $"{expectation.GoalId} failed count");
        AssertContains(document, $"- Skipped: {expectation.Skipped}", $"{expectation.GoalId} skipped count");
    }

    return Task.CompletedTask;
}

Task RuntimeSummaryCountsMatchPrerequisiteDocuments()
{
    string summary = ReadRequiredText(summaryPath);
    RuntimeGoalExpectation[] expectations = RuntimeGoals();

    int total = expectations.Sum(goal => goal.Total);
    int passed = expectations.Sum(goal => goal.Passed);
    int failed = expectations.Sum(goal => goal.Failed);
    int skipped = expectations.Sum(goal => goal.Skipped);

    AssertContains(summary, $"- Runtime contract goals covered: {expectations.Length}", "covered goal count");
    AssertContains(summary, $"- Total tests: {total}", "total tests");
    AssertContains(summary, $"- Passed: {passed}", "passed tests");
    AssertContains(summary, $"- Failed: {failed}", "failed tests");
    AssertContains(summary, $"- Skipped: {skipped}", "skipped tests");
    AssertEqual(18, total, "expected runtime aggregate total");
    AssertEqual(18, passed, "expected runtime aggregate passed");
    AssertEqual(0, failed, "expected runtime aggregate failed");
    AssertEqual(0, skipped, "expected runtime aggregate skipped");
    return Task.CompletedTask;
}

static RuntimeGoalExpectation[] RuntimeGoals()
{
    return new[]
    {
        new RuntimeGoalExpectation(
            "G1A-002",
            "G1A-002_match_lifecycle_unit_test_results.md",
            @".\.dotnet\dotnet.exe run --project tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj",
            Total: 5,
            Passed: 5,
            Failed: 0,
            Skipped: 0),
        new RuntimeGoalExpectation(
            "G1A-003",
            "G1A-003_action_contract_unit_test_results.md",
            @".\.dotnet\dotnet.exe run --project tests\G1A-003.ActionContract.Tests\G1A-003.ActionContract.Tests.csproj",
            Total: 6,
            Passed: 6,
            Failed: 0,
            Skipped: 0),
        new RuntimeGoalExpectation(
            "G1A-004",
            "G1A-004_observation_legal_action_unit_test_results.md",
            @".\.dotnet\dotnet.exe run --project tests\G1A-004.Observation.LegalAction.Tests\G1A-004.Observation.LegalAction.Tests.csproj",
            Total: 7,
            Passed: 7,
            Failed: 0,
            Skipped: 0),
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

static string ReadRequiredText(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Required document was not found: {path}");
    }

    return File.ReadAllText(path);
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out var value)
        ? value
        : throw new InvalidOperationException($"Missing key '{key}'.");
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

sealed record RuntimeGoalExpectation(
    string GoalId,
    string ResultDocumentFileName,
    string TestCommand,
    int Total,
    int Passed,
    int Failed,
    int Skipped);
