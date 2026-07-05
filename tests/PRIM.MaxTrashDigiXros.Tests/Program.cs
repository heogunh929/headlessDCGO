// PRIM special-play: max-trash DigiXros — a DigiXros material slot satisfied by a card FROM THE TRASH (up to
// AddMaxTrashCountDigiXros count). The play is offered and the trash card fuses under the new top.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a DigiXros slot is satisfied by a TRASH material (offered + fused)", TrashMaterialPlays),
    ("without a trash material available, the DigiXros is NOT offered", NoTrashMaterialNoPlay),
    ("a slot NOT allowed from trash (maxTrash 0) is not satisfied by a trash card", CapZeroBlocks),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task TrashMaterialPlays()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxMaxTrashXros", "TfxMaxTrashXros", ChoiceZone.Hand);
    var mat = await Put(ctx, P1, "MAT", "MAT", ChoiceZone.Trash);   // material only in trash
    ctx.MemoryController.Set(5);

    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    var xros = legal.FirstOrDefault(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value);
    AssertTrue(xros is not null, "the DigiXros play is offered using the trash material");

    var result = await new SpecialPlayAction().ProcessAsync(xros!, ctx);
    AssertTrue(result.IsSuccess, $"the DigiXros resolved ({result.Message})");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, top), "the DigiXros top is on the battle area");
    AssertTrue(!InZone(ctx, P1, ChoiceZone.Trash, mat), "the trash material left the trash (fused as a source)");
}

async Task NoTrashMaterialNoPlay()
{
    EngineContext ctx = Ctx();
    var top = await Put(ctx, P1, "TfxMaxTrashXros", "TfxMaxTrashXros", ChoiceZone.Hand);
    // no MAT anywhere
    ctx.MemoryController.Set(5);
    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(!legal.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value),
        "no material -> the DigiXros is not offered");
}

async Task CapZeroBlocks()
{
    EngineContext ctx = Ctx();
    // TfxZeroTrashXros: same but maxTrash 0 -> a trash MAT must NOT satisfy the slot.
    var top = await Put(ctx, P1, "TfxZeroTrashXros", "TfxZeroTrashXros", ChoiceZone.Hand);
    await Put(ctx, P1, "MAT", "MAT", ChoiceZone.Trash);
    ctx.MemoryController.Set(5);
    var legal = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(!legal.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out var c) && c?.ToString() == top.Value),
        "maxTrash 0 -> trash material does not satisfy the slot");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 71);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}
async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);
static void AssertTrue(bool v, string l) { if (!v) throw new InvalidOperationException($"{l}: expected true."); }
