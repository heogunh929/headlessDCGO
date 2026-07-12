// F1-M1: OnLoseSecurity activated-bridge expansion (first real F-1 diffusion).
//
// AS-IS fires EffectTiming.OnLoseSecurity via a global StackSkillInfos(hashtable {Player = the security-losing
// player}, OnLoseSecurity) (CardController.cs:5444) — a PLAYER-SCOPE broadcast whose reactors live on OTHER
// field cards and self-gate on WHICH player lost security (CanTriggerWhenLoseSecurity(player == card.Owner /
// card.Owner.Enemy)). Headless derives OnLoseSecurity from the removed security card's CardMoved
// (from==Security -> TriggerTimingMap), and the F1-M1 change REGISTERS it in ActivatedBridgeTimings.EventBroadcast
// so the removed-security CardMoved is threaded to every field card's activated resolver; the gate reads the
// subject's owner = the losing player.
//
// These are END-TO-END bridge tests (security move + GameFlowProcessor.RunToStableAsync), so they exercise the
// EventBroadcast registration itself (CollectActivatedBridgeTriggers), not just the resolver in isolation.
//
// Witnesses (REAL cards, dispatched by class-name = card-number):
//   * BT24_018 (BT24/Red): [All Turns][Once Per Turn] "when your OPPONENT'S security is removed, you may delete 1
//     of their Digimon" — activated SELECT-DESTROY body, isOptional, ENEMY player-scope.
//   * BT15_037 (BT15/Yellow): [All Turns][Once Per Turn] "when YOUR security is removed, gain 1 memory" —
//     MemoryBody, SELF player-scope; the memory (scheduler-vs-activated) boundary case (single-fire, no
//     scheduler-half double-collect).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the witness (BT24_018 / BT15_037)
HeadlessPlayerId P2 = new(2);   // the opponent

var tests = new (string Name, Func<Task> Body)[]
{
    ("enemy-scope activated fires: BT24_018 deletes an opponent Digimon when the OPPONENT loses security", EnemyReactorFires),
    ("enemy-scope player-gate negative: when the CONTROLLER loses their OWN security, BT24_018 does NOT fire", EnemyReactorSelfSecurityRejected),
    ("self-scope memory fires ONCE: BT15_037 gains exactly +1 memory when the OWNER loses security (no double-collect)", SelfMemoryFiresOnce),
    ("self-scope player-gate negative: when the OPPONENT loses security, BT15_037 does NOT gain memory", SelfMemoryOpponentSecurityRejected),
    ("[Once Per Turn] cap: a SECOND own-security loss the same turn does NOT re-gain; resets next turn", OncePerTurnCap),
    // (F1-M1 P1-1) effect-driven multi-security-removal batch collapse — UNCAPPED reactor fires ONCE per batch.
    ("P1-1 effect batch collapse: trashing 2 security in ONE effect batch fires the UNCAPPED reactor ONCE (+1, not +2)", EffectBatchCollapseUncapped),
    ("P1-1 independent batches: two SEPARATE effect batches in one drain fire the uncapped reactor TWICE (+2)", TwoIndependentBatchesFireTwice),
    // (F1-M1 P1-2) attack security-CHECK per-card firing + activated collect (no sync-window drop).
    ("P1-2 attack check per-card: an attack checking 2 security fires the uncapped reactor per card (+2), collapse does NOT merge the check path", AttackCheckFiresPerCard),
    ("P1-2 attack check activated fires: an activated OnLoseSecurity reactor fires during the security CHECK (not dropped by the sync window)", AttackCheckActivatedNotDropped),
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

async Task EnemyReactorFires()
{
    EngineContext ctx = Setup(seed: 41);
    var self = await Place(ctx, P1, "BT24_018", "BT24_018", "Digimon"); // real card
    var foe = await Place(ctx, P2, "FOE", "FOEdef", "Digimon");
    await Security(ctx, P2, 2);

    // isOptional "you may" -> answer the optional yes (the effect's stable EffectId), then pick the target.
    var effectId = $"{self.Value}:ae:BT24_18_AT_Sec_Removed";
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId(effectId)));
    Enqueue(ctx, ChoiceResult.Select(foe));

    await LoseSecurity(ctx, P2);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InTrash(ctx, P2, foe), "the opponent's Digimon was deleted (moved to trash) by BT24_018's OnLoseSecurity effect");
}

async Task EnemyReactorSelfSecurityRejected()
{
    EngineContext ctx = Setup(seed: 42);
    var self = await Place(ctx, P1, "BT24_018", "BT24_018", "Digimon");
    var foe = await Place(ctx, P2, "FOE", "FOEdef", "Digimon");
    await Security(ctx, P1, 2); // the CONTROLLER's own security

    // If the gate wrongly fired, it would need these; enqueue them so a spurious fire would be OBSERVABLE (the foe
    // would be deleted). The correct behaviour leaves them unconsumed.
    var effectId = $"{self.Value}:ae:BT24_18_AT_Sec_Removed";
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId(effectId)));
    Enqueue(ctx, ChoiceResult.Select(foe));

    await LoseSecurity(ctx, P1); // the OWNER loses security, not the opponent
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(!InTrash(ctx, P2, foe),
        "the enemy-scope gate (player == opponent) rejected the controller's OWN security loss — no delete");
}

async Task SelfMemoryFiresOnce()
{
    EngineContext ctx = Setup(seed: 43);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "BT15_037", "BT15_037", "Digimon");
    await Security(ctx, P1, 2);

    await LoseSecurity(ctx, P1);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    // P1 (owner) is the turn player, so a +1 gain reads as +1 on the turn-relative memory axis. EXACTLY +1 proves
    // the uniform ActivatedEffect fired ONCE via the activated bridge — a scheduler-half double-collect would be +2.
    AssertEqual(1, ctx.MemoryController.Current.Current, "BT15_037 gained exactly +1 memory (single fire, no double-collect)");
}

async Task SelfMemoryOpponentSecurityRejected()
{
    EngineContext ctx = Setup(seed: 44);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "BT15_037", "BT15_037", "Digimon");
    await Security(ctx, P2, 2); // the OPPONENT's security

    await LoseSecurity(ctx, P2);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "the self-scope gate (player == owner) rejected the OPPONENT's security loss — no memory gain");
}

async Task OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 45);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "BT15_037", "BT15_037", "Digimon");
    await Security(ctx, P1, 3);

    await LoseSecurity(ctx, P1);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(1, ctx.MemoryController.Current.Current, "first own-security loss gained +1");

    // Second loss the SAME turn — the [Once Per Turn] cap blocks the re-gain (also corroborates single-fire).
    await LoseSecurity(ctx, P1);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(1, ctx.MemoryController.Current.Current, "second own-security loss the same turn did NOT re-gain (per-turn cap)");

    // AS-IS InitUseCountThisTurn — the cap resets on the new turn; a further loss fires again. Drive a REAL turn
    // progression (P1 -> P2 -> P1) then reset the per-turn once-flags exactly as the end-turn cleanup does
    // (MetadataActionProcessor.EndTurnCleanupAsync -> OnceFlags.ResetForTurn(turn.TurnNumber, turn.TurnPlayerId)).
    ctx.TurnController.EndTurn();                 // -> P2's turn
    ctx.TurnController.EndTurn();                 // -> P1's turn again (new turn number)
    var turn = ctx.TurnController.Current;
    ctx.OnceFlags.ResetForTurn(turn.TurnNumber, turn.TurnPlayerId!.Value);
    await LoseSecurity(ctx, P1);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(2, ctx.MemoryController.Current.Current, "after the per-turn reset a new own-security loss gains again");
}

// (F1-M1 P1-1) An effect that trashes N security cards is ONE IReduceSecurity == ONE OnLoseSecurity broadcast
// (AS-IS CardController.cs:4358-4363). TrashSecurityAsync stamps all N CardMoved events with ONE shared
// security-loss batch id, so the UNCAPPED reactor fires ONCE (+1), not once-per-card (+2).
async Task EffectBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 50);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "TfxOnLoseSecurityCounter", "SECCTR", "Digimon");
    await Security(ctx, P1, 3);

    // Effect-driven trash of 2 security cards in ONE batch (one shared id) — the real IDestroySecurity boundary.
    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 2, fromTop: true, ctx.NextSecurityLossBatchId());
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the uncapped reactor fired ONCE for the 2-card effect batch (memory +1); a per-event fire would be +2");
}

// (F1-M1 P1-1) Two SEPARATE effect batches (distinct security-loss ids) co-draining in one settle fire the
// uncapped reactor TWICE (+2) — AS-IS each IReduceSecurity broadcasts separately, so the batch id distinguishes
// independent removals rather than collapsing them all together.
async Task TwoIndependentBatchesFireTwice()
{
    EngineContext ctx = Setup(seed: 51);
    ctx.MemoryController.Set(0);
    await Place(ctx, P1, "TfxOnLoseSecurityCounter", "SECCTR", "Digimon");
    await Security(ctx, P1, 4);

    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 1, fromTop: true, ctx.NextSecurityLossBatchId()); // batch A
    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 1, fromTop: true, ctx.NextSecurityLossBatchId()); // batch B
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current,
        "two independent security-loss batches fired the uncapped reactor twice (memory +2)");
}

// (F1-M1 P1-2) An attack security CHECK of N cards trashes each per-card, resolving OnSecurityCheck merged with
// OnLoseSecurity per card (AS-IS IReduceSecurity(ref triggeredSkillInfos), CardController.cs:3982-3985). Each
// reveal is its OWN per-iteration window, so the uncapped reactor fires PER CARD (+2) — the security-loss batch
// collapse (an effect-batch concern) must NOT merge the check path.
async Task AttackCheckFiresPerCard()
{
    EngineContext ctx = Setup(seed: 52);
    ctx.MemoryController.Set(0);
    var attacker = await PlaceAttacker(ctx, P1, strike: 2);
    await Place(ctx, P2, "TfxOnLoseSecurityCounter", "SECCTR2", "Digimon"); // self-scope reactor on the DEFENDER
    await Security(ctx, P2, 2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(ctx);
    AssertTrue(result.IsSuccess, "security check resolved");
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current,
        "the check fired the uncapped reactor per card (memory +2); a wrong batch-merge would be +1");
}

// (F1-M1 P1-2) The activated OnLoseSecurity reactor fires DURING the attack security check — the previous
// scheduler-only sync window CONSUMED the reveal CardMoved without collecting its activated bridge, dropping it.
async Task AttackCheckActivatedNotDropped()
{
    EngineContext ctx = Setup(seed: 53);
    ctx.MemoryController.Set(0);
    var attacker = await PlaceAttacker(ctx, P1, strike: 1);
    await Place(ctx, P2, "TfxOnLoseSecurityCounter", "SECCTR3", "Digimon");
    await Security(ctx, P2, 1);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(ctx);
    AssertTrue(result.IsSuccess, "security check resolved");
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the activated OnLoseSecurity fired during the security check (memory +1), not dropped by the sync window");
}

// --- Helpers -------------------------------------------------------------

// A Digimon attacker on the owner's battle area with a Strike metadata (number of security cards checked).
async Task<HeadlessEntityId> PlaceAttacker(EngineContext ctx, HeadlessPlayerId owner, int strike)
{
    var id = await Place(ctx, owner, "ATK", "ATKdef", "Digimon");
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record);
    var metadata = new Dictionary<string, object?>(record!.Metadata, StringComparer.Ordinal)
    {
        [SecurityResolver.StrikeKey] = strike,
    };
    ctx.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    return id;
}

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// The security stack cards (generic — no effect class), pushed onto the owner's security zone.
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

// Remove one card from the owner's security stack (Security -> Trash) — derives OnLoseSecurity (from==Security,
// to!=Security) with subject = the moved card (owner = the losing player).
async Task LoseSecurity(EngineContext ctx, HeadlessPlayerId owner)
{
    var sec = ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Security);
    if (sec.Count == 0) throw new InvalidOperationException("no security card to remove");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, sec[0], ChoiceZone.Security, ChoiceZone.Trash, FaceUp: true));
}

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

bool InTrash(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Trash).Contains(id);

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
