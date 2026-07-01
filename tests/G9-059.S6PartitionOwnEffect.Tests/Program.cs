using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S6 (G9-059): Partition triggers only when a card leaves "other than by one of YOUR effects or in battle".
// The engine now stamps DeletedByOwnEffectKey (source owner == card owner) on an effect deletion, so an
// own-effect deletion is distinguishable from an opponent-effect deletion. (Before, the engine could not tell.)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Deletion by an OPPONENT effect -> DeletedByOwnEffect is false (Partition may fire)", () => Check(sourceOwner: 2, expectOwn: false)),
    ("Deletion by the card's OWN effect -> DeletedByOwnEffect is true (Partition excluded)", () => Check(sourceOwner: 1, expectOwn: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Check(int sourceOwner, bool expectOwn)
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET");
    var source = await Place(ctx, new HeadlessPlayerId(sourceOwner), "SOURCE");

    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, source,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = target.Value }));
    await sink.FlushAsync();

    ctx.CardInstanceRepository.TryGetInstance(target, out CardInstanceRecord? rec);
    bool own = rec is not null && rec.Metadata.TryGetValue(DeletionReplacementGate.DeletedByOwnEffectKey, out object? raw) && raw is bool b && b;
    AssertTrue(own == expectOwn, $"DeletedByOwnEffect == {expectOwn} (source owner {sourceOwner})");
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 959);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
