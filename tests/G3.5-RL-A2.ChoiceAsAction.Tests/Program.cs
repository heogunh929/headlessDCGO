using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A2: Expose choices as agent actions (fixes P0-1).
// A pending choice is surfaced as ResolveChoice legal actions; the agent's action carries the
// selection and decides the outcome (instead of a scripted provider deciding internally).

var tests = new (string Name, Func<Task> Body)[]
{
    ("Pending choice surfaces one ResolveChoice action per candidate plus skip", PendingChoiceSurfacesResolveActions),
    ("Pending choice is exposed only to the choice owner", ChoiceExposedOnlyToOwner),
    ("Agent selection decides the resolved outcome", AgentSelectionDecidesOutcome),
    ("Agent can skip a skippable choice", AgentCanSkipChoice),
    ("RL environment surfaces and resolves a choice end-to-end", RlEnvironmentResolvesChoiceEndToEnd),
    ("Boundary rejects ResolveChoice when no choice is pending", BoundaryRejectsResolveWithoutPendingChoice),
    ("PUMP: the real StartGame mulligan is a choice-as-action with the same contract (owner-only, skip lane, agent answer decides)", PumpMulliganChoiceIsAnAgentAction),
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

async Task PendingChoiceSurfacesResolveActions()
{
    DcgoMatch match = new();
    await InitializeAsync(match);
    HeadlessPlayerId player = new(1);

    await RequestChoiceAsync(match, player, canSkip: true, "c1", "c2", "c3");

    IReadOnlyList<LegalAction> legal = match.GetLegalActions(player);
    AssertEqual(4, legal.Count, "3 candidate picks + 1 skip");
    AssertTrue(legal.All(a => a.ActionType == HeadlessActionTypes.ResolveChoice), "all actions are ResolveChoice");
    AssertEqual(1, legal.Count(a => IsSkip(a)), "exactly one skip action");
    AssertEqual(3, legal.Count(a => !IsSkip(a)), "three candidate-pick actions");
}

async Task ChoiceExposedOnlyToOwner()
{
    DcgoMatch match = new();
    await InitializeAsync(match);
    HeadlessPlayerId owner = new(1);
    HeadlessPlayerId other = new(2);

    await RequestChoiceAsync(match, owner, canSkip: false, "c1", "c2");

    AssertEqual(2, match.GetLegalActions(owner).Count, "owner sees the resolve actions");
    AssertEqual(0, match.GetLegalActions(other).Count, "non-owner sees nothing while a choice is pending");
}

async Task AgentSelectionDecidesOutcome()
{
    DcgoMatch match = new();
    await InitializeAsync(match);
    HeadlessPlayerId player = new(1);

    await RequestChoiceAsync(match, player, canSkip: false, "c1", "c2");

    // Pick the SECOND candidate explicitly; the resolved state must reflect that exact choice.
    LegalAction pickC2 = match.GetLegalActions(player)
        .Single(a => SelectedIdsOf(a).SequenceEqual(new[] { "c2" }));

    await match.ApplyActionAsync(pickC2);
    await match.StepAsync();

    HeadlessChoiceState choice = match.Context.ChoiceController.Current;
    AssertFalse(choice.IsPending, "choice no longer pending after resolve");
    AssertTrue(choice.IsResolved, "choice marked resolved");
    AssertFalse(choice.IsSkipped, "resolve was a selection, not a skip");
    AssertSequence(new[] { "c2" }, choice.SelectedIds.Select(id => id.Value).ToArray(), "agent's selection is applied");
}

async Task AgentCanSkipChoice()
{
    DcgoMatch match = new();
    await InitializeAsync(match);
    HeadlessPlayerId player = new(1);

    await RequestChoiceAsync(match, player, canSkip: true, "c1", "c2");

    LegalAction skip = match.GetLegalActions(player).Single(IsSkip);
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    HeadlessChoiceState choice = match.Context.ChoiceController.Current;
    AssertFalse(choice.IsPending, "choice resolved after skip");
    AssertTrue(choice.IsSkipped, "choice recorded as skipped");
}

async Task RlEnvironmentResolvesChoiceEndToEnd()
{
    // LEGACY TEST SCAFFOLD (R4 S3c-d1): this witness injects a synthetic RequestChoice on an idle OLD-driver
    // match; on the pump-driven DEFAULT match the pump would already hold the real StartGame mulligan choice
    // here. Pin an explicit legacy validated match — the pump path has its own choice witnesses (S3a/S3b).
    var env = new HeadlessRlEnvironment(DcgoMatch.CreateValidated(
        HeadlessDCGO.Engine.Headless.Bridge.EngineContext.CreateDefault(),
        new HeadlessDCGO.Engine.Headless.Diagnostics.EngineTrace()));
    await env.InitializeAsync(BuildMatchConfig());
    HeadlessPlayerId player = new(1);

    // Drive a pending choice through the underlying match (RequestChoice is an engine/system action).
    await RequestChoiceAsync(env.Match, player, canSkip: false, "c1", "c2");

    // The action mask now exposes ResolveChoice actions to the policy.
    IReadOnlyList<LegalAction> offered = env.Match.GetLegalActions(player);
    AssertEqual(2, offered.Count, "RL env exposes resolve actions while choice pending");

    LegalAction pickC1 = offered.Single(a => SelectedIdsOf(a).SequenceEqual(new[] { "c1" }));
    RlStepResult result = await env.StepAsync(pickC1);

    AssertFalse(result.HasPendingChoice, "choice resolved through the RL step");
    HeadlessChoiceState choice = env.Match.Context.ChoiceController.Current;
    AssertTrue(choice.IsResolved, "RL-resolved choice marked resolved");
    AssertSequence(new[] { "c1" }, choice.SelectedIds.Select(id => id.Value).ToArray(), "RL step applied the agent's selection");
}

async Task BoundaryRejectsResolveWithoutPendingChoice()
{
    // Validated match (A1 boundary) + no pending choice -> ResolveChoice is not offered -> rejected.
    DcgoMatch match = new(
        HeadlessDCGO.Engine.Headless.Bridge.EngineContext.CreateDefault(),
        new HeadlessDCGO.Engine.Headless.Diagnostics.EngineTrace(),
        actionProcessor: null,
        actionLegality: new LegalActionSetValidator());
    await InitializeAsync(match);
    HeadlessPlayerId player = new(1);

    StepResult result = await match.ApplyActionAsync(
        HeadlessActionFactory.ResolveChoice(player, ChoiceResult.Select(new HeadlessEntityId("ghost"))));

    AssertTrue(
        result.Events.Any(e => e.Type == GameEventType.InvalidAction),
        "ResolveChoice without a pending choice is rejected at the boundary");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no choice was created");
}

async Task PumpMulliganChoiceIsAnAgentAction()
{
    // (RL-B1) PUMP WITNESS: on the pump-driven match the FIRST decision point is the pump's own
    // StartGame mulligan — a real engine-raised choice, not a synthetic RequestChoice. The
    // choice-as-action contract must have the SAME shape there: the pending choice surfaces as
    // ResolveChoice legal actions to the owner only, a skip lane mirrors CanSkip, an out-of-table
    // crafted answer is rejected without touching the pending choice, and the agent's answer (keep
    // AND redraw) decides the outcome. Existing cases above pin the legacy scaffold unchanged.
    var env = new HeadlessRlEnvironment(DcgoMatch.CreatePumpDriven(
        CreateStarterContext(seed: 29),
        new HeadlessDCGO.Engine.Headless.Diagnostics.EngineTrace()));
    DcgoMatch match = env.Match;
    await env.InitializeAsync(BuildStarterMatchConfig(seed: 29));

    // 1) The pump parks at the mulligan; the choice surfaces as ResolveChoice actions, owner-only.
    AssertTrue(match.HasPendingChoice(), "pump parks at a pending choice after initialize");
    ChoiceRequest first = match.Context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.Mulligan, first.Type, "first pump decision is the StartGame mulligan");
    HeadlessPlayerId owner = first.PlayerId;
    HeadlessPlayerId other = owner.Value == 1 ? new HeadlessPlayerId(2) : new HeadlessPlayerId(1);

    IReadOnlyList<LegalAction> offered = match.GetLegalActions(owner);
    AssertTrue(offered.Count >= 2, "mulligan offers at least redraw + keep");
    AssertTrue(offered.All(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "the whole pump choice table is ResolveChoice actions");
    AssertEqual(1, offered.Count(IsSkip), "exactly one keep(skip) lane");
    AssertEqual(0, match.GetLegalActions(other).Count, "non-owner sees nothing while the pump choice pends");

    // 2) A crafted out-of-table answer is rejected and the pending choice is untouched.
    RlStepResult rejected = await env.StepAsync(
        HeadlessActionFactory.ResolveChoice(owner, ChoiceResult.Select(new HeadlessEntityId("ghost"))));
    AssertTrue(rejected.Events.Any(e => e.Type == GameEventType.InvalidAction),
        "ghost-candidate ResolveChoice is rejected at the boundary");
    AssertTrue(match.HasPendingChoice(), "mulligan still pending after the reject");
    AssertEqual(owner, match.Context.ChoiceController.PendingRequest!.PlayerId,
        "same owner still holds the choice after the reject");

    // 3) The agent's answers decide the outcome: owner KEEPS (skip), the other player REDRAWS (pick).
    await env.StepAsync(offered.Single(IsSkip));
    AssertTrue(match.HasPendingChoice(), "the pump hands the second mulligan over");
    AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "second mulligan type");
    AssertEqual(other, match.Context.ChoiceController.PendingRequest!.PlayerId, "second mulligan owner");

    LegalAction redraw = match.GetLegalActions(other).Single(a => !IsSkip(a));
    await env.StepAsync(redraw);

    // 4) Both answers landed in the engine: the pump only deals security 5/5 AFTER the mulligans resolve.
    AssertEqual(5, CountZone(match, owner, ChoiceZone.Security), "security dealt to the keeper after both answers");
    AssertEqual(5, CountZone(match, other, ChoiceZone.Security), "security dealt to the redrawer after both answers");
    AssertEqual(5, CountZone(match, other, ChoiceZone.Hand), "redraw refilled the hand to 5");
}

// --- Helpers -------------------------------------------------------------

// (RL-B1) Real ST1/ST2 starter decks for the pump witness — the pump's StartGameAsync owns
// hands/mulligan/security, so it needs real card definitions (CardBaseEntity), not synthetic ids.
static HeadlessDCGO.Engine.Headless.Bridge.EngineContext CreateStarterContext(int seed)
{
    HeadlessDCGO.Engine.Headless.Bridge.EngineContext context =
        HeadlessDCGO.Engine.Headless.Bridge.EngineContext.CreateDefault(randomSeed: seed);
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

static int CountZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}

static async Task RequestChoiceAsync(
    DcgoMatch match,
    HeadlessPlayerId player,
    bool canSkip,
    params string[] candidateIds)
{
    LegalAction request = HeadlessActionFactory.RequestChoice(
        player,
        ChoiceType.Card,
        message: "pick",
        minCount: 1,
        maxCount: 1,
        canSkip: canSkip,
        sourceZone: ChoiceZone.Hand,
        candidateIds: candidateIds.Select(id => new HeadlessEntityId(id)).ToArray());

    await match.ApplyActionAsync(request);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "choice is pending after RequestChoice");
}

static bool IsSkip(LegalAction action)
{
    return action.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped);
}

static string[] SelectedIdsOf(LegalAction action)
{
    if (!action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) ||
        raw is not IEnumerable<HeadlessEntityId> ids)
    {
        return Array.Empty<string>();
    }

    return ids.Select(id => id.Value).ToArray();
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
