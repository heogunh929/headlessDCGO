using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId EvolveCardId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetCardId = new("p1:main:002:P1-M02");
HeadlessEntityId EvolveDefinitionId = new("P1-M01");
HeadlessEntityId TargetDefinitionId = new("P1-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3E-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS digivolution cost references are recorded", AsIsDigivolutionCostReferencesAreRecorded),
    ("Digivolution cost resolves card record fallback and missing cost", DigivolutionCostResolvesFallbackAndMissing),
    ("Digivolution cost selects the minimum matching target requirement", DigivolutionCostSelectsMinimumMatchingRequirement),
    ("Digivolution cost ignoreLevel keeps color match while ignoring level", DigivolutionCostIgnoreLevelSkipsLevelRequirement),
    ("Digivolution cost metadata modifiers apply through shared cost pipeline", MetadataModifiersApplyThroughCostPipeline),
    ("Digivolution cost reduction permission blocks up down reduction", ReductionPermissionBlocksDigivolutionReduction),
    ("Digivolve action legal query uses target specific helper cost", DigivolveActionUsesTargetSpecificHelperCost),
    ("Digivolve action rejects wrong cost after helper resolution", DigivolveActionRejectsWrongHelperCost),
    ("Digivolution cost result values are deterministic", DigivolutionCostResultValuesAreDeterministic),
    ("G3E-002 source files stay inside digivolution cost scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3E-002")
        ?? throw new InvalidOperationException("G3E-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Costs", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "digivolution cost", "scope");
    AssertEqual("digivolution cost helper", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "digivolve cost", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3E-002_digivolution_cost_helper_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3E-001", Value(row, "blocked_until"), "prerequisite");
    AssertComplete("G3E-001_play_cost_helper_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsDigivolutionCostReferencesAreRecorded()
{
    string cardSource = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardSource.cs"));
    AssertContains(cardSource, "BaseEvoCostsFromEntity", "AS-IS base evo costs");
    AssertContains(cardSource, "public List<int> CostList", "AS-IS cost list");
    AssertContains(cardSource, "CostList(targetPermanent", "AS-IS target permanent cost list");
    AssertContains(cardSource, "baseCost = costList.Min()", "AS-IS minimum matching cost");
    AssertContains(cardSource, "GetPayingCostWithBaseCost", "AS-IS shared cost modifier pipeline");
    return Task.CompletedTask;
}

Task DigivolutionCostResolvesFallbackAndMissing()
{
    CardRecord evolve = CreateEvolveCard(evolutionCost: 2);
    CardRecord target = CreateTargetCard("Red", 3);
    DigivolutionCostResult resolved = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target));
    DigivolutionCostResult missing = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(CreateEvolveCard(evolutionCost: null), target));

    AssertTrue(resolved.IsSuccess, "fallback success");
    AssertEqual(2, resolved.Cost, "fallback cost");
    AssertEqual("cardRecordEvolutionCost", resolved.Values["selectedRequirementId"], "fallback requirement id");
    AssertFalse(missing.IsSuccess, "missing failure");
    AssertContains(missing.Reason, "no digivolution cost", "missing reason");
    return Task.CompletedTask;
}

Task DigivolutionCostSelectsMinimumMatchingRequirement()
{
    CardRecord evolve = CreateEvolveCard(
        evolutionCost: null,
        metadata: new Dictionary<string, object?>
        {
            [DigivolutionCostHelpers.DigivolutionCostsKey] = new object[]
            {
                new Dictionary<string, object?> { ["id"] = "blue-l3", ["memoryCost"] = 1, ["targetColor"] = "Blue", ["targetLevel"] = 3 },
                new Dictionary<string, object?> { ["id"] = "red-l3-high", ["memoryCost"] = 4, ["targetColor"] = "Red", ["targetLevel"] = 3 },
                new Dictionary<string, object?> { ["id"] = "red-l3-low", ["memoryCost"] = 2, ["targetColor"] = "Red", ["targetLevel"] = 3 },
            }
        });
    CardRecord target = CreateTargetCard("Red", 3);

    DigivolutionCostResult result = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(2, result.Cost, "minimum matching cost");
    AssertEqual("red-l3-low", result.Values["selectedRequirementId"], "selected id");
    AssertSequence(new[] { "red-l3-low", "red-l3-high" }, ReadStringArray(result, "matchedRequirementIds"), "matched requirement ids");
    return Task.CompletedTask;
}

Task DigivolutionCostIgnoreLevelSkipsLevelRequirement()
{
    CardRecord evolve = CreateEvolveCard(
        evolutionCost: null,
        metadata: new Dictionary<string, object?>
        {
            [DigivolutionCostHelpers.EvolutionCostsKey] = new[]
            {
                new DigivolutionCostRequirement("red-l5", memoryCost: 3, targetLevel: 5, targetColor: "Red"),
            }
        });
    CardRecord target = CreateTargetCard("Red", 3);

    DigivolutionCostResult strict = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target));
    DigivolutionCostResult ignored = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target, ignoreLevel: true));

    AssertFalse(strict.IsSuccess, "strict level mismatch");
    AssertContains(strict.Reason, "No digivolution cost requirement matched", "strict reason");
    AssertTrue(ignored.IsSuccess, "ignore level success");
    AssertEqual(3, ignored.Cost, "ignore level cost");
    return Task.CompletedTask;
}

Task MetadataModifiersApplyThroughCostPipeline()
{
    CardRecord evolve = CreateEvolveCard(
        evolutionCost: null,
        metadata: new Dictionary<string, object?>
        {
            [DigivolutionCostHelpers.DigivolutionCostsKey] = new[] { DigivolutionCostRequirement.Any("any-base", 5) },
            [DigivolutionCostHelpers.DigivolutionCostDeltaKey] = -1,
        });
    CardInstanceRecord instance = new(EvolveCardId, EvolveDefinitionId, PlayerOne, Metadata: new Dictionary<string, object?>
    {
        [DigivolutionCostHelpers.DigivolutionPayingCostDeltaKey] = -2,
    });
    CardRecord target = CreateTargetCard("Red", 3);

    DigivolutionCostResult result = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(
        evolve,
        target,
        instance,
        modifiers: DigivolutionCostHelpers.ReadModifiers(evolve, instance)));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(2, result.Cost, "final cost");
    AssertEqual(5, result.Values["baseDigivolutionCost"], "base digivolution cost");
    AssertSequence(new[] { DigivolutionCostHelpers.DigivolutionCostDeltaKey }, ReadStringArray(result, "costPipeline.appliedCostModifierIds"), "cost modifier ids");
    AssertSequence(new[] { DigivolutionCostHelpers.DigivolutionPayingCostDeltaKey }, ReadStringArray(result, "costPipeline.appliedPayingCostModifierIds"), "paying modifier ids");
    return Task.CompletedTask;
}

Task ReductionPermissionBlocksDigivolutionReduction()
{
    CardRecord evolve = CreateEvolveCard(evolutionCost: 5);
    CardRecord target = CreateTargetCard("Red", 3);
    var modifiers = new[] { PlayCostModifier.AddToCost("blocked-digivolution-reduction", -3) };

    DigivolutionCostResult result = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(
        evolve,
        target,
        modifiers: modifiers,
        canReduceCost: false));

    AssertTrue(result.IsSuccess, "success");
    AssertEqual(5, result.Cost, "cost unchanged");
    AssertSequence(new[] { "blocked-digivolution-reduction" }, ReadStringArray(result, "costPipeline.skippedCostModifierIds"), "skipped ids");
    return Task.CompletedTask;
}

async Task DigivolveActionUsesTargetSpecificHelperCost()
{
    DcgoMatch match = await CreateMatchAsync(evolutionMetadata: MatchingDigivolutionCostMetadata());
    await AdvanceToMainAsync(match, PlayerOne);

    LegalAction digivolve = SingleLegalAction(match, PlayerOne, HeadlessActionTypes.Digivolve);

    AssertEqual(EvolveCardId, ReadEntityId(digivolve.Parameters, HeadlessActionParameterKeys.CardId), "card id");
    AssertEqual(TargetCardId, ReadEntityId(digivolve.Parameters, HeadlessActionParameterKeys.TargetCardId), "target card id");
    AssertEqual(1, ReadInt(digivolve.Parameters, HeadlessActionParameterKeys.MemoryCost), "helper resolved cost");
}

async Task DigivolveActionRejectsWrongHelperCost()
{
    DcgoMatch match = await CreateMatchAsync(evolutionMetadata: MatchingDigivolutionCostMetadata());
    await AdvanceToMainAsync(match, PlayerOne);
    LegalAction wrongCost = HeadlessActionFactory.Digivolve(PlayerOne, EvolveCardId, TargetCardId, memoryCost: 2);

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(wrongCost, match.Context);

    AssertFalse(result.IsSuccess, "wrong cost success");
    AssertTrue(result.IsIllegal, "wrong cost illegal");
    AssertContains(result.Message, "does not match card evolution cost 1", "wrong cost message");
}

Task DigivolutionCostResultValuesAreDeterministic()
{
    CardRecord evolve = CreateEvolveCard(
        evolutionCost: null,
        metadata: new Dictionary<string, object?>
        {
            [DigivolutionCostHelpers.DigivolutionCostsKey] = new object[]
            {
                new Dictionary<string, object?> { ["id"] = "b", ["memoryCost"] = 3 },
                new Dictionary<string, object?> { ["id"] = "a", ["memoryCost"] = 3 },
            }
        });
    CardRecord target = CreateTargetCard("Red", 3);

    string first = Signature(DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target)));
    string second = Signature(DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(evolve, target)));

    AssertEqual(first, second, "deterministic signature");
    AssertContains(first, "selectedRequirementId=a", "stable tie break");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helperPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "DigivolutionCostHelpers.cs");
    string actionPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "DigivolveAction.cs");
    string helper = File.ReadAllText(helperPath);
    string action = File.ReadAllText(actionPath);

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("placeholder", StringComparison.OrdinalIgnoreCase), "helper must not contain placeholder");
    AssertFalse(helper.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase), "helper must not contain not implemented");
    AssertFalse(helper.Contains("DigiXros", StringComparison.Ordinal), "helper must not implement play-only DigiXros scope");
    AssertFalse(helper.Contains("Assembly", StringComparison.Ordinal), "helper must not implement play-only Assembly scope");
    AssertContains(action, "DigivolutionCostHelpers.TryResolveCost", "DigivolveAction must use helper");
    return Task.CompletedTask;
}

static IReadOnlyDictionary<string, object?> MatchingDigivolutionCostMetadata()
{
    return new Dictionary<string, object?>
    {
        [DigivolutionCostHelpers.DigivolutionCostsKey] = new object[]
        {
            new Dictionary<string, object?> { ["id"] = "red-l3", ["memoryCost"] = 1, ["targetColor"] = "Red", ["targetLevel"] = 3 },
            new Dictionary<string, object?> { ["id"] = "blue-l3", ["memoryCost"] = 4, ["targetColor"] = "Blue", ["targetLevel"] = 3 },
        }
    };
}

async Task<DcgoMatch> CreateMatchAsync(
    IReadOnlyDictionary<string, object?> evolutionMetadata,
    int initialMemory = 0,
    int minimumMemory = -5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 43);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(CreateEvolveCard(
        evolutionCost: null,
        metadata: evolutionMetadata,
        condition: "Digimon"));
    cards.Upsert(CreateTargetCard("Red", 3, playCost: 3));

    for (int index = 3; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P1-M{index:D2}"),
            $"P1-M{index:D2}",
            $"P1 filler {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
    }

    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P2-M{index:D2}"),
            $"P2-M{index:D2}",
            $"P2 filler {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
    }

    DcgoMatch match = new(context);
    HeadlessPlayerId[] players = { PlayerOne, new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            BuildDeck(PlayerOne, "P1"),
            BuildDeck(new HeadlessPlayerId(2), "P2")
        },
        firstPlayerId: PlayerOne);

    await match.InitializeAsync(MatchConfig.Create(
        players,
        randomSeed: 43,
        initialMemory: initialMemory,
        minimumMemory: minimumMemory,
        maximumMemory: 10,
        setup: setup));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(PlayerOne, TargetCardId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    return match;
}

CardRecord CreateEvolveCard(
    int? evolutionCost,
    IReadOnlyDictionary<string, object?>? metadata = null,
    string? condition = null)
{
    return new CardRecord(
        EvolveDefinitionId,
        EvolveDefinitionId.Value,
        "Evolving Digimon",
        metadata ?? new Dictionary<string, object?>(),
        CardType: "Digimon",
        EvolutionCost: evolutionCost,
        EvolutionCondition: condition);
}

CardRecord CreateTargetCard(
    string color,
    int level,
    int? playCost = null)
{
    return new CardRecord(
        TargetDefinitionId,
        TargetDefinitionId.Value,
        "Base Digimon",
        new Dictionary<string, object?>
        {
            [DigivolutionCostHelpers.CardColorKey] = color,
            [DigivolutionCostHelpers.LevelKey] = level,
        },
        CardType: "Digimon",
        PlayCost: playCost);
}

static PlayerDeckSetup BuildDeck(
    HeadlessPlayerId playerId,
    string prefix,
    int mainCount = 12,
    int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount)
            .Select(index => new HeadlessEntityId($"{prefix}-M{index:D2}"))
            .ToArray(),
        Enumerable.Range(1, digitamaCount)
            .Select(index => new HeadlessEntityId($"{prefix}-D{index:D2}"))
            .ToArray());
}

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, playerId, HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static LegalAction SingleLegalAction(
    DcgoMatch match,
    HeadlessPlayerId playerId,
    string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

static int ReadInt(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing int parameter '{key}'.");
    }

    return raw switch
    {
        int value => value,
        long value => (int)value,
        string value when int.TryParse(value, out int parsed) => parsed,
        _ => throw new InvalidOperationException($"Invalid int parameter '{key}'.")
    };
}

static HeadlessEntityId ReadEntityId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing entity id parameter '{key}'.");
    }

    return raw switch
    {
        HeadlessEntityId id => id,
        string value => new HeadlessEntityId(value),
        _ => throw new InvalidOperationException($"Invalid entity id parameter '{key}'.")
    };
}

static string[] ReadStringArray(DigivolutionCostResult result, string key)
{
    if (!result.Values.TryGetValue(key, out object? value) || value is not string[] values)
    {
        throw new InvalidOperationException($"Expected string[] value for {key}.");
    }

    return values;
}

static string Signature(DigivolutionCostResult result)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsSuccess, result.Cost, result.CanPay, result.Reason, values);
}

static string FormatValue(object? value)
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
