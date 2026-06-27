using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId PermanentId = new("p1-permanent");
HeadlessEntityId DefinitionId = new("BT8-001");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3J-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS PermanentEffectFactory references are recorded", AsIsPermanentEffectFactoryReferencesAreRecorded),
    ("Permanent id and top card lookup create bindings", PermanentIdAndTopCardLookupCreateBindings),
    ("Permanent bindings register into EffectRegistry", PermanentBindingsRegisterIntoEffectRegistry),
    ("Trigger mismatch returns explicit permanent failure", TriggerMismatchReturnsFailure),
    ("Duplicate permanent effect ids fail without registry mutation", DuplicatePermanentEffectIdsFail),
    ("Repeated permanent lookup is deterministic", RepeatedPermanentLookupIsDeterministic),
    ("Invalid permanent binding inputs fail explicitly", InvalidInputsFailExplicitly),
    ("Assets PermanentEffectFactory facade creates permanent rules", AssetsFacadeCreatesPermanentRules),
    ("G3J-002 source files stay inside permanent binding scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3J-002")
        ?? throw new InvalidOperationException("G3J-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Factory", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "permanent effect binding", "scope");
    AssertEqual("PermanentEffectFactory binding", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "permanent lookup", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3J-002_permanent_effect_factory_binding_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3J-001", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "PermanentEffectFactory", "completion gate");
    AssertComplete("G3J-001_card_effect_factory_binding_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsPermanentEffectFactoryReferencesAreRecorded()
{
    string factory = ReadAsIs("PermanentEffectFactory.cs");
    string permanent = ReadAsIs("Permanent.cs");
    string cardFactory = ReadAsIs(Path.Combine("CardEffectFactory", "KeyWordEffects", "Collision.cs"));

    AssertContains(factory, "public partial class PermanentEffectFactory", "AS-IS permanent factory");
    AssertContains(factory, "SetEffectSourcePermanent(permanent)", "AS-IS source permanent");
    AssertContains(factory, "DeleteSelfEffect", "AS-IS delete self");
    AssertContains(factory, "DigimonEffectImmunity", "AS-IS digimon immunity");
    AssertContains(factory, "CollisionEffect", "AS-IS collision permanent effect");
    AssertContains(permanent, "EffectList(EffectTiming.None)", "AS-IS permanent effect list");
    AssertContains(cardFactory, "Collision", "AS-IS collision keyword factory");
    return Task.CompletedTask;
}

Task PermanentIdAndTopCardLookupCreateBindings()
{
    CardInstanceState permanent = CreatePermanent();
    CardRecord topCard = CreateTopCard("BT8-001", "binding:source-card");
    PermanentEffectFactoryBindingRegistry registry = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.DeleteSelfEffect(
            "rule-delete-self",
            new[] { PermanentId.Value },
            "OnEndTurn"),
        PermanentEffectFactory.DigimonEffectImmunity(
            "rule-digimon-immune",
            new[] { "binding:source-card" },
            "Static"),
    });

    PermanentEffectFactoryBindingResult delete = registry.Bind(CreateRequest(permanent, "OnEndTurn", topCard));
    PermanentEffectFactoryBindingResult immunity = registry.Bind(CreateRequest(permanent, "Static", topCard));

    AssertTrue(delete.IsSuccess, "delete binding");
    AssertSequence(new[] { "rule-delete-self" }, delete.MatchedRuleIds, "delete rule");
    AssertSequence(new[] { "p1-permanent:permanent:DeleteSelf" }, delete.Bindings.Select(binding => binding.Request.EffectId.Value).ToArray(), "delete effect id");
    AssertEqual(PermanentEffectFactoryBindingRules.DeleteSelfTiming, delete.Bindings[0].Request.Timing, "delete timing");
    AssertEqual(PermanentId, delete.Bindings[0].Request.Context.SourceEntityId, "delete source permanent");
    AssertTrue(immunity.IsSuccess, "immunity binding");
    AssertTrue(immunity.Bindings[0].HasRole(EffectQueryRole.Replacement), "immunity role");
    AssertEqual("DigimonEffect", immunity.Bindings[0].Request.Context.Values[ReplacementHelpers.MutationKindKey], "immunity mutation kind");
    return Task.CompletedTask;
}

Task PermanentBindingsRegisterIntoEffectRegistry()
{
    CardInstanceState permanent = CreatePermanent();
    CardRecord topCard = CreateTopCard("BT9-001", null);
    PermanentEffectFactoryBindingRegistry factory = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.CollisionEffect(
            "rule-collision",
            new[] { DefinitionId.Value },
            "Static"),
        PermanentEffectFactory.AddDetailClass(
            "rule-detail",
            new[] { "BT9-001" },
            "This Digimon has an added detail.",
            triggerEffect: true,
            trigger: "Static"),
    });
    var effectRegistry = new InMemoryEffectRegistry();

    PermanentEffectFactoryBindingResult result = factory.BindAndRegister(effectRegistry, CreateRequest(permanent, "Static", topCard));

    AssertTrue(result.IsSuccess, "register result");
    AssertEqual(2, effectRegistry.GetEffectsForTiming(PermanentEffectFactoryBindingRules.CollisionTiming).Concat(
        effectRegistry.GetEffectsForTiming(PermanentEffectFactoryBindingRules.DetailTiming)).Count(), "registered timing count");
    AssertEqual(1, effectRegistry.GetContinuousEffects(new EffectQueryContext(PermanentEffectFactoryBindingRules.CollisionScope, targetEntityId: PermanentId)).Count, "collision continuous lookup");
    AssertEqual(1, effectRegistry.GetContinuousEffects(new EffectQueryContext(PermanentEffectFactoryBindingRules.DetailScope, targetEntityId: PermanentId)).Count, "detail continuous lookup");
    AssertTrue(effectRegistry.GetKeywordEffects("Permanent:Collision").Count == 1, "permanent collision keyword");
    return Task.CompletedTask;
}

Task TriggerMismatchReturnsFailure()
{
    PermanentEffectFactoryBindingRegistry registry = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.DeleteSelfEffect("rule-delete", new[] { PermanentId.Value }, "OnEndTurn"),
    });

    PermanentEffectFactoryBindingResult result = registry.Bind(CreateRequest(CreatePermanent(), "Static", CreateTopCard("BT8-001", null)));

    AssertFalse(result.IsSuccess, "mismatch result");
    AssertEqual("permanent_binding_not_found", result.ErrorCode, "error code");
    AssertEqual(0, result.Bindings.Count, "failure bindings");
    AssertContains(result.Message ?? string.Empty, PermanentId.Value, "failure message permanent");
    return Task.CompletedTask;
}

Task DuplicatePermanentEffectIdsFail()
{
    PermanentEffectFactoryBindingRegistry factory = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.DeleteSelfEffect("rule-delete-a", new[] { PermanentId.Value }, "OnEndTurn"),
        PermanentEffectFactory.DeleteSelfEffect("rule-delete-b", new[] { DefinitionId.Value }, "OnEndTurn"),
    });
    var effectRegistry = new InMemoryEffectRegistry();

    PermanentEffectFactoryBindingResult result = factory.BindAndRegister(
        effectRegistry,
        CreateRequest(CreatePermanent(), "OnEndTurn", CreateTopCard("BT8-001", null)));

    AssertFalse(result.IsSuccess, "duplicate result");
    AssertEqual("duplicate_permanent_effect_binding", result.ErrorCode, "duplicate error code");
    AssertEqual(0, effectRegistry.GetKeywordEffects("DeleteSelf").Count, "registry not mutated");
    return Task.CompletedTask;
}

Task RepeatedPermanentLookupIsDeterministic()
{
    CardInstanceState permanent = CreatePermanent();
    CardRecord topCard = CreateTopCard("BT10-001", "binding:deterministic");
    PermanentEffectFactoryBindingRegistry registry = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.OptionEffectImmunity("z-rule", new[] { "binding:deterministic" }, "Static"),
        PermanentEffectFactory.CollisionEffect("a-rule", new[] { PermanentId.Value }, "Static"),
    });

    string first = Signature(registry.Bind(CreateRequest(permanent, "Static", topCard)));
    string second = Signature(registry.Bind(CreateRequest(permanent, "Static", topCard)));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "matched=a-rule,z-rule", "stable rule ids");
    AssertContains(first, "effects=p1-permanent:permanent:Collision,p1-permanent:permanent:OptionEffect", "stable effect ids");
    return Task.CompletedTask;
}

Task InvalidInputsFailExplicitly()
{
    CardInstanceState permanent = CreatePermanent();
    AssertThrows<ArgumentException>(() => new PermanentEffectFactoryBindingRule("bad", Array.Empty<string>(), "Static", _ => Array.Empty<EffectBinding>()));
    AssertThrows<ArgumentException>(() => new PermanentEffectFactoryBindingRule("bad", new[] { PermanentId.Value }, " ", _ => Array.Empty<EffectBinding>()));
    AssertThrows<ArgumentNullException>(() => new PermanentEffectFactoryBindingRequest(permanent, "Static", PlayerOne, null!));
    AssertThrows<ArgumentException>(() => new PermanentEffectFactoryBindingRequest(permanent, "Static", PlayerOne, new EffectContext(PlayerOne, new HeadlessEntityId("other"))));
    AssertThrows<ArgumentException>(() => new PermanentEffectFactoryBindingRequest(permanent, "Static", PlayerOne, CreateContext(), CreateMismatchedTopCard()));
    return Task.CompletedTask;
}

Task AssetsFacadeCreatesPermanentRules()
{
    CardInstanceState permanent = CreatePermanent();
    PermanentEffectFactoryBindingRegistry registry = PermanentEffectFactory.CreateRegistry(new[]
    {
        PermanentEffectFactory.AddDetailClass(
            "rule-detail",
            new[] { PermanentId.Value },
            "Display detail",
            triggerEffect: false,
            trigger: "Static"),
    });

    PermanentEffectFactoryBindingResult result = PermanentEffectFactory.Bind(
        registry,
        permanent,
        "Static",
        PlayerOne,
        CreateContext(),
        CreateTopCard("BT8-001", null));

    AssertTrue(result.IsSuccess, "facade result");
    AssertSequence(new[] { "rule-detail" }, result.MatchedRuleIds, "facade rule");
    AssertEqual("Display detail", result.Bindings[0].Request.Context.Values[PermanentEffectFactoryBindingRules.DetailKey], "detail value");
    AssertEqual(false, result.Bindings[0].Request.Context.Values[PermanentEffectFactoryBindingRules.TriggerEffectKey], "trigger flag");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "PermanentEffectFactoryBinding.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "PermanentEffectFactory.cs");
    string testPath = Path.Combine(root, "tests", "G3J-002.PermanentEffectFactory.binding.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless permanent binding file exists");
    AssertTrue(File.Exists(facadePath), "facade file exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless binding Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless binding TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

PermanentEffectFactoryBindingRequest CreateRequest(CardInstanceState permanent, string trigger, CardRecord topCard)
{
    return new PermanentEffectFactoryBindingRequest(permanent, trigger, PlayerOne, CreateContext(), topCard);
}

EffectContext CreateContext()
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        PermanentId,
        triggerEntityId: null,
        targetEntityIds: new[] { PermanentId },
        values: new Dictionary<string, object?> { ["fixture"] = "G3J-002" });
}

CardInstanceState CreatePermanent()
{
    return new CardInstanceState(PermanentId, DefinitionId, PlayerOne);
}

CardRecord CreateTopCard(string cardNumber, string? bindingKey)
{
    return new CardRecord(
        DefinitionId,
        cardNumber,
        "Permanent Factory Test Card",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 3,
        EffectBindingKey: bindingKey);
}

CardRecord CreateMismatchedTopCard()
{
    return new CardRecord(
        new HeadlessEntityId("BT-WRONG"),
        "BT-WRONG",
        "Wrong Card",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 3);
}

string Signature(PermanentEffectFactoryBindingResult result)
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
