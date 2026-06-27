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
    ("G3H-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS cannot restriction references are recorded", AsIsCannotRestrictionReferencesAreRecorded),
    ("Attack block delete return suspend restrictions resolve", RestrictionsResolveEachGoalKind),
    ("Restriction target and source filters skip non matching restrictions", RestrictionFiltersSkipNonMatching),
    ("Metadata boolean restrictions are read from card and instance", MetadataRestrictionsAreRead),
    ("CardInstanceState modifiers and flags are read as restrictions", CardInstanceStateRestrictionsAreRead),
    ("Effect query restriction requests are read from context values", EffectQueryRestrictionsAreRead),
    ("Restriction result values are deterministic", RestrictionResultValuesAreDeterministic),
    ("Invalid restriction input fails with explicit exception", InvalidRestrictionInputFails),
    ("CardEffectCommons factory creates headless restrictions", CardEffectCommonsFactoryCreatesRestrictions),
    ("G3H-002 source files stay inside restriction helper scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3H-002")
        ?? throw new InvalidOperationException("G3H-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Restrictions", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "cannot attack block delete return suspend", "scope");
    AssertEqual("restriction helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "restriction", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3H-002_cannot_restriction_helpers_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3H-001", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "restriction", "completion gate");
    AssertComplete("G3H-001_modifier_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsCannotRestrictionReferencesAreRecorded()
{
    string attack = ReadAsIsFactory("CanNotAttack.cs");
    string block = ReadAsIsFactory("CanNotBlock.cs");
    string delete = ReadAsIsFactory("CanNotBeDeleted.cs");
    string returnHand = ReadAsIsFactory("CanNotReturnToHand.cs");
    string returnDeck = ReadAsIsFactory("CanNoReturnToDeck.cs");
    string suspend = ReadAsIsFactory("CanNotSuspend.cs");

    AssertContains(attack, "CanNotAttackStaticEffect", "AS-IS cannot attack factory");
    AssertContains(attack, "SetUpCanNotAttackTargetDefendingPermanentClass", "AS-IS attack setup");
    AssertContains(block, "CanNotBlockStaticEffect", "AS-IS cannot block factory");
    AssertContains(block, "SetUpCannotBlockClass", "AS-IS block setup");
    AssertContains(delete, "CanNotBeDestroyedStaticEffect", "AS-IS cannot delete factory");
    AssertContains(returnHand, "CannotReturnToHandStaticEffect", "AS-IS cannot return hand factory");
    AssertContains(returnDeck, "CannotReturnToDeckStaticEffect", "AS-IS cannot return deck factory");
    AssertContains(suspend, "CantSuspendStaticEffect", "AS-IS cannot suspend factory");
    return Task.CompletedTask;
}

Task RestrictionsResolveEachGoalKind()
{
    var restrictions = new[]
    {
        CannotRestriction.ForTarget("cannot-attack", CannotRestrictionKind.Attack, TargetId, "No attack."),
        CannotRestriction.ForTarget("cannot-block", CannotRestrictionKind.Block, TargetId, "No block."),
        CannotRestriction.ForTarget("cannot-delete", CannotRestrictionKind.Delete, TargetId, "No delete."),
        CannotRestriction.ForTarget("cannot-return-hand", CannotRestrictionKind.ReturnToHand, TargetId, "No hand."),
        CannotRestriction.ForTarget("cannot-return-deck", CannotRestrictionKind.ReturnToDeck, TargetId, "No deck."),
        CannotRestriction.ForTarget("cannot-suspend", CannotRestrictionKind.Suspend, TargetId, "No suspend."),
    };

    AssertRestricted(RestrictionHelpers.CannotAttack(TargetId, restrictions), "cannot-attack");
    AssertRestricted(RestrictionHelpers.CannotBlock(TargetId, restrictions), "cannot-block");
    AssertRestricted(RestrictionHelpers.CannotDelete(TargetId, restrictions), "cannot-delete");
    AssertRestricted(RestrictionHelpers.CannotReturnToHand(TargetId, restrictions), "cannot-return-hand");
    AssertRestricted(RestrictionHelpers.CannotReturnToDeck(TargetId, restrictions), "cannot-return-deck");
    AssertRestricted(RestrictionHelpers.CannotSuspend(TargetId, restrictions), "cannot-suspend");
    return Task.CompletedTask;
}

Task RestrictionFiltersSkipNonMatching()
{
    var restrictions = new[]
    {
        CannotRestriction.ForTarget("wrong-target", CannotRestrictionKind.Attack, OtherTargetId),
        new CannotRestriction("wrong-source", CannotRestrictionKind.Attack, TargetId, OtherTargetId),
        new CannotRestriction("matching", CannotRestrictionKind.Attack, TargetId, SourceId),
        new CannotRestriction("needs-availability", CannotRestrictionKind.Attack, TargetId, SourceId, requiresAvailabilityCheck: true),
    };

    CannotRestrictionResult result = RestrictionHelpers.Evaluate(new CannotRestrictionRequest(
        CannotRestrictionKind.Attack,
        TargetId,
        restrictions,
        SourceId,
        checkAvailability: false));

    AssertTrue(result.IsRestricted, "matching restriction");
    AssertSequence(new[] { "matching" }, result.AppliedRestrictionIds, "applied ids");
    AssertSequence(new[] { "needs-availability", "wrong-source", "wrong-target" }, result.SkippedRestrictionIds, "skipped ids");
    return Task.CompletedTask;
}

Task MetadataRestrictionsAreRead()
{
    CardRecord card = CreateCard(new Dictionary<string, object?>
    {
        [RestrictionHelpers.CannotAttackKey] = true,
        [RestrictionHelpers.CannotReturnToLibraryKey] = "true",
    });
    CardInstanceRecord instance = new(
        TargetId,
        new HeadlessEntityId("BT-001"),
        PlayerOne,
        Metadata: new Dictionary<string, object?>
        {
            [RestrictionHelpers.CannotBlockKey] = true,
            [RestrictionHelpers.RestrictionsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "typed-delete",
                    [RestrictionHelpers.RestrictionKindKey] = nameof(CannotRestrictionKind.Delete),
                    [RestrictionHelpers.RestrictionTargetEntityIdKey] = TargetId.Value,
                    [RestrictionHelpers.RestrictionReasonKey] = "Deletion is prevented.",
                },
            },
        });

    IReadOnlyList<CannotRestriction> restrictions = RestrictionHelpers.ReadRestrictions(card, instance);

    AssertRestricted(RestrictionHelpers.CannotAttack(TargetId, restrictions), RestrictionHelpers.CannotAttackKey);
    AssertRestricted(RestrictionHelpers.CannotBlock(TargetId, restrictions), RestrictionHelpers.CannotBlockKey);
    AssertRestricted(RestrictionHelpers.CannotDelete(TargetId, restrictions), "typed-delete");
    AssertRestricted(RestrictionHelpers.CannotReturnToDeck(TargetId, restrictions), RestrictionHelpers.CannotReturnToLibraryKey);
    return Task.CompletedTask;
}

Task CardInstanceStateRestrictionsAreRead()
{
    CardInstanceState state = new(
        TargetId,
        new HeadlessEntityId("BT-002"),
        PlayerOne,
        Modifiers: new Dictionary<string, object?> { [RestrictionHelpers.CannotReturnToHandKey] = true },
        Flags: new Dictionary<string, bool> { [RestrictionHelpers.CannotSuspendKey] = true });

    IReadOnlyList<CannotRestriction> restrictions = RestrictionHelpers.ReadRestrictions(state: state);

    AssertRestricted(RestrictionHelpers.CannotReturnToHand(TargetId, restrictions), RestrictionHelpers.CannotReturnToHandKey);
    AssertRestricted(RestrictionHelpers.CannotSuspend(TargetId, restrictions), RestrictionHelpers.CannotSuspendKey);
    AssertEqual(true, state.Flags[RestrictionHelpers.CannotSuspendKey], "state flag remains");
    return Task.CompletedTask;
}

Task EffectQueryRestrictionsAreRead()
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
            [RestrictionHelpers.RestrictionsKey] = new object[]
            {
                new Dictionary<string, object?>
                {
                    [RestrictionHelpers.RestrictionKindKey] = nameof(CannotRestrictionKind.Suspend),
                    [RestrictionHelpers.RestrictionTargetEntityIdKey] = TargetId.Value,
                    [RestrictionHelpers.RestrictionReasonKey] = "Queried restriction.",
                },
            },
        });
    EffectRequest request = new(new HeadlessEntityId("effect-restrict"), PlayerOne, "Continuous", context);
    registry.Register(new EffectBinding(
        request,
        queryRoles: EffectQueryRole.Restriction,
        queryScopes: new[] { "CannotSuspend" }));

    IReadOnlyList<CannotRestriction> restrictions = RestrictionHelpers.QueryRestrictions(
        registry,
        new EffectQueryContext("CannotSuspend", targetEntityId: TargetId));
    CannotRestrictionResult result = RestrictionHelpers.CannotSuspend(TargetId, restrictions);

    AssertTrue(result.IsRestricted, "query restriction");
    AssertContains(result.AppliedRestrictionIds[0], "effect-restrict", "effect id prefix");
    AssertContains(result.Reason, "Queried restriction", "query reason");
    return Task.CompletedTask;
}

Task RestrictionResultValuesAreDeterministic()
{
    var restrictions = new[]
    {
        CannotRestriction.ForTarget("b", CannotRestrictionKind.Attack, TargetId),
        CannotRestriction.ForTarget("a", CannotRestrictionKind.Attack, TargetId),
    };

    string first = Signature(RestrictionHelpers.CannotAttack(TargetId, restrictions));
    string second = Signature(RestrictionHelpers.CannotAttack(TargetId, restrictions));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "appliedRestrictionIds=a;b", "stable id order");
    return Task.CompletedTask;
}

Task InvalidRestrictionInputFails()
{
    AssertThrows<ArgumentException>(() => new CannotRestriction(" ", CannotRestrictionKind.Attack));
    AssertThrows<ArgumentException>(() => new CannotRestrictionRequest(CannotRestrictionKind.Block, default));
    return Task.CompletedTask;
}

Task CardEffectCommonsFactoryCreatesRestrictions()
{
    CannotRestriction attack = RestrictionHelperFactory.CannotAttack("factory-attack", TargetId);
    CannotRestriction block = RestrictionHelperFactory.CannotBlock("factory-block", TargetId);
    CannotRestriction delete = RestrictionHelperFactory.CannotDelete("factory-delete", TargetId);
    CannotRestriction returnHand = RestrictionHelperFactory.CannotReturnToHand("factory-hand", TargetId);
    CannotRestriction returnDeck = RestrictionHelperFactory.CannotReturnToDeck("factory-deck", TargetId);
    CannotRestriction suspend = RestrictionHelperFactory.CannotSuspend("factory-suspend", TargetId);

    AssertEqual(CannotRestrictionKind.Attack, attack.Kind, "factory attack kind");
    AssertEqual(CannotRestrictionKind.Block, block.Kind, "factory block kind");
    AssertEqual(CannotRestrictionKind.Delete, delete.Kind, "factory delete kind");
    AssertEqual(CannotRestrictionKind.ReturnToHand, returnHand.Kind, "factory hand kind");
    AssertEqual(CannotRestrictionKind.ReturnToDeck, returnDeck.Kind, "factory deck kind");
    AssertEqual(CannotRestrictionKind.Suspend, suspend.Kind, "factory suspend kind");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helperPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "RestrictionHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "RestrictionHelpers.cs");
    string helper = File.ReadAllText(helperPath);
    string facade = File.ReadAllText(facadePath);

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("placeholder", StringComparison.OrdinalIgnoreCase), "helper must not contain placeholder");
    AssertFalse(helper.Contains("UnityEngine", StringComparison.Ordinal), "helper must not depend on Unity");
    AssertFalse(facade.Contains("TODO", StringComparison.OrdinalIgnoreCase), "facade must not contain TODO");
    AssertContains(helper, "CannotRestrictionKind.Attack", "attack restriction support");
    AssertContains(helper, "CannotRestrictionKind.Suspend", "suspend restriction support");
    return Task.CompletedTask;
}

CardRecord CreateCard(IReadOnlyDictionary<string, object?> metadata)
{
    return new CardRecord(
        new HeadlessEntityId("BT-001"),
        "BT-001",
        "Restriction Test Card",
        metadata,
        CardType: "Digimon");
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

static void AssertRestricted(CannotRestrictionResult result, string id)
{
    AssertTrue(result.IsRestricted, $"{id} is restricted");
    AssertContains(string.Join(",", result.AppliedRestrictionIds), id, $"{id} applied id");
}

static string Signature(CannotRestrictionResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsRestricted, result.Reason, values);
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
