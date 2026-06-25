using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var root = FindRepositoryRoot();

var phase2Goals = new Phase2Goal[]
{
    new("G2A-006", "Flow", "G2A-006_legal_action_dispatch_unit_test_results.md", "G2A-006.Legal.action.dispatch.hook.Tests"),
    new("G2B-002", "Context", "G2B-002_visibility_view_unit_test_results.md", "G2B-002.Visibility.view.Tests"),
    new("G2C-002", "Player", "G2C-002_player_terminal_checks_unit_test_results.md", "G2C-002.Memory.security.deck.loss.check.Tests"),
    new("G2D-004", "Card", "G2D-004_digivolution_source_attach_unit_test_results.md", "G2D-004.Digivolution.source.attach.Tests"),
    new("G2E-005", "Action", "G2E-005_pass_cheat_guard_unit_test_results.md", "G2E-005.Pass.Cheat.guard.Tests"),
    new("G2F-004", "AutoProcessing", "G2F-004_security_delayed_trigger_hook_unit_test_results.md", "G2F-004.Security.delayed.trigger.hook.Tests"),
    new("G2G-005", "Attack", "G2G-005_end_attack_trigger_unit_test_results.md", "G2G-005.End.attack.trigger.Tests"),
};

var tests = new (string Name, Action Body)[]
{
    ("G2Z-001 goal row keeps the Phase 2 aggregate contract", GoalRowKeepsExpectedContract),
    ("All Phase 2 predecessor result documents exist and are complete", PredecessorResultDocumentsExistAndAreComplete),
    ("All Phase 2 predecessor test projects exist", PredecessorTestProjectsExist),
    ("Phase 2 aggregate result document links every predecessor result", AggregateDocumentLinksEveryPredecessor),
    ("Phase 2 aggregate result document records the gate counts", AggregateDocumentRecordsGateCounts),
    ("Porting sequence keeps Phase 2 as the Phase 3 start gate", PortingSequenceKeepsPhase2Gate),
    ("Aggregate evaluation rejects missing or incomplete predecessor evidence", AggregateEvaluationRejectsIncompleteEvidence),
    ("Aggregate evaluation fingerprint is deterministic", AggregateEvaluationFingerprintIsDeterministic),
    ("G2Z-001 stays inside documentation and test scope", ScopeIsLimitedToAggregateDocumentationAndTests),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2Z-001")
        ?? throw new InvalidOperationException("G2Z-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("Gate", Value(row, "area"), "area");
    AssertEqual("Phase 2 aggregate result", Value(row, "goal"), "goal");
    AssertEqual("Phase 2 전체 결과 집계", Value(row, "scope"), "scope");
    AssertEqual("phase2 result document", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("Phase 2", StringComparison.Ordinal), "unit test scope mentions Phase 2");
    AssertFalse(string.IsNullOrWhiteSpace(Value(row, "unit_test_scope")), "unit test scope is populated");
    AssertEqual("docs/test-results/goals/G2Z-001_phase2_aggregate_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G2A-006; G2B-002; G2C-002; G2D-004; G2E-005; G2F-004; G2G-005", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Phase 3", StringComparison.Ordinal), "completion gate");
    AssertEqual("HIGH", Value(row, "priority"), "priority");
}

void PredecessorResultDocumentsExistAndAreComplete()
{
    foreach (var goal in phase2Goals)
    {
        string path = ResultDocumentPath(goal);
        AssertTrue(File.Exists(path), $"{goal.Id} result document exists");

        string text = File.ReadAllText(path);
        var evidence = Phase2AggregateEvidence.Evaluate(goal.Id, text);
        AssertTrue(evidence.IsComplete, $"{goal.Id} records COMPLETE");
        AssertTrue(evidence.HasZeroFailedTests, $"{goal.Id} records zero failed tests");
    }
}

void PredecessorTestProjectsExist()
{
    foreach (var goal in phase2Goals)
    {
        string projectDirectory = Path.Combine(root, "tests", goal.TestProjectDirectory);
        string projectFile = Path.Combine(projectDirectory, $"{goal.TestProjectDirectory}.csproj");
        AssertTrue(Directory.Exists(projectDirectory), $"{goal.Id} test project directory exists");
        AssertTrue(File.Exists(projectFile), $"{goal.Id} test project file exists");
    }
}

void AggregateDocumentLinksEveryPredecessor()
{
    string path = Phase2AggregateDocumentPath();
    AssertTrue(File.Exists(path), "Phase 2 aggregate result document exists");

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("G2Z-001", StringComparison.Ordinal), "aggregate document mentions G2Z-001");
    AssertTrue(text.Contains("Phase 3 start: ALLOWED", StringComparison.Ordinal), "aggregate document records Phase 3 allowed gate");

    foreach (var goal in phase2Goals)
    {
        AssertTrue(text.Contains(goal.Id, StringComparison.Ordinal), $"{goal.Id} linked");
        AssertTrue(text.Contains(goal.ResultDocumentName, StringComparison.Ordinal), $"{goal.Id} result document linked");
        AssertTrue(text.Contains(goal.TestProjectDirectory, StringComparison.Ordinal), $"{goal.Id} test project linked");
    }
}

void AggregateDocumentRecordsGateCounts()
{
    string text = File.ReadAllText(Phase2AggregateDocumentPath());

    AssertTrue(text.Contains("Required Phase 2 gate goals: 7", StringComparison.Ordinal), "required gate count");
    AssertTrue(text.Contains("Complete Phase 2 gate goals: 7", StringComparison.Ordinal), "complete gate count");
    AssertTrue(text.Contains("Blocked Phase 2 gate goals: 0", StringComparison.Ordinal), "blocked gate count");
    AssertTrue(text.Contains("Failed Phase 2 gate goals: 0", StringComparison.Ordinal), "failed gate count");
}

void PortingSequenceKeepsPhase2Gate()
{
    string text = File.ReadAllText(Path.Combine(root, "docs", "headless_complete_porting_sequence.md"));

    AssertTrue(text.Contains("## Phase 2", StringComparison.Ordinal), "Phase 2 section exists");
    AssertTrue(text.Contains("docs/test-results/headless_phase2_core_flow_unit_test_results.md", StringComparison.Ordinal), "Phase 2 aggregate result path");
    AssertTrue(text.Contains("Phase 3", StringComparison.Ordinal), "Phase 3 gate reference");
}

void AggregateEvaluationRejectsIncompleteEvidence()
{
    var missing = Phase2AggregateEvidence.Evaluate("G2X-000", "");
    AssertFalse(missing.IsComplete, "missing document is not complete");
    AssertFalse(missing.HasZeroFailedTests, "missing document has no zero-failure evidence");

    var incomplete = Phase2AggregateEvidence.Evaluate("G2X-001", "Status: PASS\n| Total tests | 3 | 2 | 1 | 0 |");
    AssertFalse(incomplete.IsComplete, "document without COMPLETE is not complete");
    AssertFalse(incomplete.HasZeroFailedTests, "failed count is rejected");
}

void AggregateEvaluationFingerprintIsDeterministic()
{
    string first = Phase2AggregateEvidence.Fingerprint(root, phase2Goals);
    string second = Phase2AggregateEvidence.Fingerprint(root, phase2Goals.Reverse().ToArray());

    AssertEqual(first, second, "fingerprint");
}

void ScopeIsLimitedToAggregateDocumentationAndTests()
{
    string aggregate = File.ReadAllText(Phase2AggregateDocumentPath());

    AssertTrue(aggregate.Contains("No Phase 3 implementation was started", StringComparison.Ordinal), "no Phase 3 implementation statement");
    AssertTrue(aggregate.Contains("No original DCGO/Assets files were modified", StringComparison.Ordinal), "DCGO/Assets safety statement");
    AssertTrue(aggregate.Contains("G2Z-001 scope only", StringComparison.Ordinal), "G2Z-only scope statement");
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

string ResultDocumentPath(Phase2Goal goal)
{
    return Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
}

string Phase2AggregateDocumentPath()
{
    return Path.Combine(root, "docs", "test-results", "headless_phase2_core_flow_unit_test_results.md");
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

sealed record Phase2Goal(string Id, string Area, string ResultDocumentName, string TestProjectDirectory);

sealed record Phase2AggregateEvidence(bool IsComplete, bool HasZeroFailedTests)
{
    public static Phase2AggregateEvidence Evaluate(string goalId, string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return new Phase2AggregateEvidence(false, false);
        }

        bool isComplete = documentText.Contains("COMPLETE", StringComparison.Ordinal);
        bool zeroFailed = HasZeroFailedEvidence(documentText);

        return new Phase2AggregateEvidence(isComplete, zeroFailed);
    }

    public static string Fingerprint(string root, IReadOnlyCollection<Phase2Goal> goals)
    {
        using var sha = SHA256.Create();
        var builder = new StringBuilder();

        foreach (var goal in goals.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            string path = Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
            string text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            Phase2AggregateEvidence evidence = Evaluate(goal.Id, text);
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

        if (Regex.IsMatch(documentText, @"-\s*실패:\s*0\b", RegexOptions.IgnoreCase))
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
