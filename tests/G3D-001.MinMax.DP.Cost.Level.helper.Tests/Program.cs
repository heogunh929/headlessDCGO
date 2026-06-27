using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId LowDpId = new("p1-low-dp");
HeadlessEntityId MidDpId = new("p1-mid-dp");
HeadlessEntityId HighDpId = new("p1-high-dp");
HeadlessEntityId TieLowDpId = new("p1-tie-low-dp");
HeadlessEntityId TamerId = new("p1-tamer");
HeadlessEntityId HandId = new("p1-hand");
HeadlessEntityId OpponentHighId = new("p2-high-dp");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3D-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS min max DP cost level helper references are recorded", AsIsMinMaxReferencesAreRecorded),
    ("Min max DP uses owner battle area Digimon values", MinMaxDpUsesOwnerBattleAreaDigimonValues),
    ("Min max cost supports Digimon and Tamer candidates", MinMaxCostSupportsDigimonAndTamerCandidates),
    ("Min max level uses owner battle area Digimon values", MinMaxLevelUsesOwnerBattleAreaDigimonValues),
    ("Min max helpers reject missing or invalid sources without throwing", InvalidSourcesReturnNoMatch),
    ("Modifiers override card definition metric values", ModifiersOverrideDefinitions),
    ("Min max result values are deterministic", ResultValuesAreDeterministic),
    ("G3D-001 source stays inside DP cost level scope", SourceFilesStayInsideGoalScope),
    ("Min max helper does not mutate match state", HelperDoesNotMutateMatchState),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3D-001")
        ?? throw new InvalidOperationException("G3D-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Requirements", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "DP cost level", "scope");
    AssertEqual("min max helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "min max", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3D-001_minmax_dp_cost_level_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3C-002", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "minmax", "completion gate");

    AssertComplete("G3C-002_can_use_effect_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsMinMaxReferencesAreRecorded()
{
    string minDp = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "DP", "IsMinDP.cs"));
    string maxDp = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "DP", "IsMaxDP.cs"));
    string minCost = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "Cost", "IsMinCost.cs"));
    string maxCost = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "Cost", "IsMaxCost.cs"));
    string minLevel = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "Level", "IsMinLevel.cs"));
    string maxLevel = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMax_DP_Cost_Level", "Level", "IsMaxLevel.cs"));

    AssertContains(minDp, "IsMinDP", "AS-IS min DP");
    AssertContains(maxDp, "IsMaxDP", "AS-IS max DP");
    AssertContains(minDp, "GetBattleAreaDigimons", "AS-IS DP owner battle area");
    AssertContains(maxDp, "DPs.Max()", "AS-IS max DP comparison");
    AssertContains(minCost, "IsMinCost", "AS-IS min cost");
    AssertContains(maxCost, "IsMaxCost", "AS-IS max cost");
    AssertContains(maxCost, "IsDigimonOnly", "AS-IS cost digimon only flag");
    AssertContains(maxCost, "GetCostItself", "AS-IS cost itself");
    AssertContains(minLevel, "IsMinLevel", "AS-IS min level");
    AssertContains(maxLevel, "IsMaxLevel", "AS-IS max level");
    AssertContains(maxLevel, "Levels.Max()", "AS-IS max level comparison");
    return Task.CompletedTask;
}

Task MinMaxDpUsesOwnerBattleAreaDigimonValues()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    MinMaxRequirementResult low = MinMaxRequirementHelpers.IsMinDP(state, PlayerOne, LowDpId, definitions);
    MinMaxRequirementResult tie = MinMaxRequirementHelpers.IsMinDP(state, PlayerOne, TieLowDpId, definitions);
    MinMaxRequirementResult high = MinMaxRequirementHelpers.IsMaxDP(state, PlayerOne, HighDpId, definitions);
    MinMaxRequirementResult mid = MinMaxRequirementHelpers.IsMaxDP(state, PlayerOne, MidDpId, definitions);

    AssertTrue(low.IsMatch, "low dp is min");
    AssertTrue(tie.IsMatch, "tie low dp is also min");
    AssertTrue(high.IsMatch, "high dp is max");
    AssertFalse(mid.IsMatch, "mid dp is not max");
    AssertEqual(3000, low.Values["boundaryValue"], "min boundary");
    AssertEqual(12000, high.Values["boundaryValue"], "max boundary");
    if (low.Values["candidateIds"] is not string[] candidateIds)
    {
        throw new InvalidOperationException("candidate ids were not recorded as a string array.");
    }

    AssertSequence(
        new[] { HighDpId.Value, LowDpId.Value, MidDpId.Value, TieLowDpId.Value },
        candidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        "candidate ids");
    return Task.CompletedTask;
}

Task MinMaxCostSupportsDigimonAndTamerCandidates()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    MinMaxRequirementResult tamerMin = MinMaxRequirementHelpers.IsMinCost(state, PlayerOne, TamerId, definitions);
    MinMaxRequirementResult highMax = MinMaxRequirementHelpers.IsMaxCost(state, PlayerOne, HighDpId, definitions);
    MinMaxRequirementResult tamerExcluded = MinMaxRequirementHelpers.IsMinCost(
        state,
        PlayerOne,
        TamerId,
        definitions,
        includeTamersForCost: false);

    AssertTrue(tamerMin.IsMatch, "tamer can be min cost when included");
    AssertEqual(1, tamerMin.Values["boundaryValue"], "min cost boundary");
    AssertTrue(highMax.IsMatch, "high digimon max cost");
    AssertEqual(8, highMax.Values["boundaryValue"], "max cost boundary");
    AssertFalse(tamerExcluded.IsMatch, "tamer excluded by digimon only flag");
    AssertContains(tamerExcluded.Reason, "not valid", "tamer excluded reason");
    return Task.CompletedTask;
}

Task MinMaxLevelUsesOwnerBattleAreaDigimonValues()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    MinMaxRequirementResult low = MinMaxRequirementHelpers.IsMinLevel(state, PlayerOne, LowDpId, definitions);
    MinMaxRequirementResult high = MinMaxRequirementHelpers.IsMaxLevel(state, PlayerOne, HighDpId, definitions);
    MinMaxRequirementResult mid = MinMaxRequirementHelpers.IsMinLevel(state, PlayerOne, MidDpId, definitions);

    AssertTrue(low.IsMatch, "low level");
    AssertTrue(high.IsMatch, "high level");
    AssertFalse(mid.IsMatch, "mid level");
    AssertEqual(3, low.Values["boundaryValue"], "min level boundary");
    AssertEqual(6, high.Values["boundaryValue"], "max level boundary");
    return Task.CompletedTask;
}

Task InvalidSourcesReturnNoMatch()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();
    var withoutLowDefinition = definitions
        .Where(pair => pair.Key != new HeadlessEntityId("BT1-LOW"))
        .ToDictionary(pair => pair.Key, pair => pair.Value);

    MinMaxRequirementResult missingSource = MinMaxRequirementHelpers.IsMinDP(
        state,
        PlayerOne,
        new HeadlessEntityId("missing"),
        definitions);
    MinMaxRequirementResult handSource = MinMaxRequirementHelpers.IsMinDP(state, PlayerOne, HandId, definitions);
    MinMaxRequirementResult missingDefinition = MinMaxRequirementHelpers.IsMinDP(
        state,
        PlayerOne,
        LowDpId,
        withoutLowDefinition);

    AssertFalse(missingSource.IsMatch, "missing source");
    AssertContains(missingSource.Reason, "not found", "missing source reason");
    AssertFalse(handSource.IsMatch, "hand source");
    AssertContains(handSource.Reason, "battle area", "hand source reason");
    AssertFalse(missingDefinition.IsMatch, "missing definition");
    AssertContains(missingDefinition.Reason, "definition", "missing definition reason");
    return Task.CompletedTask;
}

Task ModifiersOverrideDefinitions()
{
    MatchState state = CreateState(new Dictionary<HeadlessEntityId, IReadOnlyDictionary<string, object?>>
    {
        [LowDpId] = new Dictionary<string, object?>
        {
            [MinMaxRequirementHelpers.DpKey] = 15000,
            [MinMaxRequirementHelpers.LevelKey] = 7,
            [MinMaxRequirementHelpers.PlayCostKey] = 12,
        },
    });
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    MinMaxRequirementResult maxDp = MinMaxRequirementHelpers.IsMaxDP(state, PlayerOne, LowDpId, definitions);
    MinMaxRequirementResult maxLevel = MinMaxRequirementHelpers.IsMaxLevel(state, PlayerOne, LowDpId, definitions);
    MinMaxRequirementResult maxCost = MinMaxRequirementHelpers.IsMaxCost(state, PlayerOne, LowDpId, definitions);

    AssertTrue(maxDp.IsMatch, "modifier dp");
    AssertEqual(15000, maxDp.Values["sourceValue"], "modifier dp source");
    AssertTrue(maxLevel.IsMatch, "modifier level");
    AssertEqual(7, maxLevel.Values["sourceValue"], "modifier level source");
    AssertTrue(maxCost.IsMatch, "modifier cost");
    AssertEqual(12, maxCost.Values["sourceValue"], "modifier cost source");
    return Task.CompletedTask;
}

Task ResultValuesAreDeterministic()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();

    string first = Signature(MinMaxRequirementHelpers.IsMaxDP(state, PlayerOne, HighDpId, definitions));
    string second = Signature(MinMaxRequirementHelpers.IsMaxDP(state, PlayerOne, HighDpId, definitions));

    AssertEqual(first, second, "signature");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "MinMaxRequirementHelpers.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "helper must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "helper must not reference MonoBehaviour");
    AssertFalse(text.Contains("Hashtable", StringComparison.Ordinal), "helper must not reference Hashtable");
    AssertFalse(text.Contains("ColorRequirement", StringComparison.Ordinal), "helper must not include next color requirement scope");
    AssertFalse(text.Contains("TraitRequirement", StringComparison.Ordinal), "helper must not include next trait requirement scope");
    AssertContains(text, "IsMinDP", "DP API");
    AssertContains(text, "IsMaxCost", "cost API");
    AssertContains(text, "IsMinLevel", "level API");
    return Task.CompletedTask;
}

Task HelperDoesNotMutateMatchState()
{
    MatchState state = CreateState();
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions = CreateDefinitions();
    string before = state.ComputeFingerprint();

    _ = MinMaxRequirementHelpers.IsMinDP(state, PlayerOne, LowDpId, definitions);
    _ = MinMaxRequirementHelpers.IsMaxCost(state, PlayerOne, HighDpId, definitions);
    _ = MinMaxRequirementHelpers.IsMinLevel(state, PlayerOne, LowDpId, definitions);

    AssertEqual(before, state.ComputeFingerprint(), "state fingerprint");
    return Task.CompletedTask;
}

MatchState CreateState(IReadOnlyDictionary<HeadlessEntityId, IReadOnlyDictionary<string, object?>>? modifiers = null)
{
    var playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.BattleArea, new[] { LowDpId, MidDpId, HighDpId, TieLowDpId, TamerId })
        .WithZone(ChoiceZone.Hand, new[] { HandId });
    var playerTwo = new PlayerState(PlayerTwo)
        .WithZone(ChoiceZone.BattleArea, new[] { OpponentHighId });

    HeadlessEntityId Definition(string suffix) => new($"BT1-{suffix}");
    IReadOnlyDictionary<string, object?> Mods(HeadlessEntityId id)
    {
        return modifiers is not null && modifiers.TryGetValue(id, out IReadOnlyDictionary<string, object?>? found)
            ? found
            : new Dictionary<string, object?>();
    }

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [LowDpId] = new CardInstanceState(LowDpId, Definition("LOW"), PlayerOne, Modifiers: Mods(LowDpId)),
        [MidDpId] = new CardInstanceState(MidDpId, Definition("MID"), PlayerOne, Modifiers: Mods(MidDpId)),
        [HighDpId] = new CardInstanceState(HighDpId, Definition("HIGH"), PlayerOne, Modifiers: Mods(HighDpId)),
        [TieLowDpId] = new CardInstanceState(TieLowDpId, Definition("TIE"), PlayerOne, Modifiers: Mods(TieLowDpId)),
        [TamerId] = new CardInstanceState(TamerId, Definition("TAMER"), PlayerOne, Modifiers: Mods(TamerId)),
        [HandId] = new CardInstanceState(HandId, Definition("HAND"), PlayerOne, Modifiers: Mods(HandId)),
        [OpponentHighId] = new CardInstanceState(OpponentHighId, Definition("OPP"), PlayerTwo, Modifiers: Mods(OpponentHighId)),
    };

    return new MatchState(new[] { playerOne, playerTwo }, cards);
}

IReadOnlyDictionary<HeadlessEntityId, CardRecord> CreateDefinitions()
{
    CardRecord Digimon(string suffix, int dp, int level, int cost)
    {
        HeadlessEntityId id = new($"BT1-{suffix}");
        return new CardRecord(
            id,
            id.Value,
            $"Digimon {suffix}",
            new Dictionary<string, object?>
            {
                [MinMaxRequirementHelpers.DpKey] = dp,
                [MinMaxRequirementHelpers.LevelKey] = level,
            },
            CardType: "Digimon",
            PlayCost: cost);
    }

    CardRecord Tamer(string suffix, int cost)
    {
        HeadlessEntityId id = new($"BT1-{suffix}");
        return new CardRecord(
            id,
            id.Value,
            $"Tamer {suffix}",
            new Dictionary<string, object?>(),
            CardType: "Tamer",
            PlayCost: cost);
    }

    var records = new[]
    {
        Digimon("LOW", 3000, 3, 2),
        Digimon("MID", 5000, 4, 4),
        Digimon("HIGH", 12000, 6, 8),
        Digimon("TIE", 3000, 3, 3),
        Tamer("TAMER", 1),
        Digimon("HAND", 1000, 2, 1),
        Digimon("OPP", 20000, 7, 12),
    };

    return records.ToDictionary(record => record.Id, record => record);
}

string Signature(MinMaxRequirementResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.Metric, result.Mode, result.IsMatch, result.Reason, values);
}

string FormatValue(object? value)
{
    return value switch
    {
        null => "null",
        string[] strings => string.Join(";", strings),
        int[] ints => string.Join(";", ints),
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

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
