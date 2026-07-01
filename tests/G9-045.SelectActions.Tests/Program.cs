using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-045): declarative select-action vocabulary — the headless replacement for the AS-IS
// SuspendPermanentsClass.Tap() / bounce / unsuspend coroutines. Each applies its mode's mutation to the
// selected target.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("SelectAndSuspendEffect -> target suspended", () => Act(
        c => CardEffectFactory.SelectAndSuspendEffect(c, _ => true, 1, false, "suspend"), suspendedStart: false, expectSuspended: true)),
    ("SelectAndUnsuspendEffect -> target unsuspended", () => Act(
        c => CardEffectFactory.SelectAndUnsuspendEffect(c, _ => true, 1, false, "unsuspend"), suspendedStart: true, expectSuspended: false)),
    ("SelectAndBounceEffect -> target returned to hand", BounceTest),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Act(Func<CardSource, ICardEffect> build, bool suspendedStart, bool expectSuspended)
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", suspended: false);
    var target = await Place(ctx, P1, "TGT", suspended: suspendedStart);
    var eff = (ActivatedSelectEffect)build(new CardSource(ctx, src, P1));
    var sink = Sink(ctx);
    eff.Apply(sink, new[] { target });
    await sink.FlushAsync();
    AssertTrue(ReadBool(ctx, target, "isSuspended") == expectSuspended, $"isSuspended == {expectSuspended}");
}

async Task BounceTest()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", suspended: false);
    var target = await Place(ctx, P1, "TGT", suspended: false);
    var eff = (ActivatedSelectEffect)CardEffectFactory.SelectAndBounceEffect(new CardSource(ctx, src, P1), _ => true, 1, false, "bounce");
    var sink = Sink(ctx);
    eff.Apply(sink, new[] { target });
    await sink.FlushAsync();
    var reader = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(reader.GetCards(P1, ChoiceZone.Hand).Contains(target), "target in hand");
    AssertTrue(!reader.GetCards(P1, ChoiceZone.BattleArea).Contains(target), "target left battle area");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext ctx) => new(
    ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue);

bool ReadBool(EngineContext ctx, HeadlessEntityId id, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 945);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
