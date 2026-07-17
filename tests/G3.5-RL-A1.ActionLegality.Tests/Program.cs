using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A1: Single authoritative agent-action legality boundary (fixes P0-3).
// The boundary shares one predicate with legal-action generation, rejects out-of-set agent
// actions at apply time without mutating state, and leaves engine-internal actions untouched.

var tests = new (string Name, Func<Task> Body)[]
{
    ("Validator accepts every dispatcher-generated legal action", ValidatorAcceptsEveryGeneratedLegalAction),
    ("Validator rejects an out-of-set agent action (shared predicate)", ValidatorRejectsOutOfSetAgentAction),
    ("Validator defers engine-internal action types (non-breaking)", ValidatorDefersInternalActionTypes),
    ("Boundary rejects illegal agent action at apply with no state change", BoundaryRejectsIllegalActionWithoutStateChange),
    ("Crafted SpecialPlay not in the legal set is rejected with no state change", CraftedSpecialPlayRejectedWithoutStateChange),
    ("Boundary accepts a legal agent action", BoundaryAcceptsLegalAction),
    ("Without a validator the apply path keeps legacy behavior", LegacyApplyPathIsUnaffected),
    ("RL environment enforces the boundary and leaves state unchanged on reject", RlEnvironmentEnforcesBoundary),
    ("PUMP: same boundary contract on the pump surface (table validates; crafted/step-cadence rejected, no state change)", PumpBoundaryEnforcesSameContract),
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

// --- Tests ---------------------------------------------------------------

async Task ValidatorAcceptsEveryGeneratedLegalAction()
{
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    var validator = new LegalActionSetValidator();
    IReadOnlyList<LegalAction> legal = match.GetLegalActions(player);
    AssertTrue(legal.Count >= 1, "main phase exposes at least one legal action");

    foreach (LegalAction action in legal)
    {
        LegalityVerdict verdict = validator.Validate(action, match.Context);
        AssertTrue(verdict.IsLegal, $"generated action '{action.ActionType}' must validate as legal");
    }
}

async Task ValidatorRejectsOutOfSetAgentAction()
{
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    var validator = new LegalActionSetValidator();
    // EndTurn is an agent-facing type but is NOT legal during the Main phase.
    LegalityVerdict verdict = validator.Validate(HeadlessActionFactory.EndTurn(player), match.Context);
    AssertFalse(verdict.IsLegal, "EndTurn during Main phase must be rejected");
    AssertTrue(verdict.Reason.Length > 0, "rejection carries a reason");
}

async Task ValidatorDefersInternalActionTypes()
{
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    var validator = new LegalActionSetValidator();
    LegalAction[] internalActions =
    {
        HeadlessActionFactory.PayMemory(player, 1),
        HeadlessActionFactory.SetMemory(player, -2),
        HeadlessActionFactory.AddMemory(player, 1),
        HeadlessActionFactory.ShuffleDeck(player),
        HeadlessActionFactory.ClearChoice(player),
    };

    foreach (LegalAction action in internalActions)
    {
        LegalityVerdict verdict = validator.Validate(action, match.Context);
        AssertTrue(verdict.IsLegal, $"internal action '{action.ActionType}' must be deferred (legal) by the boundary");
    }
}

async Task BoundaryRejectsIllegalActionWithoutStateChange()
{
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    string[] legalBefore = LegalActionTypes(match, player);
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "phase before reject");

    StepResult result = await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(player));

    AssertTrue(HasInvalidActionEvent(result), "apply returns an InvalidAction event");
    AssertEqual(0, match.PendingActions().Count, "rejected action is not enqueued");
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "phase unchanged after reject");
    AssertFalse(match.IsTerminal(), "match not terminal after reject");
    AssertFalse(match.HasPendingChoice(), "no pending choice after reject");
    AssertSequence(legalBefore, LegalActionTypes(match, player), "legal action set unchanged after reject");
}

async Task CraftedSpecialPlayRejectedWithoutStateChange()
{
    // G11-001: a forged SpecialPlay action (no recipe / materials -> the dispatcher offers none) is inside
    // the agent-facing boundary, so it must be REJECTED at apply with no state change — not silently let
    // through. Hardens the SpecialPlay legality boundary against crafted actions.
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    AssertFalse(
        match.GetLegalActions(player).Any(a => a.ActionType == HeadlessActionTypes.SpecialPlay),
        "no SpecialPlay is legal on this board (precondition)");

    string[] legalBefore = LegalActionTypes(match, player);
    var crafted = new LegalAction(
        new HeadlessEntityId("crafted:specialplay"),
        player,
        HeadlessActionTypes.SpecialPlay,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardId"] = "forged:card",
            [SpecialPlayAction.MaterialsKey] = "forged:mat1,forged:mat2",
        });

    StepResult result = await match.ApplyActionAsync(crafted);

    AssertTrue(HasInvalidActionEvent(result), "crafted SpecialPlay returns an InvalidAction event");
    AssertEqual(0, match.PendingActions().Count, "crafted SpecialPlay is not enqueued");
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "phase unchanged after reject");
    AssertFalse(match.IsTerminal(), "match not terminal after reject");
    AssertSequence(legalBefore, LegalActionTypes(match, player), "legal action set unchanged after reject");
}

async Task BoundaryAcceptsLegalAction()
{
    DcgoMatch match = await CreateValidatedMatchAsync();
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    LegalAction pass = SingleLegalAction(match, player, HeadlessActionTypes.Pass);
    StepResult result = await match.ApplyActionAsync(pass);

    AssertFalse(HasInvalidActionEvent(result), "legal Pass is not rejected at the boundary");
}

async Task LegacyApplyPathIsUnaffected()
{
    // No validator supplied -> legacy behavior: the action is queued, not boundary-rejected.
    DcgoMatch match = new();
    await InitializeAsync(match);
    HeadlessPlayerId player = new(1);
    await AdvanceToMainAsync(match, player);

    StepResult result = await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(player));
    AssertFalse(HasInvalidActionEvent(result), "legacy apply path emits no boundary rejection");
    AssertTrue(
        result.Events.Any(e => e.Type == GameEventType.ActionQueued),
        "legacy apply path queues the action");
}

async Task RlEnvironmentEnforcesBoundary()
{
    // LEGACY TEST SCAFFOLD (R4 S3c-d1): this witness drives the OLD step cadence (AdvanceEnvToMainAsync
    // issues AdvancePhase), so it pins an explicit legacy validated match — the RL environment's DEFAULT
    // match is pump-driven now (DcgoMatch.CreatePumpDriven), where AdvancePhase/EndTurn are illegal.
    var env = new HeadlessRlEnvironment(
        DcgoMatch.CreateValidated(EngineContext.CreateDefault(), new EngineTrace()));
    HeadlessPlayerId player = new(1);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceEnvToMainAsync(env, player);

    string[] legalBefore = LegalActionTypes(env.Match, player);
    RlStepResult result = await env.StepAsync(HeadlessActionFactory.EndTurn(player));

    AssertTrue(
        result.Events.Any(e => e.Type == GameEventType.InvalidAction),
        "RL env surfaces an InvalidAction event for the illegal step");
    AssertFalse(result.IsTerminal, "RL env state stays non-terminal after rejected step");
    AssertEqual(HeadlessPhase.Main, env.Match.GetObservation().Turn.Phase, "phase unchanged in RL env");
    AssertEqual(0, env.Match.PendingActions().Count, "no action queued in RL env after reject");
    AssertSequence(legalBefore, LegalActionTypes(env.Match, player), "RL env legal set unchanged after reject");
}

async Task PumpBoundaryEnforcesSameContract()
{
    // (RL-B1) PUMP WITNESS: the action-legality table and the single authoritative boundary have the
    // SAME shape on the pump surface — every dispatcher-generated action validates against the shared
    // predicate, and a crafted action outside the table (including the OLD step-cadence AdvancePhase /
    // EndTurn, which the pump owns) is rejected at apply with zero state mutation. Existing cases above
    // pin the legacy scaffold unchanged; this case is the pump-cadence counterpart.
    var env = new HeadlessRlEnvironment(
        DcgoMatch.CreatePumpDriven(CreateStarterContext(seed: 23), new EngineTrace()));
    DcgoMatch match = env.Match;
    await env.InitializeAsync(BuildStarterMatchConfig(seed: 23));
    await DrivePumpToMainAsync(env);

    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    var validator = new LegalActionSetValidator();
    IReadOnlyList<LegalAction> legal = match.GetLegalActions(turnPlayer);
    AssertTrue(legal.Count >= 1, "pump main phase exposes at least one legal action");
    AssertTrue(legal.Any(a => a.ActionType == HeadlessActionTypes.Pass), "pump main table offers Pass");
    foreach (LegalAction action in legal)
    {
        AssertTrue(
            validator.Validate(action, match.Context).IsLegal,
            $"pump-generated action '{action.ActionType}' must validate as legal (shared predicate)");
    }

    string[] legalBefore = LegalActionTypes(match, turnPlayer);
    LegalAction[] crafted =
    {
        HeadlessActionFactory.AdvancePhase(turnPlayer),   // legal on the OLD cadence, pump-illegal
        HeadlessActionFactory.EndTurn(turnPlayer),        // legal on the OLD cadence, pump-illegal
        new LegalAction(
            new HeadlessEntityId("crafted:specialplay"),
            turnPlayer,
            HeadlessActionTypes.SpecialPlay,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["cardId"] = "forged:card",
                [SpecialPlayAction.MaterialsKey] = "forged:mat1,forged:mat2",
            }),
    };

    foreach (LegalAction action in crafted)
    {
        RlStepResult rejected = await env.StepAsync(action);
        AssertTrue(
            rejected.Events.Any(e => e.Type == GameEventType.InvalidAction),
            $"pump boundary rejects crafted '{action.ActionType}'");
        AssertFalse(rejected.IsTerminal, $"match not terminal after rejected '{action.ActionType}'");
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "phase unchanged after pump rejects");
    AssertEqual(0, match.PendingActions().Count, "no crafted action was enqueued");
    AssertFalse(match.HasPendingChoice(), "no pending choice materialized from rejects");
    AssertSequence(legalBefore, LegalActionTypes(match, turnPlayer), "pump legal set unchanged after rejects");

    // And a table-member action passes the SAME boundary.
    LegalAction pass = match.GetLegalActions(turnPlayer).First(a => a.ActionType == HeadlessActionTypes.Pass);
    RlStepResult accepted = await env.StepAsync(pass);
    AssertFalse(
        accepted.Events.Any(e => e.Type == GameEventType.InvalidAction),
        "legal pump Pass is not rejected at the boundary");
}

// --- Helpers -------------------------------------------------------------

// (RL-B1) Real ST1/ST2 starter decks: the pump's StartGameAsync owns hands/mulligan/security, so the
// pump witness needs real card definitions (CardBaseEntity) rather than the synthetic deck ids the
// legacy scaffold cases use.
static EngineContext CreateStarterContext(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    return context;
}

static MatchConfig BuildStarterMatchConfig(int seed)
{
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(new HeadlessPlayerId(1), d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(new HeadlessPlayerId(2), d2.MainDefinitions, d2.DigitamaDefinitions),
        }, firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) }, randomSeed: seed, setup: setup);
}

// Resolve every pump setup decision (mulligan keep / breeding decline) with the skip lane until the
// main-phase action table opens. Bounded so a wedged pump fails the witness instead of hanging it.
static async Task DrivePumpToMainAsync(HeadlessRlEnvironment env)
{
    DcgoMatch match = env.Match;
    for (int i = 0; i < 16; i++)
    {
        if (match.GetObservation().Turn.Phase == HeadlessPhase.Main && !match.HasPendingChoice())
        {
            return;
        }

        AssertTrue(match.HasPendingChoice(), "pump decision points before Main are choices");
        HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
        LegalAction skip = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped));
        await env.StepAsync(skip);
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "pump reached the Main action table");
}

static async Task<DcgoMatch> CreateValidatedMatchAsync()
{
    DcgoMatch match = new(
        EngineContext.CreateDefault(),
        new EngineTrace(),
        actionProcessor: null,
        actionLegality: new LegalActionSetValidator());
    await InitializeAsync(match);
    return match;
}

static async Task InitializeAsync(DcgoMatch match)
{
    await match.InitializeAsync(BuildMatchConfig());
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
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

static async Task AdvanceEnvToMainAsync(HeadlessRlEnvironment env, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && env.Match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(env.Match, playerId, HeadlessActionTypes.AdvancePhase);
        await env.StepAsync(advance);
    }

    AssertEqual(HeadlessPhase.Main, env.Match.GetObservation().Turn.Phase, "advance env to main");
}

static LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId playerId, string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

static string[] LegalActionTypes(DcgoMatch match, HeadlessPlayerId playerId)
{
    return match.GetLegalActions(playerId)
        .Select(a => a.ActionType)
        .OrderBy(v => v, StringComparer.Ordinal)
        .ToArray();
}

static bool HasInvalidActionEvent(StepResult result)
{
    return result.Events.Any(e => e.Type == GameEventType.InvalidAction);
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{label}: expected {expected.Count} items, got {actual.Count}.");
    }

    for (var i = 0; i < expected.Count; i++)
    {
        if (!Equals(expected[i], actual[i]))
        {
            throw new InvalidOperationException($"{label}: index {i} expected '{expected[i]}', got '{actual[i]}'.");
        }
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
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
