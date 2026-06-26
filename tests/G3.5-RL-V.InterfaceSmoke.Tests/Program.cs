using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-V: end-to-end RL interface validation. Drives full self-play episodes the way a trainer
// would — masked-random policy over the FIXED FACTORED action space — and asserts the properties a
// learning loop depends on. Exercises A1 (legality boundary), A2 (choice), A3 (factored actions),
// A4 (perspective + card features), B1 (DP), B2/B3, C1 (deck-out terminal), C2 (battle) together.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const int MaxSteps = 600;

var tests = new (string Name, Func<Task> Body)[]
{
    ("Observation and action-mask dimensions are constant across an episode", DimensionsAreConstant),
    ("A full episode terminates with a shaped terminal reward", EpisodeTerminatesWithReward),
    ("Same seeds produce an identical trajectory (determinism)", TrajectoryIsDeterministic),
    ("Every stepped action came from the legal mask", AllSteppedActionsLegal),
    ("An out-of-mask factored index is rejected without state change (A1)", OutOfMaskIsRejected),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task DimensionsAreConstant()
{
    Episode ep = await RunEpisodeAsync(envSeed: 7, policySeed: 7);
    AssertTrue(ep.Steps > 0, "episode took at least one step");
    AssertEqual(1, ep.ObservationDims.Count, "observation vector length is constant across the episode");
    AssertEqual(1, ep.MaskSizes.Count, "factored action mask size is constant across the episode");
}

async Task EpisodeTerminatesWithReward()
{
    // Empty libraries (10 main cards = 5 hand + 5 security + 0 library) -> P2 decks out on turn 2.
    Episode ep = await RunEpisodeAsync(envSeed: 11, policySeed: 11);

    AssertTrue(ep.Terminal, "episode reached a real terminal (not just the step cap)");
    AssertEqual(0d, ep.FinalDiscount, "terminal discount is zero");
    AssertTrue(ep.FinalReward is -1d or 0d or 1d, "terminal reward is in {-1,0,1}");
    AssertEqual(1d, ep.FinalReward, "P1 wins because P2 decks out first (perspective reward +1)");
    AssertTrue(ep.NonTerminalRewardsAreZero, "non-terminal steps have zero reward (terminal-only shaping)");
}

async Task TrajectoryIsDeterministic()
{
    Episode a = await RunEpisodeAsync(envSeed: 23, policySeed: 5);
    Episode b = await RunEpisodeAsync(envSeed: 23, policySeed: 5);

    AssertEqual(a.Steps, b.Steps, "same step count");
    AssertEqual(a.Terminal, b.Terminal, "same terminal flag");
    AssertEqual(a.FinalReward, b.FinalReward, "same terminal reward");
    AssertEqual(a.Fingerprint, b.Fingerprint, "identical trajectory fingerprint");
}

async Task AllSteppedActionsLegal()
{
    Episode ep = await RunEpisodeAsync(envSeed: 31, policySeed: 9);
    AssertTrue(ep.AllActionsLegal, "no InvalidAction event occurred during a masked rollout");
}

async Task OutOfMaskIsRejected()
{
    HeadlessRlEnvironment env = BuildEnv(envSeed: 3);
    await env.InitializeAsync(BuildConfig(envSeed: 3));

    FactoredActionMask mask = env.EncodeFactoredActionMask();
    int illegalIndex = FirstUnusedIndex(mask);
    double[] obsBefore = env.Observe().ObservationVector;

    RlStepResult result = await env.StepByFactoredIndexAsync(illegalIndex);

    AssertTrue(result.Events.Any(e => e.Type == GameEventType.InvalidAction), "out-of-mask index is rejected");
    AssertFalse(result.IsTerminal, "state stays non-terminal after rejection");
    AssertEqual(
        string.Join(",", obsBefore),
        string.Join(",", env.Observe().ObservationVector),
        "observation unchanged after a rejected action");
}

// --- Rollout -------------------------------------------------------------

async Task<Episode> RunEpisodeAsync(int envSeed, int policySeed)
{
    var policyRng = new Random(policySeed);
    HeadlessRlEnvironment env = BuildEnv(envSeed);
    RlStepResult state = await env.InitializeAsync(BuildConfig(envSeed));

    var obsDims = new HashSet<int> { state.ObservationVector.Length };
    var maskSizes = new HashSet<int>();
    var fingerprint = new List<string>();
    bool allLegal = true;
    bool nonTerminalZero = true;
    int steps = 0;

    while (steps < MaxSteps && !state.IsTerminal)
    {
        FactoredActionMask mask = env.EncodeFactoredActionMask();
        maskSizes.Add(mask.Size);
        if (mask.Actions.Count == 0)
        {
            break; // no legal action available (stop reason: NoLegalActions)
        }

        FactoredAction chosen = mask.Actions[policyRng.Next(mask.Actions.Count)];
        state = await env.StepByFactoredIndexAsync(chosen.Index);
        steps++;

        if (state.Events.Any(e => e.Type == GameEventType.InvalidAction))
        {
            allLegal = false;
        }

        if (!state.IsTerminal && state.Reward != 0d)
        {
            nonTerminalZero = false;
        }

        obsDims.Add(state.ObservationVector.Length);
        fingerprint.Add($"{chosen.Index}:{state.Reward:R}:{(state.IsTerminal ? 1 : 0)}");
    }

    return new Episode(
        steps,
        state.IsTerminal,
        state.Reward,
        state.Discount,
        obsDims,
        maskSizes,
        allLegal,
        nonTerminalZero,
        string.Join("|", fingerprint));
}

HeadlessRlEnvironment BuildEnv(int envSeed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: envSeed);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 10; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context, new EngineTrace(), actionProcessor: null, actionLegality: new LegalActionSetValidator());
    var options = HeadlessRlEnvironmentOptions.Default with { PerspectivePlayerId = P1 };
    return new HeadlessRlEnvironment(match, options);
}

static MatchConfig BuildConfig(int envSeed)
{
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(new HeadlessPlayerId(1), "P1"), Deck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) }, randomSeed: envSeed, setup: setup);
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card",
        new Dictionary<string, object?> { ["dp"] = 5000, ["level"] = 3 }, CardType: "Digimon");

// 10 main -> 5 hand + 5 security + 0 library, so the player decks out on their first real draw.
static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 10).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static int FirstUnusedIndex(FactoredActionMask mask)
{
    var used = mask.Actions.Select(a => a.Index).ToHashSet();
    for (int i = 0; i < mask.Size; i++)
    {
        if (!used.Contains(i))
        {
            return i;
        }
    }

    return mask.Size; // out of range -> still rejected
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

internal sealed record Episode(
    int Steps,
    bool Terminal,
    double FinalReward,
    double FinalDiscount,
    HashSet<int> ObservationDims,
    HashSet<int> MaskSizes,
    bool AllActionsLegal,
    bool NonTerminalRewardsAreZero,
    string Fingerprint);
