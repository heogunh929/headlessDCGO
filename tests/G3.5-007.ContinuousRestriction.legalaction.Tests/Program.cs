using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
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
    ("Predecessor goal G3.5-006 is complete", PredecessorGoalsComplete),
    ("Gate reports a continuous cannot-attack restriction from the registry", GateReportsContinuousAttackRestriction),
    ("Gate reports a continuous cannot-block restriction from the registry", GateReportsContinuousBlockRestriction),
    ("Gate is a no-op when no continuous effects are registered", GateIsNoOpWithEmptyRegistry),
    ("Continuous cannot-attack removes the DeclareAttack legal action", ContinuousAttackRestrictionRemovesLegalAction),
    ("Continuous cannot-block removes the blocker candidate", ContinuousBlockRestrictionRemovesBlocker),
    ("Continuous restriction wiring source is clean", WiringSourceIsClean),
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
    AssertComplete("G3.5-006_autoprocessing_event_collection_unit_test_results.md");
    return Task.CompletedTask;
}

Task GateReportsContinuousAttackRestriction()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterCannotRestriction(context, "fx-cannot-attack", AttackerId, RestrictionHelpers.CannotAttackKey);

    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(context, AttackerId).IsRestricted, "attacker is restricted");
    AssertFalse(ContinuousRestrictionGate.EvaluateAttack(context, TargetId).IsRestricted, "unrelated entity is not restricted");
    AssertFalse(ContinuousRestrictionGate.EvaluateBlock(context, AttackerId).IsRestricted, "attack restriction does not block-restrict");
    return Task.CompletedTask;
}

Task GateReportsContinuousBlockRestriction()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterCannotRestriction(context, "fx-cannot-block", BlockerId, RestrictionHelpers.CannotBlockKey);

    AssertTrue(ContinuousRestrictionGate.EvaluateBlock(context, BlockerId).IsRestricted, "blocker is restricted");
    AssertFalse(ContinuousRestrictionGate.EvaluateBlock(context, TargetId).IsRestricted, "unrelated entity is not restricted");
    return Task.CompletedTask;
}

Task GateIsNoOpWithEmptyRegistry()
{
    EngineContext context = EngineContext.CreateDefault();
    AssertFalse(ContinuousRestrictionGate.EvaluateAttack(context, AttackerId).IsRestricted, "no attack restriction");
    AssertFalse(ContinuousRestrictionGate.EvaluateBlock(context, BlockerId).IsRestricted, "no block restriction");
    AssertEqual(0, ContinuousRestrictionGate.Evaluate(context, AttackerId).Count, "no restrictions");
    return Task.CompletedTask;
}

async Task ContinuousAttackRestrictionRemovesLegalAction()
{
    DcgoMatch match = await CreateMatchAsync(withBlocker: false);

    int baseline = CountDeclareAttack(match, Player, AttackerId);
    AssertTrue(baseline > 0, "attacker can declare an attack before the restriction");

    RegisterCannotRestriction(match.Context, "fx-cannot-attack", AttackerId, RestrictionHelpers.CannotAttackKey);

    int restricted = CountDeclareAttack(match, Player, AttackerId);
    AssertEqual(0, restricted, "continuous restriction removes all DeclareAttack actions for the attacker");
}

async Task ContinuousBlockRestrictionRemovesBlocker()
{
    DcgoMatch match = await CreateMatchAsync(withBlocker: true);
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);

    var blockTiming = new BlockTiming();
    bool baselineHasBlocker = blockTiming.GetBlockerCandidates(match.Context)
        .Any(candidate => candidate.BlockerId == BlockerId);
    AssertTrue(baselineHasBlocker, "blocker is a legal candidate before the restriction");

    RegisterCannotRestriction(match.Context, "fx-cannot-block", BlockerId, RestrictionHelpers.CannotBlockKey);

    bool restrictedHasBlocker = blockTiming.GetBlockerCandidates(match.Context)
        .Any(candidate => candidate.BlockerId == BlockerId);
    AssertFalse(restrictedHasBlocker, "continuous restriction removes the blocker candidate");
}

Task WiringSourceIsClean()
{
    string gatePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "ContinuousRestrictionGate.cs");
    if (!File.Exists(gatePath))
    {
        throw new FileNotFoundException($"Continuous restriction gate source was not found: {gatePath}");
    }

    string gate = File.ReadAllText(gatePath);
    AssertFalse(gate.Contains("TODO", StringComparison.OrdinalIgnoreCase), "gate must not contain TODO");
    AssertFalse(gate.Contains("NotImplementedException", StringComparison.Ordinal), "gate must not throw NotImplementedException");
    AssertFalse(gate.Contains("UnityEngine", StringComparison.Ordinal), "gate must not reference UnityEngine");

    string attackPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "AttackPermanentAction.cs");
    AssertContains(File.ReadAllText(attackPath), "ContinuousRestrictionGate", "attack action consults the continuous restriction gate");

    string blockPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "BlockTiming.cs");
    AssertContains(File.ReadAllText(blockPath), "ContinuousRestrictionGate", "block timing consults the continuous restriction gate");
    return Task.CompletedTask;
}

void RegisterCannotRestriction(EngineContext context, string effectId, HeadlessEntityId targetEntity, string restrictionKey)
{
    var sourceEntity = new HeadlessEntityId($"src:{effectId}");
    var request = new EffectRequest(
        new HeadlessEntityId(effectId),
        Opponent,
        "Continuous",
        new EffectContext(
            Opponent,
            Opponent,
            sourceEntity,
            triggerEntityId: null,
            targetEntityIds: new[] { targetEntity },
            values: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [restrictionKey] = true,
            }));

    context.EffectRegistry.Register(new EffectBinding(
        request,
        queryRoles: EffectQueryRole.Continuous,
        queryScopes: new[] { ContinuousRestrictionGate.Scope }));
}

int CountDeclareAttack(DcgoMatch match, HeadlessPlayerId playerId, HeadlessEntityId attackerId)
{
    return match.GetLegalActions(playerId)
        .Count(action => action.ActionType == HeadlessActionTypes.DeclareAttack
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackerId, out object? value)
            && value is HeadlessEntityId id
            && id == attackerId);
}

async Task<DcgoMatch> CreateMatchAsync(bool withBlocker)
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
        initialSecuritySize: 0);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, Player);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    if (withBlocker)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, [BattleResolver.DpKey] = 9000 });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, [BattleResolver.DpKey] = 3000 });
    if (withBlocker)
    {
        SetMetadata(match, BlockerId, new Dictionary<string, object?>
        {
            ["isSuspended"] = false,
            [BlockTiming.HasBlockerKey] = true,
            [BattleResolver.DpKey] = 5000
        });
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

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
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
