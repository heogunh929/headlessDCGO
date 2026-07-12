// F1-Tier2: OnEndAttack activated-bridge expansion (first F-1 Tier2 / self-attacker timing).
//
// AS-IS fires EffectTiming.OnEndAttack via a GLOBAL StackSkillInfos(EffectHashtable {AttackingPermanent}, OnEndAttack)
// at AttackProcess.EndAttack (after the battle / security-check outcome, before CleanUp), guarded on the attacker
// still being alive. Every one of the ~80 AS-IS reactors is SELF-scoped: the reacting card must be a cardSource
// (top OR digivolution-source) of the AttackingPermanent (gate CanTriggerOnEndAttack = CanTriggerOnAttack =
// cardSources.Contains(card)). Headless opens this by (a) registering OnEndAttack in ActivatedBridgeTimings.
// EventBroadcast and (b) an EXPLICIT TriggerEventEmitter.Emit(OnEndAttack, subject: attackerId) from
// AttackPipeline.AdvanceEndAttackAsync (guarded on attacker-alive). The EventBroadcast scan (ScanZones) reaches both
// the top attacker AND its digivolution sources, so an INHERITED reactor under the attacker fires too — the deciding
// reason for EventBroadcast over SubjectScoped.
//
// These are END-TO-END bridge tests (declare attack + GameFlowProcessor.RunToStableAsync drives the AttackPipeline
// to attack-end, emitting OnEndAttack, then drains the activated windows).
//
// Witnesses (REAL cards, dispatched by class-name = card-number):
//   * BT9_043 (BT9/Yellow): [End of Attack][Once Per Turn] "You may add the top card of your security stack to your
//     hand to unsuspend this Digimon." — SELF (top), OPTIONAL, security->hand + self-unsuspend.
//   * BT9_062 (BT9/Black): [End of Attack] "If this Digimon has [Alphamon] in its name, delete 1 of your opponent's
//     Digimon with a play cost of 5 or less." — INHERITED (digivolution source), select-destroy, uncapped. Proves
//     the shared inherited-source scan reaches the newly-registered OnEndAttack timing.
//   * TfxOnEndAttackCounter: an uncapped self OnEndAttack memory probe — infra (single fire / non-attacker / dead).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the attacker / witness
HeadlessPlayerId P2 = new(2);   // the opponent

var tests = new (string Name, Func<Task> Body)[]
{
    // --- infra (Tfx probe) ---
    ("infra self fire: an attacker's uncapped OnEndAttack reactor gains +1 memory when its attack ends (single fire, no double-collect)", TfxSelfFiresOnce),
    ("infra non-attacker no-fire: a field card that did NOT attack does NOT fire its OnEndAttack reactor (+0)", TfxNonAttackerNoFire),
    ("infra attacker-dead no-fire: an attacker removed from the battle area before attack-end does NOT fire (emit guarded on attacker-alive, +0)", TfxAttackerDeadNoFire),
    // --- BT9_043 (self, optional) ---
    ("BT9_043 self optional fires: attack-end accept adds top security to hand and unsuspends this Digimon", Bt9043OptionalFires),
    ("BT9_043 optional decline: declining the 'You may' leaves the attacker suspended and security unchanged", Bt9043OptionalDecline),
    ("BT9_043 once-per-turn cap: a SECOND attack the same turn does NOT re-fire (OPT cap)", Bt9043OncePerTurnCap),
    // --- BT9_062 (inherited) ---
    ("BT9_062 inherited fires: an inherited OnEndAttack reactor under an Alphamon host deletes an opponent's cost<=5 Digimon", Bt9062InheritedFires),
    ("BT9_062 cost>5 no-fire: an opponent Digimon with play cost 6 is not a valid target (no delete)", Bt9062CostTooHighNoFire),
    ("BT9_062 top-permanent no-fire: BT9_062 as a TOP permanent does NOT fire its INHERITED effect (membership)", Bt9062TopPermanentNoFire),
    ("BT9_062 non-Alphamon host no-fire: the inherited reactor under a non-Alphamon host is gated off (name gate)", Bt9062NonAlphamonHostNoFire),
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

async Task TfxSelfFiresOnce()
{
    EngineContext ctx = Setup(seed: 70);
    ctx.MemoryController.Set(0);
    var attacker = await Place(ctx, P1, "TfxOnEndAttackCounter", "TFX", "Digimon");
    // (MIG2) the defender needs a security stack: a direct attack into 0 security marks the defender LOST, and
    // the AS-IS rule pass (EndGameProcess, AutoProcessing.cs:386) now consolidates that terminal BEFORE any
    // pending window resolves — the old fixture asserted a fire in a game-over state.
    await Security(ctx, P2, 1);

    await Attack(ctx, attacker);

    // P1 is the turn player, so a +1 gain reads +1. EXACTLY +1 proves the reactor fired ONCE via the activated
    // bridge — the double-collect (hook + unified-seed scheduler) that the WindowResolverWiring skip prevents is +2.
    AssertEqual(1, ctx.MemoryController.Current.Current, "the attacker's OnEndAttack reactor gained exactly +1 (single fire)");
}

async Task TfxNonAttackerNoFire()
{
    EngineContext ctx = Setup(seed: 71);
    ctx.MemoryController.Set(0);
    var attacker = await Place(ctx, P1, "ATK", "ATK", "Digimon");           // plain attacker, no reactor
    await Place(ctx, P1, "TfxOnEndAttackCounter", "TFX", "Digimon");        // a reactor that did NOT attack

    await Attack(ctx, attacker);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "the non-attacking reactor did NOT fire (self/attacker gate: only a cardSource of the AttackingPermanent reacts)");
}

async Task TfxAttackerDeadNoFire()
{
    EngineContext ctx = Setup(seed: 72);
    ctx.MemoryController.Set(0);
    // A GATE-LESS reactor (no IsExistOnBattleArea CanActivate) so the emit-time attacker-alive guard is the SOLE thing
    // that keeps a dead attacker silent — remove the guard and this over-fires (+1) from the trash. A battle-area-gated
    // probe cannot detect the guard (its own gate rejects a trashed attacker regardless), so this must be the no-gate
    // fixture.
    var attacker = await Place(ctx, P1, "TfxOnEndAttackCounterNoGate", "TFX", "Digimon");

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    // Simulate the attacker being deleted in battle BEFORE end-of-attack: remove it from the battle area. AS-IS
    // AttackProcess.EndAttack only fires OnEndAttack while AttackingPermanent.TopCard != null; the headless emit
    // mirrors that guard (ScanZones also scans the trash, so without the guard a dead attacker would self-gate true).
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.BattleArea, ChoiceZone.Trash, FaceUp: true));
    var pipeline = new AttackPipeline();
    while ((await pipeline.AdvanceAsync(ctx)).Progressed) { }   // drive to attack-end; the emit guard sees no attacker
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "a dead attacker did NOT fire OnEndAttack — the emit-time attacker-alive guard suppressed the event (a gate-less reactor would otherwise over-fire from the trash)");
}

async Task Bt9043OptionalFires()
{
    EngineContext ctx = Setup(seed: 73);
    var attacker = await Place(ctx, P1, "BT9_043", "BT9_043", "Digimon");
    SetSuspended(ctx, attacker, true);                       // as it would be after attacking
    await Security(ctx, P1, 2);
    await Security(ctx, P2, 1);                              // (MIG2) no game-over mid-attack — see TfxSelfFiresOnce
    var secTop = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Security)[0];

    // Answer the "You may" yes (the effect's stable EffectId with capHash). The body is non-interactive, so no
    // further target select is needed.
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{attacker.Value}:ae:Unsuspend_BT9_043")));

    await Attack(ctx, attacker);

    AssertTrue(!IsSuspended(ctx, attacker), "BT9_043 unsuspended itself at end of attack");
    AssertTrue(InHand(ctx, P1, secTop), "the top security card was added to hand");
    AssertEqual(1, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Security).Count,
        "the security stack dropped from 2 to 1 (top card returned to hand)");
}

async Task Bt9043OptionalDecline()
{
    EngineContext ctx = Setup(seed: 74);
    var attacker = await Place(ctx, P1, "BT9_043", "BT9_043", "Digimon");
    SetSuspended(ctx, attacker, true);
    await Security(ctx, P1, 2);

    // Enqueue nothing — the optional "You may" is declined.
    await Attack(ctx, attacker);

    AssertTrue(IsSuspended(ctx, attacker), "declining the optional left BT9_043 suspended");
    AssertEqual(2, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Security).Count,
        "the security stack was unchanged (no card returned to hand)");
}

async Task Bt9043OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 78);
    var attacker = await Place(ctx, P1, "BT9_043", "BT9_043", "Digimon");
    SetSuspended(ctx, attacker, true);
    await Security(ctx, P1, 2);
    await Security(ctx, P2, 2);                              // (MIG2) two attacks — no game-over mid-attack

    // First attack this turn: accept the optional → one security card goes to hand (2 -> 1).
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{attacker.Value}:ae:Unsuspend_BT9_043")));
    await Attack(ctx, attacker);
    AssertEqual(1, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Security).Count, "first attack fired (security 2 -> 1)");

    // Second attack the SAME turn: the [Once Per Turn] cap blocks it even though a security card remains and we would
    // accept the optional. Re-suspend (the first fire unsuspended it) so CanActivate's zone/existence half is fine and
    // only the cap can stop the re-fire.
    SetSuspended(ctx, attacker, true);
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{attacker.Value}:ae:Unsuspend_BT9_043")));
    await Attack(ctx, attacker);
    AssertEqual(1, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Security).Count,
        "second attack the same turn did NOT re-fire (Once Per Turn cap) — security stayed at 1");
    AssertTrue(IsSuspended(ctx, attacker), "the capped second activation did NOT unsuspend the attacker");
}

async Task Bt9062CostTooHighNoFire()
{
    EngineContext ctx = Setup(seed: 79);
    var (host, _) = await Tuck(ctx, hostDispatch: "HOSTC", hostName: "Alphamon", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "FOE", playCost: 6);           // cost 6 > 5 — not a valid target

    Enqueue(ctx, ChoiceResult.Select(foe));                 // would be consumed if it wrongly fired

    await Attack(ctx, host);

    AssertTrue(!InTrash(ctx, P2, foe),
        "an opponent Digimon with play cost 6 (>5) is not a valid target — BT9_062 does not delete it");
}

async Task Bt9062InheritedFires()
{
    EngineContext ctx = Setup(seed: 75);
    var (host, _) = await Tuck(ctx, hostDispatch: "HOSTA", hostName: "Alphamon", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "FOE", playCost: 5);
    await Security(ctx, P2, 1);                             // (MIG2) no game-over mid-attack — see TfxSelfFiresOnce

    Enqueue(ctx, ChoiceResult.Select(foe));                 // the cost<=5 opponent Digimon to delete (mandatory select)

    await Attack(ctx, host);                                // the HOST attacks; BT9_062 is an inherited source under it

    AssertTrue(InTrash(ctx, P2, foe),
        "the inherited BT9_062 (under the Alphamon host) deleted the opponent's cost<=5 Digimon at end of attack");
}

async Task Bt9062TopPermanentNoFire()
{
    EngineContext ctx = Setup(seed: 76);
    // BT9_062 as its OWN top permanent (Name = Alphamon so the name gate would pass) — the INHERITED effect must NOT
    // fire from the top card (AS-IS EffectList_ForCard membership: an inherited effect is exposed only from a NON-TOP
    // source). Only the membership should keep it silent here.
    var top = await Place(ctx, P1, "BT9_062", "Alphamon", "Digimon");
    var foe = await Foe(ctx, "FOE", playCost: 5);

    Enqueue(ctx, ChoiceResult.Select(foe));                 // would be consumed if it wrongly fired

    await Attack(ctx, top);

    AssertTrue(!InTrash(ctx, P2, foe),
        "BT9_062 as a TOP permanent did NOT fire its inherited OnEndAttack (membership) — foe survives");
}

async Task Bt9062NonAlphamonHostNoFire()
{
    EngineContext ctx = Setup(seed: 77);
    var (host, _) = await Tuck(ctx, hostDispatch: "HOSTB", hostName: "Greymon", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "FOE", playCost: 5);

    Enqueue(ctx, ChoiceResult.Select(foe));

    await Attack(ctx, host);

    AssertTrue(!InTrash(ctx, P2, foe),
        "the inherited BT9_062 under a non-Alphamon host did NOT fire (name gate) — foe survives");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

// Declare a direct attack and drive the AttackPipeline phase-by-phase to attack-end (Declared -> Combat -> Resolved
// -> Completed, where AdvanceEndAttackAsync emits OnEndAttack), then RunToStableAsync drains the activated windows.
async Task Attack(EngineContext ctx, HeadlessEntityId attacker)
{
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    var pipeline = new AttackPipeline();
    while ((await pipeline.AdvanceAsync(ctx)).Progressed) { }
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

// A HOST top permanent (Name = hostName, dispatched by generic class hostDispatch) with BT9_062 tucked underneath as
// an active (or flipped) digivolution source via the sourceIds metadata.
async Task<(HeadlessEntityId Host, HeadlessEntityId Source)> Tuck(
    EngineContext ctx, string hostDispatch, string hostName, bool hostIsDigimon, bool sourceFlipped)
{
    var cards = (CardDatabase)ctx.CardRepository;

    // The BT9_062 source (dispatched by class-name = "BT9_062").
    var srcDef = new HeadlessEntityId($"DEF:{P1.Value}:BT9_062src");
    cards.Upsert(new CardRecord(srcDef, "BT9_062", "BT9_062src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{P1.Value}:src:BT9_062src");
    var srcMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (sourceFlipped) srcMeta["isFlipped"] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, P1, Metadata: srcMeta));

    // The host top permanent — Name carries hostName (the BT9_062 gate reads the host top card's ContainsCardName).
    var hostDef = new HeadlessEntityId($"DEF:{P1.Value}:{hostDispatch}");
    cards.Upsert(new CardRecord(hostDef, hostDispatch, hostName,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 },
        CardType: hostIsDigimon ? "Digimon" : "Tamer"));
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

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag, int playCost)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:foe:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 },
        PlayCost: playCost, CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task Security(EngineContext ctx, HeadlessPlayerId owner, int count)
{
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 1; i <= count; i++)
    {
        var def = new HeadlessEntityId($"DEF:SEC{owner.Value}{i}");
        cards.Upsert(new CardRecord(def, $"SEC{i}", $"SEC{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{owner.Value}:sec:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security));
    }
}

void SetSuspended(EngineContext ctx, HeadlessEntityId id, bool value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec);
    var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal) { ["isSuspended"] = value };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

bool IsSuspended(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
    && inst.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

bool InTrash(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Trash).Contains(id);

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
