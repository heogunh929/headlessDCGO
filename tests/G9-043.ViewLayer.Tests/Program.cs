using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5-0 (G9-043): the card-query view layer (CardSource + Permanent members) that card predicates read.
// Verifies the members compile AND evaluate off engine state, so `permanentCondition`/`cardCondition`
// predicates can be honored.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CardSource: colors/level/type/name/traits read off the definition", CardSourceViews),
    ("Permanent: TopCard + DP(+modifier) + level + IsDigimon + sources", PermanentViews),
    ("Predicate honored: Func<Permanent,bool> evaluates (DP==0 & has-Lucemon)", PredicateEval),
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

async Task CardSourceViews()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "Agu", type: "Digimon", level: 4, dp: 3000,
        colors: new[] { "Red" }, traits: new[] { "Reptile" }, name: "Greymon");
    var cs = new CardSource(ctx, id, P1);
    AssertTrue(cs.IsDigimon && !cs.IsTamer, "IsDigimon");
    AssertTrue(cs.Level == 4 && cs.HasLevel && cs.IsLevel(4), "Level");
    AssertTrue(cs.HasCardColor("red") && !cs.HasCardColor("Blue"), "HasCardColor (case-insensitive)");
    AssertTrue(cs.EqualsCardName("greymon") && cs.ContainsCardName("grey") && !cs.EqualsCardName("Agumon"), "name");
    AssertTrue(cs.EqualsTraits("Reptile") && cs.ContainsTraits("rept"), "traits");
}

async Task PermanentViews()
{
    EngineContext ctx = Ctx();
    var top = await Place(ctx, P1, "Top", type: "Digimon", level: 5, dp: 5000, colors: new[] { "Blue" }, traits: null, name: "MetalGreymon");
    var perm = new Permanent(ctx, top, P1);
    AssertTrue(perm.IsDigimon && perm.Level == 5, "Permanent level/type via TopCard");
    AssertTrue(perm.TopCard.EqualsCardName("MetalGreymon"), "TopCard reuses CardSource");
    AssertTrue(perm.DP == 5000, "base DP");
    // continuous +2000 DP → effective DP folds it
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeSelfDPStaticEffect(2000, false, new CardSource(ctx, top, P1), null).ToBinding($"dp:{top.Value}"));
    AssertTrue(perm.DP == 7000, "DP folds continuous modifier (5000+2000)");
    AssertTrue(perm.HasNoDigivolutionCards, "no sources");
}

async Task PredicateEval()
{
    EngineContext ctx = Ctx();
    // Two of my Digimon: one 0-DP Lucemon, one normal.
    var luce = await Place(ctx, P1, "Luce", type: "Digimon", level: 6, dp: 0, colors: new[] { "Purple" }, traits: null, name: "Lucemon");
    var other = await Place(ctx, P1, "Oth", type: "Digimon", level: 4, dp: 4000, colors: new[] { "Red" }, traits: null, name: "Agumon");

    // The exact predicate shape a ported card would pass (mirror of BT18_086's PermanentCondition):
    bool PermanentCondition(Permanent p) => p.DP == 0 && p.TopCard.ContainsCardName("Lucemon");

    AssertTrue(PermanentCondition(new Permanent(ctx, luce, P1)), "0-DP Lucemon matches");
    AssertTrue(!PermanentCondition(new Permanent(ctx, other, P1)), "4000-DP Agumon does not match");
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 943);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string type, int level, int dp, string[]? colors, string[]? traits, string name)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = dp };
    if (colors is not null) defMeta["colors"] = colors;
    if (traits is not null) defMeta["traits"] = traits;
    cards.Upsert(new CardRecord(defId, defId.Value, name, defMeta, CardType: type));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
