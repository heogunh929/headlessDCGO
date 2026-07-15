// F1-Tier2: OnBlockAnyone activated-bridge expansion (combat-group timing).
//
// AS-IS fires OnBlockAnyone (StackSkillInfos) inside AttackProcess.SwitchDefender(isBlock=true) after the blocker is
// suspended, payload {AttackingPermanent, DefendingPermanent(blocker), CardEffect, IsBlock}. ALL 6 AS-IS reactors gate
// on the ATTACKER (CanTriggerOnAttack — "[When this Digimon is blocked]"); none read the blocker. The headless emit
// (BlockTiming.cs) was fixed to carry subject = the attacker (was the blocker → all 6 unreachable), and
// OnBlockAnyone is registered in ActivatedBridgeTimings.EventBroadcast (inherited coverage).
//
// These are TRUE e2e tests: they declare a field attack, advance the pipeline to the parked block choice, resolve
// with a real blocker (BlockTiming.ResolveBlockChoice emits OnBlock), and drain — so they exercise the REAL emit
// (unlike the old unit tests that injected subject=attacker directly, masking the bug).
//
// Witnesses (REAL, already ported): BT1_022 (Draw 1, ACTIVATED — EventBroadcast/ScanZones half, fires as an inherited
// source under the attacker) and ST1_09 (Memory +3, SCHEDULER-HALF bound — per-card filter, fires as the top attacker).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender / blocker side

// NOTE (AS-IS scoping): all 6 AS-IS OnBlockAnyone reactors gate on the ATTACKER, and the three simple ones
// (BT1_012 DP / BT1_022 draw / ST1_09 memory) are digitama INHERITED effects — they fire only as a NON-top
// digivolution source under the attacker, never as the top attacker itself. BT1_022 (uniform ActivatedEffect) is
// served by the activated bridge's inherited scan; BT1_012/ST1_09 are SCHEDULER-half bound effects whose
// inherited-source collection is the deferred design item F1-BLOCK-SCHED-INHERIT (the scheduler half has no
// inherited-source scan yet), so they have no faithful e2e until that remediation lands.
var tests = new (string Name, Func<Task> Body)[]
{
    ("BT1_022 inherited draws on block: an inherited activated reactor under the attacker draws 1 when blocked", Bt1022InheritedDrawsOnBlock),
    ("BT1_022 top-attacker no-fire: the INHERITED effect does not fire from the top attacker (membership)", Bt1022TopAttackerNoFire),
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

async Task Bt1022InheritedDrawsOnBlock()
{
    EngineContext ctx = Setup(seed: 90);
    var (host, _) = await Tuck(ctx, srcNumber: "BT1_022", hostDispatch: "HOSTA");   // BT1_022 tucked under the attacker
    var target = await Place(ctx, P2, "TGT", "TGT", "Digimon");
    var blocker = await Blocker(ctx, "BLK");
    var deckCard = await DeckCard(ctx, P1, "DK1");

    await Block(ctx, host, target, blocker);

    AssertTrue(InHand(ctx, P1, deckCard),
        "the inherited BT1_022 under the attacker drew 1 when blocked (OnBlock subject=attacker)");
}

async Task Bt1022TopAttackerNoFire()
{
    EngineContext ctx = Setup(seed: 91);
    // BT1_022 as its OWN top attacker — the INHERITED effect must NOT fire from the top card (AS-IS
    // EffectList_ForCard membership: an inherited effect is exposed only from a NON-top source).
    var attacker = await Place(ctx, P1, "BT1_022", "BT1_022", "Digimon");
    var target = await Place(ctx, P2, "TGT", "TGT", "Digimon");
    var blocker = await Blocker(ctx, "BLK");
    var deckCard = await DeckCard(ctx, P1, "DK1");

    await Block(ctx, attacker, target, blocker);

    AssertTrue(!InHand(ctx, P1, deckCard),
        "BT1_022 as the TOP attacker did NOT fire its inherited [When Blocked] draw (membership)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return ctx;
}

// Drive the REAL block pipeline: declare a field attack, advance to the parked block choice, resolve with a blocker.
async Task Block(EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId target, HeadlessEntityId blocker)
{
    ctx.AttackController.DeclareAttack(P1, attacker, P2, target, isDirectAttack: false);
    var pipeline = new AttackPipeline();
    var timing = new BlockTiming();
    // Drive the mirror state machine to the parked block choice (the counter passes park at Declared first).
    AttackAdvanceResult step;
    do { step = await pipeline.AdvanceAsync(ctx); } while (step.Progressed && !step.ChoiceRequested);
    // (harness triage) ResolveBlockChoice -> GetBlockerCandidates -> Permanent.HasCollision -> EffectList reads
    // GManager.instance via AmbientMatchContext — the established direct-call pattern (G9-064.SwitchTargetLock.Tests)
    // scopes it explicitly; AttackPipeline.AdvanceAsync already self-scopes, but this direct ResolveBlockChoice call
    // does not.
    BlockTimingResult block;
    using (AmbientMatchContext.Enter(ctx))
    {
        block = timing.ResolveBlockChoice(ctx, ChoiceResult.Select(blocker));
    }
    if (!block.IsSuccess) throw new Exception($"block failed: {block.FailureReason}");
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A defender-side blocker candidate (opponent battle area, unsuspended, hasBlocker).
async Task<HeadlessEntityId> Blocker(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:2:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false, [BlockTiming.HasBlockerKey] = true }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> DeckCard(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:library:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    return id;
}

// A HOST top permanent (the attacker) with srcNumber tucked underneath as an active digivolution source.
async Task<(HeadlessEntityId Host, HeadlessEntityId Source)> Tuck(EngineContext ctx, string srcNumber, string hostDispatch)
{
    var cards = (CardDatabase)ctx.CardRepository;

    var srcDef = new HeadlessEntityId($"DEF:{P1.Value}:{srcNumber}src");
    cards.Upsert(new CardRecord(srcDef, srcNumber, $"{srcNumber}src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{P1.Value}:src:{srcNumber}src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));

    var hostDef = new HeadlessEntityId($"DEF:{P1.Value}:{hostDispatch}");
    cards.Upsert(new CardRecord(hostDef, hostDispatch, hostDispatch,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var host = new HeadlessEntityId($"{P1.Value}:battle:{hostDispatch}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = 5000,
            ["level"] = 4,
            ["isSuspended"] = false,
            ["sourceIds"] = new[] { source.Value },
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return (host, source);
}

bool InHand(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Hand).Contains(id);

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
