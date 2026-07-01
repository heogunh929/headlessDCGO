using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-048): special-play declaration factories. A DigiXros / Blast / Blast-DNA card DECLARES its
// recipe (SpecialPlayRecipeRegistry, keyed by card number); SpecialPlayAction then offers/executes it. These
// factories register the recipe and return a no-op marker.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("DigiXrosEffectFromNames -> DigiXros recipe with named materials registered", DigiXros),
    ("BlastDigivolveEffect -> Blast recipe registered", Blast),
    ("BlastDNADigivolveEffect -> DnaDigivolve recipe with material names registered", BlastDNA),
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

async Task DigiXros()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "XROS", "BT10-012");
    var eff = CardEffectFactory.DigiXrosEffectFromNames(new CardSource(ctx, id, P1), 0, null, "Shoutmon", "Ballistamon");
    AssertTrue(eff is SpecialPlayRecipeMarkerEffect, "factory returns a marker effect");
    AssertTrue(SpecialPlayRecipeRegistry.TryGet("BT10-012", out SpecialPlayRecipe? r) && r is not null, "recipe registered by card number");
    AssertTrue(r!.Kind == SpecialPlayKind.DigiXros, "kind DigiXros");
    AssertTrue(r.Materials.Any(m => m.Label == "Shoutmon") && r.Materials.Any(m => m.Label == "Ballistamon"), "material slots stored");
}

async Task Blast()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "BLAST", "BT17-078");
    CardEffectFactory.BlastDigivolveEffect(new CardSource(ctx, id, P1), null);
    AssertTrue(SpecialPlayRecipeRegistry.TryGet("BT17-078", out SpecialPlayRecipe? r) && r is not null && r.Kind == SpecialPlayKind.Blast, "Blast recipe registered");
}

async Task BlastDNA()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "BDNA", "BT17-078b");
    var conds = new List<BlastDNACondition> { BlastDNACondition.ByName("Omnimon"), BlastDNACondition.ByName("WarGreymon") };
    CardEffectFactory.BlastDNADigivolveEffect(new CardSource(ctx, id, P1), conds, null);
    AssertTrue(SpecialPlayRecipeRegistry.TryGet("BT17-078b", out SpecialPlayRecipe? r) && r is not null && r.Kind == SpecialPlayKind.DnaDigivolve, "DnaDigivolve recipe registered");
    AssertTrue(r!.Materials.Any(m => m.Label == "Omnimon") && r.Materials.Any(m => m.Label == "WarGreymon"), "DNA material slots stored");
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 948);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 6 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
