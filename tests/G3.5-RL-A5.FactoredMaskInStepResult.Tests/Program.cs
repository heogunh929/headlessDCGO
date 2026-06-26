using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A5: the factored action mask (A3) is now carried on every RlStepResult, so a MaskablePPO /
// MultiDiscrete trainer reads the per-position legality vector straight from each step — no separate
// EncodeFactoredActionMask() round-trip. The embedded mask is built from the same legal-action set as
// the type-based ActionMask, so the two always agree.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Reset/Observe carries a factored mask of the schema size", ResetCarriesFactoredMask),
    ("Embedded mask matches the standalone EncodeFactoredActionMask", EmbeddedMatchesStandalone),
    ("Every set bit in the factored vector resolves to a legal action", SetBitsResolveToActions),
    ("Stepping carries an updated, consistent factored mask", SteppingCarriesUpdatedMask),
    ("A custom factored schema is reflected in the step result", CustomSchemaReflected),
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

async Task ResetCarriesFactoredMask()
{
    HeadlessRlEnvironment env = BuildEnv(7);
    RlStepResult state = await env.InitializeAsync(BuildConfig(7));

    AssertNotNull(state.FactoredActionMask, "step result carries a factored mask");
    AssertEqual(FactoredActionSchema.Default.TotalSize, state.FactoredActionMask.Size, "mask size = default schema");
    AssertEqual(FactoredActionSchema.Default.TotalSize, state.FactoredActionMaskVector.Length, "vector length = schema size");
}

async Task EmbeddedMatchesStandalone()
{
    HeadlessRlEnvironment env = BuildEnv(7);
    RlStepResult state = await env.InitializeAsync(BuildConfig(7));

    FactoredActionMask standalone = env.EncodeFactoredActionMask();

    AssertEqual(standalone.Size, state.FactoredActionMask.Size, "same size");
    AssertSetEqual(
        standalone.Actions.Select(a => a.Index),
        state.FactoredActionMask.Actions.Select(a => a.Index),
        "embedded and standalone masks expose the same legal indices");
    AssertSequenceEqual(standalone.ToMaskVector(), state.FactoredActionMaskVector, "identical mask vectors");
}

async Task SetBitsResolveToActions()
{
    HeadlessRlEnvironment env = BuildEnv(7);
    RlStepResult state = await env.InitializeAsync(BuildConfig(7));

    double[] vector = state.FactoredActionMaskVector;
    int setBits = 0;
    for (int index = 0; index < vector.Length; index++)
    {
        if (vector[index] == 1d)
        {
            setBits++;
            AssertTrue(state.FactoredActionMask.TryGetAction(index, out _), $"index {index} resolves to a legal action");
        }
        else
        {
            AssertEqual(0d, vector[index], $"index {index} is 0 or 1");
        }
    }

    AssertTrue(setBits > 0, "at least one legal factored action at reset");
    AssertEqual(state.FactoredActionMask.Actions.Count, setBits, "set-bit count equals placed-action count");
}

async Task SteppingCarriesUpdatedMask()
{
    HeadlessRlEnvironment env = BuildEnv(7);
    RlStepResult state = await env.InitializeAsync(BuildConfig(7));

    FactoredAction chosen = state.FactoredActionMask.Actions[0];
    RlStepResult next = await env.StepByFactoredIndexAsync(chosen.Index);

    AssertNotNull(next.FactoredActionMask, "post-step result carries a factored mask");
    AssertEqual(FactoredActionSchema.Default.TotalSize, next.FactoredActionMask.Size, "post-step mask size constant");

    // The embedded mask reflects the new state — matching a fresh standalone encode.
    AssertSequenceEqual(
        env.EncodeFactoredActionMask().ToMaskVector(),
        next.FactoredActionMaskVector,
        "post-step embedded mask matches the standalone encode of the new state");
}

async Task CustomSchemaReflected()
{
    var schema = new FactoredActionSchema(maxHand: 8, maxField: 8, maxChoice: 8);
    HeadlessRlEnvironment env = BuildEnv(7, schema);
    RlStepResult state = await env.InitializeAsync(BuildConfig(7));

    AssertEqual(schema.TotalSize, state.FactoredActionMask.Size, "custom schema size is used");
    AssertEqual(schema.TotalSize, state.FactoredActionMaskVector.Length, "custom schema vector length");
    AssertTrue(schema.TotalSize != FactoredActionSchema.Default.TotalSize, "custom schema differs from default");
}

// --- Harness -------------------------------------------------------------

HeadlessRlEnvironment BuildEnv(int envSeed, FactoredActionSchema? schema = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: envSeed);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 10; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context, new EngineTrace(), actionProcessor: null, actionLegality: new LegalActionSetValidator());
    HeadlessRlEnvironmentOptions options = HeadlessRlEnvironmentOptions.Default with { PerspectivePlayerId = P1 };
    if (schema is not null)
    {
        options = options with { FactoredActionSchema = schema };
    }

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

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 10).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertNotNull(object? value, string label)
{
    if (value is null) throw new InvalidOperationException($"{label}: expected non-null.");
}

static void AssertSetEqual(IEnumerable<int> expected, IEnumerable<int> actual, string label)
{
    if (!expected.OrderBy(x => x).SequenceEqual(actual.OrderBy(x => x)))
    {
        throw new InvalidOperationException($"{label}: index sets differ.");
    }
}

static void AssertSequenceEqual(double[] expected, double[] actual, string label)
{
    if (expected.Length != actual.Length || !expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{label}: vectors differ.");
    }
}
