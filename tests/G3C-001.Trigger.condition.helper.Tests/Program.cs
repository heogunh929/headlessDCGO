using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId SourceId = new("p1-battle-source");
HeadlessEntityId DigivolvedSourceId = new("p1-battle-digivolved");
HeadlessEntityId HandSourceId = new("p1-hand-source");
HeadlessEntityId OpponentTargetId = new("p2-battle-target");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3C-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS trigger condition helper references are recorded", AsIsTriggerConditionReferencesAreRecorded),
    ("OnPlay matches non-evolution source in battle area", OnPlayMatchesNonEvolutionBattleSource),
    ("OnPlay rejects evolution context and out-of-battle source", OnPlayRejectsEvolutionAndWrongZone),
    ("OnDigivolve matches evolution context or attached source ids", OnDigivolveMatchesEvolutionContext),
    ("OnDigivolve rejects plain on-play context", OnDigivolveRejectsPlainPlayContext),
    ("WhenAttacking matches latest AttackDeclared attacker", WhenAttackingMatchesLatestAttackDeclaredAttacker),
    ("WhenAttacking rejects missing event or mismatched attacker", WhenAttackingRejectsMissingEventOrMismatch),
    ("Trigger condition evaluation is deterministic", TriggerConditionEvaluationIsDeterministic),
    ("G3C-001 source files contain no placeholder or Unity dependency", SourceFilesContainNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3C-001")
        ?? throw new InvalidOperationException("G3C-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Conditions", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "on play", "scope");
    AssertContains(Value(row, "scope"), "on digivolve", "scope");
    AssertContains(Value(row, "scope"), "when attacking", "scope");
    AssertEqual("trigger condition helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "trigger condition", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3C-001_trigger_condition_helpers_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3B-001", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "trigger condition", "completion gate");

    AssertComplete("G3B-001_hashtable_replacement_adapter_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsTriggerConditionReferencesAreRecorded()
{
    string settings = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "HashtableSetting.cs"));
    string effect = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ICardEffect.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));

    AssertContains(settings, "OnPlayCheckHashtableOfCard", "AS-IS OnPlay card helper");
    AssertContains(settings, "OnPlayCheckHashtableOfPermanent", "AS-IS OnPlay permanent helper");
    AssertContains(settings, "WhenDigivolvingCheckHashtableOfCard", "AS-IS WhenDigivolving card helper");
    AssertContains(settings, "WhenDigivolutionCheckHashtableOfPermanent", "AS-IS WhenDigivolution permanent helper");
    AssertContains(settings, "OnAttackCheckHashtableOfPermanent", "AS-IS OnAttack permanent helper");
    AssertContains(settings, "\"isEvolution\"", "AS-IS evolution flag");
    AssertContains(settings, "\"AttackingPermanent\"", "AS-IS attack source key");
    AssertContains(effect, "CanTrigger(hashtable)", "AS-IS CanTrigger payload");
    AssertContains(autoProcessing, "cardEffect.CanTrigger(hashtable)", "AS-IS AutoProcessing trigger check");
    return Task.CompletedTask;
}

Task OnPlayMatchesNonEvolutionBattleSource()
{
    MatchState state = CreateState();
    EffectContext context = CreateContext(SourceId, values: new Dictionary<string, object?>
    {
        [TriggerConditionHelpers.IsEvolutionKey] = false,
    });

    TriggerConditionResult direct = TriggerConditionHelpers.IsOnPlay(state, context);
    TriggerConditionResult evaluated = TriggerConditionHelpers.Evaluate(
        new TriggerConditionRequest(state, context, TriggerConditionKind.OnPlay));

    AssertTrue(direct.IsMatch, "direct on play");
    AssertTrue(evaluated.IsMatch, "evaluated on play");
    AssertEqual(TriggerConditionKind.OnPlay, direct.Condition, "condition");
    AssertEqual(SourceId.Value, direct.Values[EffectContextAdapterKeys.SourceEntityId], "source value");
    AssertEqual(false, direct.Values["sourceHasEvolutionSources"], "no sources");
    return Task.CompletedTask;
}

Task OnPlayRejectsEvolutionAndWrongZone()
{
    MatchState state = CreateState();
    TriggerConditionResult evolution = TriggerConditionHelpers.IsOnPlay(
        state,
        CreateContext(DigivolvedSourceId, values: new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.IsEvolutionKey] = true,
        }));
    TriggerConditionResult wrongZone = TriggerConditionHelpers.IsOnPlay(state, CreateContext(HandSourceId));

    AssertFalse(evolution.IsMatch, "evolution is not on play");
    AssertContains(evolution.Reason, "evolution", "evolution reason");
    AssertFalse(wrongZone.IsMatch, "hand source is not on play");
    AssertContains(wrongZone.Reason, "battle area", "zone reason");
    return Task.CompletedTask;
}

Task OnDigivolveMatchesEvolutionContext()
{
    MatchState state = CreateState();
    TriggerConditionResult explicitFlag = TriggerConditionHelpers.IsOnDigivolve(
        state,
        CreateContext(SourceId, values: new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.IsEvolutionKey] = true,
        }));
    TriggerConditionResult attachedSource = TriggerConditionHelpers.IsOnDigivolve(
        state,
        CreateContext(DigivolvedSourceId));

    AssertTrue(explicitFlag.IsMatch, "explicit evolution flag");
    AssertTrue(attachedSource.IsMatch, "attached source ids");
    AssertEqual(true, attachedSource.Values["sourceHasEvolutionSources"], "has sources value");
    return Task.CompletedTask;
}

Task OnDigivolveRejectsPlainPlayContext()
{
    TriggerConditionResult result = TriggerConditionHelpers.IsOnDigivolve(CreateState(), CreateContext(SourceId));

    AssertFalse(result.IsMatch, "plain context");
    AssertContains(result.Reason, "evolution", "reason");
    return Task.CompletedTask;
}

Task WhenAttackingMatchesLatestAttackDeclaredAttacker()
{
    MatchState state = CreateState(events: new[]
    {
        new GameEvent(1, GameEventType.AttackDeclared, "older attack", new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.AttackerIdKey] = "not-source",
        }),
        new GameEvent(2, GameEventType.AttackDeclared, "latest attack", new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.AttackerIdKey] = SourceId.Value,
            [TriggerConditionHelpers.AttackTargetIdKey] = OpponentTargetId.Value,
        }),
    });
    TriggerConditionResult result = TriggerConditionHelpers.IsWhenAttacking(state, CreateContext(SourceId));

    AssertTrue(result.IsMatch, "latest attack source");
    AssertEqual(2L, result.Values["eventSequence"], "latest event sequence");
    AssertEqual(SourceId.Value, result.Values[TriggerConditionHelpers.AttackerIdKey], "attacker value");
    return Task.CompletedTask;
}

Task WhenAttackingRejectsMissingEventOrMismatch()
{
    TriggerConditionResult missing = TriggerConditionHelpers.IsWhenAttacking(CreateState(), CreateContext(SourceId));
    MatchState mismatchState = CreateState(events: new[]
    {
        new GameEvent(1, GameEventType.AttackDeclared, "other attack", new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.AttackerIdKey] = DigivolvedSourceId.Value,
        }),
    });
    TriggerConditionResult mismatch = TriggerConditionHelpers.IsWhenAttacking(mismatchState, CreateContext(SourceId));

    AssertFalse(missing.IsMatch, "missing event");
    AssertContains(missing.Reason, "AttackDeclared", "missing event reason");
    AssertFalse(mismatch.IsMatch, "mismatch");
    AssertContains(mismatch.Reason, "attacking card", "mismatch reason");
    return Task.CompletedTask;
}

Task TriggerConditionEvaluationIsDeterministic()
{
    MatchState state = CreateState(events: new[]
    {
        new GameEvent(4, GameEventType.AttackDeclared, "attack", new Dictionary<string, object?>
        {
            [TriggerConditionHelpers.AttackerIdKey] = SourceId.Value,
        }),
    });
    EffectContext context = CreateContext(SourceId);

    string first = Signature(TriggerConditionHelpers.Evaluate(
        new TriggerConditionRequest(state, context, TriggerConditionKind.WhenAttacking)));
    string second = Signature(TriggerConditionHelpers.Evaluate(
        new TriggerConditionRequest(state, context, TriggerConditionKind.WhenAttacking)));

    AssertEqual(first, second, "signature");
    return Task.CompletedTask;
}

Task SourceFilesContainNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "TriggerConditionHelpers.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "helper must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "helper must not reference MonoBehaviour");
    AssertFalse(text.Contains("Hashtable", StringComparison.Ordinal), "helper must not reference Hashtable");
    AssertContains(text, "IsOnPlay", "on play helper");
    AssertContains(text, "IsOnDigivolve", "on digivolve helper");
    AssertContains(text, "IsWhenAttacking", "when attacking helper");
    AssertContains(text, "TriggerConditionResult", "result model");
    return Task.CompletedTask;
}

MatchState CreateState(IReadOnlyList<GameEvent>? events = null)
{
    var playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.BattleArea, new[] { SourceId, DigivolvedSourceId })
        .WithZone(ChoiceZone.Hand, new[] { HandSourceId });
    var playerTwo = new PlayerState(PlayerTwo)
        .WithZone(ChoiceZone.BattleArea, new[] { OpponentTargetId });

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [SourceId] = new CardInstanceState(SourceId, new HeadlessEntityId("BT1-001"), PlayerOne),
        [DigivolvedSourceId] = new CardInstanceState(
            DigivolvedSourceId,
            new HeadlessEntityId("BT1-002"),
            PlayerOne,
            SourceIds: new[] { new HeadlessEntityId("evolution-source") }),
        [HandSourceId] = new CardInstanceState(HandSourceId, new HeadlessEntityId("BT1-003"), PlayerOne),
        [OpponentTargetId] = new CardInstanceState(OpponentTargetId, new HeadlessEntityId("BT1-004"), PlayerTwo),
    };

    return new MatchState(
        new[] { playerOne, playerTwo },
        cards,
        Version: events?.Count ?? 0,
        Events: events ?? Array.Empty<GameEvent>());
}

EffectContext CreateContext(
    HeadlessEntityId sourceId,
    IReadOnlyDictionary<string, object?>? values = null)
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        sourceId,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        values);
}

string Signature(TriggerConditionResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.Condition, result.IsMatch, result.Reason, values);
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
