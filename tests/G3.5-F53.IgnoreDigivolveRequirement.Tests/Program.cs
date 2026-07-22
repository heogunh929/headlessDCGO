using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-5.3: a continuous "ignore digivolution requirement" effect lets a player digivolve without meeting
// the printed evolution condition (AS-IS Player.CanIgnoreDigivolutionRequirement). Wired into
// DigivolveAction.Validate. Player-scope (F-5) and card-targeted both honoured.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Evolve = new("p1:hand:E1");
HeadlessEntityId Target = new("p1:field:T1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Digivolve with a mismatched condition is illegal by default", IllegalWithoutIgnore),
    // (④) "Player-scope ignore-requirement allows the digivolve" / "Card-targeted ignore-requirement allows
    // the digivolve" RETIRED — both pinned the invented registry contract: they registered an EffectBinding
    // carrying the `ignoreDigivolutionRequirement` value-flag and relied on DigivolveAction reading it back via
    // ContinuousScopeEvaluation.ApplicableEffects. That old-model continuous collection reached producer 0 and
    // is now permanently inert (ApplicableEffects always returns empty); AS-IS has NO new-model live scan for a
    // general "ignore digivolution requirement" grant (only IgnoreColorConditionActive, a different axis), so
    // the "ignore allows" behaviour has no surviving equivalent to assert against. The real default-illegal
    // behaviour is retained below.
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

async Task IllegalWithoutIgnore()
{
    EngineContext context = await Setup();
    ActionProcessResult result = await new DigivolveAction().ProcessAsync(
        HeadlessActionFactory.Digivolve(P1, Evolve, Target, memoryCost: 2), context);
    AssertTrue(result.IsIllegal, "mismatched evolution condition is illegal without ignore");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Setup()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    // Evolving card requires "definition:NOPE" which the target does NOT satisfy.
    cards.Upsert(new CardRecord(new HeadlessEntityId("E1"), "E1", "Evolving", new Dictionary<string, object?>(),
        CardType: "Digimon", EvolutionCost: 2, EvolutionCondition: "definition:NOPE"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("T1"), "T1", "Base", new Dictionary<string, object?>(),
        CardType: "Digimon", PlayCost: 3));

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Evolve, new HeadlessEntityId("E1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Target, new HeadlessEntityId("T1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Evolve, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Target, ChoiceZone.None, ChoiceZone.BattleArea));
    return context;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
