// F1-Tier2: OnAttackTargetChanged activated-bridge expansion (first F-1 Tier2 COMBAT-group timing).
//
// AS-IS fires OnAttackTargetChanged when an attack's defender changes (a <Raid> switch or a block re-targeting the
// attack onto the blocker); subject = the ATTACKER. Headless emits it at RaidAttackSwitch.cs:146 and BlockTiming.cs:186
// (both subject=attacker). The gate CanTriggerOnPermanentAttackTargetSwitch takes an arbitrary permanentCondition; all
// three ported witnesses use `_ => true` (ANYONE — react to ANY attacker's switch), which is why this must be
// EventBroadcast (SubjectScoped would drop them). The scheduler-half (TriggerTimings.BroadcastTimings) already
// registers this timing; this golf opens the ACTIVATED half by registering it in ActivatedBridgeTimings.EventBroadcast.
//
// These tests drive a target change by emitting the timing directly (TriggerEventEmitter.Emit(OnAttackTargetChanged,
// subject=attacker)) + draining — the emit sites are already covered by NewTimingsFire; here we exercise the bridge.
//
// Witnesses (REAL cards): EX10_002 (Draw 1, inherited) and ST15_02 (Memory +1, inherited) + a top-instance Tfx probe.
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // --- Tfx (top-instance, anyone) ---
    ("Tfx anyone top fires: a top reactor gains memory on ANY attacker's target switch (attacker != reactor)", TfxAnyoneTopFires),
    ("Tfx per-change: two target switches fire the uncapped reactor twice (+2, no batch)", TfxPerChangeTwice),
    // --- ST15_02 (inherited memory) ---
    ("ST15_02 inherited fires: an inherited reactor under a field Digimon gains 1 memory on a target switch", St1502InheritedFires),
    ("ST15_02 top-permanent no-fire: ST15_02 as a TOP permanent does NOT fire its INHERITED effect (membership)", St1502TopPermanentNoFire),
    ("ST15_02 once-per-turn cap: a SECOND target switch the same turn does NOT re-gain (OPT cap)", St1502OncePerTurnCap),
    // --- EX10_002 (inherited draw) ---
    ("EX10_002 inherited fires: an inherited reactor draws 1 on a target switch (hand +1)", Ex10002InheritedDraws),
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

async Task TfxAnyoneTopFires()
{
    EngineContext ctx = Setup(seed: 200);
    ctx.MemoryController.Set(0);
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");                         // the attacker (no reactor)
    await Place(ctx, P1, "TfxOnAttackTargetChangedCounter", "TFX", "Digimon");            // an ANYONE reactor, != attacker
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the anyone top reactor gained +1 on a different attacker's target switch (anyone scope)");
}

async Task TfxPerChangeTwice()
{
    EngineContext ctx = Setup(seed: 201);
    ctx.MemoryController.Set(0);
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");
    await Place(ctx, P1, "TfxOnAttackTargetChangedCounter", "TFX", "Digimon");
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    var t2 = await Defender(ctx, "T2");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);
    await SwitchTarget(ctx, t2);

    AssertEqual(2, ctx.MemoryController.Current.Current,
        "two target switches fired the uncapped reactor twice (+2) — one emit per change, no batch");
}

async Task St1502InheritedFires()
{
    EngineContext ctx = Setup(seed: 202);
    ctx.MemoryController.Set(0);
    var (host, _) = await Tuck(ctx, srcNumber: "ST15_02", hostDispatch: "HOSTA");
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the inherited ST15_02 (under a field Digimon) gained +1 memory on a target switch");
}

async Task St1502TopPermanentNoFire()
{
    EngineContext ctx = Setup(seed: 203);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "ST15_02", "ST15_02top", "Digimon");   // ST15_02 as its OWN top permanent
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "ST15_02 as a TOP permanent did NOT fire its inherited OnAttackTargetChanged (membership)");
}

async Task St1502OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 204);
    ctx.MemoryController.Set(0);
    var (host, _) = await Tuck(ctx, srcNumber: "ST15_02", hostDispatch: "HOSTA");
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    var t2 = await Defender(ctx, "T2");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);
    AssertEqual(1, ctx.MemoryController.Current.Current, "first target switch gained +1");

    await SwitchTarget(ctx, t2);
    AssertEqual(1, ctx.MemoryController.Current.Current,
        "second target switch the same turn did NOT re-gain (Once Per Turn cap) — still +1");
}

async Task Ex10002InheritedDraws()
{
    EngineContext ctx = Setup(seed: 205);
    var (host, _) = await Tuck(ctx, srcNumber: "EX10_002", hostDispatch: "HOSTA");
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");
    var deckCard = await DeckCard(ctx, P1, "DK1");   // a card to draw
    var t0 = await Defender(ctx, "T0");
    var t1 = await Defender(ctx, "T1");
    DeclareAttack(ctx, attacker, t0);

    await SwitchTarget(ctx, t1);

    AssertTrue(InHand(ctx, P1, deckCard),
        "the inherited EX10_002 drew 1 (the deck-top card is now in hand) on a target switch");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return ctx;
}

// (harness triage) The AS-IS emit lives INSIDE AttackProcess.SwitchDefender (AttackProcess.cs:876-891, the sole
// live opener post-cutover: SkillWindowSupply GAP-drops this timing per RDW-02, so a bare TriggerEventEmitter.Emit
// off an ambient attack never reaches the mirror StackSkillInfos call). Drive the REAL retarget (RaidAttackSwitch's
// production path) instead of the raw emit so the same assertions exercise the live opener.
void DeclareAttack(EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId initialTarget) =>
    ctx.AttackController.DeclareAttack(P1, attacker, P2, initialTarget, isDirectAttack: false);

// (harness triage) Drain via AutoProcessCheck (the mirror trigger-stack drain SwitchDefender's inline insert
// stacks onto), NOT the full GameFlowProcessor.RunToStableAsync — the latter also single-steps the ATTACK
// PIPELINE (step 3, AttackPhase != None), which would resolve the whole declared attack to completion on the
// FIRST switch (no block/security choice pends here), clearing AttackController.Current before a SECOND
// SwitchDefender call could run. AutoProcessCheck alone resolves the stacked OnAttackTargetChanged window
// without touching attack-phase advancement, letting a still-open attack switch targets more than once.
async Task SwitchTarget(EngineContext ctx, HeadlessEntityId newTarget)
{
    await AttackProcess.For(ctx).SwitchDefender(causeEffectSourceId: null, isBlock: false, newDefendingPermanentId: newTarget);
    using (AmbientMatchContext.Enter(ctx))
    {
        await AutoProcessing.For(ctx).AutoProcessCheck();
    }
}

// A defender-side target (opponent battle area, unsuspended) to switch the attack onto.
async Task<HeadlessEntityId> Defender(EngineContext ctx, string tag) => await Place(ctx, P2, tag, tag, "Digimon");

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

// A HOST top permanent (generic) with srcNumber tucked underneath as an active digivolution source.
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
