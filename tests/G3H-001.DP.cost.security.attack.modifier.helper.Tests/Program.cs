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
    ("G3H-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS modifier references are recorded", AsIsModifierReferencesAreRecorded),
    ("DP modifier applies isUpDown before set and filters target", DpModifierAppliesSetBeforeAddAndFiltersTarget),
    ("Cost modifiers clamp to zero and respect reduction permission", CostModifiersClampAndRespectReductionPermission),
    ("Digivolution cost modifier reads simple metadata keys", DigivolutionCostModifierReadsSimpleMetadataKeys),
    ("Security attack modifier resolves add set and invert delta", SecurityAttackModifierResolvesAddSetAndInvertDelta),
    ("CardInstanceState modifiers are read without mutating state", CardInstanceStateModifiersAreReadWithoutMutation),
    ("Effect query modifier requests are read from context values", EffectQueryModifierRequestsAreReadFromContextValues),
    // (RD-A6-02) "CardEffectCommons factory creates headless numeric modifiers" RETIRED — it pinned
    // ModifierHelperFactory (ChangeDp/SetPlayCost/InvertSecurityAttack), a zero-src-consumer convenience
    // sugar layer over NumericModifier.Add/Set/InvertSecurityAttack that threaded no live behavior of its
    // own; the factory is now deleted (see ModifierHelpers.cs). NumericModifier.Add/Set/InvertSecurityAttack
    // themselves remain live (ReadSimpleModifiers) and are exercised directly by every other subtest here.
    ("Modifier result values are deterministic", ModifierResultValuesAreDeterministic),
    ("G3H-001 source files stay inside modifier helper scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3H-001")
        ?? throw new InvalidOperationException("G3H-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Modifiers", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "DP cost SAttack", "scope");
    AssertEqual("modifier helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "modifier", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3H-001_modifier_helpers_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3E-002", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "modifier helper", "completion gate");
    AssertComplete("G3E-002_digivolution_cost_helper_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsModifierReferencesAreRecorded()
{
    string changeDp = ReadAsIsFactory("ChangeDP.cs");
    string playCost = ReadAsIsFactory("ChangePlayCost.cs");
    string digivolutionCost = ReadAsIsFactory("ChangeDigivolutionCost.cs");
    string securityAttack = ReadAsIsFactory("ChangeSAttack.cs");

    AssertContains(changeDp, "ChangeDPStaticEffect", "AS-IS DP factory");
    AssertContains(changeDp, "DP += _changeValue()", "AS-IS DP add");
    AssertContains(playCost, "ChangePlayCostStaticEffect", "AS-IS play cost factory");
    AssertContains(playCost, "Cost = _changeValue()", "AS-IS fixed play cost");
    AssertContains(digivolutionCost, "ChangeDigivolutionCostStaticEffect", "AS-IS digivolution cost factory");
    AssertContains(digivolutionCost, "Cost += _changeValue()", "AS-IS digivolution cost add");
    AssertContains(securityAttack, "ChangeSAttackStaticEffect", "AS-IS security attack factory");
    AssertContains(securityAttack, "InvertSAttackStaticEffect", "AS-IS security attack invert");
    return Task.CompletedTask;
}

Task DpModifierAppliesSetBeforeAddAndFiltersTarget()
{
    var modifiers = new[]
    {
        NumericModifier.Add("other-target", NumericModifierMetric.Dp, 9000, OtherTargetId),
        NumericModifier.Add("dp-plus", NumericModifierMetric.Dp, 2000, TargetId),
        NumericModifier.Set("fixed-dp", NumericModifierMetric.Dp, 5000, TargetId),
        NumericModifier.Add("wrong-metric", NumericModifierMetric.SecurityAttack, 1, TargetId),
    };

    // (RD-A6-02 re-aim) ModifierHelpers.ResolveDp (zero src consumers) is deleted; fold directly via the live
    // Evaluate(NumericModifierRequest) surface it used to wrap.
    NumericModifierResult result = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, 3000, modifiers, TargetId));

    // (P0-DP-2) AS-IS Permanent.DP applies the isUpDown group FIRST, then the NotIsUpDown/Set group last
    // (Permanent.cs:290/301). So an upDown "+2000" applies before a "DP becomes 5000" set: 3000+2000=5000, set→5000.
    AssertEqual(5000, result.FinalValue, "final DP");
    AssertSequence(new[] { "dp-plus", "fixed-dp" }, result.AppliedModifierIds, "applied modifier ids");
    AssertSequence(new[] { "other-target" }, result.SkippedModifierIds, "skipped modifier ids");
    return Task.CompletedTask;
}

Task CostModifiersClampAndRespectReductionPermission()
{
    var modifiers = new[]
    {
        NumericModifier.Add("cost-reduction", NumericModifierMetric.PlayCost, -3),
        NumericModifier.Add("requires-availability", NumericModifierMetric.PlayCost, -1, requiresAvailabilityCheck: true),
        NumericModifier.Add("cost-increase", NumericModifierMetric.PlayCost, 2),
    };

    // (RD-A6-02 re-aim) ModifierHelpers.ResolvePlayCost (zero src consumers) is deleted; fold directly via the
    // live Evaluate(NumericModifierRequest) surface it used to wrap.
    NumericModifierResult blockedReduction = ModifierHelpers.Evaluate(new NumericModifierRequest(
        NumericModifierMetric.PlayCost, 5, modifiers, checkAvailability: false, canReduceValue: false, minimumValue: 0));
    NumericModifierResult allowedReduction = ModifierHelpers.Evaluate(new NumericModifierRequest(
        NumericModifierMetric.PlayCost, 1, new[] { NumericModifier.Add("large-reduction", NumericModifierMetric.PlayCost, -4) }, minimumValue: 0));

    AssertEqual(7, blockedReduction.FinalValue, "blocked reduction cost");
    AssertSequence(new[] { "cost-increase" }, blockedReduction.AppliedModifierIds, "blocked applied ids");
    AssertSequence(new[] { "cost-reduction", "requires-availability" }, blockedReduction.SkippedModifierIds, "blocked skipped ids");
    AssertEqual(0, allowedReduction.FinalValue, "clamped cost");
    return Task.CompletedTask;
}

Task DigivolutionCostModifierReadsSimpleMetadataKeys()
{
    CardRecord card = CreateCard(new Dictionary<string, object?>
    {
        [ModifierHelpers.DigivolutionCostDeltaKey] = -2,
    });
    CardInstanceRecord instance = new(
        SourceId,
        new HeadlessEntityId("BT-001"),
        PlayerOne,
        Metadata: new Dictionary<string, object?>
        {
            [ModifierHelpers.NumericModifiersKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "fixed-evo-cost",
                    [ModifierHelpers.ModifierMetricKey] = nameof(NumericModifierMetric.DigivolutionCost),
                    [ModifierHelpers.ModifierModeKey] = nameof(NumericModifierMode.Set),
                    [ModifierHelpers.ModifierValueKey] = 4,
                },
            },
        });

    // (RD-A6-02 re-aim) ModifierHelpers.ResolveDigivolutionCost (zero src consumers) is deleted; fold directly
    // via the live Evaluate(NumericModifierRequest) surface it used to wrap.
    NumericModifierResult result = ModifierHelpers.Evaluate(new NumericModifierRequest(
        NumericModifierMetric.DigivolutionCost, 5, ModifierHelpers.ReadModifiers(card, instance), minimumValue: 0));

    AssertEqual(2, result.FinalValue, "digivolution cost");
    AssertSequence(new[] { "fixed-evo-cost", ModifierHelpers.DigivolutionCostDeltaKey }, result.AppliedModifierIds, "digivolution applied ids");
    return Task.CompletedTask;
}

Task SecurityAttackModifierResolvesAddSetAndInvertDelta()
{
    var modifiers = new[]
    {
        NumericModifier.Add("sattack-plus", NumericModifierMetric.SecurityAttack, 2, TargetId),
        NumericModifier.Set("fixed-sattack", NumericModifierMetric.SecurityAttack, 1, TargetId),
        NumericModifier.InvertSecurityAttack("invert-plus-to-minus", 1, TargetId),
    };

    // (RD-A6-02 re-aim) ModifierHelpers.ResolveSecurityAttack (zero src consumers) is deleted; fold directly
    // via the live Evaluate(NumericModifierRequest) surface it used to wrap.
    NumericModifierResult result = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.SecurityAttack, 1, modifiers, TargetId, minimumValue: 0));

    // (b-remediation) the invert (+1) is now APPLIED, not merely recorded (AS-IS Permanent.InvertSecutiryValue):
    // it FLIPS the +2 increase into a -2 decrease, so base 1 -> set 1 -> (1-2) = -1 -> clamped to the 0 floor.
    AssertEqual(0, result.FinalValue, "security attack (the +2 is inverted to -2, clamped to 0)");
    AssertEqual(1, result.InvertDelta, "invert delta");
    AssertSequence(new[] { "fixed-sattack", "sattack-plus", "invert-plus-to-minus" }, result.AppliedModifierIds, "security attack applied ids");
    return Task.CompletedTask;
}

Task CardInstanceStateModifiersAreReadWithoutMutation()
{
    CardInstanceState state = new(
        TargetId,
        new HeadlessEntityId("BT-002"),
        PlayerOne,
        Modifiers: new Dictionary<string, object?>
        {
            [ModifierHelpers.DpDeltaKey] = "-1000",
            [ModifierHelpers.FixedSecurityAttackKey] = 2,
        });

    IReadOnlyList<NumericModifier> modifiers = ModifierHelpers.ReadModifiers(state: state);
    // (RD-A6-02 re-aim) ModifierHelpers.ResolveDp/ResolveSecurityAttack (zero src consumers) are deleted; fold
    // directly via the live Evaluate(NumericModifierRequest) surface they used to wrap.
    NumericModifierResult dp = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, 4000, modifiers, TargetId));
    NumericModifierResult securityAttack = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.SecurityAttack, 1, modifiers, TargetId, minimumValue: 0));

    AssertEqual(3000, dp.FinalValue, "state DP");
    AssertEqual(2, securityAttack.FinalValue, "state security attack");
    AssertEqual("-1000", state.Modifiers[ModifierHelpers.DpDeltaKey], "original state modifier remains string");
    return Task.CompletedTask;
}

Task EffectQueryModifierRequestsAreReadFromContextValues()
{
    EffectRequest request = new(
        new HeadlessEntityId("effect-001"),
        PlayerOne,
        "Continuous",
        new EffectContext(
            PlayerOne,
            PlayerOne,
            SourceId,
            triggerEntityId: null,
            targetEntityIds: new[] { TargetId },
            values: new Dictionary<string, object?>
            {
                [ModifierHelpers.SecurityAttackDeltaKey] = 1,
                [ModifierHelpers.NumericModifiersKey] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        [ModifierHelpers.ModifierMetricKey] = nameof(NumericModifierMetric.Dp),
                        [ModifierHelpers.ModifierValueKey] = 1000,
                        [ModifierHelpers.ModifierTargetEntityIdKey] = TargetId.Value,
                    },
                },
            }));

    IReadOnlyList<NumericModifier> modifiers = ModifierHelpers.ReadModifiers(effectRequests: new[] { request });
    // (RD-A6-02 re-aim) ModifierHelpers.ResolveDp/ResolveSecurityAttack (zero src consumers) are deleted; fold
    // directly via the live Evaluate(NumericModifierRequest) surface they used to wrap.
    NumericModifierResult dp = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, 2000, modifiers, TargetId));
    NumericModifierResult securityAttack = ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.SecurityAttack, 1, modifiers, TargetId, minimumValue: 0));

    AssertEqual(3000, dp.FinalValue, "query DP");
    AssertEqual(2, securityAttack.FinalValue, "query security attack");
    AssertContains(string.Join(",", dp.AppliedModifierIds), "effect-001", "effect id prefix");
    return Task.CompletedTask;
}

Task ModifierResultValuesAreDeterministic()
{
    var modifiers = new[]
    {
        NumericModifier.Add("b", NumericModifierMetric.Dp, 2),
        NumericModifier.Add("a", NumericModifierMetric.Dp, 1),
    };

    // (RD-A6-02 re-aim) ModifierHelpers.ResolveDp (zero src consumers) is deleted; fold directly via the live
    // Evaluate(NumericModifierRequest) surface it used to wrap.
    string first = Signature(ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, 1000, modifiers)));
    string second = Signature(ModifierHelpers.Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, 1000, modifiers)));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "appliedModifierIds=a;b", "stable id order");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helperPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "ModifierHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "ModifierHelpers.cs");
    string helper = File.ReadAllText(helperPath);
    string facade = File.ReadAllText(facadePath);

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("placeholder", StringComparison.OrdinalIgnoreCase), "helper must not contain placeholder");
    AssertFalse(helper.Contains("UnityEngine", StringComparison.Ordinal), "helper must not depend on Unity");
    AssertFalse(facade.Contains("TODO", StringComparison.OrdinalIgnoreCase), "facade must not contain TODO");
    AssertContains(helper, "NumericModifierMetric.SecurityAttack", "security attack modifier support");
    AssertContains(helper, "DigivolutionCostDeltaKey", "digivolution cost modifier support");
    return Task.CompletedTask;
}

CardRecord CreateCard(IReadOnlyDictionary<string, object?> metadata)
{
    return new CardRecord(
        new HeadlessEntityId("BT-001"),
        "BT-001",
        "Modifier Test Card",
        metadata,
        CardType: "Digimon",
        PlayCost: 3,
        EvolutionCost: 2);
}

string ReadAsIsFactory(string fileName)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory", fileName));
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

static string Signature(NumericModifierResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.BaseValue, result.FinalValue, result.InvertDelta, values);
}

static string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        string[] strings => string.Join(";", strings),
        IReadOnlyList<string> strings => string.Join(";", strings),
        _ => value.ToString() ?? string.Empty,
    };
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    List<List<string>> records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException("CSV file was empty.");
    }

    string[] headers = records[0].ToArray();
    var rows = new List<Dictionary<string, string>>();
    foreach (List<string> record in records.Skip(1))
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            row[headers[index]] = index < record.Count ? record[index] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

static List<List<string>> ParseCsv(string text)
{
    var rows = new List<List<string>>();
    var row = new List<string>();
    var field = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int index = 0; index < text.Length; index++)
    {
        char current = text[index];
        if (inQuotes)
        {
            if (current == '"' && index + 1 < text.Length && text[index + 1] == '"')
            {
                field.Append('"');
                index++;
            }
            else if (current == '"')
            {
                inQuotes = false;
            }
            else
            {
                field.Append(current);
            }
        }
        else if (current == '"')
        {
            inQuotes = true;
        }
        else if (current == ',')
        {
            row.Add(field.ToString());
            field.Clear();
        }
        else if (current == '\r' || current == '\n')
        {
            if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            row.Add(field.ToString());
            field.Clear();
            if (row.Count > 1 || row[0].Length > 0)
            {
                rows.Add(row);
            }

            row = new List<string>();
        }
        else
        {
            field.Append(current);
        }
    }

    if (field.Length > 0 || row.Count > 0)
    {
        row.Add(field.ToString());
        rows.Add(row);
    }

    return rows;
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    string expectedText = string.Join(",", expected);
    string actualText = string.Join(",", actual);
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expectedText}', got '{actualText}'.");
    }
}

static void AssertContains(string? text, string expected, string label)
{
    if (text is null || !text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

static string FindRepositoryRoot()
{
    string current = AppContext.BaseDirectory;
    while (!string.IsNullOrWhiteSpace(current))
    {
        if (File.Exists(Path.Combine(current, "docs", "headless_complete_goal_breakdown.csv")) &&
            Directory.Exists(Path.Combine(current, "src", "HeadlessDCGO.Engine")))
        {
            return current;
        }

        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent is null)
        {
            break;
        }

        current = parent.FullName;
    }

    throw new DirectoryNotFoundException("Repository root was not found.");
}
