using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId SourceId = new("p1-battle-source");
HeadlessEntityId DisabledSourceId = new("p1-disabled-source");
HeadlessEntityId HandSourceId = new("p1-hand-source");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3C-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS CanUseEffects helper references are recorded", AsIsCanUseReferencesAreRecorded),
    ("CanUse returns true when trigger and activation conditions pass", CanUseReturnsTrueWhenAllConditionsPass),
    ("CanUse stops when trigger condition helper fails", CanUseStopsWhenTriggerFails),
    ("CanActivate rejects disabled or non-activatable source", CanActivateRejectsDisabledSource),
    ("CanTrigger rejects max count per turn", CanTriggerRejectsMaxCount),
    ("CanUse evaluates typed required and forbidden conditions", CanUseEvaluatesTypedConditions),
    ("CanUse preserves deterministic result values", CanUseIsDeterministic),
    ("CanUse helper composes without reimplementing G3C-001 internals", CanUseComposesWithTriggerHelpers),
    ("G3C-002 source files contain no placeholder or Unity dependency", SourceFilesContainNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3C-002")
        ?? throw new InvalidOperationException("G3C-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Conditions", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "CanUseEffects", "scope");
    AssertEqual("can use helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "can use effect", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3C-002_can_use_effect_helpers_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3C-001", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "CanUseEffects", "completion gate");

    AssertComplete("G3C-001_trigger_condition_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsCanUseReferencesAreRecorded()
{
    string effect = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ICardEffect.cs"));
    string enterField = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "PermanentEnterField", "PermanentEnterField.cs"));
    string onPlay = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "PermanentEnterField", "OnPlay.cs"));
    string onAttack = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "OnAttack.cs"));
    string wouldPlay = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "CanUseEffects", "WhenPermanentWouldPlay.cs"));

    AssertContains(effect, "public bool CanTrigger(Hashtable hashtable)", "AS-IS CanTrigger");
    AssertContains(effect, "public bool CanActivate(Hashtable hashtable)", "AS-IS CanActivate");
    AssertContains(effect, "public bool CanUse(Hashtable hashtable)", "AS-IS CanUse");
    AssertContains(effect, "if (!CanTrigger(hashtable) || !CanActivate(hashtable))", "AS-IS CanUse order");
    AssertContains(enterField, "CanTriggerOnPermanentEnterField", "AS-IS enter field helper");
    AssertContains(onPlay, "CanTriggerOnPlay", "AS-IS OnPlay helper");
    AssertContains(onAttack, "CanTriggerOnPermanentAttack", "AS-IS OnAttack helper");
    AssertContains(wouldPlay, "CanTriggerWhenPermanentWouldPlay", "AS-IS would play helper");
    return Task.CompletedTask;
}

Task CanUseReturnsTrueWhenAllConditionsPass()
{
    CanUseEffectRequest request = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        contextValues: new Dictionary<string, object?>
        {
            ["hasTarget"] = true,
            ["paidCost"] = true,
        },
        triggerConditions: new[] { CanUseEffectCondition.RequireFlag("hasTarget") },
        activationConditions: new[] { CanUseEffectCondition.RequireFlag("paidCost") });

    CanUseEffectResult trigger = CanUseEffectHelpers.CanTrigger(request);
    CanUseEffectResult activate = CanUseEffectHelpers.CanActivate(request);
    CanUseEffectResult use = CanUseEffectHelpers.CanUse(request);

    AssertTrue(trigger.CanUse, "trigger");
    AssertTrue(activate.CanUse, "activate");
    AssertTrue(use.CanUse, "use");
    AssertEqual(CanUseEffectEvaluationKind.Use, use.Kind, "use kind");
    AssertEqual(SourceId.Value, use.Values[EffectContextAdapterKeys.SourceEntityId], "source");
    return Task.CompletedTask;
}

Task CanUseStopsWhenTriggerFails()
{
    CanUseEffectRequest request = CreateRequest(HandSourceId, TriggerConditionKind.OnPlay);
    CanUseEffectResult result = CanUseEffectHelpers.CanUse(request);

    AssertFalse(result.CanUse, "can use");
    AssertEqual(CanUseEffectEvaluationKind.Use, result.Kind, "kind");
    AssertContains(result.Reason, "CanTrigger failed", "reason");
    AssertContains(result.Reason, "battle area", "trigger reason");
    return Task.CompletedTask;
}

Task CanActivateRejectsDisabledSource()
{
    CanUseEffectRequest disabled = CreateRequest(DisabledSourceId, TriggerConditionKind.OnPlay);
    CanUseEffectRequest cannotActivate = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        sourceModifiers: new Dictionary<string, object?> { [CanUseEffectHelpers.SourceCanActivateKey] = false });

    CanUseEffectResult disabledResult = CanUseEffectHelpers.CanActivate(disabled);
    CanUseEffectResult cannotActivateResult = CanUseEffectHelpers.CanActivate(cannotActivate);

    AssertFalse(disabledResult.CanUse, "disabled source");
    AssertContains(disabledResult.Reason, "disabled", "disabled reason");
    AssertFalse(cannotActivateResult.CanUse, "cannot activate");
    AssertContains(cannotActivateResult.Reason, "cannot activate", "cannot activate reason");
    return Task.CompletedTask;
}

Task CanTriggerRejectsMaxCount()
{
    CanUseEffectRequest request = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        maxCountPerTurn: 2,
        contextValues: new Dictionary<string, object?>
        {
            [CanUseEffectHelpers.UseCountThisTurnKey] = 2,
        });

    CanUseEffectResult result = CanUseEffectHelpers.CanTrigger(request);

    AssertFalse(result.CanUse, "max count");
    AssertContains(result.Reason, "max count", "reason");
    AssertEqual(2, result.Values[CanUseEffectHelpers.UseCountThisTurnKey], "use count");
    AssertEqual(2, result.Values["maxCountPerTurn"], "max count value");
    return Task.CompletedTask;
}

Task CanUseEvaluatesTypedConditions()
{
    CanUseEffectRequest missingRequired = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        triggerConditions: new[] { CanUseEffectCondition.RequireFlag("requiresCard") });
    CanUseEffectRequest forbiddenPresent = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        contextValues: new Dictionary<string, object?> { ["cannotUse"] = true },
        activationConditions: new[] { CanUseEffectCondition.ForbidFlag("cannotUse") });
    CanUseEffectRequest expectedString = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        contextValues: new Dictionary<string, object?> { ["zone"] = "BattleArea" },
        activationConditions: new[] { new CanUseEffectCondition("zone", "BattleArea") });

    CanUseEffectResult missing = CanUseEffectHelpers.CanTrigger(missingRequired);
    CanUseEffectResult forbidden = CanUseEffectHelpers.CanActivate(forbiddenPresent);
    CanUseEffectResult matched = CanUseEffectHelpers.CanActivate(expectedString);

    AssertFalse(missing.CanUse, "missing required");
    AssertContains(missing.Reason, "requiresCard", "missing reason");
    AssertFalse(forbidden.CanUse, "forbidden present");
    AssertContains(forbidden.Reason, "Forbidden", "forbidden reason");
    AssertTrue(matched.CanUse, "expected value");
    return Task.CompletedTask;
}

Task CanUseIsDeterministic()
{
    CanUseEffectRequest request = CreateRequest(
        SourceId,
        TriggerConditionKind.OnPlay,
        contextValues: new Dictionary<string, object?>
        {
            ["paidCost"] = true,
            ["hasTarget"] = true,
        },
        triggerConditions: new[] { CanUseEffectCondition.RequireFlag("hasTarget") },
        activationConditions: new[] { CanUseEffectCondition.RequireFlag("paidCost") });

    string first = Signature(CanUseEffectHelpers.CanUse(request));
    string second = Signature(CanUseEffectHelpers.CanUse(request));

    AssertEqual(first, second, "signature");
    return Task.CompletedTask;
}

Task CanUseComposesWithTriggerHelpers()
{
    string canUseText = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "CanUseEffectHelpers.cs"));
    CanUseEffectRequest attackRequest = CreateRequest(SourceId, TriggerConditionKind.WhenAttacking, events: new[]
    {
        new HeadlessDCGO.Engine.Headless.Runtime.GameEvent(
            1,
            HeadlessDCGO.Engine.Headless.Runtime.GameEventType.AttackDeclared,
            "attack",
            new Dictionary<string, object?>
            {
                [TriggerConditionHelpers.AttackerIdKey] = SourceId.Value,
            }),
    });

    CanUseEffectResult result = CanUseEffectHelpers.CanUse(attackRequest);

    AssertTrue(result.CanUse, "when attacking can use");
    AssertContains(canUseText, "TriggerConditionHelpers.Evaluate", "composition");
    AssertFalse(canUseText.Contains("IsOnPlay("), "CanUse helper must not reimplement OnPlay internals");
    AssertFalse(canUseText.Contains("IsOnDigivolve("), "CanUse helper must not reimplement OnDigivolve internals");
    AssertFalse(canUseText.Contains("IsWhenAttacking("), "CanUse helper must not reimplement WhenAttacking internals");
    return Task.CompletedTask;
}

Task SourceFilesContainNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "CanUseEffectHelpers.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "helper must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "helper must not reference MonoBehaviour");
    AssertFalse(text.Contains("Hashtable", StringComparison.Ordinal), "helper must not reference Hashtable");
    AssertContains(text, "CanTrigger", "CanTrigger API");
    AssertContains(text, "CanActivate", "CanActivate API");
    AssertContains(text, "CanUse", "CanUse API");
    AssertContains(text, "CanUseEffectCondition", "typed condition model");
    return Task.CompletedTask;
}

CanUseEffectRequest CreateRequest(
    HeadlessEntityId sourceId,
    TriggerConditionKind? triggerCondition,
    int? maxCountPerTurn = 3,
    IReadOnlyDictionary<string, object?>? contextValues = null,
    IReadOnlyList<CanUseEffectCondition>? triggerConditions = null,
    IReadOnlyList<CanUseEffectCondition>? activationConditions = null,
    IReadOnlyDictionary<string, object?>? sourceModifiers = null,
    IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.GameEvent>? events = null)
{
    MatchState state = CreateState(sourceModifiers, events);
    EffectContext context = new(
        PlayerOne,
        PlayerOne,
        sourceId,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        contextValues);
    var definition = new CardEffectDefinition(
        new HeadlessEntityId($"effect-{sourceId.Value}"),
        sourceId,
        $"Effect {sourceId.Value}",
        triggerCondition?.ToString() ?? "Manual",
        maxCountPerTurn: maxCountPerTurn);
    var request = new EffectRequest(definition.EffectId, PlayerOne, definition.Timing, context);
    var skill = new SkillInfo(definition, request);

    return new CanUseEffectRequest(
        state,
        skill,
        triggerCondition,
        triggerConditions,
        activationConditions);
}

MatchState CreateState(
    IReadOnlyDictionary<string, object?>? sourceModifiers = null,
    IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.GameEvent>? events = null)
{
    var playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.BattleArea, new[] { SourceId, DisabledSourceId })
        .WithZone(ChoiceZone.Hand, new[] { HandSourceId });
    var playerTwo = new PlayerState(PlayerTwo);

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [SourceId] = new CardInstanceState(
            SourceId,
            new HeadlessEntityId("BT1-001"),
            PlayerOne,
            Modifiers: sourceModifiers ?? new Dictionary<string, object?> { [CanUseEffectHelpers.SourceIsTopKey] = true }),
        [DisabledSourceId] = new CardInstanceState(
            DisabledSourceId,
            new HeadlessEntityId("BT1-002"),
            PlayerOne,
            Flags: new Dictionary<string, bool> { [CanUseEffectHelpers.SourceDisabledKey] = true }),
        [HandSourceId] = new CardInstanceState(HandSourceId, new HeadlessEntityId("BT1-003"), PlayerOne),
    };

    return new MatchState(
        new[] { playerOne, playerTwo },
        cards,
        Version: events?.Count ?? 0,
        Events: events ?? Array.Empty<HeadlessDCGO.Engine.Headless.Runtime.GameEvent>());
}

string Signature(CanUseEffectResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.Kind, result.CanUse, result.Reason, values);
}

string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        IEnumerable<string> strings => string.Join(";", strings),
        IEnumerable<HeadlessEntityId> entityIds => string.Join(";", entityIds.Select(id => id.Value)),
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
