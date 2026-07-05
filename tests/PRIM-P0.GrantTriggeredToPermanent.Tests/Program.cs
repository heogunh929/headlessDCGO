// PRIM-P0 B.O.5-tail: grant a TRIGGERED nested effect to a permanent with a duration (AS-IS temp
// AddEffectToPermanent). PROVEN TRACTABLE SUBSET: a triggered grant at a timing where the target SURVIVES
// (here [End of Your Turn]) fires through the existing collect→gate→fire path and expires at its duration.
//
// KNOWN LIMITATION (STOP): a self-[On Deletion] grant (fires on the TARGET's own removal) does NOT fire — when
// the target leaves play, CardLeavePlayCleanup removes every binding whose SourceEntityId is the target
// (including the grant) BEFORE OnDeletion resolves. That canonical case (EX8_059) needs a leave-play cleanup
// EXEMPTION for grant-on-self-removal, a new shared-path mechanism — see the design doc. Empirically confirmed.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a triggered grant fires at its timing while the target is alive ([End of Turn] +2)", FiresAtTiming),
    ("the grant expires at its duration boundary and no longer fires", ExpiresAtBoundary),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task FiresAtTiming()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC");
    var target = await Put(ctx, P1, "TGT");
    Grant(ctx, src, target, EffectDuration.UntilOpponentTurnEnd);
    ctx.MemoryController.Set(0);

    await EmitEndTurn(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current, "the granted [End of Turn] trigger fired");
}

async Task ExpiresAtBoundary()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC");
    var target = await Put(ctx, P1, "TGT");
    Grant(ctx, src, target, EffectDuration.UntilOpponentTurnEnd);
    ctx.MemoryController.Set(0);

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);   // UntilOpponentTurnEnd expires
    await EmitEndTurn(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "after expiry the grant no longer fires");
}

// --- Harness -------------------------------------------------------------

// The nested trigger is built with the TARGET's CardSource (fires as the target's own trigger) and registered
// on the target with a duration via the existing AddEffectToPermanent (proven for the triggered case here).
void Grant(EngineContext ctx, HeadlessEntityId src, HeadlessEntityId target, EffectDuration duration)
{
    ICardEffect nested = new TriggeredGainMemoryEffect(V(ctx, target), EffectTiming.OnEndTurn, amount: 2, "[End of Your Turn] Gain 2 memory.");
    CardEffectCommons.AddEffectToPermanent(Perm(ctx, target), duration, V(ctx, src), nested, EffectTiming.OnEndTurn);
}

async Task EmitEndTurn(EngineContext ctx)
{
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1, subject: default);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1's turn (target owner) for the gain-memory gate
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : P1;

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
