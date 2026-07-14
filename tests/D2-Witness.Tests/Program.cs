// D-2 (VR-9): OnLeaveFieldAnyone batch dedup + witness AD1_025.
//
// AS-IS (CardController.cs:3748) stacks the WHOLE simultaneous-leave list as ONE
// StackSkillInfos(OnLeaveFieldAnyone) whose gate any-matches, so an OnLeaveFieldAnyone reactor fires ONCE per
// batch (N simultaneous leaves = 1 event). Headless emits one CardMoved (leave) per card; the ACTIVATED bridge
// (WindowResolverWiring.CollectActivatedBridgeTriggers) collapses them to the FIRST gate-passing leave subject
// per reactor. Witness: AD1_025's [All Turns][Once Per Turn] — "when any of your opponent's Digimon leave, trash
// 1 of their Options and trash their top security card." Observable = the enemy's top security is trashed exactly
// ONCE per batch (not N times), and only when an OPPONENT'S Digimon left (any-match gate).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls AD1_025
HeadlessPlayerId P2 = new(2);   // the opponent whose Digimon leave / whose security is trashed

var tests = new (string Name, Func<Task> Body)[]
{
    ("batch collapse: 3 opponent Digimon deleted simultaneously fire AD1_025 ONCE (top security -1, not -3)", BatchCollapse),
    ("any-match negative: a batch of only the CONTROLLER'S own Digimon leaving does NOT fire (gate rejects)", AnyMatchNegative),
    ("[Once Per Turn] cap: a SECOND leave batch the same turn does NOT re-fire (capHash AD1-025_AT)", OncePerTurnCap),
    ("resets next turn: after the per-turn reset a new leave batch fires again", ResetsNextTurn),
    ("option sub-effect: with an enemy Option in play, AD1_025 destroys the Option AND trashes top security", OptionDestroyAndSecurity),
    ("[collapse witness] an UNCAPPED reactor fires EXACTLY ONCE for a 3-card leave batch (-1 memory, not -3)", UncappedBatchCollapse),
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

// (D-2 adversarial review P2-1) BatchCollapse alone cannot prove the collapse: AD1_025 is [Once Per Turn], so
// its cap ALONE reduces N fires to one even without the batch-collapse. This test uses an UNCAPPED
// OnLeaveFieldAnyone reactor (TfxOnLeaveFieldCounter, -1 memory, no cap) so the observable isolates the collapse:
// a 3-card leave batch costs -1 IFF the reactor fired once (collapse), -3 if the bridge fired per CardMoved.
async Task UncappedBatchCollapse()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 31);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (C2 harness phase fix) DoneStartGame gate
    ctx.MemoryController.Set(0);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnLeaveFieldCounter"), "TfxOnLeaveFieldCounter", "LeaveCounter",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var reactor = new HeadlessEntityId("1:battle:LEAVECTR");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId("TfxOnLeaveFieldCounter"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.RegisterEnteredCardEffects(reactor, P1);

    var d1 = await Foe(ctx, "L1");
    var d2 = await Foe(ctx, "L2");
    var d3 = await Foe(ctx, "L3");

    await DeleteBatchViaSink(ctx, d1, d2, d3);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    // P1 (reactor owner) is the turn player; -1 memory on a turn-player owner reads as -1 on the turn-relative axis.
    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "the uncapped reactor fired ONCE for the 3-card batch (memory -1); a per-event fire would be -3");
}

async Task BatchCollapse()
{
    EngineContext ctx = await Setup();
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");
    var d3 = await Foe(ctx, "D3");
    await Security(ctx, P2, 3);
    int before = SecurityCount(ctx, P2);

    // Delete all three opponent Digimon in ONE batch (no drive between moves), then settle.
    await DeleteBatchViaSink(ctx, d1, d2, d3);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(before - 1, SecurityCount(ctx, P2),
        "AD1_025 fired ONCE for the 3-card leave batch (top security -1); a per-event fire would be -3");
}

async Task AnyMatchNegative()
{
    EngineContext ctx = await Setup();
    // Only the CONTROLLER's (P1's) own Digimon leave — the opponent-Digimon gate must reject the whole batch.
    var own = await Ally(ctx, "OWN");
    await Security(ctx, P2, 3);
    int p2Before = SecurityCount(ctx, P2);

    await DeleteBatchViaSink(ctx, own);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(p2Before, SecurityCount(ctx, P2),
        "no opponent Digimon left, so AD1_025 did not fire (any-match gate is false)");
}

async Task OncePerTurnCap()
{
    EngineContext ctx = await Setup();
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");
    await Security(ctx, P2, 3);
    int before = SecurityCount(ctx, P2);

    // Batch 1 — fires (security -1).
    await DeleteBatchViaSink(ctx, d1);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before - 1, SecurityCount(ctx, P2), "first leave batch fired");

    // Batch 2 same turn — the [Once Per Turn] cap (capHash AD1-025_AT) blocks the re-fire.
    await DeleteBatchViaSink(ctx, d2);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before - 1, SecurityCount(ctx, P2), "second leave batch the same turn did NOT re-fire (per-turn cap)");
}

async Task ResetsNextTurn()
{
    EngineContext ctx = await Setup();
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");
    await Security(ctx, P2, 3);
    int before = SecurityCount(ctx, P2);

    await DeleteBatchViaSink(ctx, d1);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before - 1, SecurityCount(ctx, P2), "first turn's leave batch fired");

    // AS-IS InitUseCountThisTurn — the per-turn cap resets on the new turn (C2: the new-model per-instance
    // cap lives on CEntity_EffectController.UseEffectsThisTurn; the real turn advance resets both).
    ctx.OnceFlags.ResetForTurn(turnSequence: 1, turnPlayerId: P1);
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CEntity_EffectControllerStore.ResetUseCountsForTurn(ctx);

    await DeleteBatchViaSink(ctx, d2);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before - 2, SecurityCount(ctx, P2), "after the per-turn reset a new leave batch fires again");
}

async Task OptionDestroyAndSecurity()
{
    EngineContext ctx = await Setup();
    var digimon = await Foe(ctx, "DGM");
    var option = await FoeOption(ctx, "OPT");
    await Security(ctx, P2, 3);
    int secBefore = SecurityCount(ctx, P2);

    // The enemy Option is a valid destroy target, so AD1_025 offers the select — pick the Option.
    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(option));

    await DeleteBatchViaSink(ctx, digimon);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InTrash(ctx, P2, option), "the selected enemy Option was destroyed (moved to trash)");
    AssertEqual(secBefore - 1, SecurityCount(ctx, P2), "the enemy's top security was also trashed");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 25);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (C2 harness phase fix) DoneStartGame gate
    var cards = (CardDatabase)ctx.CardRepository;
    // The REAL AD1_025 dispatches by card number = class name.
    cards.Upsert(new CardRecord(new HeadlessEntityId("AD1_025"), "AD1_025", "Omnimon",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var self = new HeadlessEntityId("1:battle:AD1_025");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("AD1_025"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));
    return ctx;
}

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag) => await Place(ctx, P2, tag, "Digimon");
async Task<HeadlessEntityId> FoeOption(EngineContext ctx, string tag) => await Place(ctx, P2, tag, "Option");
async Task<HeadlessEntityId> Ally(EngineContext ctx, string tag) => await Place(ctx, P1, tag, "Digimon");

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
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

// A field->trash DELETION move. (R2-P1-4) the deletion marker (DeletionBatchIdKey) is what derives
// OnLeaveFieldAnyone/OnDeletion from a field->Trash move now (an unmarked move is a top-swap/no-trigger
// trash), so this test's simulated deletions stamp it — one shared id per SIMULTANEOUS batch, mirroring
// "one Destroy() call == one batch".
// (C2 harness retarget) The pre-flip helper performed a RAW ZoneMover move stamped with a batch id and relied on
// the retired CardMoved-derivation bridge to open the leave window. Post-flip the deletion window opens at the
// REAL deletion seam (MatchStateMutationSink DeleteKind, decision-4 collect-before-removal transport), so the
// witness drives that seam: one call = one sink flush = one delete batch (same intent, real mechanism).
async Task DeleteBatchViaSink(EngineContext ctx, params HeadlessEntityId[] targets)
{
    var sink = new HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    foreach (HeadlessEntityId target in targets)
    {
        sink.Apply(new HeadlessDCGO.Engine.Headless.Effects.EffectMutation(
            HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.DeleteKind,
            new HeadlessEntityId("effect:deleter"),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.TargetEntityIdKey] = target.Value,
            }));
    }

    await sink.FlushAsync();
}

int SecurityCount(EngineContext ctx, HeadlessPlayerId owner) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Security).Count;

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
