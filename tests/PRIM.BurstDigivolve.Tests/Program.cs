// PRIM special-play: Burst Digivolution — a hand card digivolves onto a battle-area target Digimon while a
// matching Tamer returns to hand (AS-IS BurstDigivolutionCondition). Fixture: TfxBurstDigivolve.
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
    ("target + tamer present: burst is offered, tamer returns to hand, card digivolves onto target", BurstPlays),
    ("no matching tamer: the burst is NOT offered", NoTamerNoPlay),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task BurstPlays()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxBurstDigivolve", "Digimon", ChoiceZone.Hand);
    var target = await Put(ctx, P1, "TARGET", "Digimon", ChoiceZone.BattleArea);
    var tamer = await Put(ctx, P1, "TAMER", "Tamer", ChoiceZone.BattleArea);
    ctx.MemoryController.Set(5);

    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    var burst = legal.FirstOrDefault(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value);
    AssertTrue(burst is not null, "the burst play is offered");

    var result = await new SpecialPlayAction().ProcessAsync(burst!, ctx);
    AssertTrue(result.IsSuccess, $"the burst resolved ({result.Message})");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, top), "the burst card is on the battle area");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Hand, tamer), "the tamer returned to hand");
    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, top);
    AssertTrue(stack.UnderCards.Any(s => s.InstanceId == target), "the target became a source under the burst card");
}

async Task NoTamerNoPlay()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxBurstDigivolve", "Digimon", ChoiceZone.Hand);
    await Put(ctx, P1, "TARGET", "Digimon", ChoiceZone.BattleArea);   // target but no tamer
    ctx.MemoryController.Set(5);
    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(!legal.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value),
        "no tamer -> the burst is not offered");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 75);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}
async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string cardType, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);
static void AssertTrue(bool v, string l) { if (!v) throw new InvalidOperationException($"{l}: expected true."); }
