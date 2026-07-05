// PRIM special-play: max-under-Tamer DigiXros — a DigiXros material slot satisfied by a card UNDER A TAMER
// (its digivolution source). The play is offered; the source detaches from the Tamer and fuses under the new top.
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
    ("a DigiXros slot is satisfied by an UNDER-TAMER source (offered + detached + fused)", UnderTamerPlays),
    ("a source under a NON-Tamer permanent is NOT a candidate", NonTamerExcluded),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task UnderTamerPlays()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxMaxUnderTamerXros", "TfxMaxUnderTamerXros", ChoiceZone.Hand);
    var mat = MakeSource(ctx, P1, "MAT");                          // a MAT card, off-field
    var tamer = await PutWithSources(ctx, P1, "TAM", "Tamer", new[] { mat });   // Tamer with MAT as its source
    ctx.MemoryController.Set(5);

    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    var xros = legal.FirstOrDefault(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value);
    AssertTrue(xros is not null, "the DigiXros play is offered using the under-Tamer source");

    var result = await new SpecialPlayAction().ProcessAsync(xros!, ctx);
    AssertTrue(result.IsSuccess, $"the DigiXros resolved ({result.Message})");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, top), "the DigiXros top is on the battle area");
    // MAT detached from the Tamer's sources.
    var tamerStack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, tamer);
    AssertTrue(!tamerStack.UnderCards.Any(s => s.InstanceId == mat), "MAT detached from the Tamer's source stack");
    // MAT is now a source under the new DigiXros top.
    var topStack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, top);
    AssertTrue(topStack.UnderCards.Any(s => s.InstanceId == mat), "MAT fused as a source under the new top");
}

async Task NonTamerExcluded()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxMaxUnderTamerXros", "TfxMaxUnderTamerXros", ChoiceZone.Hand);
    var mat = MakeSource(ctx, P1, "MAT");
    await PutWithSources(ctx, P1, "DIG", "Digimon", new[] { mat });   // a Digimon (NOT Tamer) with MAT source
    ctx.MemoryController.Set(5);
    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(!legal.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value),
        "a source under a non-Tamer is not an under-Tamer candidate");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 72);
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
HeadlessEntityId MakeSource(EngineContext ctx, HeadlessPlayerId owner, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), owner, Metadata: new Dictionary<string, object?>()));
    return id;   // off-field; becomes a source under the host
}
async Task<HeadlessEntityId> PutWithSources(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string cardType, HeadlessEntityId[] sources)
{
    var host = await Put(ctx, owner, cardNumber, cardType, ChoiceZone.BattleArea);
    ctx.CardInstanceRepository.TryGetInstance(host, out var rec);
    var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal) { [DigivolutionStackReader.SourceIdsKey] = sources.Select(s => s.Value).ToArray() };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
    return host;
}
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);
static void AssertTrue(bool v, string l) { if (!v) throw new InvalidOperationException($"{l}: expected true."); }
