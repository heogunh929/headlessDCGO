using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var root = FindRepositoryRoot();

var phase1Goals = new Phase1Goal[]
{
    new("G1A-005", "Runtime", "G1A-005_runtime_aggregate_unit_test_results.md", "G1A-005.Runtime.Aggregate.Tests"),
    new("G1B-006", "State", "G1B-006_state_snapshot_fingerprint_unit_test_results.md", "G1B-006.State.snapshot.fingerprint.Tests"),
    new("G1C-004", "Bridge", "G1C-004_unity_exclusion_policy_unit_test_results.md", "G1C-004.Unity.only.exclusion.policy.Tests"),
    new("G1D-004", "Coroutine", "G1D-004_task_runner_unit_test_results.md", "G1D-004.TaskRunner.Tests"),
    new("G1E-005", "Choice", "G1E-005_choice_pause_resume_unit_test_results.md", "G1E-005.Choice.pause.resume.contract.Tests"),
    new("G1F-006", "Effect", "G1F-006_continuous_replacement_query_unit_test_results.md", "G1F-006.Continuous.Replacement.query.contract.Tests"),
    new("G1G-003", "Session", "G1G-003_photon_dependency_guard_unit_test_results.md", "G1G-003.Photon.dependency.guard.Tests"),
    new("G1H-005", "Data", "G1H-005_unity_asset_exclusion_guard_unit_test_results.md", "G1H-005.Unity.asset.exclusion.guard.Tests"),
    new("G1I-004", "Diagnostics", "G1I-004_forbidden_dependency_scan_unit_test_results.md", "G1I-004.Forbidden.dependency.scan.Tests"),
};

var tests = new (string Name, Action Body)[]
{
    ("G1I-005 goal row keeps the Phase 1 aggregate contract", GoalRowKeepsExpectedContract),
    ("All Phase 1 predecessor result documents exist and are complete", PredecessorResultDocumentsExistAndAreComplete),
    ("All Phase 1 predecessor test projects exist", PredecessorTestProjectsExist),
    ("Phase 1 aggregate result document links every predecessor result", AggregateDocumentLinksEveryPredecessor),
    ("Phase 1 aggregate result document records the gate counts", AggregateDocumentRecordsGateCounts),
    ("Porting sequence keeps G1I-005 as the Phase 1 completion gate", PortingSequenceKeepsPhase1Gate),
    ("Aggregate evaluation rejects missing or incomplete predecessor evidence", AggregateEvaluationRejectsIncompleteEvidence),
    ("Aggregate evaluation fingerprint is deterministic", AggregateEvaluationFingerprintIsDeterministic),
    ("G1I-005 stays inside documentation and test scope", ScopeIsLimitedToAggregateDocumentationAndTests),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Body();
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

void GoalRowKeepsExpectedContract()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1I-005")
        ?? throw new InvalidOperationException("G1I-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Gate", Value(row, "area"), "area");
    AssertEqual("Phase 1 aggregate result", Value(row, "goal"), "goal");
    AssertEqual("phase1 result document", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("Goal", StringComparison.Ordinal), "unit test scope mentions Goal");
    AssertFalse(string.IsNullOrWhiteSpace(Value(row, "unit_test_scope")), "unit test scope is populated");
    AssertEqual("docs/test-results/goals/G1I-005_phase1_aggregate_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-005; G1B-006; G1C-004; G1D-004; G1E-005; G1F-006; G1G-003; G1H-005; G1I-004", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Phase 2", StringComparison.Ordinal), "completion gate");
    AssertEqual("HIGH", Value(row, "priority"), "priority");
}

void PredecessorResultDocumentsExistAndAreComplete()
{
    foreach (var goal in phase1Goals)
    {
        string path = ResultDocumentPath(goal);
        AssertTrue(File.Exists(path), $"{goal.Id} result document exists");

        string text = File.ReadAllText(path);
        var evidence = Phase1AggregateEvidence.Evaluate(goal.Id, text);
        AssertTrue(evidence.IsComplete, $"{goal.Id} records COMPLETE");
        AssertTrue(evidence.HasZeroFailedTests, $"{goal.Id} records zero failed tests");
    }
}

void PredecessorTestProjectsExist()
{
    foreach (var goal in phase1Goals)
    {
        string projectDirectory = Path.Combine(root, "tests", goal.TestProjectDirectory);
        string projectFile = Path.Combine(projectDirectory, $"{goal.TestProjectDirectory}.csproj");
        AssertTrue(Directory.Exists(projectDirectory), $"{goal.Id} test project directory exists");
        AssertTrue(File.Exists(projectFile), $"{goal.Id} test project file exists");
    }
}

void AggregateDocumentLinksEveryPredecessor()
{
    string path = Phase1AggregateDocumentPath();
    AssertTrue(File.Exists(path), "Phase 1 aggregate result document exists");

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("G1I-005", StringComparison.Ordinal), "aggregate document mentions G1I-005");
    AssertTrue(text.Contains("Phase 2 start: ALLOWED", StringComparison.Ordinal), "aggregate document records Phase 2 allowed gate");

    foreach (var goal in phase1Goals)
    {
        AssertTrue(text.Contains(goal.Id, StringComparison.Ordinal), $"{goal.Id} linked");
        AssertTrue(text.Contains(goal.ResultDocumentName, StringComparison.Ordinal), $"{goal.Id} result document linked");
        AssertTrue(text.Contains(goal.TestProjectDirectory, StringComparison.Ordinal), $"{goal.Id} test project linked");
    }
}

void AggregateDocumentRecordsGateCounts()
{
    string text = File.ReadAllText(Phase1AggregateDocumentPath());

    AssertTrue(text.Contains("Required Phase 1 gate goals: 9", StringComparison.Ordinal), "required gate count");
    AssertTrue(text.Contains("Complete Phase 1 gate goals: 9", StringComparison.Ordinal), "complete gate count");
    AssertTrue(text.Contains("Blocked Phase 1 gate goals: 0", StringComparison.Ordinal), "blocked gate count");
    AssertTrue(text.Contains("Failed Phase 1 gate goals: 0", StringComparison.Ordinal), "failed gate count");
}

void PortingSequenceKeepsPhase1Gate()
{
    string text = File.ReadAllText(Path.Combine(root, "docs", "headless_complete_porting_sequence.md"));

    AssertTrue(text.Contains("## Phase 1", StringComparison.Ordinal), "Phase 1 section exists");
    AssertTrue(text.Contains("docs/test-results/headless_phase1_unity_replacement_unit_test_results.md", StringComparison.Ordinal), "Phase 1 aggregate result path");
    AssertTrue(text.Contains("Phase 2", StringComparison.Ordinal), "Phase 2 gate reference");
}

void AggregateEvaluationRejectsIncompleteEvidence()
{
    var missing = Phase1AggregateEvidence.Evaluate("G1X-000", "");
    AssertFalse(missing.IsComplete, "missing document is not complete");
    AssertFalse(missing.HasZeroFailedTests, "missing document has no zero-failure evidence");

    var incomplete = Phase1AggregateEvidence.Evaluate("G1X-001", "Status: PASS\n| Total tests | 3 | 2 | 1 | 0 |");
    AssertFalse(incomplete.IsComplete, "document without COMPLETE is not complete");
    AssertFalse(incomplete.HasZeroFailedTests, "failed count is rejected");
}

void AggregateEvaluationFingerprintIsDeterministic()
{
    string first = Phase1AggregateEvidence.Fingerprint(root, phase1Goals);
    string second = Phase1AggregateEvidence.Fingerprint(root, phase1Goals.Reverse().ToArray());

    AssertEqual(first, second, "fingerprint");
}

void ScopeIsLimitedToAggregateDocumentationAndTests()
{
    string aggregate = File.ReadAllText(Phase1AggregateDocumentPath());

    AssertTrue(aggregate.Contains("No Phase 2 implementation was started", StringComparison.Ordinal), "no Phase 2 implementation statement");
    AssertTrue(aggregate.Contains("No original DCGO/Assets files were modified", StringComparison.Ordinal), "DCGO/Assets safety statement");
    AssertFalse(aggregate.Contains("G2A-001 COMPLETE", StringComparison.Ordinal), "no Phase 2 goal completion");
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}

string ResultDocumentPath(Phase1Goal goal)
{
    return Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
}

string Phase1AggregateDocumentPath()
{
    return Path.Combine(root, "docs", "test-results", "headless_phase1_unity_replacement_unit_test_results.md");
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    if (lines.Length == 0)
    {
        throw new InvalidOperationException($"CSV file is empty: {path}");
    }

    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
    {
        string[] values = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < values.Length ? values[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

static IEnumerable<string> SplitCsvLine(string line)
{
    var current = new StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];

        if (c == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            inQuotes = !inQuotes;
            continue;
        }

        if (c == ',' && !inQuotes)
        {
            yield return current.ToString();
            current.Clear();
            continue;
        }

        current.Append(c);
    }

    yield return current.ToString();
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
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

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

sealed record Phase1Goal(string Id, string Area, string ResultDocumentName, string TestProjectDirectory);

sealed record Phase1AggregateEvidence(bool IsComplete, bool HasZeroFailedTests)
{
    public static Phase1AggregateEvidence Evaluate(string goalId, string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return new Phase1AggregateEvidence(false, false);
        }

        bool isComplete = documentText.Contains("COMPLETE", StringComparison.Ordinal);
        bool zeroFailed = HasZeroFailedEvidence(documentText);

        return new Phase1AggregateEvidence(isComplete, zeroFailed);
    }

    public static string Fingerprint(string root, IReadOnlyCollection<Phase1Goal> goals)
    {
        using var sha = SHA256.Create();
        var builder = new StringBuilder();

        foreach (var goal in goals.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            string path = Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
            string text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            Phase1AggregateEvidence evidence = Evaluate(goal.Id, text);
            builder.Append(goal.Id)
                .Append('|')
                .Append(goal.ResultDocumentName)
                .Append('|')
                .Append(evidence.IsComplete)
                .Append('|')
                .Append(evidence.HasZeroFailedTests)
                .AppendLine();
        }

        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static bool HasZeroFailedEvidence(string documentText)
    {
        if (Regex.IsMatch(documentText, @"-\s*Failed:\s*0\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        foreach (string line in documentText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (!line.TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            var numbers = Regex.Matches(line, @"(?<![\w.-])\d+(?![\w.-])")
                .Select(match => int.Parse(match.Value))
                .ToArray();

            if (numbers.Length >= 4 && numbers[^2] == 0)
            {
                return true;
            }
        }

        if (Regex.IsMatch(documentText, @"\b0\s+failed\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }
}
