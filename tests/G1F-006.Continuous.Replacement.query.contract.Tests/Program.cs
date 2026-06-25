using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-006 goal row keeps the effect query contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("EffectQueryContext trims scope and filters source player and target", EffectQueryContextFiltersRequestFields),
    ("Continuous query returns scoped continuous effects only", ContinuousQueryReturnsScopedEffectsOnly),
    ("Replacement query is isolated from continuous modifier and restriction roles", ReplacementQueryIsRoleIsolated),
    ("Modifier and restriction queries return matching scoped requests", ModifierAndRestrictionQueriesReturnMatches),
    ("Missing query boundaries return empty results", MissingQueryBoundariesReturnEmptyResults),
    ("EffectBinding preserves query role and scope snapshots", EffectBindingPreservesQueryRoleAndScopeSnapshots),
    ("Effect query contracts validate invalid inputs", EffectQueryContractsValidateInvalidInputs),
    ("AS-IS continuous replacement query references remain read-only inputs", AsIsContinuousReplacementReferencesRemainReadOnlyInputs),
    ("Effect query source files have no placeholder or Unity dependency", EffectQuerySourceFilesHaveNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-006")
        ?? throw new InvalidOperationException("G1F-006 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("Continuous Replacement query contract", Value(row, "goal"), "goal");
    AssertEqual("IEffectQueryService contracts", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("modifier restriction replacement query", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-006_continuous_replacement_query_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1F-005", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Effect query", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1F-005_effect_registry_contract_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1F-005 COMPLETE");
    return Task.CompletedTask;
}

Task EffectQueryContextFiltersRequestFields()
{
    EffectRequest matching = CreateRequest(
        "effect-match",
        "source-a",
        "Main",
        owner: new HeadlessPlayerId(2),
        targetIds: new[] { new HeadlessEntityId("target-a") });
    EffectRequest sourceMismatch = CreateRequest(
        "effect-source-mismatch",
        "source-b",
        "Main",
        owner: new HeadlessPlayerId(2),
        targetIds: new[] { new HeadlessEntityId("target-a") });
    EffectRequest targetMismatch = CreateRequest(
        "effect-target-mismatch",
        "source-a",
        "Main",
        owner: new HeadlessPlayerId(2),
        targetIds: new[] { new HeadlessEntityId("target-b") });
    var context = new EffectQueryContext(
        " cost ",
        new HeadlessEntityId("source-a"),
        new HeadlessPlayerId(2),
        new HeadlessEntityId("target-a"));

    AssertEqual("cost", context.Scope, "trimmed scope");
    AssertTrue(context.Matches(matching), "matching request");
    AssertFalse(context.Matches(sourceMismatch), "source mismatch");
    AssertFalse(context.Matches(targetMismatch), "target mismatch");
    return Task.CompletedTask;
}

Task ContinuousQueryReturnsScopedEffectsOnly()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding memory = CreateBinding("effect-memory", "source-a", "Main", EffectQueryRole.Continuous, "memory");
    EffectBinding cost = CreateBinding("effect-cost", "source-a", "Main", EffectQueryRole.Continuous, "cost");
    EffectBinding otherSource = CreateBinding("effect-other-source", "source-b", "Main", EffectQueryRole.Continuous, "memory");
    registry.Register(memory);
    registry.Register(cost);
    registry.Register(otherSource);

    IReadOnlyList<EffectRequest> effects = registry.GetContinuousEffects(new EffectQueryContext(
        "memory",
        new HeadlessEntityId("source-a")));

    AssertEqual(1, effects.Count, "continuous count");
    AssertSame(memory.Request, effects[0], "continuous request");
    return Task.CompletedTask;
}

Task ReplacementQueryIsRoleIsolated()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding replacement = CreateBinding("effect-replacement", "source-a", "Main", EffectQueryRole.Replacement, "draw");
    EffectBinding continuous = CreateBinding("effect-continuous", "source-a", "Main", EffectQueryRole.Continuous, "draw");
    EffectBinding modifier = CreateBinding("effect-modifier", "source-a", "Main", EffectQueryRole.Modifier, "draw");
    EffectBinding restriction = CreateBinding("effect-restriction", "source-a", "Main", EffectQueryRole.Restriction, "draw");
    registry.Register(replacement);
    registry.Register(continuous);
    registry.Register(modifier);
    registry.Register(restriction);

    IReadOnlyList<EffectRequest> effects = registry.GetReplacementEffects(new EffectQueryContext("draw"));

    AssertEqual(1, effects.Count, "replacement count");
    AssertSame(replacement.Request, effects[0], "replacement request");
    return Task.CompletedTask;
}

Task ModifierAndRestrictionQueriesReturnMatches()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding modifier = CreateBinding(
        "effect-cost-minus",
        "source-a",
        "Main",
        EffectQueryRole.Modifier,
        "cost",
        targetIds: new[] { new HeadlessEntityId("target-a") });
    EffectBinding restriction = CreateBinding(
        "effect-cannot-play",
        "source-a",
        "Main",
        EffectQueryRole.Restriction,
        "play",
        targetIds: new[] { new HeadlessEntityId("target-a") });
    registry.Register(modifier);
    registry.Register(restriction);

    IReadOnlyList<EffectRequest> modifiers = registry.GetModifierEffects(new EffectQueryContext(
        "cost",
        targetEntityId: new HeadlessEntityId("target-a")));
    IReadOnlyList<EffectRequest> restrictions = registry.GetRestrictionEffects(new EffectQueryContext(
        "play",
        targetEntityId: new HeadlessEntityId("target-a")));

    AssertEqual(1, modifiers.Count, "modifier count");
    AssertSame(modifier.Request, modifiers[0], "modifier request");
    AssertEqual(1, restrictions.Count, "restriction count");
    AssertSame(restriction.Request, restrictions[0], "restriction request");
    return Task.CompletedTask;
}

Task MissingQueryBoundariesReturnEmptyResults()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(CreateBinding("effect-1", "source-a", "Main", EffectQueryRole.Continuous, "memory"));

    AssertEqual(0, registry.GetContinuousEffects(new EffectQueryContext("cost")).Count, "missing scope");
    AssertEqual(0, registry.GetContinuousEffects(new EffectQueryContext("memory", new HeadlessEntityId("source-b"))).Count, "missing source");
    AssertEqual(0, registry.GetReplacementEffects(new EffectQueryContext("memory")).Count, "missing role");
    AssertEqual(0, registry.GetModifierEffects(new EffectQueryContext("memory")).Count, "missing modifier");
    AssertEqual(0, registry.GetRestrictionEffects(new EffectQueryContext("memory")).Count, "missing restriction");

    IEffectQueryService emptyQuery = new InMemoryEffectQueryService();
    AssertEqual(0, emptyQuery.GetContinuousEffects(new EffectQueryContext("memory")).Count, "empty service continuous");
    AssertEqual(0, emptyQuery.GetReplacementEffects(new EffectQueryContext("memory")).Count, "empty service replacement");
    AssertEqual(0, emptyQuery.GetModifierEffects(new EffectQueryContext("memory")).Count, "empty service modifier");
    AssertEqual(0, emptyQuery.GetRestrictionEffects(new EffectQueryContext("memory")).Count, "empty service restriction");
    return Task.CompletedTask;
}

Task EffectBindingPreservesQueryRoleAndScopeSnapshots()
{
    var scopes = new List<string> { " cost ", "cost", "play" };
    var binding = new EffectBinding(
        CreateRequest("effect-1", "source-a", "Main"),
        keywords: null,
        EffectQueryRole.Continuous | EffectQueryRole.Replacement,
        scopes);
    scopes.Add("mutated");

    AssertTrue(binding.HasRole(EffectQueryRole.Continuous), "continuous role");
    AssertTrue(binding.HasRole(EffectQueryRole.Replacement), "replacement role");
    AssertFalse(binding.HasRole(EffectQueryRole.Modifier), "modifier role");
    AssertEqual(2, binding.QueryScopes.Count, "scope count");
    AssertEqual("cost", binding.QueryScopes[0], "trimmed scope");
    AssertEqual("play", binding.QueryScopes[1], "deduplicated scope");
    AssertFalse(binding.QueryScopes.Contains("mutated", StringComparer.Ordinal), "scope snapshot");
    AssertTrue(binding.MatchesQuery(new EffectQueryContext("cost", new HeadlessEntityId("source-a"))), "query match");
    AssertFalse(binding.MatchesQuery(new EffectQueryContext("memory", new HeadlessEntityId("source-a"))), "query scope mismatch");
    return Task.CompletedTask;
}

Task EffectQueryContractsValidateInvalidInputs()
{
    var registry = new InMemoryEffectRegistry();

    ExpectThrows<ArgumentNullException>(() => new EffectQueryContext(null!));
    ExpectThrows<ArgumentException>(() => new EffectQueryContext(" "));
    ExpectThrows<ArgumentException>(() => new EffectQueryContext("cost", default(HeadlessEntityId)));
    ExpectThrows<ArgumentException>(() => new EffectQueryContext("cost", playerId: default(HeadlessPlayerId)));
    ExpectThrows<ArgumentException>(() => new EffectQueryContext("cost", targetEntityId: default(HeadlessEntityId)));
    ExpectThrows<ArgumentOutOfRangeException>(() => new EffectBinding(CreateRequest("effect-1", "source-a", "Main"), null, (EffectQueryRole)1024, new[] { "cost" }));
    ExpectThrows<ArgumentException>(() => new EffectBinding(CreateRequest("effect-2", "source-a", "Main"), null, EffectQueryRole.Continuous, new[] { " " }));
    ExpectThrows<ArgumentNullException>(() => registry.GetContinuousEffects(null!));
    ExpectThrows<ArgumentNullException>(() => registry.GetReplacementEffects(null!));
    ExpectThrows<ArgumentNullException>(() => registry.GetModifierEffects(null!));
    ExpectThrows<ArgumentNullException>(() => registry.GetRestrictionEffects(null!));
    return Task.CompletedTask;
}

Task AsIsContinuousReplacementReferencesRemainReadOnlyInputs()
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
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ContinuousController.cs"),
            new[] { "ContinuousController", "ContinuousController instance" }),
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

Task EffectQuerySourceFilesHaveNoPlaceholderOrUnityDependency()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Services", "IEffectQueryService.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Services", "InMemoryEffectQueryService.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectRegistry.cs"),
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

    string queryService = File.ReadAllText(paths[0]);
    AssertTrue(queryService.Contains("GetContinuousEffects", StringComparison.Ordinal), "continuous query contract");
    AssertTrue(queryService.Contains("GetReplacementEffects", StringComparison.Ordinal), "replacement query contract");
    AssertTrue(queryService.Contains("GetModifierEffects", StringComparison.Ordinal), "modifier query contract");
    AssertTrue(queryService.Contains("GetRestrictionEffects", StringComparison.Ordinal), "restriction query contract");
    return Task.CompletedTask;
}

static EffectBinding CreateBinding(
    string effectId,
    string sourceId,
    string timing,
    EffectQueryRole roles,
    string queryScope,
    IReadOnlyList<HeadlessEntityId>? targetIds = null)
{
    return new EffectBinding(
        CreateRequest(effectId, sourceId, timing, targetIds: targetIds),
        keywords: null,
        roles,
        new[] { queryScope });
}

static EffectRequest CreateRequest(
    string effectId,
    string sourceId,
    string timing,
    HeadlessPlayerId? owner = null,
    IReadOnlyList<HeadlessEntityId>? targetIds = null)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        timing,
        new EffectContext(
            player,
            owner ?? player,
            new HeadlessEntityId(sourceId),
            triggerEntityId: null,
            targetEntityIds: targetIds ?? Array.Empty<HeadlessEntityId>()));
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

static void AssertSame<T>(T expected, T? actual, string label)
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
