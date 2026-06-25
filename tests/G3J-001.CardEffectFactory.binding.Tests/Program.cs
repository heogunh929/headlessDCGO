using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId SourceId = new("p1-card");
HeadlessEntityId TargetId = new("p1-target");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3J-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS CardEffectFactory binding references are recorded", AsIsCardEffectFactoryReferencesAreRecorded),
    ("Card number and binding key lookup creates effect bindings", CardNumberAndBindingKeyLookupCreatesBindings),
    ("Factory binding registers bindings into EffectRegistry", FactoryBindingRegistersIntoEffectRegistry),
    ("Trigger mismatch returns explicit failure result", TriggerMismatchReturnsFailure),
    ("Duplicate effect ids return failure without registry mutation", DuplicateEffectIdsReturnFailure),
    ("Repeated lookup is deterministic", RepeatedLookupIsDeterministic),
    ("Invalid binding inputs fail explicitly", InvalidInputsFailExplicitly),
    ("Assets CardEffectFactory facade creates keyword binding rules", AssetsFacadeCreatesKeywordRules),
    ("G3J-001 source files stay inside factory binding scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3J-001")
        ?? throw new InvalidOperationException("G3J-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Factory", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "card id trigger", "scope");
    AssertEqual("CardEffectFactory binding", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "factory lookup", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3J-001_card_effect_factory_binding_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3I-002", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "CardEffectFactory", "completion gate");
    AssertComplete("G3I-002_continuous_effect_evaluator_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsCardEffectFactoryReferencesAreRecorded()
{
    string rootFactory = ReadAsIs("CardEffectFactory.cs");
    string blocker = ReadAsIs(Path.Combine("CardEffectFactory", "KeyWordEffects", "Blocker.cs"));
    string blitz = ReadAsIs(Path.Combine("CardEffectFactory", "KeyWordEffects", "Blitz.cs"));
    string changeDp = ReadAsIs(Path.Combine("CardEffectFactory", "ChangeDP.cs"));

    AssertContains(rootFactory, "public partial class CardEffectFactory", "AS-IS root factory");
    AssertContains(rootFactory, "SetUpICardEffect", "AS-IS effect setup");
    AssertContains(blocker, "BlockerClass", "AS-IS blocker factory");
    AssertContains(blitz, "SetUpICardEffect(\"Blitz\"", "AS-IS blitz factory");
    AssertContains(changeDp, "ChangeDPStaticEffect", "AS-IS static factory");
    return Task.CompletedTask;
}

Task CardNumberAndBindingKeyLookupCreatesBindings()
{
    CardRecord card = CreateCard("BT1-001", "binding:agumon");
    CardEffectFactoryBindingRegistry registry = CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch1(
            "rule-blocker",
            new[] { "binding:agumon" },
            "OnPlay",
            KeywordBaseBatch1Kind.Blocker),
        CardEffectFactoryBinding.BindKeywordBaseBatch2(
            "rule-rush",
            new[] { "BT1-001" },
            "OnPlay",
            KeywordBaseBatch2Kind.Rush),
    });

    CardEffectFactoryBindingResult result = registry.Bind(CreateRequest(card, "OnPlay"));

    AssertTrue(result.IsSuccess, "binding result");
    AssertSequence(new[] { "rule-blocker", "rule-rush" }, result.MatchedRuleIds, "matched rules");
    AssertSequence(
        new[] { "p1-card:Blocker:base1", "p1-card:Rush:base2" },
        result.Bindings.Select(binding => binding.Request.EffectId.Value).ToArray(),
        "effect ids");
    AssertEqual(KeywordBaseBatch1Timings.BlockTiming, result.Bindings[0].Request.Timing, "blocker timing");
    AssertTrue(result.Bindings[0].Keywords.Contains("Blocker"), "blocker keyword");
    AssertTrue(result.Bindings[1].HasRole(EffectQueryRole.Restriction), "rush role");
    return Task.CompletedTask;
}

Task FactoryBindingRegistersIntoEffectRegistry()
{
    CardRecord card = CreateCard("BT2-001", "binding:pierce");
    CardEffectFactoryBindingRegistry factory = CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch1(
            "rule-pierce",
            new[] { "BT2-001" },
            "OnAttack",
            KeywordBaseBatch1Kind.Piercing),
    });
    var effectRegistry = new InMemoryEffectRegistry();

    CardEffectFactoryBindingResult result = factory.BindAndRegister(effectRegistry, CreateRequest(card, "OnAttack"));

    AssertTrue(result.IsSuccess, "register result");
    AssertEqual(1, effectRegistry.GetKeywordEffects("Pierce").Count, "keyword alias lookup");
    AssertEqual(1, effectRegistry.GetEffectsForTiming(KeywordBaseBatch1Timings.DetermineSecurityCheck).Count, "timing lookup");
    AssertEqual(1, effectRegistry.GetContinuousEffects(new EffectQueryContext(KeywordBaseBatch1Scopes.SecurityCheck, targetEntityId: TargetId)).Count, "continuous scope lookup");
    return Task.CompletedTask;
}

Task TriggerMismatchReturnsFailure()
{
    CardRecord card = CreateCard("BT3-001", "binding:jamming");
    CardEffectFactoryBindingRegistry registry = CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch1(
            "rule-jamming",
            new[] { "binding:jamming" },
            "OnBattle",
            KeywordBaseBatch1Kind.Jamming),
    });

    CardEffectFactoryBindingResult result = registry.Bind(CreateRequest(card, "OnPlay"));

    AssertFalse(result.IsSuccess, "mismatch result");
    AssertEqual("factory_binding_not_found", result.ErrorCode, "error code");
    AssertEqual(0, result.Bindings.Count, "failure binding count");
    AssertContains(result.Message ?? string.Empty, "BT3-001", "failure message card");
    return Task.CompletedTask;
}

Task DuplicateEffectIdsReturnFailure()
{
    CardRecord card = CreateCard("BT4-001", null);
    CardEffectFactoryBindingRegistry factory = CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch2("rule-rush-a", new[] { "BT4-001" }, "OnPlay", KeywordBaseBatch2Kind.Rush),
        CardEffectFactoryBinding.BindKeywordBaseBatch2("rule-rush-b", new[] { "BT4-001" }, "OnPlay", KeywordBaseBatch2Kind.Rush),
    });
    var effectRegistry = new InMemoryEffectRegistry();

    CardEffectFactoryBindingResult result = factory.BindAndRegister(effectRegistry, CreateRequest(card, "OnPlay"));

    AssertFalse(result.IsSuccess, "duplicate result");
    AssertEqual("duplicate_effect_binding", result.ErrorCode, "duplicate error");
    AssertEqual(0, effectRegistry.GetKeywordEffects("Rush").Count, "registry not mutated");
    return Task.CompletedTask;
}

Task RepeatedLookupIsDeterministic()
{
    CardRecord card = CreateCard("BT5-001", "binding:multi");
    CardEffectFactoryBindingRegistry registry = CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch2("z-rule", new[] { "binding:multi" }, "OnPlay", KeywordBaseBatch2Kind.Blitz),
        CardEffectFactoryBinding.BindKeywordBaseBatch1("a-rule", new[] { "BT5-001" }, "OnPlay", KeywordBaseBatch1Kind.Reboot),
    });

    string first = Signature(registry.Bind(CreateRequest(card, "OnPlay")));
    string second = Signature(registry.Bind(CreateRequest(card, "OnPlay")));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "matched=a-rule,z-rule", "rule order");
    AssertContains(first, "effects=p1-card:Blitz:base2,p1-card:Reboot:base1", "effect id order");
    return Task.CompletedTask;
}

Task InvalidInputsFailExplicitly()
{
    CardRecord card = CreateCard("BT6-001", null);
    AssertThrows<ArgumentException>(() => new CardEffectFactoryBindingRule("bad", Array.Empty<string>(), "OnPlay", _ => Array.Empty<EffectBinding>()));
    AssertThrows<ArgumentException>(() => new CardEffectFactoryBindingRule("bad", new[] { "BT6-001" }, " ", _ => Array.Empty<EffectBinding>()));
    AssertThrows<ArgumentNullException>(() => new CardEffectFactoryBindingRequest(card, "OnPlay", SourceId, PlayerOne, null!));
    AssertThrows<ArgumentException>(() => new CardEffectFactoryBindingRequest(card, "OnPlay", new HeadlessEntityId("other"), PlayerOne, CreateContext()));
    return Task.CompletedTask;
}

Task AssetsFacadeCreatesKeywordRules()
{
    CardRecord card = CreateCard("BT7-001", "binding:armor");
    CardEffectFactoryBindingRule rule = CardEffectFactoryBinding.BindKeywordBaseBatch2(
        "rule-armor",
        new[] { "binding:armor" },
        "WhenRemoveField",
        KeywordBaseBatch2Kind.ArmorPurge);
    CardEffectFactoryBindingRegistry registry = CardEffectFactoryBinding.CreateRegistry(new[] { rule });

    CardEffectFactoryBindingResult result = CardEffectFactoryBinding.Bind(
        registry,
        card,
        "WhenRemoveField",
        SourceId,
        PlayerOne,
        CreateContext(),
        TargetId);

    AssertTrue(result.IsSuccess, "facade result");
    AssertSequence(new[] { "rule-armor" }, result.MatchedRuleIds, "facade rule");
    AssertTrue(result.Bindings[0].Keywords.Contains("ArmorPurge"), "armor alias");
    AssertTrue(result.Bindings[0].HasRole(EffectQueryRole.Replacement), "armor role");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "CardEffectFactoryBinding.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "CardEffectFactoryBinding.cs");
    string testPath = Path.Combine(root, "tests", "G3J-001.CardEffectFactory.binding.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless binding file exists");
    AssertTrue(File.Exists(facadePath), "facade file exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless binding Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless binding TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

CardEffectFactoryBindingRequest CreateRequest(CardRecord card, string trigger)
{
    return new CardEffectFactoryBindingRequest(card, trigger, SourceId, PlayerOne, CreateContext(), TargetId);
}

EffectContext CreateContext()
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        SourceId,
        triggerEntityId: null,
        targetEntityIds: new[] { TargetId },
        values: new Dictionary<string, object?> { ["fixture"] = "G3J-001" });
}

CardRecord CreateCard(string cardNumber, string? bindingKey)
{
    return new CardRecord(
        SourceId,
        cardNumber,
        "Factory Test Card",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 3,
        EffectBindingKey: bindingKey);
}

string Signature(CardEffectFactoryBindingResult result)
{
    return string.Join(
        "|",
        $"success={result.IsSuccess}",
        $"error={result.ErrorCode}",
        $"matched={string.Join(",", result.MatchedRuleIds)}",
        $"effects={string.Join(",", result.Bindings.Select(binding => binding.Request.EffectId.Value))}",
        $"values={string.Join(";", result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={ValueToString(pair.Value)}"))}");
}

string ValueToString(object? value)
{
    if (value is null)
    {
        return "<null>";
    }

    if (value is string text)
    {
        return text;
    }

    if (value is System.Collections.IEnumerable values)
    {
        return string.Join(",", values.Cast<object?>().Select(ValueToString));
    }

    return value.ToString() ?? string.Empty;
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

void AssertThrows<TException>(Action action)
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
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Expected {typeof(TException).Name}, actual {ex.GetType().Name}.");
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
}
