using System.Collections;
using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId SourceId = new("source-card");
HeadlessEntityId HostId = new("host-digimon");
HeadlessEntityId GrantedTargetId = new("granted-target");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3L-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS inherited granted security references are recorded", AsIsReferencesAreRecorded),
    ("Inherited binding records source kind and host", InheritedBindingRecordsSourceKind),
    ("Granted binding records target and query metadata", GrantedBindingRecordsTarget),
    ("Security binding records security owner", SecurityBindingRecordsOwner),
    ("Query filters inherited granted and security effects by role and scope", QueryFiltersByKindRoleAndScope),
    ("Query returns deterministic ordered effect ids", QueryIsDeterministic),
    ("Invalid query input returns explicit failure", InvalidQueryReturnsFailure),
    ("Source kind detection falls back to legacy boolean flags", SourceKindDetectionSupportsBooleanFlags),
    ("Assets facade delegates and source files stay inside G3L scope", AssetsFacadeAndSourceScope),
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

Task GoalRowAndPredecessorAreSatisfied()
{
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3L-002")
        ?? throw new InvalidOperationException("G3L-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Inherited", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "inherited granted security", "scope");
    AssertEqual("inherited granted helpers", Value(row, "deliverables"), "deliverables");
    AssertEqual("inherited granted security 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G3L-002_inherited_granted_security_helpers_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G3L-001", Value(row, "blocked_until"), "predecessor");
    AssertContains(Value(row, "completion_gate"), "inherited", "completion gate");
    AssertComplete("G3L-001_once_per_turn_flags_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsReferencesAreRecorded()
{
    string controller = ReadAsIs("CEntity_EffectController.cs");
    string effect = ReadAsIs("ICardEffect.cs");
    string security = ReadAsIs(Path.Combine("CardEffectCommons", "CanUseEffects", "SecurityEffect.cs"));
    string attack = ReadAsIs("AttackProcess.cs");

    AssertContains(controller, "IAddSkillEffect", "AS-IS granted effect hook");
    AssertContains(controller, "IsInheritedEffect", "AS-IS inherited filter");
    AssertContains(controller, "SecurityCards", "AS-IS security source scan");
    AssertContains(effect, "SetIsInheritedEffect", "AS-IS inherited setter");
    AssertContains(effect, "SetIsSecurityEffect", "AS-IS security setter");
    AssertContains(security, "CanTriggerSecurityEffect", "AS-IS security effect condition");
    AssertContains(attack, "SecurityCheck", "AS-IS attack security check");
    return Task.CompletedTask;
}

Task InheritedBindingRecordsSourceKind()
{
    EffectBinding binding = InheritedGrantedSecurityHelpers.CreateInheritedBinding(
        Request("inherited-effect"),
        HostId,
        EffectQueryRole.Continuous,
        new[] { "InheritedScope" },
        new[] { "InheritedKeyword" },
        new Dictionary<string, object?> { ["fixture"] = "inherited" });

    AssertEqual(EffectSourceKind.Inherited, InheritedGrantedSecurityHelpers.SourceKind(binding.Request.Context), "source kind");
    AssertEqual(true, binding.Request.Context.GetRequiredValue<bool>(InheritedGrantedSecurityHelpers.IsInheritedKey), "inherited bool");
    AssertEqual(false, binding.Request.Context.GetRequiredValue<bool>(InheritedGrantedSecurityHelpers.IsGrantedKey), "granted bool");
    AssertEqual(HostId.Value, binding.Request.Context.GetRequiredValue<string>(InheritedGrantedSecurityHelpers.HostEntityIdKey), "host id");
    AssertEqual("inherited", binding.Request.Context.GetRequiredValue<string>("fixture"), "custom value");
    AssertTrue(binding.HasRole(EffectQueryRole.Continuous), "query role");
    AssertSequence(new[] { "InheritedScope" }, binding.QueryScopes, "query scope");
    return Task.CompletedTask;
}

Task GrantedBindingRecordsTarget()
{
    EffectBinding binding = InheritedGrantedSecurityHelpers.CreateGrantedBinding(
        Request("granted-effect"),
        GrantedTargetId,
        EffectQueryRole.Modifier,
        new[] { "GrantedScope" },
        new[] { "GrantedKeyword" });

    AssertEqual(EffectSourceKind.Granted, InheritedGrantedSecurityHelpers.SourceKind(binding.Request.Context), "source kind");
    AssertEqual(true, binding.Request.Context.GetRequiredValue<bool>(InheritedGrantedSecurityHelpers.IsGrantedKey), "granted bool");
    AssertEqual(GrantedTargetId.Value, binding.Request.Context.GetRequiredValue<string>(InheritedGrantedSecurityHelpers.GrantedTargetEntityIdKey), "target id");
    AssertTrue(binding.HasRole(EffectQueryRole.Modifier), "query role");
    AssertSequence(new[] { "GrantedKeyword" }, binding.Keywords, "keyword");
    return Task.CompletedTask;
}

Task SecurityBindingRecordsOwner()
{
    EffectBinding binding = InheritedGrantedSecurityHelpers.CreateSecurityBinding(
        Request("security-effect", PlayerTwo),
        PlayerTwo,
        EffectQueryRole.Continuous,
        new[] { "SecuritySkill" },
        new[] { "Security" });

    AssertEqual(EffectSourceKind.Security, InheritedGrantedSecurityHelpers.SourceKind(binding.Request.Context), "source kind");
    AssertEqual(true, binding.Request.Context.GetRequiredValue<bool>(InheritedGrantedSecurityHelpers.IsSecurityKey), "security bool");
    AssertEqual(PlayerTwo.Value, binding.Request.Context.GetRequiredValue<int>(InheritedGrantedSecurityHelpers.SecurityOwnerIdKey), "security owner");
    AssertEqual(PlayerTwo, binding.Request.ControllerId, "controller");
    return Task.CompletedTask;
}

Task QueryFiltersByKindRoleAndScope()
{
    InMemoryEffectRegistry registry = RegistryWithMixedBindings();
    var context = new EffectQueryContext("SharedScope", playerId: PlayerOne);

    InheritedGrantedSecurityQueryResult inherited = InheritedGrantedSecurityHelpers.Query(
        registry,
        EffectQueryRole.Continuous,
        context,
        EffectSourceKind.Inherited);
    InheritedGrantedSecurityQueryResult granted = InheritedGrantedSecurityHelpers.Query(
        registry,
        EffectQueryRole.Continuous,
        context,
        EffectSourceKind.Granted);
    InheritedGrantedSecurityQueryResult security = InheritedGrantedSecurityHelpers.Query(
        registry,
        EffectQueryRole.Continuous,
        context,
        EffectSourceKind.Security);

    AssertTrue(inherited.IsSuccess, "inherited query");
    AssertEqual("inherited-a,inherited-b", JoinIds(inherited.Effects), "inherited ids");
    AssertEqual("granted-a", JoinIds(granted.Effects), "granted ids");
    AssertEqual("security-a", JoinIds(security.Effects), "security ids");
    AssertSequence(new[] { "inherited-a", "inherited-b" }, Strings(inherited.Values[InheritedGrantedSecurityHelpers.EffectIdsKey]), "values ids");
    return Task.CompletedTask;
}

Task QueryIsDeterministic()
{
    InMemoryEffectRegistry registry = RegistryWithMixedBindings();
    var context = new EffectQueryContext("SharedScope", playerId: PlayerOne);

    string first = Signature(InheritedGrantedSecurityHelpers.Query(registry, EffectQueryRole.Continuous, context));
    string second = Signature(InheritedGrantedSecurityHelpers.Query(registry, EffectQueryRole.Continuous, context));

    AssertEqual(first, second, "same signature");
    AssertContains(first, "effects=granted-a,inherited-a,inherited-b,native-a,security-a", "deterministic order");
    return Task.CompletedTask;
}

Task InvalidQueryReturnsFailure()
{
    InMemoryEffectRegistry registry = RegistryWithMixedBindings();
    var context = new EffectQueryContext("SharedScope");

    InheritedGrantedSecurityQueryResult nullService = InheritedGrantedSecurityHelpers.Query(null!, EffectQueryRole.Continuous, context);
    InheritedGrantedSecurityQueryResult nullContext = InheritedGrantedSecurityHelpers.Query(registry, EffectQueryRole.Continuous, null!);
    InheritedGrantedSecurityQueryResult invalidRole = InheritedGrantedSecurityHelpers.Query(registry, EffectQueryRole.Continuous | EffectQueryRole.Modifier, context);

    AssertFalse(nullService.IsSuccess, "null service");
    AssertContains(nullService.FailureReason, "service", "null service reason");
    AssertFalse(nullContext.IsSuccess, "null context");
    AssertContains(nullContext.FailureReason, "context", "null context reason");
    AssertFalse(invalidRole.IsSuccess, "invalid role");
    AssertContains(invalidRole.FailureReason, "single known role", "invalid role reason");
    return Task.CompletedTask;
}

Task SourceKindDetectionSupportsBooleanFlags()
{
    EffectContext inherited = Context(SourceId, new Dictionary<string, object?> { [InheritedGrantedSecurityHelpers.IsInheritedKey] = true });
    EffectContext granted = Context(SourceId, new Dictionary<string, object?> { [InheritedGrantedSecurityHelpers.IsGrantedKey] = true });
    EffectContext security = Context(SourceId, new Dictionary<string, object?> { [InheritedGrantedSecurityHelpers.IsSecurityKey] = true });
    EffectContext native = Context(SourceId);

    AssertEqual(EffectSourceKind.Inherited, InheritedGrantedSecurityHelpers.SourceKind(inherited), "inherited fallback");
    AssertEqual(EffectSourceKind.Granted, InheritedGrantedSecurityHelpers.SourceKind(granted), "granted fallback");
    AssertEqual(EffectSourceKind.Security, InheritedGrantedSecurityHelpers.SourceKind(security), "security fallback");
    AssertEqual(EffectSourceKind.Native, InheritedGrantedSecurityHelpers.SourceKind(native), "native fallback");
    return Task.CompletedTask;
}

Task AssetsFacadeAndSourceScope()
{
    InMemoryEffectRegistry registry = new();
    registry.Register(InheritedGrantedSecurityHelperFactory.CreateInheritedBinding(
        Request("facade-inherited"),
        HostId,
        EffectQueryRole.Continuous,
        new[] { "FacadeScope" }));

    InheritedGrantedSecurityQueryResult result = InheritedGrantedSecurityHelperFactory.Query(
        registry,
        EffectQueryRole.Continuous,
        new EffectQueryContext("FacadeScope"),
        EffectSourceKind.Inherited);

    AssertTrue(result.IsSuccess, "facade query");
    AssertEqual("facade-inherited", JoinIds(result.Effects), "facade id");

    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "InheritedGrantedSecurityHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "InheritedGrantedSecurityHelpers.cs");
    string testPath = Path.Combine(root, "tests", "G3L-002.Inherited.granted.security.helper.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless helper exists");
    AssertTrue(File.Exists(facadePath), "facade helper exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

InMemoryEffectRegistry RegistryWithMixedBindings()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(InheritedGrantedSecurityHelpers.CreateInheritedBinding(Request("inherited-b"), HostId, EffectQueryRole.Continuous, new[] { "SharedScope" }));
    registry.Register(InheritedGrantedSecurityHelpers.CreateSecurityBinding(Request("security-a"), PlayerOne, EffectQueryRole.Continuous, new[] { "SharedScope" }));
    registry.Register(new EffectBinding(Request("native-a"), queryRoles: EffectQueryRole.Continuous, queryScopes: new[] { "SharedScope" }));
    registry.Register(InheritedGrantedSecurityHelpers.CreateGrantedBinding(Request("granted-a"), GrantedTargetId, EffectQueryRole.Continuous, new[] { "SharedScope" }));
    registry.Register(InheritedGrantedSecurityHelpers.CreateInheritedBinding(Request("inherited-a"), HostId, EffectQueryRole.Continuous, new[] { "SharedScope" }));
    registry.Register(InheritedGrantedSecurityHelpers.CreateInheritedBinding(Request("replacement-a"), HostId, EffectQueryRole.Replacement, new[] { "SharedScope" }));
    return registry;
}

EffectRequest Request(string effectId, HeadlessPlayerId? controller = null)
{
    HeadlessPlayerId player = controller ?? PlayerOne;
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        player,
        "Main",
        Context(SourceId, owner: player));
}

EffectContext Context(
    HeadlessEntityId sourceId,
    IReadOnlyDictionary<string, object?>? values = null,
    HeadlessPlayerId? owner = null)
{
    HeadlessPlayerId player = owner ?? PlayerOne;
    return new EffectContext(
        player,
        player,
        sourceId,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        values: values);
}

string JoinIds(IEnumerable<EffectRequest> effects)
{
    return string.Join(",", effects.Select(effect => effect.EffectId.Value));
}

string Signature(InheritedGrantedSecurityQueryResult result)
{
    return string.Join(
        "|",
        $"success={result.IsSuccess}",
        $"kind={result.SourceKind}",
        $"effects={JoinIds(result.Effects)}",
        $"values={string.Join(";", result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={ValueToString(pair.Value)}"))}");
}

string[] Strings(object? value)
{
    if (value is null)
    {
        return Array.Empty<string>();
    }

    if (value is string text)
    {
        return new[] { text };
    }

    if (value is IEnumerable enumerable)
    {
        return enumerable.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray();
    }

    return new[] { value.ToString() ?? string.Empty };
}

string ValueToString(object? value)
{
    return string.Join(",", Strings(value));
}

string ReadAsIs(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", relativePath));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    AssertTrue(File.Exists(path), $"predecessor result document {fileName}");
    AssertContains(File.ReadAllText(path), "COMPLETE", $"predecessor completion {fileName}");
}

List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] values = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < values.Length ? values[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

IEnumerable<string> SplitCsvLine(string line)
{
    var value = new StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }

            continue;
        }

        if (ch == ',' && !inQuotes)
        {
            yield return value.ToString();
            value.Clear();
            continue;
        }

        value.Append(ch);
    }

    yield return value.ToString();
}

string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(Environment.CurrentDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}

void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
    }
}

void AssertDoesNotContain(string text, string unexpected, string label)
{
    if (text.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: text must not contain '{unexpected}'.");
    }
}

void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}
