using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-010 (EX8-074 final): the real 1:1 ported card EX8_074, verified region by region through the live /
// activation mechanisms built across the bundle. Card number "EX8_074" resolves to the ported class by
// reflection dispatch.
//   #1 BeforePayCost  -> playing it suspends 2 and pays the reduced cost (LIVE)
//   #1 availability    -> offered as a legal play when full cost is unaffordable but reduced is (LIVE)
//   #3 Alliance        -> registers as a live keyword binding on enter-play
//   #4 Vortex          -> registers (OnEndTurn) and opens the end-of-turn attack window (LIVE)
//   #5 When Digivolving-> suspend 1, then delete a <=8000(+3000/suspended) opponent (activation flow)
//   #6 All Turns       -> ReuseWhenDigivolving re-runs the [When Digivolving] effects (activation flow)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string EX = "EX8_074";

var tests = new (string Name, Func<Task> Body)[]
{
    ("#1 BeforePayCost: playing EX8_074 suspends 2 Digimon and pays the reduced cost (6 -> 2)", BeforePayCostLive),
    ("#1 availability: EX8_074 is a legal play when reduced cost is affordable though full is not", AvailabilityLive),
    ("#3 Alliance: enters play with a live Alliance keyword binding", AllianceRegistered),
    ("#4 Vortex: registers at enter-play and opens the end-of-turn effect attack window", VortexLive),
    ("#5 When Digivolving: suspend 1 then delete a 10000 opponent (cap raised by the suspend)", WhenDigivolvingDelete),
    ("#6 All Turns: ReuseWhenDigivolving re-runs the [When Digivolving] suspend+delete", AllTurnsReuse),
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

async Task BeforePayCostLive()
{
    EngineContext context = Context();
    context.MemoryController.Set(8);
    var hand = await PlaceCard(context, P1, EX, zone: ChoiceZone.Hand, dp: 6000, playCost: 6);
    var d1 = await PlaceCard(context, P1, "U1", ChoiceZone.BattleArea, dp: 4000);
    var d2 = await PlaceCard(context, P1, "U2", ChoiceZone.BattleArea, dp: 4000);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(d1, d2));

    ActionProcessResult r = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, hand, 6), context);
    AssertTrue(r.IsSuccess, $"play succeeded ({r.Message})");
    AssertTrue(IsSuspended(context, d1) && IsSuspended(context, d2), "2 Digimon suspended as the cost");
    AssertEqual(6, context.MemoryController.Current.Current, "paid reduced cost 2 (8 -> 6), not full 6");
}

async Task AvailabilityLive()
{
    EngineContext context = Context();
    context.MemoryController.Set(-5); // full 6 unaffordable; reduced 2 affordable
    var hand = await PlaceCard(context, P1, EX, ChoiceZone.Hand, dp: 6000, playCost: 6);
    await PlaceCard(context, P1, "U1", ChoiceZone.BattleArea, dp: 4000);
    await PlaceCard(context, P1, "U2", ChoiceZone.BattleArea, dp: 4000);

    bool offered = new PlayCardAction().GetLegalActions(context, P1)
        .Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? id) && id is HeadlessEntityId e && e == hand);
    AssertTrue(offered, "EX8_074 is offered as a legal play at the reduced availability cost");
}

async Task AllianceRegistered()
{
    EngineContext context = Context();
    var id = await PlaceCard(context, P1, EX, ChoiceZone.BattleArea, dp: 6000);
    CardEffectRegistrar.RegisterCard(context, id, P1);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Alliance), "Alliance keyword is live after enter-play");
}

async Task VortexLive()
{
    EngineContext context = Context();
    var id = await PlaceCard(context, P1, EX, ChoiceZone.BattleArea, dp: 6000, suspended: false);
    await PlaceCard(context, P2, "FOE", ChoiceZone.BattleArea, dp: 3000, suspended: true);
    CardEffectRegistrar.RegisterCard(context, id, P1);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Vortex), "Vortex keyword is live after enter-play");
    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "Vortex opens the end-of-turn effect-driven attack window");
}

async Task WhenDigivolvingDelete()
{
    EngineContext context = Context();
    var self = await PlaceCard(context, P1, EX, ChoiceZone.BattleArea, dp: 6000, suspended: false);
    var ally = await PlaceCard(context, P1, "ALLY", ChoiceZone.BattleArea, dp: 4000, suspended: false);
    var big = await PlaceCard(context, P2, "BIG", ChoiceZone.BattleArea, dp: 10000, suspended: false);

    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally)); // suspend 1 -> cap 11000
    provider.Enqueue(ChoiceResult.Select(big));  // delete the 10000 opponent

    int resolved = await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.WhenDigivolving);
    AssertEqual(2, resolved, "both [When Digivolving] effects resolved");
    AssertTrue(IsSuspended(context, ally), "the ally was suspended");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, big), "the 10000 opponent was deleted (cap raised by the suspend)");
}

async Task AllTurnsReuse()
{
    EngineContext context = Context();
    var self = await PlaceCard(context, P1, EX, ChoiceZone.BattleArea, dp: 6000, suspended: false);
    var ally = await PlaceCard(context, P1, "ALLY", ChoiceZone.BattleArea, dp: 4000, suspended: false);
    var foe = await PlaceCard(context, P2, "FOE", ChoiceZone.BattleArea, dp: 7000, suspended: false);

    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally));
    provider.Enqueue(ChoiceResult.Select(foe));

    // Resolving the [All Turns] (OnEnterFieldAnyone) effect re-runs the [When Digivolving] suspend+delete.
    int resolved = await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(resolved >= 1, "the [All Turns] reuse effect resolved");
    AssertTrue(IsSuspended(context, ally), "re-run [When Digivolving] suspended the ally");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe), "re-run [When Digivolving] deleted the opponent");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceCard(EngineContext context, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, int dp, int? playCost = null, bool suspended = false)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 5 }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{cardNumber}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended };
    if (playCost.HasValue) { meta["playCost"] = playCost.Value; }
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
