using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId SourceId = new("p1-source");
HeadlessEntityId TargetId = new("p1-target");
HeadlessEntityId OtherTargetId = new("p2-target");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3I-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS continuous effect references are recorded", AsIsContinuousReferencesAreRecorded),
    ("Registry continuous effects are collected by query scope", RegistryContinuousEffectsAreCollected),
    ("Card instance and state metadata are recalculated together", MetadataSourcesAreRecalculatedTogether),
    ("State mutation changes recalculated modifier result", StateMutationChangesRecalculation),
    ("Continuous restrictions and replacements are exposed together", RestrictionsAndReplacementsAreExposedTogether),
    ("Evaluation result values are deterministic", EvaluationResultValuesAreDeterministic),
    ("Invalid continuous evaluation input fails explicitly", InvalidInputFailsExplicitly),
    ("CardEffectCommons facade delegates to evaluator", CardEffectCommonsFacadeDelegates),
    ("G3I-002 source files stay inside continuous evaluator scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3I-002")
        ?? throw new InvalidOperationException("G3I-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Continuous", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "상시 효과", "scope");
    AssertEqual("continuous evaluator", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "continuous recalculation", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3I-002_continuous_effect_evaluator_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3I-001", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "continuous", "completion gate");
    AssertComplete("G3I-001_replacement_prevention_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsContinuousReferencesAreRecorded()
{
    string continuousController = ReadAsIs("ContinuousController.cs");
    string changeDp = ReadAsIsFactory("ChangeDP.cs");
    string changePlayCost = ReadAsIsFactory("ChangePlayCost.cs");
    string cannotAttack = ReadAsIsFactory("CanNotAttack.cs");

    AssertContains(continuousController, "public class ContinuousController", "AS-IS controller");
    AssertContains(changeDp, "ChangeDPStaticEffect", "AS-IS DP static effect");
    AssertContains(changeDp, "DP += _changeValue()", "AS-IS DP recalculation");
    AssertContains(changePlayCost, "ChangePlayCostStaticEffect", "AS-IS play cost static effect");
    AssertContains(changePlayCost, "Cost = _changeValue()", "AS-IS fixed play cost recalculation");
    AssertContains(cannotAttack, "CanNotAttackStaticEffect", "AS-IS cannot attack static effect");
    AssertContains(cannotAttack, "return condition == null || condition()", "AS-IS condition re-evaluation");
    return Task.CompletedTask;
}

Task RegistryContinuousEffectsAreCollected()
{
    var registry = new InMemoryEffectRegistry();
    EffectRequest matching = CreateEffect(
        "effect-continuous-a",
        new Dictionary<string, object?>
        {
            [ModifierHelpers.DpDeltaKey] = 1000,
            [RestrictionHelpers.CannotAttackKey] = true,
            [ReplacementHelpers.PreventRemovalKey] = true,
        });
    EffectRequest wrongScope = CreateEffect(
        "effect-continuous-b",
        new Dictionary<string, object?> { [ModifierHelpers.DpDeltaKey] = 9000 });

    registry.Register(new EffectBinding(
        matching,
        queryRoles: EffectQueryRole.Continuous,
        queryScopes: new[] { "ContinuousRecalculation" }));
    registry.Register(new EffectBinding(
        wrongScope,
        queryRoles: EffectQueryRole.Continuous,
        queryScopes: new[] { "OtherScope" }));

    ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(
        registry,
        new EffectQueryContext("ContinuousRecalculation", targetEntityId: TargetId));

    AssertSequence(new[] { "effect-continuous-a" }, result.ContinuousEffects.Select(effect => effect.EffectId.Value).ToArray(), "continuous ids");
    AssertEqual(4000, result.ResolveDp(3000, TargetId).FinalValue, "resolved DP");
    AssertTrue(RestrictionHelpers.CannotAttack(TargetId, result.Restrictions).IsRestricted, "cannot attack");
    AssertTrue(ReplacementHelpers.PreventRemoval(TargetId, result.Replacements).IsReplaced, "prevent removal");
    AssertEqual(1, result.Values["continuousEffectCount"], "continuous effect count");
    return Task.CompletedTask;
}

Task MetadataSourcesAreRecalculatedTogether()
{
    CardRecord card = CreateCard(new Dictionary<string, object?>
    {
        [ModifierHelpers.SecurityAttackDeltaKey] = 1,
    });
    CardInstanceRecord instance = new(
        TargetId,
        card.Id,
        PlayerOne,
        Metadata: new Dictionary<string, object?>
        {
            [RestrictionHelpers.CannotBlockKey] = true,
        });
    CardInstanceState state = new(
        TargetId,
        card.Id,
        PlayerOne,
        Modifiers: new Dictionary<string, object?> { [ModifierHelpers.DpDeltaKey] = -1000 },
        Flags: new Dictionary<string, bool> { [ReplacementHelpers.PreventDeletionKey] = true });

    ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(new ContinuousEvaluationRequest(
        new EffectQueryContext("ContinuousRecalculation", targetEntityId: TargetId),
        card: card,
        instance: instance,
        state: state));

    AssertEqual(3000, result.ResolveDp(4000, TargetId).FinalValue, "state DP");
    AssertEqual(2, result.ResolveSecurityAttack(1, TargetId).FinalValue, "card security attack");
    AssertTrue(RestrictionHelpers.CannotBlock(TargetId, result.Restrictions).IsRestricted, "instance cannot block");
    AssertTrue(ReplacementHelpers.PreventDeletion(TargetId, result.Replacements).IsReplaced, "state prevent deletion");
    return Task.CompletedTask;
}

Task StateMutationChangesRecalculation()
{
    EffectQueryContext query = new("ContinuousRecalculation", targetEntityId: TargetId);
    CardInstanceState firstState = CreateState(new Dictionary<string, object?> { [ModifierHelpers.DpDeltaKey] = 1000 });
    CardInstanceState secondState = firstState.AddModifier(ModifierHelpers.DpDeltaKey, 2000);

    ContinuousEvaluationResult first = ContinuousEffectEvaluator.Recalculate(
        new ContinuousEvaluationRequest(query, state: firstState));
    ContinuousEvaluationResult second = ContinuousEffectEvaluator.Recalculate(
        new ContinuousEvaluationRequest(query, state: secondState));

    AssertEqual(4000, first.ResolveDp(3000, TargetId).FinalValue, "first DP");
    AssertEqual(5000, second.ResolveDp(3000, TargetId).FinalValue, "second DP");
    AssertEqual(1000, first.Modifiers.Single().Value, "first modifier value");
    AssertEqual(2000, second.Modifiers.Single().Value, "second modifier value");
    return Task.CompletedTask;
}

Task RestrictionsAndReplacementsAreExposedTogether()
{
    EffectRequest effect = CreateEffect(
        "effect-continuous-lock",
        new Dictionary<string, object?>
        {
            [RestrictionHelpers.RestrictionsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    [RestrictionHelpers.RestrictionKindKey] = nameof(CannotRestrictionKind.Suspend),
                    [RestrictionHelpers.RestrictionTargetEntityIdKey] = TargetId.Value,
                },
            },
            [ReplacementHelpers.ReplacementsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    [ReplacementHelpers.EventKindKey] = nameof(ReplacementEventKind.DpReduction),
                    [ReplacementHelpers.ActionKindKey] = nameof(ReplacementActionKind.Immune),
                    [ReplacementHelpers.TargetEntityIdKey] = TargetId.Value,
                    [ReplacementHelpers.SourceEntityIdKey] = SourceId.Value,
                    [ReplacementHelpers.MutationKindKey] = "ChangeDP",
                },
            },
        });

    ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(new ContinuousEvaluationRequest(
        new EffectQueryContext("ContinuousRecalculation", targetEntityId: TargetId),
        new[] { effect }));

    AssertTrue(RestrictionHelpers.CannotSuspend(TargetId, result.Restrictions).IsRestricted, "cannot suspend");
    AssertTrue(ReplacementHelpers.ImmuneFromDpReduction(TargetId, result.Replacements, SourceId).IsReplaced, "immune DP reduction");
    AssertContains(Signature(result), "replacementIds=effect-continuous-lock:DpReduction-Immune", "replacement id");
    return Task.CompletedTask;
}

Task EvaluationResultValuesAreDeterministic()
{
    var effects = new[]
    {
        CreateEffect("effect-b", new Dictionary<string, object?> { [ModifierHelpers.DpDeltaKey] = 2000 }),
        CreateEffect("effect-a", new Dictionary<string, object?> { [ModifierHelpers.DpDeltaKey] = 1000 }),
    };
    EffectQueryContext query = new("ContinuousRecalculation", targetEntityId: TargetId);

    string first = Signature(ContinuousEffectEvaluator.Evaluate(new ContinuousEvaluationRequest(query, effects)));
    string second = Signature(ContinuousEffectEvaluator.Evaluate(new ContinuousEvaluationRequest(query, effects)));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "continuousEffectIds=effect-a,effect-b", "stable effect id order");
    AssertContains(first, "modifierIds=effect-a:dpDelta,effect-b:dpDelta", "stable modifier id order");
    return Task.CompletedTask;
}

Task InvalidInputFailsExplicitly()
{
    AssertThrows<ArgumentNullException>(() => new ContinuousEvaluationRequest(null!));
    AssertThrows<ArgumentException>(() => new EffectQueryContext(" "));
    AssertThrows<ArgumentNullException>(() => ContinuousEffectEvaluator.Evaluate(null!));
    AssertThrows<ArgumentNullException>(() => ContinuousEffectEvaluator.Evaluate(null!, new EffectQueryContext("ContinuousRecalculation")));
    return Task.CompletedTask;
}

Task CardEffectCommonsFacadeDelegates()
{
    EffectRequest effect = CreateEffect("effect-facade", new Dictionary<string, object?> { [ModifierHelpers.PlayCostDeltaKey] = -2 });
    ContinuousEvaluationResult result = ContinuousEffectEvaluatorFactory.Evaluate(
        new EffectQueryContext("ContinuousRecalculation", targetEntityId: TargetId),
        new[] { effect });

    AssertEqual(3, result.ResolvePlayCost(5).FinalValue, "facade play cost");
    AssertSequence(new[] { "effect-facade:playCostDelta" }, result.Modifiers.Select(modifier => modifier.Id).ToArray(), "facade modifier id");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string evaluatorPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "ContinuousEffectEvaluator.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "ContinuousEffectEvaluator.cs");
    string testPath = Path.Combine(root, "tests", "G3I-002.Continuous.effect.evaluator.Tests", "Program.cs");

    AssertTrue(File.Exists(evaluatorPath), "evaluator file exists");
    AssertTrue(File.Exists(facadePath), "facade file exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(evaluatorPath), "UnityEngine", "evaluator Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(evaluatorPath), "TODO", "evaluator TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

EffectRequest CreateEffect(string id, IReadOnlyDictionary<string, object?> values)
{
    return new EffectRequest(
        new HeadlessEntityId(id),
        PlayerOne,
        "Continuous",
        new EffectContext(
            PlayerOne,
            PlayerOne,
            SourceId,
            triggerEntityId: null,
            targetEntityIds: new[] { TargetId },
            values: values));
}

CardRecord CreateCard(IReadOnlyDictionary<string, object?> metadata)
{
    return new CardRecord(
        new HeadlessEntityId("BT-001"),
        "BT-001",
        "Test Card",
        metadata,
        CardType: "Digimon",
        PlayCost: 3,
        EvolutionCost: 2);
}

CardInstanceState CreateState(IReadOnlyDictionary<string, object?> modifiers)
{
    return new CardInstanceState(TargetId, new HeadlessEntityId("BT-001"), PlayerOne, Modifiers: modifiers);
}

string ReadAsIs(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", relativePath));
}

string ReadAsIsFactory(string fileName)
{
    return ReadAsIs(Path.Combine("CardEffectFactory", fileName));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    AssertTrue(File.Exists(path), $"predecessor result document {fileName}");
    AssertContains(File.ReadAllText(path), "COMPLETE", $"predecessor completion {fileName}");
}

string Signature(ContinuousEvaluationResult result)
{
    var parts = new List<string>
    {
        $"effects={string.Join(",", result.ContinuousEffects.Select(effect => effect.EffectId.Value))}",
        $"modifiers={string.Join(",", result.Modifiers.Select(modifier => modifier.Id))}",
        $"restrictions={string.Join(",", result.Restrictions.Select(restriction => restriction.Id))}",
        $"replacements={string.Join(",", result.Replacements.Select(replacement => replacement.Id))}",
    };

    foreach (KeyValuePair<string, object?> pair in result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        parts.Add($"{pair.Key}={ValueToString(pair.Value)}");
    }

    return string.Join("|", parts);
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
