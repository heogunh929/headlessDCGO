using System.Text;

var root = FindRepositoryRoot();
var docs = Path.Combine(root, "docs");

var tests = new (string Name, Action Body)[]
{
    ("G0-003 goal row keeps the expected Phase 0 gate contract", GoalRowKeepsExpectedContract),
    ("G0-003 predecessors have passing result documents", PredecessorsHavePassingResultDocuments),
    ("G0-003 phase0 validation result proves the Phase 1 gate", Phase0ValidationResultProvesGate),
    ("G0-003 documents prohibit later asset/card porting before Phase 1 completion", DocumentsProhibitLaterPortingBeforePhase1Completion),
    ("G0-003 goal graph opens Phase 1 only and does not advance later phases", GoalGraphOpensOnlyPhase1),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G0-003")
        ?? throw new InvalidOperationException("G0-003 row was not found.");

    AssertEqual("Phase 0", Value(row, "phase"), "phase");
    AssertEqual("Gate", Value(row, "area"), "area");
    AssertEqual("Unity 대체 기반 선행 조건 확인", Value(row, "scope"), "scope");
    AssertEqual("phase0 validation result", Value(row, "deliverables"), "deliverables");
    AssertEqual("Phase 1 선행 조건과 후속 포팅 금지 확인", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G0-003_phase1_gate_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G0-002", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Phase 1 착수 가능 판정", Value(row, "completion_gate"), "completion_gate");
}

void PredecessorsHavePassingResultDocuments()
{
    var predecessorDocs = new[]
    {
        "docs/test-results/goals/G0-001_design_artifacts_unit_test_results.md",
        "docs/test-results/goals/G0-002_test_policy_unit_test_results.md",
    };

    foreach (var relativePath in predecessorDocs)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing predecessor result document: {relativePath}");
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        if (!text.Contains("Status: PASS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{relativePath} does not record Status: PASS.");
        }

        if (!text.Contains("test(s) passed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{relativePath} does not record a passing test count.");
        }
    }
}

void Phase0ValidationResultProvesGate()
{
    var relativePath = "docs/test-results/headless_phase0_design_validation_results.md";
    var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Missing Phase 0 validation result: {relativePath}");
    }

    var text = File.ReadAllText(path, Encoding.UTF8);
    var requiredFragments = new[]
    {
        "Phase 1 선행 조건",
        "후속 포팅 차단 조건",
        "PASS",
        "Unity 대체 기반",
        "Phase 1 완료 전",
    };

    foreach (var fragment in requiredFragments)
    {
        if (!text.Contains(fragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{relativePath} is missing '{fragment}'.");
        }
    }
}

void DocumentsProhibitLaterPortingBeforePhase1Completion()
{
    AssertContainsAll(
        "docs/headless_complete_architecture_design.md",
        "Unity 대체 기반",
        "완성되기 전에는 `Assets/...` 룰, 카드 효과, 전투 로직 포팅을 시작하지 않는다",
        "Phase 1 단위테스트가 통과하지 않으면 Phase 2 포팅을 시작하지 않는다");

    AssertContainsAll(
        "docs/headless_complete_porting_sequence.md",
        "`Assets/...` 룰/카드 효과 포팅은 Unity 대체 기반이 완료된 뒤에 시작한다",
        "이 단계가 끝나기 전에는 `Assets/...` 룰/효과 포팅을 시작하지 않는다",
        "Phase 1 전체 단위테스트가 통과한다");

    AssertContainsAll(
        "docs/headless_complete_unit_test_plan.md",
        "Unity 대체 기반이 Phase 1로 명시되어 있다",
        "Phase 1 완료 전 `Assets/...` 포팅 금지가 명시되어 있다",
        "Phase 1 전체 단위테스트가 통과한다");
}

void GoalGraphOpensOnlyPhase1()
{
    var rows = ReadCsv(Path.Combine(docs, "headless_complete_goal_breakdown.csv"));
    var firstPhase1 = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-001")
        ?? throw new InvalidOperationException("G1A-001 row was not found.");

    AssertEqual("Phase 1", Value(firstPhase1, "phase"), "G1A-001 phase");
    AssertEqual("G0-003", Value(firstPhase1, "blocked_until"), "G1A-001 blocked_until");

    var laterRowsDirectlyOpenedByG0_003 = rows
        .Where(r => IsLaterThanPhase1(Value(r, "phase")) && Value(r, "blocked_until").Split(';', StringSplitOptions.TrimEntries).Contains("G0-003"))
        .Select(r => Value(r, "goal_id"))
        .ToList();

    if (laterRowsDirectlyOpenedByG0_003.Count > 0)
    {
        throw new InvalidOperationException("Later phases are directly opened by G0-003: " + string.Join(", ", laterRowsDirectlyOpenedByG0_003));
    }
}

void AssertContainsAll(string relativePath, params string[] fragments)
{
    var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Missing document: {relativePath}");
    }

    var text = File.ReadAllText(path, Encoding.UTF8);
    foreach (var fragment in fragments)
    {
        if (!text.Contains(fragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{relativePath} is missing '{fragment}'.");
        }
    }
}

static bool IsLaterThanPhase1(string phase)
{
    return phase.StartsWith("Phase ", StringComparison.Ordinal)
        && int.TryParse(phase["Phase ".Length..], out var number)
        && number > 1;
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
