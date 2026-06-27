using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId SourceId = new("p1-source");
HeadlessEntityId TargetId = new("p1-target");
HeadlessEntityId OtherTargetId = new("p2-target");
HeadlessEntityId RedirectTargetId = new("p1-redirect-target");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3I-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS replacement prevention references are recorded", AsIsReplacementReferencesAreRecorded),
    ("Prevent replacement cancels field removal", PreventReplacementCancelsFieldRemoval),
    ("Redirect replacement returns substitute target", RedirectReplacementReturnsSubstituteTarget),
    ("Immune replacement filters source and mutation kind", ImmuneReplacementFiltersSourceAndMutationKind),
    ("Metadata replacements are read from card and instance", MetadataReplacementsAreRead),
    ("CardInstanceState replacements are read from modifiers and flags", CardInstanceStateReplacementsAreRead),
    ("Effect query replacement requests are read from context values", EffectQueryReplacementsAreRead),
    ("Replacement result values are deterministic", ReplacementResultValuesAreDeterministic),
    ("Invalid redirect input fails with explicit exception", InvalidRedirectInputFails),
    ("CardEffectCommons factory creates headless replacements", CardEffectCommonsFactoryCreatesReplacements),
    ("G3I-001 source files stay inside replacement helper scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3I-001")
        ?? throw new InvalidOperationException("G3I-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Replacement", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "prevent redirect immune replacement", "scope");
    AssertEqual("replacement helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "replacement", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3I-001_replacement_prevention_helpers_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3H-002", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "replacement", "completion gate");
    AssertComplete("G3H-002_cannot_restriction_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsReplacementReferencesAreRecorded()
{
    string armorPurge = ReadAsIsCommons("KeyWordEffects/ArmorPurge.cs");
    string evade = ReadAsIsCommons("KeyWordEffects/Evade.cs");
    string decoy = ReadAsIsCommons("KeyWordEffects/Decoy.cs");
    string scapegoat = ReadAsIsCommons("KeyWordEffects/Scapegoat.cs");
    string immune = ReadAsIsFactory("ImmuneFromDPMinus.cs");

    AssertContains(armorPurge, "willBeRemoveField = false", "AS-IS armor purge prevents removal");
    AssertContains(evade, "willBeRemoveField = false", "AS-IS evade prevents removal");
    AssertContains(decoy, "Select 1 Digimon to prevent deletion", "AS-IS decoy prevent prompt");
    AssertContains(scapegoat, "Select 1 Digimon to delete", "AS-IS scapegoat redirect cost");
    AssertContains(immune, "ImmuneFromDPMinusStaticEffect", "AS-IS immune factory");
    AssertContains(immune, "SetUpImmuneFromDPMinusClass", "AS-IS immune setup");
    return Task.CompletedTask;
}

Task PreventReplacementCancelsFieldRemoval()
{
    var replacements = new[]
    {
        ReplacementEffect.Prevent("armor-purge", ReplacementEventKind.RemoveFromField, TargetId, "Armor Purge prevents removal."),
    };

    ReplacementResult result = ReplacementHelpers.PreventRemoval(TargetId, replacements);

    AssertTrue(result.IsReplaced, "prevent result");
    AssertEqual(ReplacementActionKind.Prevent, result.ActionKind, "prevent action");
    AssertSequence(new[] { "armor-purge" }, result.AppliedReplacementIds, "applied ids");
    AssertContains(result.Reason, "Armor Purge", "prevent reason");
    return Task.CompletedTask;
}

Task RedirectReplacementReturnsSubstituteTarget()
{
    var replacements = new[]
    {
        ReplacementEffect.Redirect("scapegoat", ReplacementEventKind.Delete, TargetId, RedirectTargetId, "Delete another Digimon instead."),
    };

    ReplacementResult result = ReplacementHelpers.PreventDeletion(TargetId, replacements);

    AssertTrue(result.IsReplaced, "redirect result");
    AssertEqual(ReplacementActionKind.Redirect, result.ActionKind, "redirect action");
    AssertEqual(RedirectTargetId, result.ReplacementEntityId!.Value, "redirect target");
    AssertSequence(new[] { "scapegoat" }, result.AppliedReplacementIds, "redirect applied ids");
    return Task.CompletedTask;
}

Task ImmuneReplacementFiltersSourceAndMutationKind()
{
    var replacements = new[]
    {
        ReplacementEffect.Immune("wrong-source", ReplacementEventKind.DpReduction, TargetId, OtherTargetId, "ChangeDP"),
        ReplacementEffect.Immune("wrong-mutation", ReplacementEventKind.DpReduction, TargetId, SourceId, "Delete"),
        ReplacementEffect.Immune("matching-immune", ReplacementEventKind.DpReduction, TargetId, SourceId, "ChangeDP"),
    };

    ReplacementResult result = ReplacementHelpers.ImmuneFromDpReduction(TargetId, replacements, SourceId);

    AssertTrue(result.IsReplaced, "immune result");
    AssertEqual(ReplacementActionKind.Immune, result.ActionKind, "immune action");
    AssertSequence(new[] { "matching-immune" }, result.AppliedReplacementIds, "immune applied ids");
    AssertSequence(new[] { "wrong-mutation", "wrong-source" }, result.SkippedReplacementIds, "immune skipped ids");
    return Task.CompletedTask;
}

Task MetadataReplacementsAreRead()
{
    CardRecord card = CreateCard(new Dictionary<string, object?>
    {
        [ReplacementHelpers.PreventRemovalKey] = true,
        [ReplacementHelpers.ImmuneFromDpMinusKey] = "true",
    });
    CardInstanceRecord instance = new(
        TargetId,
        new HeadlessEntityId("BT-001"),
        PlayerOne,
        Metadata: new Dictionary<string, object?>
        {
            [ReplacementHelpers.ReplacementsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "typed-redirect",
                    [ReplacementHelpers.EventKindKey] = nameof(ReplacementEventKind.Delete),
                    [ReplacementHelpers.ActionKindKey] = nameof(ReplacementActionKind.Redirect),
                    [ReplacementHelpers.TargetEntityIdKey] = TargetId.Value,
                    [ReplacementHelpers.ReplacementEntityIdKey] = RedirectTargetId.Value,
                },
            },
        });

    IReadOnlyList<ReplacementEffect> replacements = ReplacementHelpers.ReadReplacements(card, instance);

    AssertTrue(ReplacementHelpers.PreventRemoval(TargetId, replacements).IsReplaced, "metadata prevent");
    AssertTrue(ReplacementHelpers.ImmuneFromDpReduction(TargetId, replacements).IsReplaced, "metadata immune");
    AssertEqual(RedirectTargetId, ReplacementHelpers.PreventDeletion(TargetId, replacements).ReplacementEntityId!.Value, "metadata redirect");
    return Task.CompletedTask;
}

Task CardInstanceStateReplacementsAreRead()
{
    CardInstanceState state = new(
        TargetId,
        new HeadlessEntityId("BT-002"),
        PlayerOne,
        Modifiers: new Dictionary<string, object?> { [ReplacementHelpers.PreventDeletionKey] = true },
        Flags: new Dictionary<string, bool> { [ReplacementHelpers.PreventRemovalKey] = true });

    IReadOnlyList<ReplacementEffect> replacements = ReplacementHelpers.ReadReplacements(state: state);

    AssertTrue(ReplacementHelpers.PreventDeletion(TargetId, replacements).IsReplaced, "state prevent deletion");
    AssertTrue(ReplacementHelpers.PreventRemoval(TargetId, replacements).IsReplaced, "state prevent removal");
    AssertEqual(true, state.Flags[ReplacementHelpers.PreventRemovalKey], "state flag remains");
    return Task.CompletedTask;
}

Task EffectQueryReplacementsAreRead()
{
    var registry = new InMemoryEffectRegistry();
    EffectContext context = new(
        PlayerOne,
        PlayerOne,
        SourceId,
        triggerEntityId: null,
        targetEntityIds: new[] { TargetId },
        values: new Dictionary<string, object?>
        {
            [ReplacementHelpers.ReplacementsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    [ReplacementHelpers.EventKindKey] = nameof(ReplacementEventKind.RemoveFromField),
                    [ReplacementHelpers.ActionKindKey] = nameof(ReplacementActionKind.Prevent),
                    [ReplacementHelpers.TargetEntityIdKey] = TargetId.Value,
                    [ReplacementHelpers.ReasonKey] = "Queried replacement.",
                },
            },
        });
    EffectRequest request = new(new HeadlessEntityId("effect-replacement"), PlayerOne, "WhenRemoveField", context);
    registry.Register(new EffectBinding(
        request,
        queryRoles: EffectQueryRole.Replacement,
        queryScopes: new[] { "RemoveFromFieldReplacement" }));

    IReadOnlyList<ReplacementEffect> replacements = ReplacementHelpers.QueryReplacements(
        registry,
        new EffectQueryContext("RemoveFromFieldReplacement", targetEntityId: TargetId));
    ReplacementResult result = ReplacementHelpers.PreventRemoval(TargetId, replacements);

    AssertTrue(result.IsReplaced, "query replacement");
    AssertContains(result.AppliedReplacementIds[0], "effect-replacement", "effect id prefix");
    AssertContains(result.Reason, "Queried replacement", "query reason");
    return Task.CompletedTask;
}

Task ReplacementResultValuesAreDeterministic()
{
    var replacements = new[]
    {
        ReplacementEffect.Prevent("b", ReplacementEventKind.RemoveFromField, TargetId, priority: 1),
        ReplacementEffect.Prevent("a", ReplacementEventKind.RemoveFromField, TargetId, priority: 1),
    };

    string first = Signature(ReplacementHelpers.PreventRemoval(TargetId, replacements));
    string second = Signature(ReplacementHelpers.PreventRemoval(TargetId, replacements));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "appliedReplacementIds=a", "stable id order");
    AssertContains(first, "skippedReplacementIds=b", "stable skipped id");
    return Task.CompletedTask;
}

Task InvalidRedirectInputFails()
{
    AssertThrows<ArgumentException>(() => new ReplacementEffect("bad", ReplacementEventKind.Delete, ReplacementActionKind.Redirect, TargetId));
    AssertThrows<ArgumentException>(() => new ReplacementRequest(ReplacementEventKind.Delete, default));
    return Task.CompletedTask;
}

Task CardEffectCommonsFactoryCreatesReplacements()
{
    ReplacementEffect preventRemoval = ReplacementHelperFactory.PreventRemoval("factory-removal", TargetId);
    ReplacementEffect preventDeletion = ReplacementHelperFactory.PreventDeletion("factory-delete", TargetId);
    ReplacementEffect redirect = ReplacementHelperFactory.RedirectDeletion("factory-redirect", TargetId, RedirectTargetId);
    ReplacementEffect immune = ReplacementHelperFactory.ImmuneFromDpReduction("factory-immune", TargetId, SourceId);

    AssertEqual(ReplacementActionKind.Prevent, preventRemoval.ActionKind, "factory prevent removal");
    AssertEqual(ReplacementEventKind.Delete, preventDeletion.EventKind, "factory prevent deletion");
    AssertEqual(ReplacementActionKind.Redirect, redirect.ActionKind, "factory redirect");
    AssertEqual(ReplacementActionKind.Immune, immune.ActionKind, "factory immune");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helperPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "ReplacementHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "ReplacementHelpers.cs");
    string helper = File.ReadAllText(helperPath);
    string facade = File.ReadAllText(facadePath);

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("placeholder", StringComparison.OrdinalIgnoreCase), "helper must not contain placeholder");
    AssertFalse(helper.Contains("UnityEngine", StringComparison.Ordinal), "helper must not depend on Unity");
    AssertFalse(facade.Contains("TODO", StringComparison.OrdinalIgnoreCase), "facade must not contain TODO");
    AssertContains(helper, "ReplacementActionKind.Prevent", "prevent support");
    AssertContains(helper, "ReplacementActionKind.Redirect", "redirect support");
    AssertContains(helper, "ReplacementActionKind.Immune", "immune support");
    return Task.CompletedTask;
}

CardRecord CreateCard(IReadOnlyDictionary<string, object?> metadata)
{
    return new CardRecord(
        new HeadlessEntityId("BT-001"),
        "BT-001",
        "Replacement Test Card",
        metadata,
        CardType: "Digimon");
}

string ReadAsIsCommons(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", relativePath.Replace('/', Path.DirectorySeparatorChar)));
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

static string Signature(ReplacementResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsReplaced, result.ActionKind, result.ReplacementEntityId, result.Reason, values);
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

static void AssertThrows<TException>(Action action)
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
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
