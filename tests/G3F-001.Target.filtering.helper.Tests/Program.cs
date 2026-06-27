using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId SourceCard = new("p1-source");
HeadlessEntityId OwnHandCard = new("p1-hand");
HeadlessEntityId OpponentBattleCard = new("p2-battle");
HeadlessEntityId OpponentUnsuspendedCard = new("p2-unsuspended");
HeadlessEntityId OwnHiddenSecurity = new("p1-security");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3F-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS target filtering references are recorded", AsIsTargetFilteringReferencesAreRecorded),
    ("Card targets filter by owner zone visibility and requirements", CardTargetsFilterByOwnerZoneVisibilityAndRequirements),
    ("Permanent targets default to battle and breeding zones", PermanentTargetsDefaultToBattleAndBreedingZones),
    ("Player targets filter source player and opponent", PlayerTargetsFilterSourceAndOpponent),
    ("Visibility scope controls private card access", VisibilityScopeControlsPrivateCardAccess),
    ("Suspension and flag filters are applied to card candidates", SuspensionAndFlagFiltersApply),
    ("Invalid source or viewer returns failure without throwing", InvalidSourceOrViewerReturnsFailure),
    ("Target filter result values are deterministic", TargetFilterResultValuesAreDeterministic),
    ("G3F-001 source files stay inside target filtering scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3F-001")
        ?? throw new InvalidOperationException("G3F-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Targeting", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "target filtering", "scope");
    AssertEqual("target filter helpers", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "target filtering", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3F-001_target_filtering_helpers_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3D-002", Value(row, "blocked_until"), "prerequisite");
    AssertComplete("G3D-002_name_color_trait_requirements_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsTargetFilteringReferencesAreRecorded()
{
    string selectCard = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"));
    string player = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Player.cs"));
    AssertContains(selectCard, "RootCardList()", "AS-IS root list");
    AssertContains(selectCard, "CanSelectCard", "AS-IS select predicate");
    AssertContains(selectCard, "_canTargetCondition", "AS-IS target condition");
    AssertContains(selectCard, "_allowFaceDown", "AS-IS face-down access");
    AssertContains(selectCard, "SetTargetCardAndIndicies", "AS-IS selected payload");
    AssertContains(player, "GetFieldPermanents()", "AS-IS permanent candidates");
    return Task.CompletedTask;
}

Task CardTargetsFilterByOwnerZoneVisibilityAndRequirements()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult result = TargetFilterHelpers.Cards(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        zones: new[] { ChoiceZone.BattleArea },
        ownerScope: TargetOwnerScope.Opponent,
        visibilityScope: TargetVisibilityScope.PublicOnly,
        cardRequirements: new[] { CardRequirement.Color("Red"), CardRequirement.Trait("Dragon", CardRequirementTextMode.Contains) }));

    AssertTrue(result.IsSuccess, "success");
    AssertSequence(new[] { $"card:{OpponentBattleCard.Value}" }, CandidateIds(result), "candidate ids");
    AssertEqual(TargetCandidateKind.Card, result.Candidates[0].Kind, "candidate kind");
    AssertEqual(PlayerTwo, result.Candidates[0].OwnerId, "owner");
    AssertEqual(ChoiceZone.BattleArea, result.Candidates[0].Zone, "zone");
    return Task.CompletedTask;
}

Task PermanentTargetsDefaultToBattleAndBreedingZones()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult result = TargetFilterHelpers.Permanents(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        ownerScope: TargetOwnerScope.Opponent,
        visibilityScope: TargetVisibilityScope.PublicOnly));

    AssertTrue(result.IsSuccess, "success");
    AssertSequence(new[] { $"card:{OpponentBattleCard.Value}", $"card:{OpponentUnsuspendedCard.Value}" }, CandidateIds(result), "permanent ids");
    AssertTrue(result.Candidates.All(candidate => candidate.Kind == TargetCandidateKind.Permanent), "permanent kind");
    return Task.CompletedTask;
}

Task PlayerTargetsFilterSourceAndOpponent()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult source = TargetFilterHelpers.Players(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        ownerScope: TargetOwnerScope.SourcePlayer));
    TargetFilterResult opponent = TargetFilterHelpers.Players(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        ownerScope: TargetOwnerScope.Opponent));

    AssertSequence(new[] { "player:1" }, CandidateIds(source), "source player");
    AssertSequence(new[] { "player:2" }, CandidateIds(opponent), "opponent player");
    return Task.CompletedTask;
}

Task VisibilityScopeControlsPrivateCardAccess()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult publicOnly = TargetFilterHelpers.Cards(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        zones: new[] { ChoiceZone.Security },
        ownerScope: TargetOwnerScope.SourcePlayer,
        visibilityScope: TargetVisibilityScope.PublicOnly));
    TargetFilterResult controllerPrivate = TargetFilterHelpers.Cards(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        zones: new[] { ChoiceZone.Security },
        ownerScope: TargetOwnerScope.SourcePlayer,
        visibilityScope: TargetVisibilityScope.ControllerPrivate));
    TargetFilterResult opponentViewer = TargetFilterHelpers.Cards(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerTwo,
        zones: new[] { ChoiceZone.Security },
        ownerScope: TargetOwnerScope.SourcePlayer,
        visibilityScope: TargetVisibilityScope.ControllerPrivate));

    AssertSequence(Array.Empty<string>(), CandidateIds(publicOnly), "public excludes hidden");
    AssertSequence(new[] { $"card:{OwnHiddenSecurity.Value}" }, CandidateIds(controllerPrivate), "controller sees private");
    AssertSequence(Array.Empty<string>(), CandidateIds(opponentViewer), "opponent cannot see controller private");
    return Task.CompletedTask;
}

Task SuspensionAndFlagFiltersApply()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult suspended = TargetFilterHelpers.Permanents(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        ownerScope: TargetOwnerScope.Opponent,
        visibilityScope: TargetVisibilityScope.PublicOnly,
        suspensionScope: TargetSuspensionScope.SuspendedOnly,
        requiredFlags: new[] { "canBeTargeted" }));
    TargetFilterResult excluded = TargetFilterHelpers.Permanents(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        ownerScope: TargetOwnerScope.Opponent,
        visibilityScope: TargetVisibilityScope.PublicOnly,
        excludedFlags: new[] { "cannotBeTargeted" }));

    AssertSequence(new[] { $"card:{OpponentBattleCard.Value}" }, CandidateIds(suspended), "suspended with required flag");
    AssertSequence(new[] { $"card:{OpponentBattleCard.Value}" }, CandidateIds(excluded), "excluded flag removes unsuspended");
    return Task.CompletedTask;
}

Task InvalidSourceOrViewerReturnsFailure()
{
    Fixture fixture = CreateFixture();
    TargetFilterResult missingSource = TargetFilterHelpers.Evaluate(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        new HeadlessPlayerId(9),
        PlayerOne));
    TargetFilterResult missingViewer = TargetFilterHelpers.Evaluate(new TargetFilterRequest(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        new HeadlessPlayerId(9)));

    AssertFalse(missingSource.IsSuccess, "missing source");
    AssertContains(missingSource.Reason, "Source player", "missing source reason");
    AssertFalse(missingViewer.IsSuccess, "missing viewer");
    AssertContains(missingViewer.Reason, "Viewer player", "missing viewer reason");
    return Task.CompletedTask;
}

Task TargetFilterResultValuesAreDeterministic()
{
    Fixture fixture = CreateFixture();
    TargetFilterRequest request = new(
        fixture.State,
        fixture.Definitions,
        PlayerOne,
        PlayerOne,
        new[] { TargetCandidateKind.Permanent, TargetCandidateKind.Card },
        new[] { ChoiceZone.BattleArea, ChoiceZone.Hand },
        TargetOwnerScope.Any,
        TargetVisibilityScope.ControllerPrivate);

    string first = Signature(TargetFilterHelpers.Evaluate(request));
    string second = Signature(TargetFilterHelpers.Evaluate(request));

    AssertEqual(first, second, "signature");
    AssertContains(first, $"card:{OwnHandCard.Value}", "own hand stable id");
    AssertContains(first, $"card:{OpponentBattleCard.Value}", "opponent battle stable id");
    return Task.CompletedTask;
}

Task SourceFilesStayInsideGoalScope()
{
    string helper = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "TargetFilterHelpers.cs"));

    AssertFalse(helper.Contains("TODO", StringComparison.OrdinalIgnoreCase), "helper must not contain TODO");
    AssertFalse(helper.Contains("placeholder", StringComparison.OrdinalIgnoreCase), "helper must not contain placeholder");
    AssertFalse(helper.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase), "helper must not contain not implemented");
    AssertFalse(helper.Contains("ZoneQueryHelpers", StringComparison.Ordinal), "helper must not implement next zone query helper");
    AssertContains(helper, "public static class TargetFilterHelpers", "helper public API");
    AssertContains(helper, "TargetFilterRequest", "request contract");
    AssertContains(helper, "TargetFilterResult", "result contract");
    return Task.CompletedTask;
}

Fixture CreateFixture()
{
    var definitions = new Dictionary<HeadlessEntityId, CardRecord>
    {
        [new("SRC")] = CreateDefinition("SRC", "Source Digimon", "Blue", "Cyborg", "Digimon"),
        [new("OWN-HAND")] = CreateDefinition("OWN-HAND", "Own Hidden Hand", "Yellow", "Angel", "Option"),
        [new("P2-RED")] = CreateDefinition("P2-RED", "Opponent Red Dragon", "Red", "Dragonkin", "Digimon"),
        [new("P2-BLUE")] = CreateDefinition("P2-BLUE", "Opponent Blue Beast", "Blue", "Beast", "Digimon"),
        [new("P1-SEC")] = CreateDefinition("P1-SEC", "Own Security", "Purple", "Undead", "Digimon"),
    };
    var instances = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [SourceCard] = new(SourceCard, new HeadlessEntityId("SRC"), PlayerOne, IsSuspended: false, IsFaceUp: true),
        [OwnHandCard] = new(OwnHandCard, new HeadlessEntityId("OWN-HAND"), PlayerOne, IsSuspended: false, IsFaceUp: false),
        [OpponentBattleCard] = new(
            OpponentBattleCard,
            new HeadlessEntityId("P2-RED"),
            PlayerTwo,
            IsSuspended: true,
            IsFaceUp: true,
            Flags: new Dictionary<string, bool> { ["canBeTargeted"] = true }),
        [OpponentUnsuspendedCard] = new(
            OpponentUnsuspendedCard,
            new HeadlessEntityId("P2-BLUE"),
            PlayerTwo,
            IsSuspended: false,
            IsFaceUp: true,
            Flags: new Dictionary<string, bool> { ["cannotBeTargeted"] = true }),
        [OwnHiddenSecurity] = new(OwnHiddenSecurity, new HeadlessEntityId("P1-SEC"), PlayerOne, IsSuspended: false, IsFaceUp: false),
    };
    var players = new[]
    {
        new PlayerState(PlayerOne).WithZone(ChoiceZone.Hand, new[] { SourceCard, OwnHandCard }).WithZone(ChoiceZone.Security, new[] { OwnHiddenSecurity }),
        new PlayerState(PlayerTwo).WithZone(ChoiceZone.BattleArea, new[] { OpponentBattleCard, OpponentUnsuspendedCard }),
    };
    MatchState state = new(players, instances);
    return new Fixture(state, definitions);
}

static CardRecord CreateDefinition(
    string id,
    string name,
    string color,
    string trait,
    string cardType)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        name,
        new Dictionary<string, object?>
        {
            [CardRequirementHelpers.CardColorKey] = color,
            [CardRequirementHelpers.TraitKey] = trait,
        },
        CardType: cardType);
}

static string[] CandidateIds(TargetFilterResult result)
{
    return result.Candidates.Select(candidate => candidate.StableId).ToArray();
}

static string Signature(TargetFilterResult result)
{
    string candidates = string.Join(",", result.Candidates.Select(candidate =>
        $"{candidate.Kind}:{candidate.StableId}:{candidate.OwnerId.Value}:{candidate.Zone}:{candidate.IsFaceUp}:{candidate.IsSuspended}"));
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));

    return string.Join("|", result.IsSuccess, result.Reason, candidates, values);
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

sealed record Fixture(
    MatchState State,
    IReadOnlyDictionary<HeadlessEntityId, CardRecord> Definitions);
