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
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-003 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS battle DP deletion references are recorded", AsIsBattleDeletionReferencesAreRecorded),
    ("Higher attacker DP deletes defender and resolves attack", HigherAttackerDpDeletesDefender),
    ("Higher defender DP deletes attacker and resolves attack", HigherDefenderDpDeletesAttacker),
    ("Equal DP deletes both battle participants", EqualDpDeletesBoth),
    ("Blocked attack resolves battle against selected blocker", BlockedAttackUsesSelectedBlocker),
    ("Direct attack is rejected without zone mutation", DirectAttackIsRejectedWithoutMutation),
    ("Missing DP is rejected without zone mutation", MissingDpIsRejectedWithoutMutation),
    ("Non-Digimon participant is rejected without zone mutation", NonDigimonParticipantIsRejectedWithoutMutation),
    ("Battle resolver is deterministic and source scoped", BattleResolverIsDeterministicAndSourceScoped),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-003")
        ?? throw new InvalidOperationException("G2G-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Battle DP deletion", "goal");
    AssertContains(Value(row, "scope"), "DP", "scope DP");
    AssertContains(Value(row, "scope"), "battle deletion", "scope deletion");
    AssertEqual("battle resolver", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "battle DP deletion", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-003_battle_dp_deletion_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-002", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-002_block_timing_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsBattleDeletionReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(attackProcess, "DetermineAttackOutcome", "AS-IS attack outcome");
    AssertContains(attackProcess, "new IBattle(AttackingPermanent: AttackingPermanent, DefendingPermanent: DefendingPermanent, null)", "AS-IS battle call");
    AssertContains(cardController, "public class IBattle", "AS-IS battle class");
    AssertContains(cardController, "AttackingPermanent.DP - DefendingPermanent.DP", "AS-IS DP comparison");
    AssertContains(cardController, "LoserPermanents.Add(DefendingPermanent)", "AS-IS defender deletion");
    AssertContains(cardController, "LoserPermanents.Add(AttackingPermanent)", "AS-IS attacker deletion");
    AssertContains(cardController, "DestroyPermanentsClass destoryBattlePermanents", "AS-IS battle destroy handoff");
    AssertContains(cardController, "CardObjectController.AddTrashCard(topCard)", "AS-IS trash movement");
    return Task.CompletedTask;
}

async Task HigherAttackerDpDeletesDefender()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 9000, targetDp: 7000);
    await DeclareTargetAttackAsync(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertEqual(9000, result.AttackerDp, "attacker dp");
    AssertEqual(7000, result.DefenderDp, "defender dp");
    AssertFalse(result.AttackerDeleted, "attacker deleted");
    AssertTrue(result.DefenderDeleted, "defender deleted");
    AssertSequence(result.DeletedCardIds, TargetId);
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains in battle");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
    AssertMetadata(match, TargetId, BattleResolver.DeletedByBattleKey, true);
    AssertMetadata(match, TargetId, BattleResolver.DpBeforeBattleKey, 7000);
    AssertFalse(match.Context.AttackController.Current.IsPending, "attack no longer pending");
    AssertTrue(match.Context.AttackController.Current.IsResolved, "attack resolved");
}

async Task HigherDefenderDpDeletesAttacker()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 5000, targetDp: 8000);
    await DeclareTargetAttackAsync(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.AttackerDeleted, "attacker deleted");
    AssertFalse(result.DefenderDeleted, "defender deleted");
    AssertSequence(result.DeletedCardIds, AttackerId);
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "defender remains in battle");
    AssertMetadata(match, AttackerId, BattleResolver.DeletedByBattleKey, true);
    AssertMetadata(match, AttackerId, BattleResolver.DpBeforeBattleKey, 5000);
}

async Task EqualDpDeletesBoth()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 6000, targetDp: 6000);
    await DeclareTargetAttackAsync(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.AttackerDeleted, "attacker deleted");
    AssertTrue(result.DefenderDeleted, "defender deleted");
    AssertSequence(result.DeletedCardIds, AttackerId, TargetId);
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
    AssertEqual(2, result.MovementResults.Count, "movement count");
}

async Task BlockedAttackUsesSelectedBlocker()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 9000, targetDp: 3000, blockerDp: 12000);
    await DeclareDirectAttackAsync(match);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);
    BlockTimingResult block = timing.ResolveBlockChoice(match.Context, ChoiceResult.Select(BlockerId));
    AssertTrue(block.IsSuccess, "block resolve");

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.AttackerDeleted, "attacker deleted by blocker");
    AssertFalse(result.DefenderDeleted, "blocker not deleted");
    AssertSequence(result.DeletedCardIds, AttackerId);
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "blocker remains in battle");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "original target unaffected");
}

async Task DirectAttackIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 9000, targetDp: 7000);
    await DeclareDirectAttackAsync(match);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "non-direct", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

async Task MissingDpIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: null, targetDp: 7000);
    await DeclareTargetAttackAsync(match);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "has no battle DP", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

async Task NonDigimonParticipantIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerDp: 9000, targetDp: 7000, targetCardType: "Option");
    HeadlessAttackState beforeAttack = match.Context.AttackController.DeclareAttack(
        Player,
        AttackerId,
        Opponent,
        TargetId,
        isDirectAttack: false);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "not a Digimon", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertEqual(beforeAttack, match.Context.AttackController.Current, "attack unchanged");
}

async Task BattleResolverIsDeterministicAndSourceScoped()
{
    DcgoMatch first = await CreateConfiguredMatchAsync(attackerDp: 6000, targetDp: 6000);
    DcgoMatch second = await CreateConfiguredMatchAsync(attackerDp: 6000, targetDp: 6000);
    await DeclareTargetAttackAsync(first);
    await DeclareTargetAttackAsync(second);

    BattleResolutionResult firstResult = await new BattleResolver().ResolveAsync(first.Context);
    BattleResolutionResult secondResult = await new BattleResolver().ResolveAsync(second.Context);

    AssertEqual(SnapshotResult(firstResult), SnapshotResult(secondResult), "result snapshot");

    string resolverPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "BattleResolver.cs");
    string resolverText = File.ReadAllText(resolverPath);
    AssertFalse(resolverText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "BattleResolver must not contain TODO");
    AssertFalse(resolverText.Contains("UnityEngine", StringComparison.Ordinal), "BattleResolver must not reference UnityEngine");
    AssertFalse(resolverText.Contains("MonoBehaviour", StringComparison.Ordinal), "BattleResolver must not reference MonoBehaviour");
    AssertContains(resolverText, "ResolveAsync", "resolver API");
    AssertContains(resolverText, "ChoiceZone.Trash", "battle deletion moves to trash");
    AssertContains(resolverText, "ResolveAttack", "attack resolution");
}

async Task DeclareTargetAttackAsync(DcgoMatch match)
{
    LegalAction targetAttack = match.GetLegalActions(Player)
        .Single(action => action.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadString(action.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(targetAttack);
    await match.StepAsync();
}

async Task DeclareDirectAttackAsync(DcgoMatch match)
{
    LegalAction direct = match.GetLegalActions(Player)
        .Single(action => action.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadBool(action.Parameters, HeadlessActionParameterKeys.IsDirectAttack));
    await match.ApplyActionAsync(direct);
    await match.StepAsync();
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(
    int? attackerDp,
    int? targetDp,
    int? blockerDp = 5000,
    string targetCardType = "Digimon")
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDefinition($"P1-M{index:D2}", "Digimon"));
        cards.Upsert(CreateDefinition($"P2-M{index:D2}", index == 1 ? targetCardType : "Digimon"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var attackerMetadata = new Dictionary<string, object?> { ["isSuspended"] = false };
    if (attackerDp.HasValue)
    {
        attackerMetadata[BattleResolver.DpKey] = attackerDp.Value;
    }

    var targetMetadata = new Dictionary<string, object?> { ["isSuspended"] = true };
    if (targetDp.HasValue)
    {
        targetMetadata[BattleResolver.DpKey] = targetDp.Value;
    }

    var blockerMetadata = new Dictionary<string, object?>
    {
        ["isSuspended"] = false,
        [BlockTiming.HasBlockerKey] = true
    };
    if (blockerDp.HasValue)
    {
        blockerMetadata[BattleResolver.DpKey] = blockerDp.Value;
    }

    SetMetadata(match, AttackerId, attackerMetadata);
    SetMetadata(match, TargetId, targetMetadata);
    SetMetadata(match, BlockerId, blockerMetadata);
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
            new[] { ChoiceZone.BattleArea, ChoiceZone.Trash }.Select(zone =>
                $"{player.Value}:{zone}:{string.Join(",", zoneReader.GetCards(player, zone).Select(card => card.Value))}")));
}

static string SnapshotResult(BattleResolutionResult result)
{
    return string.Join(
        ":",
        result.IsSuccess,
        result.AttackerDp,
        result.DefenderDp,
        result.AttackerDeleted,
        result.DefenderDeleted,
        string.Join(",", result.DeletedCardIds.Select(card => card.Value)));
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
