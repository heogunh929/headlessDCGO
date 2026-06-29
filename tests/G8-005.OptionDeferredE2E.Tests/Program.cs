using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-005: Option activation with an interactive DeferredChoiceProvider is an action-level commit-once /
// resolve-resume cycle: ProcessAsync pays the cost + moves the card ONCE and suspends at the choice
// (pending); after the agent answers, only the effect resolution resumes — the cost is NOT paid again.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Activate ST1_16 -> cost paid once + pending; answer -> apply, no re-pay", CommitOnceResumeApplies),
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

async Task CommitOnceResumeApplies()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 805, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_16"), "ST1_16", "Gaia Force", new Dictionary<string, object?>(), CardType: "Option", PlayCost: 2));
    var option = new HeadlessEntityId("p1:hand:ST1_16");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(option, new HeadlessEntityId("ST1_16"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, option, ChoiceZone.None, ChoiceZone.Hand));

    var b1 = await Opponent(context, "B1");
    var b2 = await Opponent(context, "B2");

    // Activate the option (pipeline action). Pays cost + moves; the select effect suspends at the choice.
    var optionAction = new OptionActivateAction();
    LegalAction action = optionAction.GetLegalActions(context, P1)
        .Single(a => ReadString(a.Parameters, HeadlessActionParameterKeys.CardId) == option.Value);
    ActionProcessResult activate = await optionAction.ProcessAsync(action, context);

    AssertTrue(activate.IsSuccess, "activation returned (pending)");
    AssertTrue(context.ChoiceController.Current.IsPending, "choice is pending for the agent");
    AssertEqual(3, context.MemoryController.Current.Current, "cost paid ONCE (5 - 2 = 3)");
    AssertTrue(InBattle(context, b1) && InBattle(context, b2), "nothing applied while pending");

    // The agent answers; the effect resolution resumes (no re-pay).
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(b1));
    await ActivatedEffectResolver.ResolveAsync(context, option, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, b1, ChoiceZone.Trash), "chosen B1 deleted after the answer");
    AssertTrue(InBattle(context, b2), "unchosen B2 untouched");
    AssertEqual(3, context.MemoryController.Current.Current, "cost NOT paid again on resume (still 3)");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> Opponent(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p2:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static string? ReadString(IReadOnlyDictionary<string, object?> p, string key) =>
    p.TryGetValue(key, out object? raw) ? raw?.ToString() : null;

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, ChoiceZone.BattleArea).Contains(id);

bool InZone(EngineContext context, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P2, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
