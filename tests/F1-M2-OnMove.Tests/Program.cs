// F1 Tier1: OnMove activated-bridge expansion (the breeding->battle promotion window).
//
// AS-IS fires EffectTiming.OnMove via a global StackSkillInfos(hashtable {Permanent = movingPermanent}, OnMove)
// (CardObjectController.cs:1111) once per Digimon PROMOTED from the breeding (training) area onto the battle area.
// The reactors' gate CanTriggerOnMove(hashtable, permanentCondition) reads the moved permanent (requiring it to be
// on the battle area) and self-gates. Of the 30 AS-IS OnMove cards, 18 are SELF reactors (permanent == self) and 12
// are ANYONE/cross-card reactors (property gates), so OnMove is registered in ActivatedBridgeTimings.EventBroadcast
// (the field scan reaches the promoted subject too, so it subsumes the self reactors; SubjectScoped would drop the
// anyone ones). Headless derives OnMove from the promoted card's CardMoved (BreedingArea->BattleArea,
// TriggerTimingMap) and threads it as the driving event to every field card's activated resolver.
//
// These are END-TO-END bridge tests (a real BreedingArea->BattleArea zone move + RunToStableAsync), so they exercise
// the EventBroadcast registration itself (CollectActivatedBridgeTriggers), not the resolver in isolation.
//
// Witnesses:
//   * BT8_092 (BT8/Black, REAL, ANYONE): [Your Turn] "when one of your Digimon with [X Antibody] moves from breeding
//     to battle, gain 1 memory and <Draw 1>" — cross-card reactor (a DIFFERENT card than the mover), trait gate on
//     the moved subject, and an IsOwnerTurn ([Your Turn]) gate. Body = Draw 1 then +1 memory.
//   * TfxOnMoveSelfDraw (TestFixtures, SELF): [Your Turn] "when THIS Digimon moves from breeding to battle, draw 1" —
//     the majority self shape (permanent == PermanentOfThisCard()); proves the subject-as-reactor path.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("anyone fires: a P1 Digimon with [X Antibody] promoted breeding->battle fires BT8_092 (+1 memory, +1 draw)", AnyoneFires),
    ("anyone trait-gate negative: a mover WITHOUT [X Antibody] does NOT fire BT8_092 (no draw)", AnyoneTraitGateRejects),
    ("anyone owner-turn negative: a P1 promotion on the OPPONENT'S turn does NOT fire BT8_092's [Your Turn] gate", AnyoneOffTurnRejects),
    ("self fires: TfxOnMoveSelfDraw promoted breeding->battle draws 1 (subject-as-reactor via the field scan)", SelfFires),
    ("self negative: a DIFFERENT Digimon promoting does NOT fire TfxOnMoveSelfDraw (permanent == self rejects)", SelfRejectsOther),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task AnyoneFires()
{
    EngineContext ctx = Setup(seed: 61);
    await PlaceBattle(ctx, P1, "BT8_092", "WIT", "Tamer"); // the cross-card reactor
    await SeedLibrary(ctx, P1, 2);
    var mover = await PlaceBreeding(ctx, P1, "MOVER", "Digimon", traits: "X Antibody");
    ctx.MemoryController.Set(0);
    int handBefore = HandCount(ctx, P1);

    await Promote(ctx, P1, mover);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "BT8_092 gained +1 memory on the promotion");
    AssertEqual(handBefore + 1, HandCount(ctx, P1), "BT8_092 drew exactly 1 card on the promotion");
}

async Task AnyoneTraitGateRejects()
{
    EngineContext ctx = Setup(seed: 62);
    await PlaceBattle(ctx, P1, "BT8_092", "WIT", "Tamer");
    await SeedLibrary(ctx, P1, 2);
    var mover = await PlaceBreeding(ctx, P1, "PLAIN", "Digimon", traits: null); // no [X Antibody]
    ctx.MemoryController.Set(0);
    int handBefore = HandCount(ctx, P1);

    await Promote(ctx, P1, mover);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(handBefore, HandCount(ctx, P1), "a mover without [X Antibody] did NOT fire BT8_092 (no draw)");
    AssertEqual(0, ctx.MemoryController.Current.Current, "no memory gained (trait gate rejected)");
}

async Task AnyoneOffTurnRejects()
{
    EngineContext ctx = Setup(seed: 63);
    await PlaceBattle(ctx, P1, "BT8_092", "WIT", "Tamer");
    await SeedLibrary(ctx, P1, 2);
    var mover = await PlaceBreeding(ctx, P1, "MOVER", "Digimon", traits: "X Antibody");
    ctx.TurnController.EndTurn(); // -> P2's turn; BT8_092's [Your Turn] (IsOwnerTurn == P1's turn) is now false
    int handBefore = HandCount(ctx, P1);

    await Promote(ctx, P1, mover);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(handBefore, HandCount(ctx, P1),
        "a P1 promotion on the OPPONENT'S turn did NOT fire BT8_092's [Your Turn] gate (no draw)");
}

async Task SelfFires()
{
    EngineContext ctx = Setup(seed: 64);
    await SeedLibrary(ctx, P1, 2);
    var self = await PlaceBreeding(ctx, P1, "TfxOnMoveSelfDraw", "Digimon", traits: null); // the reactor IS the mover
    int handBefore = HandCount(ctx, P1);

    await Promote(ctx, P1, self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(handBefore + 1, HandCount(ctx, P1),
        "TfxOnMoveSelfDraw drew 1 when it was itself promoted (subject-as-reactor via the field scan)");
}

async Task SelfRejectsOther()
{
    EngineContext ctx = Setup(seed: 65);
    await PlaceBattle(ctx, P1, "TfxOnMoveSelfDraw", "SELF", "Digimon"); // reactor already on the field
    await SeedLibrary(ctx, P1, 2);
    var other = await PlaceBreeding(ctx, P1, "OTHER", "Digimon", traits: null);
    int handBefore = HandCount(ctx, P1);

    await Promote(ctx, P1, other);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(handBefore, HandCount(ctx, P1),
        "a DIFFERENT Digimon promoting did NOT fire TfxOnMoveSelfDraw (permanent == self rejected)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> PlaceBattle(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType)
{
    var id = await Define(ctx, owner, cardNumber, tag, cardType, traits: null);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceBreeding(EngineContext ctx, HeadlessPlayerId owner, string cardNumberOrTag, string cardType, string? traits)
{
    var id = await Define(ctx, owner, cardNumberOrTag, cardNumberOrTag, cardType, traits);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BreedingArea));
    return id;
}

// Promote a Digimon out of the breeding area onto the battle area — derives OnMove (BreedingArea->BattleArea) with
// subject = the moved card.
async Task Promote(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.BreedingArea, ChoiceZone.BattleArea));

async Task<HeadlessEntityId> Define(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType, string? traits)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 };
    if (traits is not null) meta["traits"] = traits;
    cards.Upsert(new CardRecord(def, cardNumber, tag, meta, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await Task.CompletedTask;
    return id;
}

// Put N generic (no-effect) cards into the owner's library so a <Draw 1> is observable.
async Task SeedLibrary(EngineContext ctx, HeadlessPlayerId owner, int count)
{
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 1; i <= count; i++)
    {
        var def = new HeadlessEntityId($"DEF:LIB{owner.Value}{i}");
        cards.Upsert(new CardRecord(def, $"LIB{i}", $"LIB{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{owner.Value}:lib:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    }
}

int HandCount(EngineContext ctx, HeadlessPlayerId owner) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Hand).Count;

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}
