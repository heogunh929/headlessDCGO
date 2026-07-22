// PRIM: triggered-activated resolution bridge — a card's ACTIVATED effect ([When Attacking] draw 1) at a general
// trigger timing (OnAllyAttack) is now resolved by GameFlowProcessor auto-processing (previously dropped — only
// IHeadlessCardEffect mutation triggers resolved). Fixture: TfxWhenAttackDraw.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Attacker = new("1:battle:ATK");

var tests = new (string Name, Func<Task> Body)[]
{
    ("[When Attacking] draw 1 (activated) fires via the auto-processing bridge on OnAllyAttack", DrawsOnAttack),
    ("a NON-subject card's OnAllyAttack does not fire another card's activated trigger", ScopedToSubject),
    ("[End of Your Turn] draw 1 (boundary, no subject) fires via the scan bridge — owner's turn only, once", EndTurnDraw),
    ("[When Attacking] draw 1 at OnDeclaration (attack-declaration, Digi-Burst timing) fires via the bridge (v4)", DrawsOnDeclaration),
    ("[on unsuspend] draw 1: fires once, and a SECOND unsuspend same turn does NOT re-fire (v3 cap)", UnsuspendOncePerTurn),
    ("OnDigivolutionCardDiscarded (event-broadcast): a NON-subject listener fires when the OPPONENT's host loses a source", BroadcastDigiTrashFires),
    ("OnDigivolutionCardDiscarded gate scopes: OWN host's trashed source does NOT fire the opponent-gated listener", BroadcastDigiTrashGateScopes),
    ("OnEndBattle (event-broadcast): a winner card's [End of Battle] activated effect fires, reading event.winnerIds", BroadcastEndBattleWinnerFires),
    ("OnEndBattle gate scopes: a NON-winner card's win-gated effect does NOT fire", BroadcastEndBattleNonWinnerScoped),
    ("OnTappedAnyone (event-broadcast): a NON-subject card fires when an OPPONENT's Digimon is suspended", BroadcastOppSuspendFires),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DrawsOnAttack()
{
    EngineContext ctx = await Setup();
    // Faithful drive (C2r): the OnAllyAttack "[When Attacking]" window is opened SOLELY by the inline
    // StackSkillInfos(OnAllyAttack) inside a REAL attack declaration (AttackProcess.cs:290-295). The synthetic
    // OnAllyAttack event conversion in SkillWindowSupply was retired at the C2r flip (a raw OnAllyAttack event is
    // no longer supply-converted), so emitting the event directly is a dead path. Declare a real attack and let
    // the game-loop pump drain the stacked ActivateClass — the same drive AttackPermanentAction uses in play.
    CardEffectRegistrar.RegisterCard(ctx, Attacker, P1);
    int before = HandCount(ctx, P1);
    LegalAction attack = HeadlessActionFactory.DeclareAttack(P1, Attacker, P2, targetId: null, isDirectAttack: true);
    ActionProcessResult result = new HeadlessDCGO.Engine.Headless.Runtime.AttackPermanentAction().Process(attack, ctx);
    AssertEqual(true, result.IsSuccess, $"attack declared ({result.Message})");
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "the attacker's [When Attacking] draw 1 resolved via the real-attack bridge");
}

async Task DrawsOnDeclaration()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    var cards = (HeadlessDCGO.Engine.Headless.DataLoading.CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxWhenDeclareDraw"), "TfxWhenDeclareDraw", "WD", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var attacker = new HeadlessEntityId("1:battle:WD");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(attacker, new HeadlessEntityId("TfxWhenDeclareDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 0; i < 3; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:d{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId("TfxWhenDeclareDraw"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }
    int before = HandCount(ctx, P1);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnDeclaration, actor: P1, subject: attacker);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "the attacker's OnDeclaration draw 1 resolved via the bridge");
}

async Task ScopedToSubject()
{
    EngineContext ctx = await Setup();
    int before = HandCount(ctx, P1);
    // subject = a different card with no activated trigger -> the attacker's draw must NOT fire.
    var other = new HeadlessEntityId("1:battle:OTHER");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(other, new HeadlessEntityId("DEF:O"), P1, Metadata: new Dictionary<string, object?>()));
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnAllyAttack, actor: P1, subject: other);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before, HandCount(ctx, P1), "another card's attack did not fire the fixture's draw");
}

async Task EndTurnDraw()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase   // P1's turn
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnDraw"), "TfxEndTurnDraw", "ET", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var mine = new HeadlessEntityId("1:battle:MINE");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(mine, new HeadlessEntityId("TfxEndTurnDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, mine, ChoiceZone.None, ChoiceZone.BattleArea));
    // an opponent card with the same effect — must NOT fire on P1's turn end.
    var foe = new HeadlessEntityId("2:battle:FOE");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, new HeadlessEntityId("TfxEndTurnDraw"), P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 1; i <= 4; i++) { var lib=new HeadlessEntityId($"1:lib:{i}"); cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:E{i}"),$"E{i}",$"E{i}",new Dictionary<string,object?>(StringComparer.Ordinal),CardType:"Digimon")); ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib,new HeadlessEntityId($"DEF:E{i}"),P1,Metadata:new Dictionary<string,object?>())); await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1,lib,ChoiceZone.None,ChoiceZone.Library)); }

    int before = HandCount(ctx, P1);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1, subject: default);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "[End of Your Turn] draw fired once for the owner (boundary scan)");
}

async Task UnsuspendOncePerTurn()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 4);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxUnsuspendDraw"), "TfxUnsuspendDraw", "U", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var sub = new HeadlessEntityId("1:battle:SUB");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sub, new HeadlessEntityId("TfxUnsuspendDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, sub, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 1; i <= 6; i++) { var lib=new HeadlessEntityId($"1:lib:{i}"); cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:U{i}"),$"U{i}",$"U{i}",new Dictionary<string,object?>(StringComparer.Ordinal),CardType:"Digimon")); ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib,new HeadlessEntityId($"DEF:U{i}"),P1,Metadata:new Dictionary<string,object?>())); await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1,lib,ChoiceZone.None,ChoiceZone.Library)); }

    int before = HandCount(ctx, P1);
    // first unsuspend
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnUnTappedAnyone", actor: P1, subject: sub);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "first unsuspend drew 1");
    // second unsuspend SAME turn -> capped, no re-draw
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnUnTappedAnyone", actor: P1, subject: sub);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "second unsuspend same turn did NOT re-fire (once-per-turn cap)");
}

async Task BroadcastDigiTrashFires()
{
    (EngineContext ctx, HeadlessEntityId host) = await SetupDigiTrash(hostOwner: P2);
    int before = HandCount(ctx, P1);
    // Real emission path: trash 1 digivolution source off the OPPONENT's host (emits the event with
    // subject = host + discardedCardIds metadata), then let auto-processing bridge it.
    int trashed = await DigivolutionStackHelpers.TrashSourcesAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, 1, fromBottom: true,
        cancellationToken: default, gameEventQueue: ctx.GameEventQueue, context: ctx);
    AssertEqual(1, trashed, "one digivolution source trashed off the host");
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "the non-subject listener's draw fired via the event-broadcast bridge");
}

async Task BroadcastDigiTrashGateScopes()
{
    (EngineContext ctx, HeadlessEntityId host) = await SetupDigiTrash(hostOwner: P1);
    int before = HandCount(ctx, P1);
    // The host is the LISTENER'S OWN Digimon — the opponent-scope gate must reject it.
    int trashed = await DigivolutionStackHelpers.TrashSourcesAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, 1, fromBottom: true,
        cancellationToken: default, gameEventQueue: ctx.GameEventQueue, context: ctx);
    AssertEqual(1, trashed, "one digivolution source trashed off the host");
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before, HandCount(ctx, P1), "own host's trash did NOT pass the opponent-scope gate");
}

// Listener (P1, TfxDigiTrashDraw) on the battle area + a host Digimon (hostOwner) with one digivolution source.
async Task<(EngineContext Ctx, HeadlessEntityId Host)> SetupDigiTrash(HeadlessPlayerId hostOwner)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxDigiTrashDraw"), "TfxDigiTrashDraw", "DT", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var listener = new HeadlessEntityId("1:battle:LIS");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(listener, new HeadlessEntityId("TfxDigiTrashDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, listener, ChoiceZone.None, ChoiceZone.BattleArea));

    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:HOST"), "HOST", "H", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var host = new HeadlessEntityId($"{hostOwner.Value}:battle:HOST");
    var source = new HeadlessEntityId($"{hostOwner.Value}:src:1");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId("DEF:HOST"), hostOwner, Metadata: new Dictionary<string, object?>()));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DEF:HOST"), hostOwner,
        Metadata: new Dictionary<string, object?> { ["sourceIds"] = new[] { source.Value } }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(hostOwner, host, ChoiceZone.None, ChoiceZone.BattleArea));

    for (int i = 1; i <= 3; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:dt{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:DT{i}"), $"DT{i}", $"DT{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:DT{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }

    return (ctx, host);
}

async Task BroadcastEndBattleWinnerFires()
{
    (EngineContext ctx, HeadlessEntityId winner, HeadlessEntityId loser) = await SetupEndBattle();
    int before = HandCount(ctx, P1);
    // Faithful drive: the OnEndBattle "[End of Battle]" window is opened SOLELY by the inline
    // StackSkillInfos(battle.hashtable, OnEndBattle) inside BattleResolver.FinalizeAsync (BattleResolver.cs:397-402,
    // AS-IS CardController.cs:4718). The winner gate CanTriggerWhenWinBattle reads the RICH battle object
    // (WinnerPermanents list / WasTie / LoserCard), which the flat OnEndBattle carrier GameEvent cannot carry —
    // so supply-converting the emitted event would be a fabrication. Open the window the way the resolver does:
    // build the battle payload with the winner card's permanent among the winners, then let the pump drain it.
    await DriveEndBattleWindow(ctx, winnerPerm: (winner, P1), loserPerm: (loser, P2));
    AssertEqual(before + 1, HandCount(ctx, P1), "the winner card's [End of Battle] draw fired via the real OnEndBattle window (WhenWinBattle read the battle payload)");
}

async Task BroadcastEndBattleNonWinnerScoped()
{
    (EngineContext ctx, HeadlessEntityId winner, HeadlessEntityId loser) = await SetupEndBattle();
    int before = HandCount(ctx, P1);
    // The winner card's permanent is NOT among the battle's WinnerPermanents (only the opponent is a winner) ->
    // CanTriggerWhenWinBattle's `permanent.cardSources.Contains(card)` fails -> its win-gated effect must NOT fire.
    await DriveEndBattleWindow(ctx, winnerPerm: (loser, P2), loserPerm: (winner, P1));
    AssertEqual(before, HandCount(ctx, P1), "a non-winner card's win-gated effect did not fire");
}

// Open the AS-IS OnEndBattle window exactly as BattleResolver.FinalizeAsync does (BattleResolver.cs:280-402):
// build the rich battle payload via BuildBattleResultPayload and StackSkillInfos(battle.hashtable, OnEndBattle)
// under the ambient match scope; the game-loop pump drains the stacked ActivateClass reactors.
async Task DriveEndBattleWindow(EngineContext ctx, (HeadlessEntityId Id, HeadlessPlayerId Owner) winnerPerm, (HeadlessEntityId Id, HeadlessPlayerId Owner) loserPerm)
{
    var winners = new List<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent>
        { new(ctx, winnerPerm.Id, winnerPerm.Owner) };
    var losers = new List<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent>
        { new(ctx, loserPerm.Id, loserPerm.Owner) };
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.IBattle battle =
        HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons.BuildBattleResultPayload(
            winnerPermanents: winners, winnerPermanentsReal: winners,
            loserPermanents: losers, loserPermanentsReal: losers, loserCard: null!, wasTie: false);
    using (AmbientMatchContext.Enter(ctx))
    {
        await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(ctx).StackSkillInfos(
            battle.hashtable, HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming.OnEndBattle);
    }
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

// P1 has the TfxWinBattleDraw card in play + library; an opponent card sits in play as the loser.
async Task<(EngineContext Ctx, HeadlessEntityId Winner, HeadlessEntityId Loser)> SetupEndBattle()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxWinBattleDraw"), "TfxWinBattleDraw", "WB", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:LOSE"), "LOSE", "LO", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var winner = new HeadlessEntityId("1:battle:WIN");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(winner, new HeadlessEntityId("TfxWinBattleDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, winner, ChoiceZone.None, ChoiceZone.BattleArea));
    var loser = new HeadlessEntityId("2:battle:LOSE");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(loser, new HeadlessEntityId("DEF:LOSE"), P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, loser, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 1; i <= 3; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:wb{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:WB{i}"), $"WB{i}", $"WB{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:WB{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }
    return (ctx, winner, loser);
}

async Task BroadcastOppSuspendFires()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 12);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase   // P1's turn
    ctx.MemoryController.Initialize(0);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOppSuspendMemory"), "TfxOppSuspendMemory", "OS", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:OPPD"), "OPPD", "OD", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    // P1's reacting card (a different card than the suspended subject)
    var reactor = new HeadlessEntityId("1:battle:REACT");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId("TfxOppSuspendMemory"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
    // P2's Digimon that becomes suspended (the event subject)
    var oppDigi = new HeadlessEntityId("2:battle:OPPD");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(oppDigi, new HeadlessEntityId("DEF:OPPD"), P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, oppDigi, ChoiceZone.None, ChoiceZone.BattleArea));

    int beforeMem = ctx.MemoryController.Current.Current;
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnTappedAnyone", actor: P2, subject: oppDigi);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(beforeMem + 1, ctx.MemoryController.Current.Current, "opponent's Digimon suspended -> the non-subject P1 card gained 1 memory via the broadcast bridge");
}

// --- Harness ---
async Task<EngineContext> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxWhenAttackDraw"), "TfxWhenAttackDraw", "Atk", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Attacker, new HeadlessEntityId("TfxWhenAttackDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Attacker, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 1; i <= 5; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:L{i}"), $"L{i}", $"L{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:L{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }
    return ctx;
}
int HandCount(EngineContext ctx, HeadlessPlayerId p) => ((IZoneStateReader)ctx.ZoneMover).GetCards(p, ChoiceZone.Hand).Count;
static void AssertEqual<T>(T e, T a, string l) { if (!EqualityComparer<T>.Default.Equals(e,a)) throw new InvalidOperationException($"{l}: expected '{e}', actual '{a}'."); }
