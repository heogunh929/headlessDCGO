using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1F-005 goal row keeps the EffectRegistry contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("Register stores bindings and rejects duplicate effect ids", RegisterStoresBindingsAndRejectsDuplicates),
    ("GetEffects filters by source entity and timing in registration order", GetEffectsFiltersBySourceAndTiming),
    ("GetKeywordEffects trims keywords and returns matching bindings", GetKeywordEffectsTrimsAndReturnsMatches),
    ("Missing binding lookups return empty results or false", MissingBindingLookupsReturnEmptyResults),
    ("Registry exposes IEffectQueryService timing lookup contract", RegistryExposesEffectQueryServiceContract),
    ("EffectBinding preserves request and immutable keyword snapshot", EffectBindingPreservesRequestAndKeywordSnapshot),
    ("Registry validates binding source timing keyword and id inputs", RegistryValidatesInputs),
    ("AS-IS effect registry references remain read-only inputs", AsIsEffectRegistryReferencesRemainReadOnlyInputs),
    ("EffectRegistry source has no placeholder or Unity dependency", EffectRegistrySourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1F-005")
        ?? throw new InvalidOperationException("G1F-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Effects", Value(row, "area"), "area");
    AssertEqual("EffectRegistry contract", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("effect lookup contract", StringComparison.Ordinal), "scope");
    AssertEqual("EffectRegistry interface", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("register lookup missing binding", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1F-005_effect_registry_contract_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1F-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("EffectRegistry", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1F-001_effect_context_schema_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1F-001 COMPLETE");
    return Task.CompletedTask;
}

Task RegisterStoresBindingsAndRejectsDuplicates()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding binding = CreateBinding("effect-1", "card-a", "Main", "OnPlay");
    registry.Register(binding);

    AssertTrue(registry.HasEffect(new HeadlessEntityId("effect-1")), "has effect");
    AssertSame(binding, registry.Find(new HeadlessEntityId("effect-1")), "found binding");
    ExpectThrows<InvalidOperationException>(() => registry.Register(CreateBinding("effect-1", "card-b", "Attack")));
    return Task.CompletedTask;
}

Task GetEffectsFiltersBySourceAndTiming()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding first = CreateBinding("effect-1", "card-a", "Main");
    EffectBinding second = CreateBinding("effect-2", "card-b", "Main");
    EffectBinding third = CreateBinding("effect-3", "card-a", "Attack");
    EffectBinding fourth = CreateBinding("effect-4", "card-a", "Main");
    registry.Register(first);
    registry.Register(second);
    registry.Register(third);
    registry.Register(fourth);

    IReadOnlyList<EffectBinding> effects = registry.GetEffects(new HeadlessEntityId("card-a"), " Main ");

    AssertEqual(2, effects.Count, "effect count");
    AssertSame(first, effects[0], "first matching binding");
    AssertSame(fourth, effects[1], "second matching binding");
    AssertEqual("effect-1,effect-4", JoinEffectIds(effects), "registration order");
    return Task.CompletedTask;
}

Task GetKeywordEffectsTrimsAndReturnsMatches()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding onPlay = CreateBinding("effect-1", "card-a", "Main", " OnPlay ", "Draw");
    EffectBinding onAttack = CreateBinding("effect-2", "card-b", "Attack", "OnAttack");
    EffectBinding draw = CreateBinding("effect-3", "card-c", "Main", "Draw");
    registry.Register(onPlay);
    registry.Register(onAttack);
    registry.Register(draw);

    IReadOnlyList<EffectBinding> drawEffects = registry.GetKeywordEffects(" Draw ");
    IReadOnlyList<EffectBinding> attackEffects = registry.GetKeywordEffects("OnAttack");

    AssertEqual(2, drawEffects.Count, "draw effect count");
    AssertSame(onPlay, drawEffects[0], "first draw effect");
    AssertSame(draw, drawEffects[1], "second draw effect");
    AssertEqual(1, attackEffects.Count, "attack effect count");
    AssertSame(onAttack, attackEffects[0], "attack effect");
    return Task.CompletedTask;
}

Task MissingBindingLookupsReturnEmptyResults()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(CreateBinding("effect-1", "card-a", "Main", "OnPlay"));

    AssertFalse(registry.HasEffect(new HeadlessEntityId("missing-effect")), "missing has effect");
    AssertEqual(null, registry.Find(new HeadlessEntityId("missing-effect")), "missing find");
    AssertEqual(null, registry.Find(default), "empty find");
    AssertEqual(0, registry.GetEffects(new HeadlessEntityId("card-a"), "Attack").Count, "missing timing");
    AssertEqual(0, registry.GetEffects(new HeadlessEntityId("card-b"), "Main").Count, "missing source");
    AssertEqual(0, registry.GetKeywordEffects("OnDelete").Count, "missing keyword");
    AssertEqual(0, registry.GetEffectsForTiming("Attack").Count, "missing timing query");
    return Task.CompletedTask;
}

Task RegistryExposesEffectQueryServiceContract()
{
    var registry = new InMemoryEffectRegistry();
    EffectBinding first = CreateBinding("effect-1", "card-a", "Main");
    EffectBinding second = CreateBinding("effect-2", "card-b", "Attack");
    EffectBinding third = CreateBinding("effect-3", "card-c", "Main");
    registry.Register(first);
    registry.Register(second);
    registry.Register(third);

    IEffectQueryService queryService = registry;
    IReadOnlyList<EffectRequest> effects = queryService.GetEffectsForTiming(" Main ");

    AssertEqual(2, effects.Count, "query effect count");
    AssertSame(first.Request, effects[0], "first request");
    AssertSame(third.Request, effects[1], "third request");
    AssertTrue(queryService.HasEffect(new HeadlessEntityId("effect-2")), "query has effect");
    return Task.CompletedTask;
}

Task EffectBindingPreservesRequestAndKeywordSnapshot()
{
    EffectRequest request = CreateRequest("effect-1", "card-a", "Main");
    var keywords = new List<string> { " OnPlay ", "Draw", "Draw" };
    var binding = new EffectBinding(request, keywords);
    keywords.Add("Mutated");

    AssertSame(request, binding.Request, "request");
    AssertEqual(2, binding.Keywords.Count, "keyword count");
    AssertEqual("OnPlay", binding.Keywords[0], "trimmed keyword");
    AssertEqual("Draw", binding.Keywords[1], "deduplicated keyword");
    AssertFalse(binding.Keywords.Contains("Mutated", StringComparer.Ordinal), "keyword snapshot");
    return Task.CompletedTask;
}

Task RegistryValidatesInputs()
{
    var registry = new InMemoryEffectRegistry();

    ExpectThrows<ArgumentNullException>(() => registry.Register(null!));
    ExpectThrows<ArgumentNullException>(() => new EffectBinding(null!));
    ExpectThrows<ArgumentException>(() => new EffectBinding(CreateRequest("effect-1", "card-a", "Main"), new[] { " " }));
    ExpectThrows<ArgumentException>(() => registry.GetEffects(default, "Main"));
    ExpectThrows<ArgumentNullException>(() => registry.GetEffects(new HeadlessEntityId("card-a"), null!));
    ExpectThrows<ArgumentException>(() => registry.GetEffects(new HeadlessEntityId("card-a"), " "));
    ExpectThrows<ArgumentNullException>(() => registry.GetKeywordEffects(null!));
    ExpectThrows<ArgumentException>(() => registry.GetKeywordEffects(" "));
    ExpectThrows<ArgumentNullException>(() => registry.GetEffectsForTiming(null!));
    ExpectThrows<ArgumentException>(() => registry.GetEffectsForTiming(" "));
    AssertFalse(registry.HasEffect(default), "empty has effect");
    return Task.CompletedTask;
}

Task AsIsEffectRegistryReferencesRemainReadOnlyInputs()
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

Task EffectRegistrySourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectRegistry.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("EffectRegistry.cs still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "EffectRegistry must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "EffectRegistry must not reference MonoBehaviour");
    AssertTrue(text.Contains("Register", StringComparison.Ordinal), "register contract");
    AssertTrue(text.Contains("GetEffects", StringComparison.Ordinal), "get effects contract");
    AssertTrue(text.Contains("GetKeywordEffects", StringComparison.Ordinal), "keyword lookup contract");
    return Task.CompletedTask;
}

static EffectBinding CreateBinding(
    string effectId,
    string sourceId,
    string timing,
    params string[] keywords)
{
    return new EffectBinding(CreateRequest(effectId, sourceId, timing), keywords);
}

static EffectRequest CreateRequest(
    string effectId,
    string sourceId,
    string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        timing,
        new EffectContext(
            player,
            player,
            new HeadlessEntityId(sourceId),
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

static string JoinEffectIds(IEnumerable<EffectBinding> bindings)
{
    return string.Join(",", bindings.Select(binding => binding.Request.EffectId.Value));
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
