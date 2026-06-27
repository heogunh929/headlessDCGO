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
HeadlessEntityId SecurityId = new("p2:main:006:P2-M06");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predecessor goal G3.5-004 is complete", PredecessorGoalsComplete),
    ("Target attack auto-resolves battle and clears the attack", TargetAttackAutoResolvesBattleAndClears),
    ("Direct attack auto-resolves security and clears the attack", DirectAttackAutoResolvesSecurityAndClears),
    ("Blocked attack pauses for choice then resolves against blocker", BlockedAttackPausesThenResolvesAgainstBlocker),
    ("Skipped block resolves battle against the original target", SkippedBlockResolvesAgainstOriginalTarget),
    ("AttackPipeline advances one phase per step to completion", AttackPipelineAdvancesPhaseByPhase),
    ("AttackPipeline source is clean and wired into the flow", AttackPipelineSourceIsClean),
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

Task PredecessorGoalsComplete()
{
    AssertComplete("G3.5-004_game_flow_processor_unit_test_results.md");
    return Task.CompletedTask;
}

async Task TargetAttackAutoResolvesBattleAndClears()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 7000, withBlocker: false);
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);

    await match.StepAsync();

    AssertFalse(match.HasPendingChoice(), "no pending choice for unblockable target attack");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after resolution");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains in battle");
    AssertMetadata(match, TargetId, BattleResolver.DeletedByBattleKey, true);
}

async Task DirectAttackAutoResolvesSecurityAndClears()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: null, withBlocker: false, securityCount: 1);
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);

    await match.StepAsync();

    AssertFalse(match.HasPendingChoice(), "no pending choice for direct attack without blockers");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after security check");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, SecurityId, "checked security card moved to trash");
    AssertMetadata(match, SecurityId, SecurityResolver.CheckedBySecurityCheckKey, true);
}

async Task BlockedAttackPausesThenResolvesAgainstBlocker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 3000, withBlocker: true, blockerDp: 12000);
    ((ScriptedChoiceProvider)match.Context.ChoiceProvider).Enqueue(ChoiceResult.Select(BlockerId));
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);

    await match.StepAsync();

    AssertTrue(match.HasPendingChoice(), "attack pauses for the block choice");
    AssertEqual(AttackPhase.Blocking, match.Context.AttackController.Current.Phase, "attack parked in blocking phase");

    await match.ApplyActionAsync(HeadlessActionFactory.ResolveChoice(Opponent));
    await match.StepAsync();

    AssertFalse(match.HasPendingChoice(), "block choice resolved");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after blocked battle");
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker deleted by stronger blocker");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "blocker survives");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "original target untouched");
}

async Task SkippedBlockResolvesAgainstOriginalTarget()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 3000, withBlocker: true, blockerDp: 12000);
    ((ScriptedChoiceProvider)match.Context.ChoiceProvider).Enqueue(ChoiceResult.Skip());
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);

    await match.StepAsync();
    AssertTrue(match.HasPendingChoice(), "attack pauses for the block choice");

    await match.ApplyActionAsync(HeadlessActionFactory.ResolveChoice(Opponent));
    await match.StepAsync();

    AssertFalse(match.HasPendingChoice(), "block choice skipped");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after unblocked battle");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "original target deleted by attacker");
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker survives weaker target");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "unused blocker remains");
}

async Task AttackPipelineAdvancesPhaseByPhase()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    AssertEqual(AttackPhase.Declared, context.AttackController.Current.Phase, "declared phase");

    var pipeline = new AttackPipeline();

    AttackAdvanceResult step1 = await pipeline.AdvanceAsync(context);
    AssertTrue(step1.Progressed, "step 1 progressed");
    AssertEqual(AttackPhase.Combat, context.AttackController.Current.Phase, "no blockers skips straight to combat");

    AttackAdvanceResult step2 = await pipeline.AdvanceAsync(context);
    AssertTrue(step2.Progressed, "step 2 progressed");
    AssertEqual(AttackPhase.Resolved, context.AttackController.Current.Phase, "combat resolves the attack");

    AttackAdvanceResult step3 = await pipeline.AdvanceAsync(context);
    AssertTrue(step3.Progressed, "step 3 progressed");
    AssertEqual(AttackPhase.Completed, context.AttackController.Current.Phase, "end attack triggers collected");

    AttackAdvanceResult step4 = await pipeline.AdvanceAsync(context);
    AssertTrue(step4.Progressed, "step 4 progressed");
    AssertEqual(AttackPhase.None, context.AttackController.Current.Phase, "cleanup clears the attack");

    AttackAdvanceResult idle = await pipeline.AdvanceAsync(context);
    AssertFalse(idle.Progressed, "no work remains once cleared");
}

Task AttackPipelineSourceIsClean()
{
    string pipelinePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "AttackPipeline.cs");
    if (!File.Exists(pipelinePath))
    {
        throw new FileNotFoundException($"Attack pipeline source was not found: {pipelinePath}");
    }

    string pipeline = File.ReadAllText(pipelinePath);
    AssertFalse(pipeline.Contains("TODO", StringComparison.OrdinalIgnoreCase), "AttackPipeline must not contain TODO");
    AssertFalse(pipeline.Contains("NotImplementedException", StringComparison.Ordinal), "AttackPipeline must not throw NotImplementedException");
    AssertFalse(pipeline.Contains("UnityEngine", StringComparison.Ordinal), "AttackPipeline must not reference UnityEngine");

    string flowPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameFlowProcessor.cs");
    string flow = File.ReadAllText(flowPath);
    AssertContains(flow, "AttackPipeline", "flow processor wires the attack pipeline");
    AssertFalse(flow.Contains("Placeholder: attack pipeline", StringComparison.Ordinal), "attack placeholder must be removed");
    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateMatchAsync(
    int? attackerDp,
    int? targetDp,
    bool withBlocker,
    int? blockerDp = 5000,
    int securityCount = 0)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDefinition($"P1-M{index:D2}", "Digimon"));
        cards.Upsert(CreateDefinition($"P2-M{index:D2}", "Digimon"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, Player);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    if (withBlocker)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    if (securityCount > 0)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, SecurityId, ChoiceZone.None, ChoiceZone.Security));
    }

    var attackerMetadata = new Dictionary<string, object?> { ["isSuspended"] = false };
    if (attackerDp.HasValue)
    {
        attackerMetadata[BattleResolver.DpKey] = attackerDp.Value;
    }

    SetMetadata(match, AttackerId, attackerMetadata);

    var targetMetadata = new Dictionary<string, object?> { ["isSuspended"] = true };
    if (targetDp.HasValue)
    {
        targetMetadata[BattleResolver.DpKey] = targetDp.Value;
    }

    SetMetadata(match, TargetId, targetMetadata);

    if (withBlocker)
    {
        var blockerMetadata = new Dictionary<string, object?>
        {
            ["isSuspended"] = false,
            [BlockTiming.HasBlockerKey] = true
        };
        if (blockerDp.HasValue)
        {
            blockerMetadata[BattleResolver.DpKey] = blockerDp.Value;
        }

        SetMetadata(match, BlockerId, blockerMetadata);
    }

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

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, playerId, HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId playerId, string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
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

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src"))
            && Directory.Exists(Path.Combine(directory.FullName, "docs")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root with 'src' and 'docs' was not found.");
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
