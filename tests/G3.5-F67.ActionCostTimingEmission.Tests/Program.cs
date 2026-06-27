using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.6 / F-6.7: action and cost timing windows. BeforePayCost/AfterPayCost wrap a card's cost payment
// (play / digivolve / option); OnUseOption opens when an Option card is used. Verified by running each
// action's processor and checking the timings the published events derive.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Constants are defined", () => Pure(ConstantsDefined)),
    ("PlayCard opens Before/AfterPayCost around the payment", PlayWrapsPayCost),
    ("Digivolve opens Before/AfterPayCost around the payment", DigivolveWrapsPayCost),
    ("ActivateOption opens OnUseOption and Before/AfterPayCost", OptionOpensUseAndPayCost),
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

void ConstantsDefined()
{
    foreach (string t in new[] { TriggerTimings.OnUseOption, TriggerTimings.BeforePayCost, TriggerTimings.AfterPayCost })
    {
        AssertTrue(!string.IsNullOrWhiteSpace(t), "constant non-empty");
    }
}

async Task PlayWrapsPayCost()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 51);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("PL"), "PL", "Playable", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 0));
    HeadlessEntityId card = new("p1:hand:PL");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("PL"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.Hand));
    context.GameEventQueue.DrainPending();

    ActionProcessResult result = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, card, memoryCost: 0), context);
    AssertTrue(result.IsSuccess, $"play succeeded ({result.Message})");

    var opened = OpenedTimings(context);
    AssertContains(opened, TriggerTimings.BeforePayCost, "BeforePayCost");
    AssertContains(opened, TriggerTimings.AfterPayCost, "AfterPayCost");
}

async Task DigivolveWrapsPayCost()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 52);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("E1"), "E1", "Evolving", new Dictionary<string, object?>(), CardType: "Digimon", EvolutionCost: 2));
    cards.Upsert(new CardRecord(new HeadlessEntityId("T1"), "T1", "Base", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 3));
    HeadlessEntityId evolve = new("p1:hand:E1");
    HeadlessEntityId target = new("p1:field:T1");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evolve, new HeadlessEntityId("E1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(target, new HeadlessEntityId("T1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evolve, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending();

    ActionProcessResult result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evolve, target, memoryCost: 2), context);
    AssertTrue(result.IsSuccess, $"digivolve succeeded ({result.Message})");

    var opened = OpenedTimings(context);
    AssertContains(opened, TriggerTimings.BeforePayCost, "BeforePayCost");
    AssertContains(opened, TriggerTimings.AfterPayCost, "AfterPayCost");
}

async Task OptionOpensUseAndPayCost()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 53);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("OPT"), "OPT", "An Option", new Dictionary<string, object?>(), CardType: "Option", PlayCost: 0));
    HeadlessEntityId opt = new("p1:hand:OPT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(opt, new HeadlessEntityId("OPT"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, opt, ChoiceZone.None, ChoiceZone.Hand));
    context.GameEventQueue.DrainPending();

    HeadlessEntityId effectId = new("OPT:option");
    ActionProcessResult result = await new OptionActivateAction().ProcessAsync(
        HeadlessActionFactory.ActivateOption(P1, opt, effectId, memoryCost: 0), context);
    AssertTrue(result.IsSuccess, $"option activated ({result.Message})");

    var opened = OpenedTimings(context);
    AssertContains(opened, TriggerTimings.OnUseOption, "OnUseOption");
    AssertContains(opened, TriggerTimings.BeforePayCost, "BeforePayCost");
    AssertContains(opened, TriggerTimings.AfterPayCost, "AfterPayCost");
}

// --- Helpers -------------------------------------------------------------

HashSet<string> OpenedTimings(EngineContext context)
{
    var opened = new HashSet<string>(StringComparer.Ordinal);
    foreach (GameEvent e in context.GameEventQueue.DrainPending())
    {
        foreach (string t in TriggerTimingMap.Derive(e))
        {
            opened.Add(t);
        }
    }

    return opened;
}

static void AssertContains(HashSet<string> opened, string timing, string label)
{
    if (!opened.Contains(timing)) throw new InvalidOperationException($"{label}: expected the {timing} window to open.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
