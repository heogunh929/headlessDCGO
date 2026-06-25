using System.Text;

var root = FindRepositoryRoot();
var docs = Path.Combine(root, "docs");

var tests = new (string Name, Action Body)[]
{
    ("G0-001 goal row keeps the expected Phase 0 design contract", GoalRowKeepsExpectedContract),
    ("G0-001 design deliverable documents exist and are non-empty", DesignDeliverablesExist),
    ("G0-001 CSV deliverables parse with required headers and rows", CsvDeliverablesParse),
    ("G0-001 porting sequence lists the design artifact bundle", PortingSequenceListsDesignBundle),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G0-001")
        ?? throw new InvalidOperationException("G0-001 row was not found.");

    AssertEqual("Phase 0", Value(row, "phase"), "phase");
    AssertEqual("Design", Value(row, "area"), "area");
    AssertEqual("핵심 설계 문서와 CSV 확정", Value(row, "scope"), "scope");
    AssertEqual("architecture design; modules csv; dependency csv; porting sequence", Value(row, "deliverables"), "deliverables");
    AssertEqual("문서 존재와 CSV 파싱", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G0-001_design_artifacts_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("None", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("문서와 CSV가 검증됨", Value(row, "completion_gate"), "completion_gate");
}

void DesignDeliverablesExist()
{
    var deliverables = new[]
    {
        "headless_complete_architecture_design.md",
        "headless_complete_architecture_modules.csv",
        "headless_complete_dependency_replacement.csv",
        "headless_complete_porting_sequence.md",
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

void CsvDeliverablesParse()
{
    AssertCsv(
        "headless_complete_architecture_modules.csv",
        new[] { "stage", "area", "module", "path", "responsibility", "asis_sources", "replaces", "public_api", "input", "output", "completion_criteria", "priority", "notes" },
        minimumRows: 1);

    AssertCsv(
        "headless_complete_dependency_replacement.csv",
        new[] { "gate", "dependency", "asis_kind", "source_patterns", "role_in_unity_client", "headless_replacement_module", "replacement_design", "completion_criteria", "priority", "notes" },
        minimumRows: 1);

    AssertCsv(
        "headless_complete_goal_breakdown.csv",
        new[] { "goal_id", "phase", "area", "goal", "scope", "deliverables", "unit_test_scope", "result_document", "blocked_until", "completion_gate", "priority" },
        minimumRows: 1);
}

void PortingSequenceListsDesignBundle()
{
    var text = File.ReadAllText(Path.Combine(docs, "headless_complete_porting_sequence.md"), Encoding.UTF8);
    var expected = new[]
    {
        "docs/headless_complete_architecture_design.md",
        "docs/headless_complete_architecture_modules.csv",
        "docs/headless_complete_dependency_replacement.csv",
        "docs/headless_complete_porting_sequence.md",
    };

    foreach (var item in expected)
    {
        if (!text.Contains(item, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Porting sequence does not list {item}.");
        }
    }
}

void AssertCsv(string fileName, IReadOnlyCollection<string> requiredHeaders, int minimumRows)
{
    var rows = ReadCsv(Path.Combine(docs, fileName));
    if (rows.Count < minimumRows)
    {
        throw new InvalidOperationException($"{fileName} has {rows.Count} row(s); expected at least {minimumRows}.");
    }

    var headers = rows[0].Keys.ToHashSet(StringComparer.Ordinal);
    foreach (var header in requiredHeaders)
    {
        if (!headers.Contains(header))
        {
            throw new InvalidOperationException($"{fileName} is missing required header '{header}'.");
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
