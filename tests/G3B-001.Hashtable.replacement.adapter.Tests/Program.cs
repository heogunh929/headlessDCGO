using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3B-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS Hashtable replacement references are recorded", AsIsHashtableReferencesAreRecorded),
    ("Adapter maps structural keys into typed EffectContext", AdapterMapsStructuralKeys),
    ("Adapter maps legacy key aliases without carrying structural payload values", AdapterMapsLegacyAliases),
    ("Adapter rejects missing or invalid structural values", AdapterRejectsInvalidValues),
    ("Adapter rejects duplicate or empty targets", AdapterRejectsInvalidTargets),
    ("Adapter round trips exported values deterministically", AdapterRoundTripsDeterministically),
    ("Adapted context composes with SkillInfo model", AdaptedContextComposesWithSkillInfo),
    ("G3B-001 source files contain no placeholder or Unity dependency", SourceFilesContainNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3B-001")
        ?? throw new InvalidOperationException("G3B-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("EffectContext", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "GetFromHashtable", "scope");
    AssertContains(Value(row, "scope"), "HashtableSetting", "scope");
    AssertEqual("typed context adapter", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "hashtable key replacement", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3B-001_hashtable_replacement_adapter_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3A-002", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "Hashtable replacement", "completion gate");

    AssertComplete("G3A-002_skill_info_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsHashtableReferencesAreRecorded()
{
    string settings = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "HashtableSetting.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string controller = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(settings, "CardEffectHashtable", "AS-IS CardEffect helper");
    AssertContains(settings, "OptionMainCheckHashtable", "AS-IS option helper");
    AssertContains(settings, "\"Card\"", "AS-IS Card key");
    AssertContains(settings, "\"Permanent\"", "AS-IS Permanent key");
    AssertContains(settings, "\"Permanents\"", "AS-IS Permanents key");
    AssertContains(settings, "\"battle\"", "AS-IS battle key");
    AssertContains(autoProcessing, "StackSkillInfos(Hashtable hashtable", "AS-IS stack payload");
    AssertContains(autoProcessing, "ActivateEffectProcess(ICardEffect cardEffect, Hashtable hashtable", "AS-IS activation payload");
    AssertContains(controller, "CardEffectCommons", "AS-IS controller helper calls");
    return Task.CompletedTask;
}

Task AdapterMapsStructuralKeys()
{
    EffectContext context = CreateContext(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = "1",
        [EffectContextAdapterKeys.OwnerPlayerId] = new HeadlessPlayerId(2),
        [EffectContextAdapterKeys.SourceEntityId] = "source-card",
        [EffectContextAdapterKeys.TriggerEntityId] = new HeadlessEntityId("trigger-battle"),
        [EffectContextAdapterKeys.TargetEntityIds] = new[] { "target-a", "target-b" },
        ["isEvolution"] = true,
        ["DigiXrosCount"] = 2,
    });

    AssertEqual(new HeadlessPlayerId(1), context.SourcePlayerId, "source player");
    AssertEqual(new HeadlessPlayerId(2), context.OwnerPlayerId, "owner player");
    AssertEqual(new HeadlessEntityId("source-card"), context.SourceEntityId, "source entity");
    AssertEqual(new HeadlessEntityId("trigger-battle"), context.TriggerEntityId, "trigger entity");
    AssertEqual(2, context.TargetEntityIds.Count, "target count");
    AssertEqual(new HeadlessEntityId("target-a"), context.TargetEntityIds[0], "target a");
    AssertEqual(true, context.GetRequiredValue<bool>("isEvolution"), "extra bool");
    AssertEqual(2, context.GetRequiredValue<int>("DigiXrosCount"), "extra int");
    AssertFalse(context.Values.ContainsKey(EffectContextAdapterKeys.SourceEntityId), "structural source excluded");
    return Task.CompletedTask;
}

Task AdapterMapsLegacyAliases()
{
    EffectContext context = CreateContext(new Dictionary<string, object?>
    {
        ["Player"] = 1,
        ["Owner"] = "2",
        ["Card"] = "BT1-001-instance",
        ["battle"] = "battle-44",
        ["Permanents"] = new[] { new HeadlessEntityId("target-permanent") },
        ["DPZero"] = true,
    });

    AssertEqual(new HeadlessPlayerId(1), context.SourcePlayerId, "legacy source player");
    AssertEqual(new HeadlessPlayerId(2), context.OwnerPlayerId, "legacy owner");
    AssertEqual(new HeadlessEntityId("BT1-001-instance"), context.SourceEntityId, "legacy card alias");
    AssertEqual(new HeadlessEntityId("battle-44"), context.TriggerEntityId, "legacy battle alias");
    AssertEqual(new HeadlessEntityId("target-permanent"), context.TargetEntityIds.Single(), "legacy target alias");
    AssertEqual(true, context.GetRequiredValue<bool>("DPZero"), "legacy extra");
    AssertFalse(context.Values.ContainsKey("Card"), "legacy structural Card excluded");
    AssertFalse(context.Values.ContainsKey("battle"), "legacy structural battle excluded");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidValues()
{
    EffectContextAdapterResult missingSourcePlayer = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourceEntityId] = "source",
    }));
    EffectContextAdapterResult missingSourceEntity = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
    }));
    EffectContextAdapterResult invalidOwner = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
        [EffectContextAdapterKeys.OwnerPlayerId] = 0,
        [EffectContextAdapterKeys.SourceEntityId] = "source",
    }));

    AssertFalse(missingSourcePlayer.IsSuccess, "missing source player failed");
    AssertEqual("missing_source_player", missingSourcePlayer.ErrorCode, "missing source player code");
    AssertFalse(missingSourceEntity.IsSuccess, "missing source entity failed");
    AssertEqual("missing_source_entity", missingSourceEntity.ErrorCode, "missing source entity code");
    AssertFalse(invalidOwner.IsSuccess, "invalid owner failed");
    AssertEqual("invalid_owner_player", invalidOwner.ErrorCode, "invalid owner code");
    AssertNull(missingSourcePlayer.Context, "failure context");
    return Task.CompletedTask;
}

Task AdapterRejectsInvalidTargets()
{
    EffectContextAdapterResult duplicateTargets = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
        [EffectContextAdapterKeys.SourceEntityId] = "source",
        [EffectContextAdapterKeys.TargetEntityIds] = new[] { "target", "target" },
    }));
    EffectContextAdapterResult invalidTarget = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
        [EffectContextAdapterKeys.SourceEntityId] = "source",
        [EffectContextAdapterKeys.TargetEntityIds] = new object?[] { "target", " " },
    }));

    AssertFalse(duplicateTargets.IsSuccess, "duplicate targets failed");
    AssertEqual("invalid_target_entities", duplicateTargets.ErrorCode, "duplicate code");
    AssertFalse(invalidTarget.IsSuccess, "invalid target failed");
    AssertEqual("invalid_target_entities", invalidTarget.ErrorCode, "invalid target code");
    return Task.CompletedTask;
}

Task AdapterRoundTripsDeterministically()
{
    EffectContext first = CreateContext(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
        [EffectContextAdapterKeys.SourceEntityId] = "source",
        [EffectContextAdapterKeys.TargetEntityIds] = new[] { "b", "a" },
        ["z"] = 3,
        ["a"] = "value",
    });
    EffectContext second = CreateContext(EffectContextAdapter.ExportValues(first));

    AssertEqual(Signature(first), Signature(second), "round trip signature");
    AssertEqual(Signature(first), Signature(CreateContext(EffectContextAdapter.ExportValues(second))), "second round trip signature");
    return Task.CompletedTask;
}

Task AdaptedContextComposesWithSkillInfo()
{
    EffectContext context = CreateContext(new Dictionary<string, object?>
    {
        [EffectContextAdapterKeys.SourcePlayerId] = 1,
        [EffectContextAdapterKeys.SourceEntityId] = "source-card",
        ["timingSource"] = "OnPlayCheck",
    });
    var definition = new CardEffectDefinition(
        new HeadlessEntityId("effect-on-play"),
        new HeadlessEntityId("source-card"),
        "On Play Test",
        "OnPlay");
    var request = new EffectRequest(definition.EffectId, context.SourcePlayerId, definition.Timing, context);
    var skill = new SkillInfo(definition, request);

    AssertEqual(context, skill.Context, "skill context");
    AssertEqual(new HeadlessEntityId("source-card"), skill.SourceEntityId, "skill source");
    AssertEqual("OnPlayCheck", skill.Context.GetRequiredValue<string>("timingSource"), "extra carried");
    return Task.CompletedTask;
}

Task SourceFilesContainNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectContextAdapter.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "adapter must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "adapter must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "adapter must not reference MonoBehaviour");
    AssertContains(text, "EffectContextAdapterKeys", "keys contract");
    AssertContains(text, "LegacyAliases", "legacy key aliases");
    AssertContains(text, "TryCreate", "explicit creation result");
    AssertContains(text, "ExportValues", "round trip export");
    return Task.CompletedTask;
}

EffectContext CreateContext(IReadOnlyDictionary<string, object?> values)
{
    EffectContextAdapterResult result = EffectContextAdapter.TryCreate(new EffectContextAdapterInput(values));
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
    }

    return result.Context!;
}

string Signature(EffectContext context)
{
    string targets = string.Join(",", context.TargetEntityIds.Select(id => id.Value));
    string extras = string.Join(
        ",",
        context.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join(
        "|",
        context.SourcePlayerId.Value,
        context.OwnerPlayerId.Value,
        context.SourceEntityId.Value,
        context.TriggerEntityId?.Value ?? "",
        targets,
        extras);
}

string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        HeadlessEntityId entityId => entityId.Value,
        HeadlessPlayerId playerId => playerId.Value.ToString(),
        IEnumerable<HeadlessEntityId> entityIds => string.Join(";", entityIds.Select(id => id.Value)),
        IEnumerable<string> strings => string.Join(";", strings),
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

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

static void AssertNull(object? value, string label)
{
    if (value is not null)
    {
        throw new InvalidOperationException($"{label}: expected null.");
    }
}
