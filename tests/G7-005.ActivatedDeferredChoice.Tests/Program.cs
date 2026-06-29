using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G7-005: with an interactive DeferredChoiceProvider, an activated select effect SUSPENDS at the choice
// (nothing applied, choice surfaced to the agent), then RESUMES and applies once the agent answers and the
// resolver is re-invoked (W7 suspend/resume at the activated-effect layer).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_16 select-delete suspends then resumes via DeferredChoiceProvider", SuspendResumeCycle),
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

async Task SuspendResumeCycle()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 705, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_16"), "ST1_16", "ST1_16", new Dictionary<string, object?>(), CardType: "Option"));
    var option = new HeadlessEntityId("p1:trash:ST1_16");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(option, new HeadlessEntityId("ST1_16"), P1));

    var b1 = await OpponentDigimon(context, "B1");
    var b2 = await OpponentDigimon(context, "B2");

    // Pass 1: the select effect suspends at the choice.
    bool suspended = false;
    try
    {
        await ActivatedEffectResolver.ResolveAsync(context, option, P1, EffectTiming.OptionSkill);
    }
    catch (DeferredChoicePendingException)
    {
        suspended = true;
    }

    AssertTrue(suspended, "resolver suspended at the choice");
    AssertTrue(context.ChoiceController.Current.IsPending, "choice surfaced to the agent");
    AssertTrue(InBattle(context, b1) && InBattle(context, b2), "nothing applied while suspended");

    // The agent answers the pending choice.
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(b1));

    // Pass 2: the resolver replays the answer and applies.
    await ActivatedEffectResolver.ResolveAsync(context, option, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, b1, ChoiceZone.Trash), "chosen B1 deleted after the answer");
    AssertTrue(InBattle(context, b2), "unchosen B2 untouched");
    AssertTrue(!context.ChoiceController.Current.IsPending, "no pending choice remains");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> OpponentDigimon(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p2:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, ChoiceZone.BattleArea).Contains(id);

bool InZone(EngineContext context, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
