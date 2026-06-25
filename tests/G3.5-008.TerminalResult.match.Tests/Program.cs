using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predecessor goal G3.5-007 is complete", PredecessorGoalsComplete),
    ("Player status controller marks, reads, and resets the lose flag", PlayerStatusControllerLifecycle),
    ("Terminal evaluator is a no-op without an active player order or lose flag", TerminalEvaluatorNoOp),
    ("Terminal evaluator reports the opponent as winner when a player is marked lose", TerminalEvaluatorReportsLoseFlagWinner),
    ("End-turn check writes the terminal outcome from a lose flag", EndTurnCheckWritesTerminalOutcome),
    ("Direct attack with no security ends the match with the attacker as winner", DirectAttackOnEmptySecurityEndsMatch),
    ("Terminal result wiring source is clean", WiringSourceIsClean),
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
    AssertComplete("G3.5-007_continuous_restriction_legalaction_unit_test_results.md");
    return Task.CompletedTask;
}

Task PlayerStatusControllerLifecycle()
{
    var controller = new InMemoryHeadlessPlayerStatusController();
    AssertFalse(controller.IsLose(Opponent), "no lose flag initially");
    AssertEqual(0, controller.LosingPlayers.Count, "no losing players initially");

    controller.MarkLose(Opponent, "test reason");
    AssertTrue(controller.IsLose(Opponent), "lose flag set");
    AssertTrue(controller.TryGetLoseReason(Opponent, out string reason), "reason present");
    AssertEqual("test reason", reason, "reason value");

    // First reason wins; re-marking must not overwrite the original cause.
    controller.MarkLose(Opponent, "later reason");
    controller.TryGetLoseReason(Opponent, out string keptReason);
    AssertEqual("test reason", keptReason, "first reason preserved");

    AssertEqual(1, controller.LosingPlayers.Count, "one losing player");

    controller.ResetMatchState();
    AssertFalse(controller.IsLose(Opponent), "lose flag cleared after reset");
    return Task.CompletedTask;
}

Task TerminalEvaluatorNoOp()
{
    EngineContext empty = EngineContext.CreateDefault();
    AssertTrue(TerminalEvaluator.Evaluate(empty) is null, "no players => null");

    EngineContext context = EngineContext.CreateDefault();
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    AssertTrue(TerminalEvaluator.Evaluate(context) is null, "no lose flag => null");
    return Task.CompletedTask;
}

Task TerminalEvaluatorReportsLoseFlagWinner()
{
    EngineContext context = EngineContext.CreateDefault();
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    context.PlayerStatusController.MarkLose(Opponent, "marked for test");

    PlayerTerminalCheck? check = TerminalEvaluator.Evaluate(context);
    AssertTrue(check is not null, "terminal check produced");
    AssertTrue(check!.IsTerminal, "check is terminal");
    AssertEqual(PlayerTerminalReason.PlayerLoseFlag, check.Reason, "lose flag reason");
    AssertEqual(Player, check.WinnerPlayerId, "winner is opponent of loser");
    AssertEqual(Opponent, check.LosingPlayerId, "loser matches marked player");
    return Task.CompletedTask;
}

async Task EndTurnCheckWritesTerminalOutcome()
{
    EngineContext context = EngineContext.CreateDefault();
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    context.PlayerStatusController.MarkLose(Opponent, "marked for end-turn test");

    var flow = new GameFlowProcessor();
    FlowProcessResult result = await flow.RunToStableAsync(context);

    AssertTrue(result.ProgressedAny, "end-turn check makes progress on a terminal condition");
    AssertTrue(context.RuleQueryService.IsTerminal(), "rule query is terminal");

    var sink = (ITerminalOutcomeSink)context.RuleQueryService;
    AssertTrue(sink.TryGetTerminalOutcome(out TerminalOutcome? outcome) && outcome is not null, "terminal outcome stored");
    AssertEqual(Player, outcome!.WinnerPlayerId, "winner recorded in outcome");
    AssertFalse(outcome.IsDraw, "not a draw");
    AssertTrue(outcome.Reason.Length > 0, "outcome carries a reason message");
}

async Task DirectAttackOnEmptySecurityEndsMatch()
{
    DcgoMatch match = await CreateMatchAsync();

    // A direct attack against a player whose security stack is empty hits the player directly.
    match.Context.AttackController.DeclareAttack(
        Player,
        AttackerId,
        Opponent,
        targetId: null,
        isDirectAttack: true);

    StepResult step = await match.StepAsync();

    AssertTrue(step.IsTerminal, "match is terminal after the lethal direct attack");
    AssertTrue(match.IsTerminal(), "match reports terminal");

    MatchResult result = match.GetResult();
    AssertEqual(Player, result.WinnerId, "attacker is the winner");
    AssertFalse(result.IsDraw, "not a draw");
    AssertTrue(result.Reason.Length > 0, "match result carries a reason");

    AssertTrue(match.Context.PlayerStatusController.IsLose(Opponent), "defending player is marked lose");
}

Task WiringSourceIsClean()
{
    string evaluatorPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "TerminalEvaluator.cs");
    if (!File.Exists(evaluatorPath))
    {
        throw new FileNotFoundException($"Terminal evaluator source was not found: {evaluatorPath}");
    }

    string evaluator = File.ReadAllText(evaluatorPath);
    AssertFalse(evaluator.Contains("TODO", StringComparison.OrdinalIgnoreCase), "evaluator must not contain TODO");
    AssertFalse(evaluator.Contains("NotImplementedException", StringComparison.Ordinal), "evaluator must not throw NotImplementedException");
    AssertFalse(evaluator.Contains("UnityEngine", StringComparison.Ordinal), "evaluator must not reference UnityEngine");

    string flowPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameFlowProcessor.cs");
    AssertContains(File.ReadAllText(flowPath), "TerminalEvaluator", "end-turn check consults the terminal evaluator");

    string matchPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "DcgoMatch.cs");
    AssertContains(File.ReadAllText(matchPath), "TryGetTerminalOutcome", "match reads the terminal outcome into its result");
    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
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

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 91, setup: setup));
    await AdvanceToMainAsync(match, Player);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false });

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
