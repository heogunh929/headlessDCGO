using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-004 (EX8_074 Stage 1): the read predicates the original BeforePayCost effect composes —
// IsExistOnHand, IsSuspended, MatchConditionPermanentCount / HasMatchConditionPermanent. These mirror the
// original CardEffectCommons helpers in the headless entity-id idiom. EffectTiming.BeforePayCost is also
// now in the enum (compile-checked below). The interactive cost-reduction WINDOW that consumes them is a
// later stage; these predicates are reusable across many cards and verified here in isolation.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("EffectTiming.BeforePayCost exists in the enum", BeforePayCostEnumExists),
    ("IsExistOnHand is true for a hand card, false once it leaves the hand", IsExistOnHandTracksZone),
    ("IsSuspended reflects the live isSuspended flag", IsSuspendedReadsFlag),
    ("MatchConditionPermanentCount counts BOTH players' matching battle-area Digimon", CountsBothPlayers),
    ("MatchConditionPermanentCount(unsuspended) gates the EX8_074 '>= 2 suspendable' check", SuspendableCountGate),
    ("HasMatchConditionPermanent is count >= 1", HasIsCountGEOne),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

Task BeforePayCostEnumExists()
{
    // Compile-time + runtime presence: the value is defined and distinct from None.
    AssertTrue(Enum.IsDefined(typeof(EffectTiming), EffectTiming.BeforePayCost), "BeforePayCost is a defined EffectTiming");
    AssertTrue(EffectTiming.BeforePayCost != EffectTiming.None, "BeforePayCost is not None");
    return Task.CompletedTask;
}

async Task IsExistOnHandTracksZone()
{
    EngineContext context = Context();
    var id = await PlaceInHand(context, P1, "HAND");
    var card = new CardSource(context, id, P1);
    AssertTrue(CardEffectCommons.IsExistOnHand(card), "card is in hand");

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.Hand, ChoiceZone.BattleArea));
    AssertTrue(!CardEffectCommons.IsExistOnHand(card), "card no longer in hand after moving to battle area");
}

async Task IsSuspendedReadsFlag()
{
    EngineContext context = Context();
    var unsus = await PlaceDigimon(context, P1, "UNSUS", suspended: false);
    var sus = await PlaceDigimon(context, P1, "SUS", suspended: true);
    var card = new CardSource(context, unsus, P1);
    AssertTrue(!CardEffectCommons.IsSuspended(card, unsus), "unsuspended Digimon reads false");
    AssertTrue(CardEffectCommons.IsSuspended(card, sus), "suspended Digimon reads true");
}

async Task CountsBothPlayers()
{
    EngineContext context = Context();
    await PlaceDigimon(context, P1, "A", suspended: false);
    await PlaceDigimon(context, P1, "B", suspended: false);
    await PlaceDigimon(context, P2, "C", suspended: false);
    var card = new CardSource(context, new HeadlessEntityId("1:battle:A"), P1);

    int count = CardEffectCommons.MatchConditionPermanentCount(card, _ => true);
    AssertEqual(3, count, "counts all 3 battle-area cards across both players");
}

async Task SuspendableCountGate()
{
    EngineContext context = Context();
    // 2 of the owner's Digimon are unsuspended (suspendable), 1 is already suspended.
    await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);
    await PlaceDigimon(context, P1, "S1", suspended: true);
    var card = new CardSource(context, new HeadlessEntityId("1:battle:U1"), P1);

    bool Suspendable(HeadlessEntityId id) =>
        CardEffectCommons.IsOwnerBattleAreaDigimon(card, id) && !CardEffectCommons.IsSuspended(card, id);

    int suspendable = CardEffectCommons.MatchConditionPermanentCount(card, Suspendable);
    AssertEqual(2, suspendable, "exactly 2 owner Digimon are suspendable (EX8_074 '>= 2' is met)");
    AssertTrue(suspendable >= 2, "the EX8_074 CanActivate gate (>= 2) passes");
}

async Task HasIsCountGEOne()
{
    EngineContext context = Context();
    var card = new CardSource(context, new HeadlessEntityId("1:battle:NONE"), P1);
    AssertTrue(!CardEffectCommons.HasMatchConditionPermanent(card, (HeadlessEntityId _) => true), "no permanents -> Has is false");
    await PlaceDigimon(context, P1, "ONE", suspended: false);
    AssertTrue(CardEffectCommons.HasMatchConditionPermanent(card, (HeadlessEntityId _) => true), "one permanent -> Has is true");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceInHand(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
