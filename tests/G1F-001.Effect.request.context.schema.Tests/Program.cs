using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-001 goal row keeps the effect context schema contract", GoalRowKeepsExpectedContract),
    ("Predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("EffectContext preserves source owner trigger target and context snapshots", EffectContextPreservesSourceOwnerTriggerTargetAndContextSnapshots),
    ("EffectContext rejects invalid source owner trigger target and value keys", EffectContextRejectsInvalidSchemaValues),
    ("EffectRequest preserves effect controller timing and context", EffectRequestPreservesEffectControllerTimingAndContext),
    ("EffectRequest rejects invalid effect controller timing and context", EffectRequestRejectsInvalidSchemaValues),
    ("EffectResult preserves resolved message and value snapshots", EffectResultPreservesResolvedMessageAndValueSnapshots),
    ("EffectResult factories expose success and failure contracts", EffectResultFactoriesExposeSuccessAndFailureContracts),
    ("AS-IS effect context references remain read-only inputs", AsIsEffectContextReferencesRemainReadOnlyInputs),
    ("Effect context schema source files have no placeholder or Unity dependency", EffectContextSchemaSourceFilesHaveNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-001")
        ?? throw new InvalidOperationException("G1F-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("Effect request context schema", Value(row, "goal"), "goal");
    AssertEqual("effect context 계약 확정", Value(row, "scope"), "scope");
    AssertEqual("EffectRequest EffectContext EffectResult", Value(row, "deliverables"), "deliverables");
    AssertEqual("source owner trigger target context 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-001_effect_context_schema_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1B-001; G1E-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Effect context 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1B-001_stable_ids_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1E-001_choice_schema_unit_test_results.md"),
    };

    foreach (string path in paths)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Predecessor result document was not found: {path}");
        }

        string text = File.ReadAllText(path);
        AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), $"{Path.GetFileName(path)} COMPLETE");
    }

    return Task.CompletedTask;
}

Task EffectContextPreservesSourceOwnerTriggerTargetAndContextSnapshots()
{
    var values = new Dictionary<string, object?>
    {
        [" count "] = 2,
        ["reason"] = "on-play",
    };
    var targets = new List<HeadlessEntityId>
    {
        new("target-a"),
        new("target-b"),
    };

    var context = new EffectContext(
        new HeadlessPlayerId(1),
        new HeadlessPlayerId(2),
        new HeadlessEntityId("source-card"),
        new HeadlessEntityId("trigger-card"),
        targets,
        values);

    values["count"] = 99;
    targets.Add(new HeadlessEntityId("target-c"));

    AssertEqual(new HeadlessPlayerId(1), context.SourcePlayerId, "source player");
    AssertEqual(new HeadlessPlayerId(2), context.OwnerPlayerId, "owner player");
    AssertEqual(new HeadlessEntityId("source-card"), context.SourceEntityId, "source entity");
    AssertEqual(new HeadlessEntityId("trigger-card"), context.TriggerEntityId, "trigger entity");
    AssertEqual(2, context.TargetEntityIds.Count, "target snapshot count");
    AssertEqual(new HeadlessEntityId("target-a"), context.TargetEntityIds[0], "first target");
    AssertEqual(2, context.Values["count"], "trimmed context value");
    AssertEqual("on-play", context.Values["reason"], "reason value");
    AssertTrue(context.Values is System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>, "read-only values");
    return Task.CompletedTask;
}

Task EffectContextRejectsInvalidSchemaValues()
{
    var player = new HeadlessPlayerId(1);
    var source = new HeadlessEntityId("source");

    ExpectThrows<ArgumentException>(() => new EffectContext(default, source));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, default));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, default, source, null, Array.Empty<HeadlessEntityId>()));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, player, default, null, Array.Empty<HeadlessEntityId>()));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, player, source, default(HeadlessEntityId), Array.Empty<HeadlessEntityId>()));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, player, source, null, new[] { default(HeadlessEntityId) }));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, player, source, null, new[] { new HeadlessEntityId("target"), new HeadlessEntityId("target") }));
    ExpectThrows<ArgumentException>(() => new EffectContext(player, source, new Dictionary<string, object?> { [" "] = 1 }));
    return Task.CompletedTask;
}

Task EffectRequestPreservesEffectControllerTimingAndContext()
{
    EffectContext context = CreateContext();
    var request = new EffectRequest(
        new HeadlessEntityId("effect-1"),
        new HeadlessPlayerId(2),
        " Main ",
        context);

    AssertEqual(new HeadlessEntityId("effect-1"), request.EffectId, "effect id");
    AssertEqual(new HeadlessPlayerId(2), request.ControllerId, "controller id");
    AssertEqual("Main", request.Timing, "timing trim");
    AssertSame(context, request.Context, "context");
    return Task.CompletedTask;
}

Task EffectRequestRejectsInvalidSchemaValues()
{
    EffectContext context = CreateContext();
    var effectId = new HeadlessEntityId("effect-1");
    var controller = new HeadlessPlayerId(1);

    ExpectThrows<ArgumentException>(() => new EffectRequest(default, controller, "Main", context));
    ExpectThrows<ArgumentException>(() => new EffectRequest(effectId, default, "Main", context));
    ExpectThrows<ArgumentException>(() => new EffectRequest(effectId, controller, "", context));
    ExpectThrows<ArgumentNullException>(() => new EffectRequest(effectId, controller, null!, context));
    ExpectThrows<ArgumentNullException>(() => new EffectRequest(effectId, controller, "Main", null!));
    return Task.CompletedTask;
}

Task EffectResultPreservesResolvedMessageAndValueSnapshots()
{
    var values = new Dictionary<string, object?>
    {
        [" cards "] = 1,
    };

    var result = new EffectResult(Resolved: true, Message: " resolved ", Values: values);
    values["cards"] = 3;

    AssertTrue(result.Resolved, "resolved");
    AssertEqual("resolved", result.Message, "message trim");
    AssertEqual(1, result.Values["cards"], "value snapshot");
    AssertTrue(result.Values is System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>, "read-only values");
    AssertEqual(null, new EffectResult(Resolved: true, Message: " ").Message, "blank message");
    ExpectThrows<ArgumentException>(() => new EffectResult(Resolved: false, Values: new Dictionary<string, object?> { [""] = 1 }));
    return Task.CompletedTask;
}

Task EffectResultFactoriesExposeSuccessAndFailureContracts()
{
    EffectResult success = EffectResult.Success("done", new Dictionary<string, object?> { ["resolved"] = true });
    EffectResult failure = EffectResult.Failure("blocked");

    AssertTrue(success.Resolved, "success resolved");
    AssertEqual("done", success.Message, "success message");
    AssertEqual(true, success.Values["resolved"], "success metadata");
    AssertFalse(failure.Resolved, "failure resolved");
    AssertEqual("blocked", failure.Message, "failure message");
    return Task.CompletedTask;
}

Task AsIsEffectContextReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"),
            new[] { "Skill", "Stack", "Effect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Effects.cs"),
            new[] { "Effects", "MonoBehaviour" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MultipleSkills.cs"),
            new[] { "MultipleSkills", "SkillInfo" }),
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

Task EffectContextSchemaSourceFilesHaveNoPlaceholderOrUnityDependency()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectContext.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectRequest.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectResult.cs"),
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

    string context = File.ReadAllText(paths[0]);
    AssertTrue(context.Contains("OwnerPlayerId", StringComparison.Ordinal), "owner player contract");
    AssertTrue(context.Contains("TriggerEntityId", StringComparison.Ordinal), "trigger contract");
    AssertTrue(context.Contains("TargetEntityIds", StringComparison.Ordinal), "target contract");
    return Task.CompletedTask;
}

static EffectContext CreateContext()
{
    return new EffectContext(
        new HeadlessPlayerId(1),
        new HeadlessPlayerId(1),
        new HeadlessEntityId("source"),
        new HeadlessEntityId("trigger"),
        new[] { new HeadlessEntityId("target") },
        new Dictionary<string, object?> { ["timing"] = "Main" });
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

static void AssertSame<T>(T expected, T actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same reference.");
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
