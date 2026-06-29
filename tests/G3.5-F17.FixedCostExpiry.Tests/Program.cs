using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-1.7: a one-shot "until cost is calculated" cost modifier (EffectDuration.UntilCalculateFixedCost)
// applies to the next play's cost, then expires once a card is played (cost locked) — mirroring the
// AS-IS clear of Player.UntilCalculateFixedCostEffect on play.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Future = new("p1:hand:FUTURE"); // the card whose cost the temp effect will reduce

var tests = new (string Name, Func<Task> Body)[]
{
    ("Temp cost modifier reduces cost, then expires after a card is played", ExpiresOnPlay),
    ("EffectDurationExpiry.ExpireFixedCostCalc removes the binding directly", () => Pure(DirectExpire)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

async Task ExpiresOnPlay()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 31);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("PL"), "PL", "Playable", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 0));
    cards.Upsert(new CardRecord(new HeadlessEntityId("FUTURE"), "FUTURE", "Future", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 3));

    HeadlessEntityId playNow = new("p1:hand:PL");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(playNow, new HeadlessEntityId("PL"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Future, new HeadlessEntityId("FUTURE"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, playNow, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Future, ChoiceZone.None, ChoiceZone.Hand));

    // "Until cost is calculated: the next card you play costs 2 less" — targets FUTURE.
    RegisterTempCostReducer(context, Future, -2);
    AssertEqual(1, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "temp reducer applies before any play");

    // Play a (different) card — the fixed cost is now calculated, so the one-shot reducer expires.
    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, playNow, memoryCost: 0), context);
    AssertTrue(result.IsSuccess, $"play succeeded ({result.Message})");

    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "temp reducer expired after the play");
}

void DirectExpire()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 32);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Future, new HeadlessEntityId("FUTURE"), P1));
    RegisterTempCostReducer(context, Future, -2);

    AssertEqual(1, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "applies before expiry");
    int removed = EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
    AssertEqual(1, removed, "one fixed-cost binding removed");
    AssertEqual(3, ContinuousModifierGate.ResolvePlayCost(context, Future, basePlayCost: 3), "back to base after expiry");
}

// --- Helpers -------------------------------------------------------------

void RegisterTempCostReducer(EngineContext context, HeadlessEntityId target, int delta)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ModifierHelpers.PlayCostDeltaKey] = delta };
    var effectContext = new EffectContext(P1, P1, new HeadlessEntityId("temp-cost"),
        triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"tempcost:{target.Value}"), P1, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope },
        effect: null, duration: EffectDuration.UntilCalculateFixedCost));
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
