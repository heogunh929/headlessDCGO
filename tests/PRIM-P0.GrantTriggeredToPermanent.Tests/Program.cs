// PRIM-P0 B.O.5-tail: grant a TRIGGERED nested effect to a permanent with a duration (AS-IS temp
// AddEffectToPermanent, e.g. EX8_059 "1 Digimon gains '[On Deletion] ...' until end of turn").
//   - tractable subset (target survives): AddEffectToPermanent registers the nested trigger; it fires at its
//     timing and expires at the duration boundary.
//   - self-[On Deletion] (fires on the target's OWN removal): AddSelfRemovalEffectToPermanent marks the binding
//     SurviveOwnLeave so leave-play cleanup does not drop it before OnDeletion resolves; it fires on the real
//     deletion then self-removes (DelayedOneShot). Target-scoped via the nested effect's self-gate.
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
    ("tractable: a triggered grant fires at its timing while the target is alive ([End of Turn] +2)", FiresAtTiming),
    ("tractable: the grant expires at its duration boundary and no longer fires", ExpiresAtBoundary),
    ("self-[On Deletion]: survives leave-play cleanup, fires on the target's real deletion (+2), self-removes", SelfDeletionFires),
    ("self-[On Deletion]: target-scoped — another card's deletion does NOT fire it", SelfDeletionScoped),
    ("self-[On Deletion]: after the duration expires it no longer fires on deletion", SelfDeletionExpires),
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
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 4000);
    GrantEndTurn(ctx, src, target);
    ctx.MemoryController.Set(0);
    await EmitEndTurn(ctx);
    AssertEqual(2, ctx.MemoryController.Current.Current, "the granted [End of Turn] trigger fired");
}

async Task ExpiresAtBoundary()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 4000);
    GrantEndTurn(ctx, src, target);
    ctx.MemoryController.Set(0);
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    await EmitEndTurn(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "after expiry the grant no longer fires");
}

async Task SelfDeletionFires()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 0);   // 0-DP -> really swept
    GrantSelfDeletion(ctx, src, target);
    ctx.MemoryController.Set(0);
    await Sweep(ctx);
    AssertEqual(2, ctx.MemoryController.Current.Current, "survived cleanup and fired on the target's deletion");
    AssertEqual(0, ctx.EffectRegistry.GetEffectsForTiming(TriggerTimings.OnDeletion).Count, "self-removed after firing");
}

async Task SelfDeletionScoped()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 4000);   // survives
    await Put(ctx, "OTH", 0);                    // this one is deleted
    GrantSelfDeletion(ctx, src, target);
    ctx.MemoryController.Set(0);
    await Sweep(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "another card's deletion did NOT fire the target's grant");
}

async Task SelfDeletionExpires()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 0);
    GrantSelfDeletion(ctx, src, target);
    ctx.MemoryController.Set(0);
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);   // UntilOpponentTurnEnd expires the grant first
    await Sweep(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "expired before the deletion -> no fire");
}

// --- Harness -------------------------------------------------------------

void GrantEndTurn(EngineContext ctx, HeadlessEntityId src, HeadlessEntityId target)
{
    ICardEffect nested = new TriggeredGainMemoryEffect(V(ctx, target), EffectTiming.OnEndTurn, amount: 2, "[End of Your Turn] Gain 2 memory.");
    CardEffectCommons.AddEffectToPermanent(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), nested, EffectTiming.OnEndTurn);
}

void GrantSelfDeletion(EngineContext ctx, HeadlessEntityId src, HeadlessEntityId target)
{
    ICardEffect nested = CardEffectFactory.AddMemoryTriggerEffect(
        EffectTiming.OnDestroyedAnyone, amount: 2, isInheritedEffect: false, card: V(ctx, target), condition: null,
        description: "[On Deletion] Gain 2 memory.",
        triggerGate: rc => rc.EffectContext.TriggerEntityId is HeadlessEntityId subj && subj == target,
        isOptional: false);
    CardEffectCommons.AddSelfRemovalEffectToPermanent(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), nested, EffectTiming.OnDestroyedAnyone);
}

async Task EmitEndTurn(EngineContext ctx)
{
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1, subject: default);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

async Task Sweep(EngineContext ctx)
{
    await DpZeroDeletionHelpers.SweepAsync(ctx, new[] { P1, P2 });
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1's turn (target owner) for the gain-memory gate
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, string tag, int dp)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1, P1);
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1);

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
