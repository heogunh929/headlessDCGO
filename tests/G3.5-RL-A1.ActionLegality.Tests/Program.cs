using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
    // 4b B3: "Without a validator the apply path keeps legacy behavior" RETIRED — its verification target
    // is the OLD unguarded (no-validator) basic-ctor apply-queuing path, which B6-Db deletes when the basic
    // DcgoMatch ctor flips to pump (validator defaults ON). The opt-in nature of the boundary is now carried
    // by CreatePumpDriven(enforceActionLegality:) instead. (design principle 2 — retire on target vanish.)
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

async Task RlEnvironmentEnforcesBoundary()
{
    // 4b B3 RL re-aim: the RL env now wraps the pump-driven match (its production default); the boundary
    // still surfaces an InvalidAction for the OLD step-cadence EndTurn (pump owns cadence, EndTurn illegal)
    // with no state change. AdvanceEnvToMainAsync drives the pump's auto-flow (AdvancePhase step retired).
    var env = new HeadlessRlEnvironment(
        DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(), new EngineTrace()));
    HeadlessPlayerId player = new(1);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceEnvToMainAsync(env, player);

    HeadlessPlayerId turnPlayer = env.Match.Context.TurnController.Current.TurnPlayerId!.Value;
    string[] legalBefore = LegalActionTypes(env.Match, turnPlayer);
    RlStepResult result = await env.StepAsync(HeadlessActionFactory.EndTurn(turnPlayer));

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
    // 4b B3 RL re-aim: the boundary contract is now pinned on the pump surface (the OLD validated-step
    // cadence is retired). CreatePumpDriven installs the SAME LegalActionSetValidator boundary by default.
    DcgoMatch match = DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(), new EngineTrace());
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

// --- Phase driving (pump auto-flow, F62/C1-Witness precedent, 4b B3 RL re-aim) ---
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired (4b B6 gate). Breeding/Mulligan decisions are declined.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

// Env counterpart: resolve every pre-Main pump decision (mulligan keep / breeding decline) via the skip
// lane through the RL env's own StepAsync until the Main action table opens.
static async Task AdvanceEnvToMainAsync(HeadlessRlEnvironment env, HeadlessPlayerId playerId)
{
    DcgoMatch match = env.Match;
    for (int i = 0; i < 32 && !AtMainWaitOf(match, playerId); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? skip;
            using (AmbientMatchContext.Enter(match.Context))
            {
                skip = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (skip is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
            await env.StepAsync(skip);
        }
        else
        {
            using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
            await match.StepAsync();
        }
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance env to main");
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
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
