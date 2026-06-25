using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var root = FindRepositoryRoot();

var phase3Goals = new Phase3Goal[]
{
    new("G3A-001", "EffectContract", "G3A-001_icard_effect_contract_unit_test_results.md", "G3A-001.ICardEffect.contract.Tests"),
    new("G3A-002", "EffectContract", "G3A-002_skill_info_unit_test_results.md", "G3A-002.SkillInfo.Tests"),
    new("G3B-001", "EffectContext", "G3B-001_hashtable_replacement_adapter_unit_test_results.md", "G3B-001.Hashtable.replacement.adapter.Tests"),
    new("G3C-001", "Conditions", "G3C-001_trigger_condition_helpers_unit_test_results.md", "G3C-001.Trigger.condition.helper.Tests"),
    new("G3C-002", "Conditions", "G3C-002_can_use_effect_helpers_unit_test_results.md", "G3C-002.CanUseEffects.helper.Tests"),
    new("G3D-001", "Requirements", "G3D-001_minmax_dp_cost_level_unit_test_results.md", "G3D-001.MinMax.DP.Cost.Level.helper.Tests"),
    new("G3D-002", "Requirements", "G3D-002_name_color_trait_requirements_unit_test_results.md", "G3D-002.Name.color.trait.requirement.Tests"),
    new("G3E-001", "Costs", "G3E-001_play_cost_helper_unit_test_results.md", "G3E-001.Play.cost.helper.Tests"),
    new("G3E-002", "Costs", "G3E-002_digivolution_cost_helper_unit_test_results.md", "G3E-002.Digivolution.cost.helper.Tests"),
    new("G3F-001", "Targeting", "G3F-001_target_filtering_helpers_unit_test_results.md", "G3F-001.Target.filtering.helper.Tests"),
    new("G3F-002", "Targeting", "G3F-002_zone_query_helpers_unit_test_results.md", "G3F-002.Zone.query.helper.Tests"),
    new("G3G-001", "Keywords", "G3G-001_keyword_base_batch1_unit_test_results.md", "G3G-001.Keyword.base.batch.1.Tests"),
    new("G3G-002", "Keywords", "G3G-002_keyword_base_batch2_unit_test_results.md", "G3G-002.Keyword.base.batch.2.Tests"),
    new("G3H-001", "Modifiers", "G3H-001_modifier_helpers_unit_test_results.md", "G3H-001.DP.cost.security.attack.modifier.helper.Tests"),
    new("G3H-002", "Restrictions", "G3H-002_cannot_restriction_helpers_unit_test_results.md", "G3H-002.Cannot.restriction.helper.Tests"),
    new("G3I-001", "Replacement", "G3I-001_replacement_prevention_helpers_unit_test_results.md", "G3I-001.Replacement.prevention.helper.Tests"),
    new("G3I-002", "Continuous", "G3I-002_continuous_effect_evaluator_unit_test_results.md", "G3I-002.Continuous.effect.evaluator.Tests"),
    new("G3J-001", "Factory", "G3J-001_card_effect_factory_binding_unit_test_results.md", "G3J-001.CardEffectFactory.binding.Tests"),
    new("G3J-002", "Factory", "G3J-002_permanent_effect_factory_binding_unit_test_results.md", "G3J-002.PermanentEffectFactory.binding.Tests"),
    new("G3K-001", "Selection", "G3K-001_effect_selection_helpers_unit_test_results.md", "G3K-001.Effect.selection.helper.Tests"),
    new("G3K-002", "Timing", "G3K-002_timing_priority_helpers_unit_test_results.md", "G3K-002.Timing.priority.helper.Tests"),
    new("G3L-001", "Flags", "G3L-001_once_per_turn_flags_unit_test_results.md", "G3L-001.Once.per.turn.flag.helper.Tests"),
    new("G3L-002", "Inherited", "G3L-002_inherited_granted_security_helpers_unit_test_results.md", "G3L-002.Inherited.granted.security.helper.Tests"),
};

var tests = new (string Name, Action Body)[]
{
    ("G3Z-001 goal row keeps the Phase 3 aggregate contract", GoalRowKeepsExpectedContract),
    ("All Phase 3 result documents exist and are complete", Phase3ResultDocumentsExistAndAreComplete),
    ("All Phase 3 test projects exist", Phase3TestProjectsExist),
    ("Phase 3 aggregate result document links every Phase 3 result", AggregateDocumentLinksEveryPhase3Result),
    ("Phase 3 aggregate result document records the gate counts", AggregateDocumentRecordsGateCounts),
    ("Porting sequence keeps Phase 3 as the Phase 4 start gate", PortingSequenceKeepsPhase3Gate),
    ("Aggregate evaluation rejects missing or incomplete predecessor evidence", AggregateEvaluationRejectsIncompleteEvidence),
    ("Aggregate evaluation fingerprint is deterministic", AggregateEvaluationFingerprintIsDeterministic),
    ("G3Z-001 stays inside documentation and test scope", ScopeIsLimitedToAggregateDocumentationAndTests),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3Z-001")
        ?? throw new InvalidOperationException("G3Z-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Gate", Value(row, "area"), "area");
    AssertEqual("Phase 3 aggregate result", Value(row, "goal"), "goal");
    AssertEqual("phase3 result document", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("Phase 3", StringComparison.Ordinal), "unit test scope mentions Phase 3");
    AssertFalse(string.IsNullOrWhiteSpace(Value(row, "unit_test_scope")), "unit test scope is populated");
    AssertEqual("docs/test-results/goals/G3Z-001_phase3_aggregate_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G3L-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Phase 4", StringComparison.Ordinal), "completion gate");
    AssertEqual("HIGH", Value(row, "priority"), "priority");
}

void Phase3ResultDocumentsExistAndAreComplete()
{
    foreach (var goal in phase3Goals)
    {
        string path = ResultDocumentPath(goal);
        AssertTrue(File.Exists(path), $"{goal.Id} result document exists");

        string text = File.ReadAllText(path);
        var evidence = Phase3AggregateEvidence.Evaluate(goal.Id, text);
        AssertTrue(evidence.IsComplete, $"{goal.Id} records COMPLETE");
        AssertTrue(evidence.HasZeroFailedTests, $"{goal.Id} records zero failed tests");
    }
}

void Phase3TestProjectsExist()
{
    foreach (var goal in phase3Goals)
    {
        string projectDirectory = Path.Combine(root, "tests", goal.TestProjectDirectory);
        string projectFile = Path.Combine(projectDirectory, $"{goal.TestProjectDirectory}.csproj");
        AssertTrue(Directory.Exists(projectDirectory), $"{goal.Id} test project directory exists");
        AssertTrue(File.Exists(projectFile), $"{goal.Id} test project file exists");
    }
}

void AggregateDocumentLinksEveryPhase3Result()
{
    string path = Phase3AggregateDocumentPath();
    AssertTrue(File.Exists(path), "Phase 3 aggregate result document exists");

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("G3Z-001", StringComparison.Ordinal), "aggregate document mentions G3Z-001");
    AssertTrue(text.Contains("Phase 4 start: ALLOWED", StringComparison.Ordinal), "aggregate document records Phase 4 allowed gate");

    foreach (var goal in phase3Goals)
    {
        AssertTrue(text.Contains(goal.Id, StringComparison.Ordinal), $"{goal.Id} linked");
        AssertTrue(text.Contains(goal.ResultDocumentName, StringComparison.Ordinal), $"{goal.Id} result document linked");
        AssertTrue(text.Contains(goal.TestProjectDirectory, StringComparison.Ordinal), $"{goal.Id} test project linked");
    }
}

void AggregateDocumentRecordsGateCounts()
{
    string text = File.ReadAllText(Phase3AggregateDocumentPath());

    AssertTrue(text.Contains("Required Phase 3 gate goals: 23", StringComparison.Ordinal), "required gate count");
    AssertTrue(text.Contains("Complete Phase 3 gate goals: 23", StringComparison.Ordinal), "complete gate count");
    AssertTrue(text.Contains("Blocked Phase 3 gate goals: 0", StringComparison.Ordinal), "blocked gate count");
    AssertTrue(text.Contains("Failed Phase 3 gate goals: 0", StringComparison.Ordinal), "failed gate count");
}

void PortingSequenceKeepsPhase3Gate()
{
    string text = File.ReadAllText(Path.Combine(root, "docs", "headless_complete_porting_sequence.md"));

    AssertTrue(text.Contains("## Phase 3", StringComparison.Ordinal), "Phase 3 section exists");
    AssertTrue(text.Contains("docs/test-results/headless_phase3_shared_rule_effect_unit_test_results.md", StringComparison.Ordinal), "Phase 3 aggregate result path");
    AssertTrue(text.Contains("Phase 4", StringComparison.Ordinal), "Phase 4 gate reference");
}

void AggregateEvaluationRejectsIncompleteEvidence()
{
    var missing = Phase3AggregateEvidence.Evaluate("G3X-000", "");
    AssertFalse(missing.IsComplete, "missing document is not complete");
    AssertFalse(missing.HasZeroFailedTests, "missing document has no zero-failure evidence");

    var incomplete = Phase3AggregateEvidence.Evaluate("G3X-001", "Status: PASS\n| Total tests | 3 | 2 | 1 | 0 |");
    AssertFalse(incomplete.IsComplete, "document without COMPLETE is not complete");
    AssertFalse(incomplete.HasZeroFailedTests, "failed count is rejected");
}

void AggregateEvaluationFingerprintIsDeterministic()
{
    string first = Phase3AggregateEvidence.Fingerprint(root, phase3Goals);
    string second = Phase3AggregateEvidence.Fingerprint(root, phase3Goals.Reverse().ToArray());

    AssertEqual(first, second, "fingerprint");
}

void ScopeIsLimitedToAggregateDocumentationAndTests()
{
    string aggregate = File.ReadAllText(Phase3AggregateDocumentPath());

    AssertTrue(aggregate.Contains("No Phase 4 implementation was started", StringComparison.Ordinal), "no Phase 4 implementation statement");
    AssertTrue(aggregate.Contains("No original DCGO/Assets files were modified", StringComparison.Ordinal), "DCGO/Assets safety statement");
    AssertTrue(aggregate.Contains("G3Z-001 scope only", StringComparison.Ordinal), "G3Z-only scope statement");
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

string ResultDocumentPath(Phase3Goal goal)
{
    return Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
}

string Phase3AggregateDocumentPath()
{
    return Path.Combine(root, "docs", "test-results", "headless_phase3_shared_rule_effect_unit_test_results.md");
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

sealed record Phase3Goal(string Id, string Area, string ResultDocumentName, string TestProjectDirectory);

sealed record Phase3AggregateEvidence(bool IsComplete, bool HasZeroFailedTests)
{
    public static Phase3AggregateEvidence Evaluate(string goalId, string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return new Phase3AggregateEvidence(false, false);
        }

        bool isComplete = documentText.Contains("COMPLETE", StringComparison.Ordinal);
        bool zeroFailed = HasZeroFailedEvidence(documentText);

        return new Phase3AggregateEvidence(isComplete, zeroFailed);
    }

    public static string Fingerprint(string root, IReadOnlyCollection<Phase3Goal> goals)
    {
        using var sha = SHA256.Create();
        var builder = new StringBuilder();

        foreach (var goal in goals.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            string path = Path.Combine(root, "docs", "test-results", "goals", goal.ResultDocumentName);
            string text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            Phase3AggregateEvidence evidence = Evaluate(goal.Id, text);
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

        if (Regex.IsMatch(documentText, @"-\s*?ㅽ뙣:\s*0\b", RegexOptions.IgnoreCase))
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
