// (C2b) RETARGETED. The old EndAttackTriggerHook mechanism this suite exercised was retired at the SkillInfo
// window cutover (C2) and physically deleted (C2b) — its live replacement is the inline OnEndAttack window opened
// by the mirror AttackProcess (StackSkillInfos) + supply conversion, whose behavior is covered end-to-end by
// F1-Tier2-OnEndAttack.Tests. The 6 hook-mechanism tests that constructed `new EndAttackTriggerHook(...)` were
// removed with the type. The Phase-2 goal-row / AS-IS-reference documentation assertions (the evidence the
// G2Z-001 aggregate contract depends on) are preserved verbatim, plus a cutover-state assertion that records the
// hook's physical retirement.

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-005 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS end attack trigger references are recorded", AsIsEndAttackReferencesAreRecorded),
    ("End attack trigger hook is retired; live coverage is the OnEndAttack window", EndAttackHookRetiredWithLiveCoverage),
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
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-005")
        ?? throw new InvalidOperationException("G2G-005 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "End attack trigger", "goal");
    AssertContains(Value(row, "scope"), "end attack event", "scope event");
    AssertContains(Value(row, "scope"), "trigger", "scope trigger");
    AssertEqual("end attack trigger hook", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "end attack trigger", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-005_end_attack_trigger_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-004; G2F-001", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-004_security_check_unit_test_results.md");
    AssertComplete("G2F-001_trigger_collection_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsEndAttackReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string onEndAttack = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "OnEndAttack.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));

    AssertContains(attackProcess, "public IEnumerator EndAttack()", "AS-IS EndAttack method");
    AssertContains(attackProcess, "IsEndAttack = true", "AS-IS end attack flag");
    AssertContains(attackProcess, "StackSkillInfos(EffectHashtable, EffectTiming.OnEndAttack)", "AS-IS OnEndAttack stack");
    AssertContains(attackProcess, "permanent.UntilEndAttackEffects", "AS-IS cleanup until end attack effects");
    AssertContains(onEndAttack, "CanTriggerOnEndAttack", "AS-IS OnEndAttack predicate");
    AssertContains(autoProcessing, "IsEndAttack = true", "AS-IS forced end attack from auto processing");
    return Task.CompletedTask;
}

Task EndAttackHookRetiredWithLiveCoverage()
{
    // The old registry-currency EndAttackTriggerHook substrate was physically deleted at C2b.
    string hookPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EndAttackTriggerHook.cs");
    AssertFalse(File.Exists(hookPath), "retired EndAttackTriggerHook.cs must no longer exist");

    // The mirror AttackProcess now opens the OnEndAttack window inline (StackSkillInfos / OnEndAttack emit), the
    // AS-IS 1:1 mechanism; end-to-end behavior is covered by F1-Tier2-OnEndAttack.
    string mirrorAttackProcess = File.ReadAllText(
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    AssertContains(mirrorAttackProcess, "OnEndAttack", "mirror AttackProcess wires the OnEndAttack window");

    string liveCoverage = Path.Combine(root, "tests", "F1-Tier2-OnEndAttack.Tests", "F1-Tier2-OnEndAttack.Tests.csproj");
    AssertTrue(File.Exists(liveCoverage), "live OnEndAttack coverage suite (F1-Tier2-OnEndAttack) exists");
    return Task.CompletedTask;
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = ParseCsvLine(lines[0]).ToArray();
    return lines.Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line =>
        {
            string[] values = ParseCsvLine(line).ToArray();
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = index < values.Length ? values[index] : string.Empty;
            }

            return row;
        })
        .ToArray();
}

static IEnumerable<string> ParseCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    values.Add(current.ToString());
    return values;
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    string directory = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(directory, "docs", "headless_complete_goal_breakdown.csv")))
    {
        directory = Directory.GetParent(directory)?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    return directory;
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}
