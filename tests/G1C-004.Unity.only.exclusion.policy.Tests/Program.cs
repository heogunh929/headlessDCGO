using HeadlessDCGO.Engine.Headless.Bridge;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1C-004 goal row keeps the Unity-only exclusion contract", GoalRowKeepsExpectedContract),
    ("UnityNullObjectPolicy excludes UI scene animation access as headless no-op", PolicyExcludesUiSceneAnimationAccess),
    ("UnityNullObjectPolicy maps gameplay Unity access to explicit services", PolicyMapsGameplayUnityAccessToServices),
    ("UnityNullObjectPolicy rejects invalid or unknown access deterministically", PolicyRejectsInvalidOrUnknownAccess),
    ("UnityNullObjectPolicy decisions are deterministic for repeated input", PolicyDecisionsAreDeterministic),
    ("AS-IS Unity-only access examples remain read-only references", AsIsUnityOnlyAccessExamplesRemainReadOnlyReferences),
    ("UnityNullObjectPolicy source file no longer contains placeholder TODO contracts", UnityNullObjectPolicyFileHasNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1C-004")
        ?? throw new InvalidOperationException("G1C-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Bridge", Value(row, "area"), "area");
    AssertEqual("Unity-only exclusion policy", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("Unity-only", StringComparison.Ordinal), "scope");
    AssertEqual("UnityNullObjectPolicy", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("UI scene animation access exclusion", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1C-004_unity_exclusion_policy_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1C-002; G1C-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").StartsWith("Unity-only exclusion", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PolicyExcludesUiSceneAnimationAccess()
{
    UnityNullObjectPolicy policy = UnityNullObjectPolicy.Default;
    var cases = new[]
    {
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/AttackProcess.cs",
            "SelectAttackTarget",
            "Outline_Select.gameObject.SetActive(true)",
            UnityOnlyAccessCategory.RenderingUi),
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/ContinuousController.cs",
            "BattleSceneTransition",
            "SceneManager.SetActiveScene(newScene)",
            UnityOnlyAccessCategory.SceneLifecycle),
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/ContinuousController.cs",
            "LoadShowCutInAnimation",
            "showCutInAnimation",
            UnityOnlyAccessCategory.Animation),
    };

    foreach (UnityOnlyAccess access in cases)
    {
        UnityNullObjectDecision decision = policy.Evaluate(access);

        AssertEqual(UnityNullObjectDecisionKind.ExcludeNoOp, decision.Decision, access.AccessExpression);
        AssertTrue(decision.IsExcluded, $"{access.AccessExpression} excluded");
        AssertFalse(decision.MutatesGameplayState, $"{access.AccessExpression} gameplay mutation");
        AssertEqual("No-op in headless execution.", decision.Replacement, $"{access.AccessExpression} replacement");
        AssertFalse(decision.IsRejected, $"{access.AccessExpression} rejected");
        AssertTrue(policy.ShouldExclude(access), $"{access.AccessExpression} should exclude");
    }

    return Task.CompletedTask;
}

Task PolicyMapsGameplayUnityAccessToServices()
{
    UnityNullObjectPolicy policy = new();
    var cases = new[]
    {
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/AttackProcess.cs",
            "Attack",
            "GManager.instance.turnStateMachine",
            UnityOnlyAccessCategory.GlobalSingleton,
            MutatesGameplayState: true),
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/ContinuousController.cs",
            "Awake",
            "ContinuousController.instance",
            UnityOnlyAccessCategory.GlobalSingleton),
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
            "RuleProcess",
            "GManager.instance.GetComponent<Effects>()",
            UnityOnlyAccessCategory.EffectContext,
            MutatesGameplayState: true),
        new UnityOnlyAccess(
            "DCGO/Assets/Scripts/Script/AttackProcess.cs",
            "OnTargetArrow",
            "PermanentFrame.GetLocalCanvasPosition()",
            UnityOnlyAccessCategory.GameObjectTransform,
            MutatesGameplayState: true),
    };

    foreach (UnityOnlyAccess access in cases)
    {
        UnityNullObjectDecision decision = policy.Evaluate(access);

        AssertEqual(UnityNullObjectDecisionKind.ReplaceWithHeadlessService, decision.Decision, access.AccessExpression);
        AssertTrue(decision.IsReplacementRequired, $"{access.AccessExpression} replacement");
        AssertFalse(decision.IsExcluded, $"{access.AccessExpression} excluded");
        AssertFalse(decision.IsRejected, $"{access.AccessExpression} rejected");
        AssertTrue(decision.Replacement.Contains("Use ", StringComparison.Ordinal), $"{access.AccessExpression} replacement text");
    }

    return Task.CompletedTask;
}

Task PolicyRejectsInvalidOrUnknownAccess()
{
    UnityNullObjectPolicy policy = new();
    UnityNullObjectDecision unknown = policy.Evaluate(new UnityOnlyAccess(
        "DCGO/Assets/Scripts/Script/GManager.cs",
        "UnknownMember",
        "UnknownUnityAccess",
        UnityOnlyAccessCategory.Unknown));

    AssertEqual(UnityNullObjectDecisionKind.RejectInvalid, unknown.Decision, "unknown decision");
    AssertTrue(unknown.IsRejected, "unknown rejected");
    AssertFalse(unknown.IsExcluded, "unknown excluded");
    AssertFalse(unknown.IsReplacementRequired, "unknown replacement");

    bool evaluated = policy.TryEvaluate(new UnityOnlyAccess(
        "",
        "Member",
        "gameObject.SetActive(false)",
        UnityOnlyAccessCategory.RenderingUi),
        out UnityNullObjectDecision invalid);

    AssertFalse(evaluated, "blank source try evaluate");
    AssertEqual(UnityNullObjectDecisionKind.RejectInvalid, invalid.Decision, "blank source decision");
    ExpectThrows<ArgumentNullException>(() => policy.Evaluate(null!));
    return Task.CompletedTask;
}

Task PolicyDecisionsAreDeterministic()
{
    UnityNullObjectPolicy policy = UnityNullObjectPolicy.Default;
    UnityOnlyAccess access = new(
        "DCGO/Assets/Scripts/Script/ContinuousController.cs",
        "LoadShowCutInAnimation",
        "showCutInAnimation",
        UnityOnlyAccessCategory.Animation);

    UnityNullObjectDecision first = policy.Evaluate(access);
    UnityNullObjectDecision second = policy.Evaluate(access);

    AssertEqual(first, second, "repeated decision");
    AssertEqual(first.GetHashCode(), second.GetHashCode(), "repeated decision hash");
    return Task.CompletedTask;
}

Task AsIsUnityOnlyAccessExamplesRemainReadOnlyReferences()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"),
            new[] { "GManager.instance", "ContinuousController.instance", "SetActive" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ContinuousController.cs"),
            new[] { "ContinuousController instance", "SceneManager.SetActiveScene", "showCutInAnimation" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"),
            new[] { "GManager.instance", "GetComponent<Effects>", "CardObjectController" }),
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

Task UnityNullObjectPolicyFileHasNoTodoContracts()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Bridge", "UnityNullObjectPolicy.cs");
    string text = File.ReadAllText(path);
    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("UnityNullObjectPolicy.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "policy must not reference UnityEngine");
    AssertFalse(text.Contains("Photon", StringComparison.Ordinal), "policy must not reference Photon");
    return Task.CompletedTask;
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

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
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
