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
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using System.Collections;
using System.Linq;

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
    ("[On Play] D2w-25 revive: mass deck-bottom-bounce every enemy Digimon with <= sources, THEN delete 1 of the POST-bounce survivors", OnPlayBounceThenConditionalDelete),
    // (RDW-01-BOUNCE-SNAPSHOT-TRANSPORT) the sink leave-path: a BOUNCE (field->Hand / ->Library / ->Security) opens
    // the pre-move OnLeaveFieldAnyone (+ OnPermamemtReturnedToHand for a hand bounce) window, like a deletion.
    ("[RDW-01] hand-bounce fires the uncapped OnLeaveFieldAnyone reactor (memory -1)", BounceHandFiresLeave),
    ("[RDW-01] a 2-card hand-bounce batch fires the uncapped OnLeaveFieldAnyone reactor ONCE (-1, not -2)", BounceHandLeaveCollapses),
    ("[RDW-01] hand-bounce fires the uncapped OnPermamemtReturnedToHand reactor (memory -1)", BounceHandFiresReturned),
    ("[RDW-01] deck-bounce fires OnLeaveFieldAnyone but NOT OnPermamemtReturnedToHand (deck != hand)", BounceDeckFiresLeaveNotReturned),
    ("[RDW-01] negative: a NON-field ReturnToHand (card already in trash) fires no leave window", NonFieldReturnDoesNotFire),
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
    // (uniform-사멸 flip) per-turn caps live on the AS-IS CEntity_EffectController store now.
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CEntity_EffectControllerStore.ResetUseCountsForTurn(ctx);
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

// (D2w-25 revive) The [On Play]/[When Digivolving] shared body: mass deck-bottom-bounce every opponent Digimon
// whose source count is <= this Digimon's, THEN (re-scanning the POST-bounce board) delete 1 of the opponent's
// remaining (higher-source) Digimon. AD1_025 has 0 sources here, so the 0-source enemies bounce and the 1-source
// enemies survive as the delete pool — proving the bounce COMMITS before the select enumerates candidates.
async Task OnPlayBounceThenConditionalDelete()
{
    EngineContext ctx = await Setup();               // AD1_025 self on P1 battle area, 0 sources
    var low1 = await Foe(ctx, "LOW1");               // 0 sources -> bounced (0 <= 0)
    var low2 = await Foe(ctx, "LOW2");               // 0 sources -> bounced
    var high1 = await FoeWithSources(ctx, "HIGH1", 1); // 1 source -> survives (1 > 0) -> delete candidate
    var high2 = await FoeWithSources(ctx, "HIGH2", 1); // 1 source -> survives -> delete candidate

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, new HeadlessEntityId("1:battle:AD1_025"), P1);
    ActivateClass onPlay = card.EffectList(EffectTiming.OnEnterFieldAnyone).OfType<ActivateClass>().First();

    // Post-bounce the delete pool is {high1, high2} (2 candidates -> a genuine select); pick high1.
    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(high1));

    await onPlay.Activate(new Hashtable());

    AssertTrue(!InBattle(ctx, P2, low1) && !InBattle(ctx, P2, low2),
        "mass bounce: both enemy Digimon with <= sources left the battle area");
    AssertTrue(InLibrary(ctx, P2, low1) && InLibrary(ctx, P2, low2),
        "mass bounce: the <=-source enemy Digimon are on the bottom of the opponent's deck (library)");
    AssertTrue(InBattle(ctx, P2, high1) == false && InTrash(ctx, P2, high1),
        "conditional delete over the POST-bounce survivors: the selected higher-source Digimon was deleted (trash)");
    AssertTrue(InBattle(ctx, P2, high2),
        "the non-selected higher-source survivor stayed in the battle area (bounce did not touch it, delete picked only 1)");
}

// (RDW-01-BOUNCE-SNAPSHOT-TRANSPORT) --- sink bounce/put leave-window witnesses -------------------------
// A field DEPARTURE via the sink (ReturnToHand / ReturnToDeck* / a field-origin AddToSecurity) must open the
// AS-IS OnLeaveFieldAnyone (+ OnPermamemtReturnedToHand for a hand bounce) trigger window pre-move — the same
// window the deletion path opens, but from the un-ported HandBounceClaass / DeckBounceClass / IPutSecurityPermanent
// routines, now opened at the sink. Uncapped reactors (Tfx*Counter) so firing (window opened at all — was a GAP)
// and collapse (one window per bounce class) are both observable. P1 is the turn player, so -1 reads as -1.

async Task BounceHandFiresLeave()
{
    EngineContext ctx = await LeaveReactorCtx("TfxOnLeaveFieldCounter", 41);
    HeadlessEntityId foe = await Foe(ctx, "BH1");
    await BounceViaSink(ctx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToHandKind, foe);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InHand(ctx, P2, foe), "the bounced Digimon reached the opponent's hand");
    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "the uncapped OnLeaveFieldAnyone reactor fired on the field->hand bounce (memory -1; was 0 = window not opened)");
}

async Task BounceHandLeaveCollapses()
{
    EngineContext ctx = await LeaveReactorCtx("TfxOnLeaveFieldCounter", 42);
    HeadlessEntityId f1 = await Foe(ctx, "BC1");
    HeadlessEntityId f2 = await Foe(ctx, "BC2");
    // Both bounced in ONE sink flush == one AS-IS HandBounceClaass over the list == one StackSkillInfos.
    await BounceViaSink(ctx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToHandKind, f1, f2);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "the uncapped reactor fired ONCE for the 2-card hand-bounce batch (memory -1); a per-card window would be -2");
}

async Task BounceHandFiresReturned()
{
    EngineContext ctx = await LeaveReactorCtx("TfxOnReturnToHandCounter", 43);
    HeadlessEntityId foe = await Foe(ctx, "BR1");
    await BounceViaSink(ctx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToHandKind, foe);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "the uncapped OnPermamemtReturnedToHand reactor fired on the field->hand bounce (memory -1)");
}

async Task BounceDeckFiresLeaveNotReturned()
{
    // A DECK bounce opens OnLeaveFieldAnyone (AS-IS DeckBottomBounceClass :2374) ...
    EngineContext leaveCtx = await LeaveReactorCtx("TfxOnLeaveFieldCounter", 44);
    HeadlessEntityId foeA = await Foe(leaveCtx, "BD1");
    await BounceViaSink(leaveCtx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToDeckBottomKind, foeA);
    await new GameFlowProcessor().RunToStableAsync(leaveCtx);
    AssertTrue(InLibrary(leaveCtx, P2, foeA), "the deck-bounced Digimon reached the opponent's library");
    AssertEqual(-1, leaveCtx.MemoryController.Current.Current, "deck bounce opened OnLeaveFieldAnyone (memory -1)");

    // ... but NOT OnPermamemtReturnedToHand (that timing is hand-bounce only — AS-IS DeckBounceClass never opens it).
    EngineContext returnCtx = await LeaveReactorCtx("TfxOnReturnToHandCounter", 45);
    HeadlessEntityId foeB = await Foe(returnCtx, "BD2");
    await BounceViaSink(returnCtx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToDeckBottomKind, foeB);
    await new GameFlowProcessor().RunToStableAsync(returnCtx);
    AssertEqual(0, returnCtx.MemoryController.Current.Current,
        "a deck bounce did NOT open OnPermamemtReturnedToHand (memory unchanged — hand-only timing)");
}

async Task NonFieldReturnDoesNotFire()
{
    // The field-origin gate: a ReturnToHand of a card NOT on the battle field (already in the trash) is not a
    // field departure, so NO leave window opens (AS-IS bounce routines only take battle-area Permanents).
    EngineContext ctx = await LeaveReactorCtx("TfxOnLeaveFieldCounter", 46);
    HeadlessEntityId trashed = await FoeInZone(ctx, "NF1", ChoiceZone.Trash);
    await BounceViaSink(ctx, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.ReturnToHandKind, trashed);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InHand(ctx, P2, trashed), "the trash card was moved to hand (the move itself still runs)");
    AssertEqual(0, ctx.MemoryController.Current.Current,
        "a non-field ReturnToHand opened NO OnLeaveFieldAnyone window (memory unchanged — field-origin gate)");
}

async Task<EngineContext> LeaveReactorCtx(string fixtureNumber, int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // DoneStartGame gate (new-model ActivateClass fires past Setup)
    ctx.MemoryController.Set(0);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId(fixtureNumber), fixtureNumber, "Reactor",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var reactor = new HeadlessEntityId($"1:battle:{fixtureNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId(fixtureNumber), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.RegisterEnteredCardEffects(reactor, P1);
    return ctx;
}

async Task BounceViaSink(EngineContext ctx, string kind, params HeadlessEntityId[] targets)
{
    var sink = new HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.GameEventQueue, context: ctx);
    foreach (HeadlessEntityId target in targets)
    {
        sink.Apply(new HeadlessDCGO.Engine.Headless.Effects.EffectMutation(
            kind,
            new HeadlessEntityId("effect:bouncer"),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.TargetEntityIdKey] = target.Value,
            }));
    }

    await sink.FlushAsync();
}

bool InHand(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Hand).Contains(id);

async Task<HeadlessEntityId> FoeInZone(EngineContext ctx, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{P2.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, zone));
    return id;
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> FoeWithSources(EngineContext ctx, string tag, int sourceCount)
{
    HeadlessEntityId id = await Foe(ctx, tag);
    if (sourceCount > 0)
    {
        var cards = (CardDatabase)ctx.CardRepository;
        var srcIds = new List<string>();
        for (int i = 0; i < sourceCount; i++)
        {
            var sdef = new HeadlessEntityId($"DEF:{tag}S{i}");
            cards.Upsert(new CardRecord(sdef, $"{tag}S{i}", $"{tag}S{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
            var sid = new HeadlessEntityId($"{P2.Value}:under:{tag}:{i}");
            ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, sdef, P2, Metadata: new Dictionary<string, object?>()));
            srcIds.Add(sid.Value);
        }

        ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec);
        var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal)
        {
            [DigivolutionStackReader.SourceIdsKey] = srcIds.ToArray(),
        };
        ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
    }

    return id;
}

bool InBattle(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.BattleArea).Contains(id);

bool InLibrary(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Library).Contains(id);

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
        ctx.GameEventQueue, context: ctx);
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
