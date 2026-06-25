using System.Text;

var root = FindRepositoryRoot();
var docs = Path.Combine(root, "docs");

var tests = new (string Name, Action Body)[]
{
    ("G0-002 goal row keeps the expected Phase 0 testing contract", GoalRowKeepsExpectedContract),
    ("G0-002 test policy deliverables exist and are non-empty", TestPolicyDeliverablesExist),
    ("G0-002 unit test plan fixes result document policy", UnitTestPlanFixesResultDocumentPolicy),
    ("G0-002 unit test matrix parses with required phase coverage", UnitTestMatrixParsesWithPhaseCoverage),
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
        failures.Add($"{test.Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(ex.Message);
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
    var rows = ReadCsv(Path.Combine(docs, "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G0-002")
        ?? throw new InvalidOperationException("G0-002 row was not found.");

    AssertEqual("Phase 0", Value(row, "phase"), "phase");
    AssertEqual("Testing", Value(row, "area"), "area");
    AssertEqual("단위테스트 정책과 매트릭스 확정", Value(row, "scope"), "scope");
    AssertEqual("unit test plan; unit test matrix", Value(row, "deliverables"), "deliverables");
    AssertEqual("테스트 계획 문서와 매트릭스 파싱", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G0-002_test_policy_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G0-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("테스트 정책 문서가 검증됨", Value(row, "completion_gate"), "completion_gate");
}

void TestPolicyDeliverablesExist()
{
    var deliverables = new[]
    {
        "headless_complete_unit_test_plan.md",
        "headless_complete_unit_test_matrix.csv",
    };

    foreach (var fileName in deliverables)
    {
        var path = Path.Combine(docs, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing deliverable: {path}");
        }

        var info = new FileInfo(path);
        if (info.Length == 0)
        {
            throw new InvalidOperationException($"Deliverable is empty: {path}");
        }
    }
}

void UnitTestPlanFixesResultDocumentPolicy()
{
    var text = File.ReadAllText(Path.Combine(docs, "headless_complete_unit_test_plan.md"), Encoding.UTF8);
    var expectedFragments = new[]
    {
        "docs/test-results/",
        "docs/test-results/goals/<goal_id>_<short_name>_unit_test_results.md",
        "docs/headless_goal_execution_prompt.md",
        "docs/test-results/headless_phase0_design_validation_results.md",
        "docs/test-results/headless_phase1_unity_replacement_unit_test_results.md",
        "docs/test-results/headless_phase2_core_flow_unit_test_results.md",
        "docs/test-results/headless_phase3_shared_rule_effect_unit_test_results.md",
        "docs/test-results/headless_phase4_card_pool_unit_test_results.md",
        "docs/test-results/headless_phase5_rl_adapter_unit_test_results.md",
        "docs/test-results/headless_phase6_parity_regression_test_results.md",
    };

    foreach (var fragment in expectedFragments)
    {
        if (!text.Contains(fragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unit test plan is missing '{fragment}'.");
        }
    }
}

void UnitTestMatrixParsesWithPhaseCoverage()
{
    var path = Path.Combine(docs, "headless_complete_unit_test_matrix.csv");
    var rows = ReadCsv(path);
    if (rows.Count < 7)
    {
        throw new InvalidOperationException($"Unit test matrix has {rows.Count} row(s); expected at least 7.");
    }

    var requiredHeaders = new[]
    {
        "phase",
        "work_area",
        "test_scope",
        "required_test_examples",
        "result_document",
        "completion_gate",
    };

    var headers = rows[0].Keys.ToHashSet(StringComparer.Ordinal);
    foreach (var header in requiredHeaders)
    {
        if (!headers.Contains(header))
        {
            throw new InvalidOperationException($"Unit test matrix is missing required header '{header}'.");
        }
    }

    var requiredPhases = new[] { "Phase 0", "Phase 1", "Phase 2", "Phase 3", "Phase 4", "Phase 5", "Phase 6" };
    foreach (var phase in requiredPhases)
    {
        if (!rows.Any(r => Value(r, "phase") == phase))
        {
            throw new InvalidOperationException($"Unit test matrix has no row for {phase}.");
        }
    }

    foreach (var row in rows)
    {
        var resultDocument = Value(row, "result_document");
        if (!resultDocument.StartsWith("docs/test-results/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid result_document path: {resultDocument}");
        }

        if (string.IsNullOrWhiteSpace(Value(row, "test_scope")))
        {
            throw new InvalidOperationException($"Empty test_scope for {Value(row, "phase")} / {Value(row, "work_area")}.");
        }

        if (string.IsNullOrWhiteSpace(Value(row, "completion_gate")))
        {
            throw new InvalidOperationException($"Empty completion_gate for {Value(row, "phase")} / {Value(row, "work_area")}.");
        }
    }
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    var records = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
    if (records.Count == 0)
    {
        throw new InvalidOperationException($"CSV file has no header row: {path}");
    }

    var headers = records[0];
    if (headers.Any(string.IsNullOrWhiteSpace))
    {
        throw new InvalidOperationException($"CSV file has an empty header: {path}");
    }

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
    var field = new StringBuilder();
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

static void AssertEqual(string expected, string actual, string label)
{
    if (!StringComparer.Ordinal.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}
