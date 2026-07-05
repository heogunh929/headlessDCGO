// PRIM special-play: Digi-Burst — "[Digi-Burst N] <effect>" trashes N of the card's own digivolution sources
// as a cost, then resolves the inner effect. Gated on >= N sources. Fixture: TfxDigiBurst ([Digi-Burst 2] Draw 1).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[Digi-Burst 2] with 3 sources: trashes 2 sources (1 remains) and draws 1", BurstsAndResolves),
    ("[Digi-Burst 2] with only 1 source: does NOT fire (no trash, no draw)", GateBlocks),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task BurstsAndResolves()
{
    EngineContext ctx = Ctx();
    var s = new[] { MakeSource(ctx, "S1"), MakeSource(ctx, "S2"), MakeSource(ctx, "S3") };
    var burst = await PutWithSources(ctx, "TfxDigiBurst", s);
    for (int i = 1; i <= 4; i++) await Lib(ctx, i);
    int handBefore = HandCount(ctx, P1);

    await ActivatedEffectResolver.ResolveAsync(ctx, burst, P1, EffectTiming.OptionSkill);

    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, burst);
    AssertEqual(1, stack.UnderCards.Count, "2 of the 3 sources were trashed (1 remains)");
    AssertEqual(handBefore + 1, HandCount(ctx, P1), "the [Digi-Burst] inner effect (draw 1) resolved");
}

async Task GateBlocks()
{
    EngineContext ctx = Ctx();
    var burst = await PutWithSources(ctx, "TfxDigiBurst", new[] { MakeSource(ctx, "S1") });   // only 1 source
    for (int i = 1; i <= 4; i++) await Lib(ctx, i);
    int handBefore = HandCount(ctx, P1);

    await ActivatedEffectResolver.ResolveAsync(ctx, burst, P1, EffectTiming.OptionSkill);

    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, burst);
    AssertEqual(1, stack.UnderCards.Count, "the single source was NOT trashed (gate blocked Digi-Burst 2)");
    AssertEqual(handBefore, HandCount(ctx, P1), "the inner effect did NOT resolve (no draw)");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}
HeadlessEntityId MakeSource(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:src:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1, Metadata: new Dictionary<string, object?>()));
    return id;
}
async Task<HeadlessEntityId> PutWithSources(EngineContext ctx, string cardNumber, HeadlessEntityId[] sources)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:battle:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [DigivolutionStackReader.SourceIdsKey] = sources.Select(x => x.Value).ToArray() }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}
async Task Lib(EngineContext ctx, int i)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:L{i}"), $"L{i}", $"L{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:lib:{i}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:L{i}"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library));
}
int HandCount(EngineContext ctx, HeadlessPlayerId p) => ((IZoneStateReader)ctx.ZoneMover).GetCards(p, ChoiceZone.Hand).Count;
static void AssertEqual<T>(T e, T a, string l) { if (!EqualityComparer<T>.Default.Equals(e,a)) throw new InvalidOperationException($"{l}: expected '{e}', actual '{a}'."); }
