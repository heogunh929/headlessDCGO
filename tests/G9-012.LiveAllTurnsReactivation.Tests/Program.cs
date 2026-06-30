using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-012 (LA-3): EX8_074's "[All Turns] (Once Per Turn) when Digimon are played, activate this Digimon's
// [When Digivolving] effects" now fires LIVE. When ANOTHER Digimon is played, the in-play EX8_074 re-runs
// its [When Digivolving] suspend+delete through the action flow (OnPlayReactivation). Once-per-turn guarded;
// cleared at turn end.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Playing another Digimon triggers in-play EX8_074's [All Turns] re-activation (suspend+delete)", FiresOnOtherPlay),
    ("Once per turn: a second play in the same turn does NOT re-activate", OncePerTurnGuard),
    ("Turn-end clear (ClearAll) resets the guard so it can fire again", GuardResets),
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

async Task FiresOnOtherPlay()
{
    var (context, ally, foe) = await Board(P1, P2);
    var trigger = await PlaceHand(context, P1, "TRIGGER", playCost: 1);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(ally));
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(foe));

    ActionProcessResult r = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, trigger, 1), context);
    AssertTrue(r.IsSuccess, $"play succeeded ({r.Message})");
    AssertTrue(IsSuspended(context, ally), "EX8_074's [All Turns] re-ran [When Digivolving] -> suspended the ally");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe), "[All Turns] re-run deleted the opponent");
}

async Task OncePerTurnGuard()
{
    var (context, ally, foe) = await Board(P1, P2);
    var ally2 = await Place(context, P1, "ALLY2", ChoiceZone.BattleArea, dp: 4000);
    var t1 = await PlaceHand(context, P1, "T1", playCost: 1);
    var t2 = await PlaceHand(context, P1, "T2", playCost: 1);
    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally));
    provider.Enqueue(ChoiceResult.Select(foe));

    await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, t1, 1), context); // activates once
    AssertTrue(IsSuspended(context, ally), "first play activated [All Turns]");

    // Second play same turn: guard set -> no re-activation (no choices enqueued; must NOT try to activate).
    ActionProcessResult r2 = await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, t2, 1), context);
    AssertTrue(r2.IsSuccess, $"second play succeeded ({r2.Message})");
    AssertTrue(!IsSuspended(context, ally2), "second play did NOT re-activate (once per turn)");
}

async Task GuardResets()
{
    var (context, ally, foe) = await Board(P1, P2);
    var ally2 = await Place(context, P1, "ALLY2", ChoiceZone.BattleArea, dp: 4000);
    var foe2 = await Place(context, P2, "FOE2", ChoiceZone.BattleArea, dp: 6000);
    var t1 = await PlaceHand(context, P1, "T1", playCost: 1);
    var t2 = await PlaceHand(context, P1, "T2", playCost: 1);
    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally));
    provider.Enqueue(ChoiceResult.Select(foe));
    await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, t1, 1), context);

    OnPlayReactivation.ClearAll(context); // turn-end reset

    provider.Enqueue(ChoiceResult.Select(ally2));
    provider.Enqueue(ChoiceResult.Select(foe2));
    await new PlayCardAction().ProcessAsync(HeadlessActionFactory.PlayCard(P1, t2, 1), context);
    AssertTrue(IsSuspended(context, ally2), "after ClearAll, the next play re-activates [All Turns] again");
}

// --- Helpers -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId ally, HeadlessEntityId foe)> Board(HeadlessPlayerId p1, HeadlessPlayerId p2)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { p1, p2 }, p1);
    context.MemoryController.Set(8);
    var ex = await Place(context, p1, "EX8_074", ChoiceZone.BattleArea, dp: 6000);
    CardEffectRegistrar.RegisterCard(context, ex, p1);
    var ally = await Place(context, p1, "ALLY", ChoiceZone.BattleArea, dp: 4000);
    var foe = await Place(context, p2, "FOE", ChoiceZone.BattleArea, dp: 7000);
    return (context, ally, foe);
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, int dp)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> PlaceHand(EngineContext context, HeadlessPlayerId owner, string tag, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["playCost"] = playCost }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
