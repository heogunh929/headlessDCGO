using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId CardId = new("play-card-instance");
HeadlessEntityId DefinitionId = new("BT1-PLAY");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3E-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS play cost references are recorded", AsIsPlayCostReferencesAreRecorded),
    ("Play cost resolves base fixed and missing cost cases", PlayCostResolvesBaseFixedAndMissing),
    ("Cost itself and paying cost modifiers are applied in AS-IS order", CostModifiersApplyInOrder),
    ("Cost reduction permission blocks up down reductions", CostReductionPermissionBlocksUpDownReduction),
    ("Check availability filters modifiers that require availability", CheckAvailabilityFiltersModifiers),
    ("Available memory determines can pay without mutating state", AvailableMemoryDeterminesCanPay),
    ("Metadata modifiers are read from card and instance records", MetadataModifiersAreRead),
    ("Play cost result values are deterministic", PlayCostResultValuesAreDeterministic),
    ("G3E-001 source files stay inside play cost scope", SourceFilesStayInsideGoalScope),
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
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3E-001")
        ?? throw new InvalidOperationException("G3E-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Costs", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "play cost", "scope");
    AssertEqual("play cost helper", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "play cost", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3E-001_play_cost_helper_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3D-002", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "play cost", "completion gate");

    AssertComplete("G3D-002_name_color_trait_requirements_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsPlayCostReferencesAreRecorded()
{
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));
    string showReducedCost = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "ShowReducedCost.cs"));
    string changePlayCost = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory", "ChangePlayCost.cs"));
    string changeCostClass = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffects", "ChangeCostClass.cs"));

    AssertContains(cardSource, "public int PayingCost", "AS-IS PayingCost");
    AssertContains(cardSource, "GetPayingCostWithBaseCost", "AS-IS base cost pipeline");
    AssertContains(cardSource, "GetChangedCostItselef", "AS-IS cost itself stage");
    AssertContains(cardSource, "GetChangedPayingCost", "AS-IS paying cost stage");
    AssertContains(showReducedCost, "ShowReducedCost", "AS-IS reduced cost UI hook");
    AssertContains(showReducedCost, "Card.PayingCost", "AS-IS reduced cost calculation");
    AssertContains(changePlayCost, "ChangePlayCostStaticEffect", "AS-IS play cost modifier");
    AssertContains(changePlayCost, "MandatorySelfPlayCostReduction", "AS-IS paying cost reduction");
    AssertContains(changeCostClass, "CanReduceCost", "AS-IS reduction permission");
    return Task.CompletedTask;
}

Task PlayCostResolvesBaseFixedAndMissing()
{
    CardRecord baseCard = CreateCard(playCost: 5);
    PlayCostResult baseResult = PlayCostHelpers.Evaluate(new PlayCostRequest(baseCard));
    PlayCostResult fixedResult = PlayCostHelpers.Evaluate(new PlayCostRequest(baseCard, fixedCost: 2));
    PlayCostResult missing = PlayCostHelpers.Evaluate(new PlayCostRequest(CreateCard(playCost: null)));

    AssertTrue(baseResult.IsSuccess, "base success");
    AssertEqual(5, baseResult.Cost, "base cost");
    AssertTrue(fixedResult.IsSuccess, "fixed success");
    AssertEqual(2, fixedResult.Cost, "fixed cost");
    AssertFalse(missing.IsSuccess, "missing play cost");
    AssertContains(missing.Reason, "no play cost", "missing reason");
    return Task.CompletedTask;
}

Task CostModifiersApplyInOrder()
{
    CardRecord card = CreateCard(playCost: 7);
    var modifiers = new[]
    {
        PlayCostModifier.AddToPayingCost("paying-discount", -3),
        PlayCostModifier.AddToCost("cost-bonus", 2),
        PlayCostModifier.SetCost("set-cost", 4),
    };

    PlayCostResult result = PlayCostHelpers.Evaluate(new PlayCostRequest(card, modifiers: modifiers));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(3, result.Cost, "final cost");
    AssertEqual(6, result.Values["costItself"], "cost itself stage");
    AssertSequence(new[] { "set-cost", "cost-bonus" }, ReadStringArray(result, "appliedCostModifierIds"), "cost modifier order");
    AssertSequence(new[] { "paying-discount" }, ReadStringArray(result, "appliedPayingCostModifierIds"), "paying modifier order");
    return Task.CompletedTask;
}

Task CostReductionPermissionBlocksUpDownReduction()
{
    CardRecord card = CreateCard(playCost: 5);
    var modifiers = new[]
    {
        PlayCostModifier.AddToCost("blocked-reduction", -2),
        PlayCostModifier.SetCost("fixed-low-cost", 1),
    };

    PlayCostResult result = PlayCostHelpers.Evaluate(new PlayCostRequest(
        card,
        modifiers: modifiers,
        canReduceCost: false));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(1, result.Cost, "set modifier still applies");
    AssertSequence(new[] { "fixed-low-cost" }, ReadStringArray(result, "appliedCostModifierIds"), "applied ids");
    AssertSequence(new[] { "blocked-reduction" }, ReadStringArray(result, "skippedCostModifierIds"), "skipped ids");
    return Task.CompletedTask;
}

Task CheckAvailabilityFiltersModifiers()
{
    CardRecord card = CreateCard(playCost: 5);
    var modifier = PlayCostModifier.AddToCost("availability-only", -2, requiresAvailabilityCheck: true);

    PlayCostResult skipped = PlayCostHelpers.Evaluate(new PlayCostRequest(card, modifiers: new[] { modifier }));
    PlayCostResult applied = PlayCostHelpers.Evaluate(new PlayCostRequest(card, modifiers: new[] { modifier }, checkAvailability: true));

    AssertTrue(skipped.IsSuccess, "skipped success");
    AssertEqual(5, skipped.Cost, "skipped cost");
    AssertSequence(new[] { "availability-only" }, ReadStringArray(skipped, "skippedCostModifierIds"), "skipped ids");
    AssertTrue(applied.IsSuccess, "applied success");
    AssertEqual(3, applied.Cost, "applied cost");
    return Task.CompletedTask;
}

Task AvailableMemoryDeterminesCanPay()
{
    CardRecord card = CreateCard(playCost: 6);
    PlayCostResult cannotPay = PlayCostHelpers.Evaluate(new PlayCostRequest(card, availableMemory: 5));
    PlayCostResult canPay = PlayCostHelpers.Evaluate(new PlayCostRequest(card, availableMemory: 6));

    AssertTrue(cannotPay.IsSuccess, "cannot pay resolves");
    AssertFalse(cannotPay.CanPay, "cannot pay");
    AssertContains(cannotPay.Reason, "exceeds", "cannot pay reason");
    AssertTrue(canPay.CanPay, "can pay");
    return Task.CompletedTask;
}

Task MetadataModifiersAreRead()
{
    CardRecord card = CreateCard(
        playCost: 8,
        metadata: new Dictionary<string, object?>
        {
            [PlayCostHelpers.FixedPlayCostKey] = 6,
            [PlayCostHelpers.PlayCostDeltaKey] = -1,
        });
    var instance = new CardInstanceRecord(
        CardId,
        DefinitionId,
        PlayerOne,
        Metadata: new Dictionary<string, object?>
        {
            [PlayCostHelpers.PayingCostDeltaKey] = -2,
            [PlayCostHelpers.PlayCostModifiersKey] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "metadata-set-paying",
                    ["value"] = 2,
                    ["stage"] = PlayCostModifierStage.PayingCost.ToString(),
                    ["mode"] = PlayCostModifierMode.Set.ToString(),
                    ["isUpDown"] = false,
                },
            },
        });

    PlayCostResult result = PlayCostHelpers.Evaluate(new PlayCostRequest(
        card,
        instance,
        PlayCostHelpers.ReadModifiers(card, instance),
        PlayCostHelpers.ReadFixedCost(card, instance)));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(0, result.Cost, "metadata final cost");
    AssertEqual(6, result.Values["baseCost"], "metadata fixed cost");
    AssertSequence(new[] { "playCostDelta" }, ReadStringArray(result, "appliedCostModifierIds"), "metadata cost ids");
    AssertSequence(new[] { "metadata-set-paying", "payingCostDelta" }, ReadStringArray(result, "appliedPayingCostModifierIds"), "metadata paying ids");
    return Task.CompletedTask;
}

Task PlayCostResultValuesAreDeterministic()
{
    CardRecord card = CreateCard(playCost: 7);
    var request = new PlayCostRequest(
        card,
        modifiers: new[]
        {
            PlayCostModifier.AddToCost("b", -1),
            PlayCostModifier.SetCost("a", 4),
            PlayCostModifier.AddToPayingCost("c", -2),
        },
        availableMemory: 2);

    string first = Signature(PlayCostHelpers.Evaluate(request));
    string second = Signature(PlayCostHelpers.Evaluate(request));

    AssertEqual(first, second, "signature");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helper = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "PlayCostHelpers.cs"));
    string playAction = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "PlayCardAction.cs"));
    string optionAction = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "OptionActivateAction.cs"));

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("UnityEngine", StringComparison.Ordinal), "helper must not reference UnityEngine");
    AssertFalse(helper.Contains("MonoBehaviour", StringComparison.Ordinal), "helper must not reference MonoBehaviour");
    AssertFalse(helper.Contains("Hashtable", StringComparison.Ordinal), "helper must not reference Hashtable");
    AssertFalse(helper.Contains("EvolutionCost", StringComparison.Ordinal), "helper must not implement next evolution cost scope");
    AssertFalse(helper.Contains("TargetFilter", StringComparison.Ordinal), "helper must not implement target filtering scope");
    AssertContains(helper, "PlayCostHelpers", "helper API");
    AssertContains(playAction, "PlayCostHelpers.TryResolveCost", "PlayCardAction integration");
    AssertContains(optionAction, "PlayCostHelpers.TryResolveCost", "OptionActivateAction integration");
    return Task.CompletedTask;
}

CardRecord CreateCard(int? playCost, IReadOnlyDictionary<string, object?>? metadata = null)
{
    return new CardRecord(
        DefinitionId,
        DefinitionId.Value,
        "Playable Card",
        metadata ?? new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: playCost);
}

string Signature(PlayCostResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsSuccess, result.Cost, result.CanPay, result.Reason, values);
}

string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        string[] strings => string.Join(";", strings),
        _ => value.ToString() ?? string.Empty,
    };
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
    var records = new List<List<string>>();
    var record = new List<string>();
    var field = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int index = 0; index < text.Length; index++)
    {
        char current = text[index];
        if (inQuotes)
        {
            if (current == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(current);
            }

            continue;
        }

        if (current == '"')
        {
            inQuotes = true;
        }
        else if (current == ',')
        {
            record.Add(field.ToString());
            field.Clear();
        }
        else if (current == '\r')
        {
            if (index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            AddRecord();
        }
        else if (current == '\n')
        {
            AddRecord();
        }
        else
        {
            field.Append(current);
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
        if (record.Any(value => value.Length > 0))
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
        if (File.Exists(Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value)
        ? value
        : throw new KeyNotFoundException($"CSV column was not found: {key}");
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{label}: expected count {expected.Count}, actual {actual.Count}.");
    }

    for (int index = 0; index < expected.Count; index++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
        {
            throw new InvalidOperationException($"{label}: index {index} expected '{expected[index]}', actual '{actual[index]}'.");
        }
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static string[] ReadStringArray(PlayCostResult result, string key)
{
    if (!result.Values.TryGetValue(key, out object? value) || value is not string[] values)
    {
        throw new InvalidOperationException($"Expected string[] value for {key}.");
    }

    return values;
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
