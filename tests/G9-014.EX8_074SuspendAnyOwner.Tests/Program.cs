using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G9-014 (fidelity fix): EX8_074's [When Would be Played] "suspend 2 Digimon -> play cost -4" is NOT
// owner-scoped. The original predicate is IsPermanentExistsOnBattleAreaDigimon (any owner), so EITHER
// player's unsuspended Digimon count toward the ">= 2" reduction gate and may be suspended to pay it. The
// headless port had narrowed this to IsOwnerBattleAreaDigimon (own only) — a guard-narrowing fidelity bug.
//
// This drives the real card through PlayCardAction.GetLegalActions: at 0 memory the FULL cost 11 is
// unaffordable (0-11 = -11, below the -10 floor) but the reduced cost 7 is affordable (0-7 = -7). So
// EX8_074 is offered ONLY when the reduction gate passes — and that gate must count the OPPONENT's Digimon.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gate counts OPPONENT's Digimon: own 1 + opp 1 => reduction => EX8_074 playable at 0 memory", OpponentCountsTowardGate),
    ("Total 1 (own 1, opp 0) => no reduction => EX8_074 NOT playable at 0 memory", BelowGateNotPlayable),
    ("Own 2 (opp 0) still works (own-only path unregressed) => playable", OwnTwoStillPlayable),
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

async Task OpponentCountsTowardGate()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    await PlaceEX8_074InHand(context, P1);
    await PlaceDigimon(context, P1, "OWN", suspended: false);
    await PlaceDigimon(context, P2, "OPP", suspended: false);   // opponent's Digimon — counts per AS-IS

    AssertTrue(CanPlayEX8_074(context), "EX8_074 is offered: own 1 + opp 1 meets the >=2 gate => -4 => cost 7 affordable at 0");
}

async Task BelowGateNotPlayable()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    await PlaceEX8_074InHand(context, P1);
    await PlaceDigimon(context, P1, "OWN", suspended: false);   // only 1 total => gate fails

    AssertTrue(!CanPlayEX8_074(context), "EX8_074 is NOT offered: only 1 suspendable => no reduction => full cost 11 unaffordable at 0");
}

async Task OwnTwoStillPlayable()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    await PlaceEX8_074InHand(context, P1);
    await PlaceDigimon(context, P1, "OWN1", suspended: false);
    await PlaceDigimon(context, P1, "OWN2", suspended: false);

    AssertTrue(CanPlayEX8_074(context), "EX8_074 is offered: own 2 meets the gate (own-only path unregressed)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 914);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

bool CanPlayEX8_074(EngineContext context) =>
    new PlayCardAction().GetLegalActions(context, P1)
        .Any(a => a.ActionType == HeadlessActionTypes.PlayCard && a.Id.Value.Contains("EX8_074", StringComparison.Ordinal));

// EX8_074 in hand: cardNumber "EX8_074" so CardEffectDispatch resolves the real ported effect (its
// BeforePayCost region drives the reduction gate). playCost 11 per cards.json.
async Task PlaceEX8_074InHand(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("EX8_074");
    cards.Upsert(new CardRecord(defId, "EX8_074", "MedievalGallantmon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 11000, ["level"] = 6 },
        CardType: "Digimon", PlayCost: 11));
    var id = new HeadlessEntityId($"{owner.Value}:hand:EX8_074");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
