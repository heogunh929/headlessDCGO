using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId SecurityThreeId = new("p2:main:008:P2-M08");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-004 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS security check references are recorded", AsIsSecurityCheckReferencesAreRecorded),
    ("Direct attack checks top security and moves it to trash", DirectAttackChecksTopSecurity),
    ("Strike checks multiple security cards in order", StrikeChecksMultipleSecurityCards),
    ("Zero strike resolves without moving security", ZeroStrikeResolvesWithoutMovingSecurity),
    ("No security is rejected without mutation", NoSecurityIsRejectedWithoutMutation),
    ("Target attack is rejected without mutation", TargetAttackIsRejectedWithoutMutation),
    ("Non-Digimon attacker is rejected without mutation", NonDigimonAttackerIsRejectedWithoutMutation),
    ("Security resolver is deterministic and source scoped", SecurityResolverIsDeterministicAndSourceScoped),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-004")
        ?? throw new InvalidOperationException("G2G-004 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Security check", "goal");
    AssertContains(Value(row, "scope"), "security check", "scope check");
    AssertContains(Value(row, "scope"), "security zone", "scope zone");
    AssertEqual("security resolver", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "security check", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-004_security_check_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-003", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-003_battle_dp_deletion_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsSecurityCheckReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(attackProcess, "DoSecurityCheck = true", "AS-IS direct attack enables security check");
    AssertContains(attackProcess, "new ISecurityCheck", "AS-IS security check call");
    AssertContains(cardController, "public class ISecurityCheck", "AS-IS security class");
    AssertContains(cardController, "AttackingPermanent.Strike", "AS-IS strike count");
    AssertContains(cardController, "CardSource brokenSecurityCard = player.SecurityCards[0]", "AS-IS top security");
    AssertContains(cardController, "new IReduceSecurity", "AS-IS security reduction");
    AssertContains(cardController, "CardObjectController.AddTrashCard(brokenSecurityCard)", "AS-IS security to trash");
    return Task.CompletedTask;
}

async Task DirectAttackChecksTopSecurity()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 3);
    await DeclareDirectAttackAsync(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertEqual(Opponent, result.CheckedPlayerId, "checked player");
    AssertEqual(1, result.Strike, "strike");
    AssertSequence(result.CheckedCardIds, SecurityOneId);
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, SecurityOneId, "top security moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.Security, SecurityTwoId, "second security remains");
    AssertMetadata(match, SecurityOneId, SecurityResolver.CheckedBySecurityCheckKey, true);
    AssertMetadata(match, SecurityOneId, SecurityResolver.SecurityCheckOrderKey, 1);
    AssertTrue(result.AttackResolved, "result attack resolved");
    AssertTrue(match.Context.AttackController.Current.IsResolved, "controller attack resolved");
    AssertEqual(1, result.MovementResults.Count, "movement count");
    AssertEqual(GameEventType.CardMoved, result.MovementResults[0].Event.Type, "movement event");
}

async Task StrikeChecksMultipleSecurityCards()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 2, securityCount: 3);
    await DeclareDirectAttackAsync(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertSequence(result.CheckedCardIds, SecurityOneId, SecurityTwoId);
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, SecurityOneId, "first security trash");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, SecurityTwoId, "second security trash");
    AssertZoneContains(match, Opponent, ChoiceZone.Security, SecurityThreeId, "third security remains");
    AssertMetadata(match, SecurityTwoId, SecurityResolver.SecurityCheckOrderKey, 2);
}

async Task ZeroStrikeResolvesWithoutMovingSecurity()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 0, securityCount: 2);
    await DeclareDirectAttackAsync(match);
    string before = SnapshotZones(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "zero strike success");
    AssertEqual(0, result.Strike, "strike");
    AssertEqual(0, result.CheckedCardIds.Count, "checked count");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsResolved, "attack resolved");
}

async Task NoSecurityIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 0);
    await DeclareDirectAttackAsync(match);
    string before = SnapshotZones(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "no security", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

async Task TargetAttackIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 2);
    await DeclareTargetAttackAsync(match);
    string before = SnapshotZones(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "direct attack", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

async Task NonDigimonAttackerIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 2, attackerCardType: "Option");
    HeadlessAttackState beforeAttack = match.Context.AttackController.DeclareAttack(
        Player,
        AttackerId,
        Opponent,
        targetId: null,
        isDirectAttack: true);
    string before = SnapshotZones(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "not a Digimon", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertEqual(beforeAttack, match.Context.AttackController.Current, "attack unchanged");
}

async Task SecurityResolverIsDeterministicAndSourceScoped()
{
    DcgoMatch first = await CreateConfiguredMatchAsync(strike: 2, securityCount: 3);
    DcgoMatch second = await CreateConfiguredMatchAsync(strike: 2, securityCount: 3);
    await DeclareDirectAttackAsync(first);
    await DeclareDirectAttackAsync(second);

    SecurityResolutionResult firstResult = await new SecurityResolver().ResolveAsync(first.Context);
    SecurityResolutionResult secondResult = await new SecurityResolver().ResolveAsync(second.Context);

    AssertEqual(SnapshotResult(firstResult), SnapshotResult(secondResult), "result snapshot");

    string resolverPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "SecurityResolver.cs");
    string resolverText = File.ReadAllText(resolverPath);
    AssertFalse(resolverText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "SecurityResolver must not contain TODO");
    AssertFalse(resolverText.Contains("UnityEngine", StringComparison.Ordinal), "SecurityResolver must not reference UnityEngine");
    AssertFalse(resolverText.Contains("MonoBehaviour", StringComparison.Ordinal), "SecurityResolver must not reference MonoBehaviour");
    AssertContains(resolverText, "ResolveAsync", "resolver API");
    AssertContains(resolverText, "ChoiceZone.Security", "security source zone");
    AssertContains(resolverText, "ChoiceZone.Trash", "security destination zone");
}

Task DeclareDirectAttackAsync(DcgoMatch match)
{
    // Declare directly on the controller so the common loop (G3.5-005) does not auto-advance the
    // attack; this keeps SecurityResolver under isolated test exactly as in Phase 2.
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    return Task.CompletedTask;
}

Task DeclareTargetAttackAsync(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(
    int strike,
    int securityCount,
    string attackerCardType = "Digimon")
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDefinition($"P1-M{index:D2}", index == 1 ? attackerCardType : "Digimon"));
        cards.Upsert(CreateDefinition($"P2-M{index:D2}", "Digimon"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    HeadlessEntityId[] securityCards = { SecurityOneId, SecurityTwoId, SecurityThreeId };
    for (int index = 0; index < securityCount; index++)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, securityCards[index], ChoiceZone.None, ChoiceZone.Security));
    }

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, [SecurityResolver.StrikeKey] = strike });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });
    return match;
}

static CardRecord CreateDefinition(string id, string cardType)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        new Dictionary<string, object?>(),
        CardType: cardType);
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

static LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId playerId, string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

static void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

void AssertMetadata(DcgoMatch match, HeadlessEntityId cardId, string key, object expected)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    if (!record.Metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"Metadata '{key}' was not found on '{cardId}'.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
}

void AssertZoneContains(DcgoMatch match, HeadlessPlayerId playerId, ChoiceZone zone, HeadlessEntityId cardId, string message)
{
    if (match.Context.ZoneMover is not IZoneStateReader zoneReader)
    {
        throw new InvalidOperationException("Zone reader missing.");
    }

    AssertTrue(zoneReader.GetCards(playerId, zone).Contains(cardId), message);
}

string SnapshotZones(DcgoMatch match)
{
    if (match.Context.ZoneMover is not IZoneStateReader zoneReader)
    {
        throw new InvalidOperationException("Zone reader missing.");
    }

    return string.Join(
        "|",
        new[] { Player, Opponent }.SelectMany(player =>
            new[] { ChoiceZone.BattleArea, ChoiceZone.Security, ChoiceZone.Trash }.Select(zone =>
                $"{player.Value}:{zone}:{string.Join(",", zoneReader.GetCards(player, zone).Select(card => card.Value))}")));
}

static string SnapshotResult(SecurityResolutionResult result)
{
    return string.Join(
        ":",
        result.IsSuccess,
        result.CheckedPlayerId?.Value,
        result.Strike,
        result.AttackResolved,
        string.Join(",", result.CheckedCardIds.Select(card => card.Value)));
}

static bool ReadBool(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        return false;
    }

    return raw switch
    {
        bool value => value,
        string value => bool.TryParse(value, out bool parsed) && parsed,
        _ => false
    };
}

static string? ReadString(IReadOnlyDictionary<string, object?> parameters, string key)
{
    return parameters.TryGetValue(key, out object? raw) ? raw?.ToString() : null;
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

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = ParseCsvLine(lines[0]).ToArray();
    return lines.Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line =>
        {
            string[] values = ParseCsvLine(line).ToArray();
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = index < values.Length ? values[index] : string.Empty;
            }

            return row;
        })
        .ToArray();
}

static IEnumerable<string> ParseCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    values.Add(current.ToString());
    return values;
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    string directory = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(directory, "docs", "headless_complete_goal_breakdown.csv")))
    {
        directory = Directory.GetParent(directory)?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    return directory;
}

static void AssertSequence(IReadOnlyList<HeadlessEntityId> actual, params HeadlessEntityId[] expected)
{
    AssertEqual(expected.Length, actual.Count, "sequence length");
    for (var index = 0; index < expected.Length; index++)
    {
        AssertEqual(expected[index], actual[index], $"sequence[{index}]");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}
