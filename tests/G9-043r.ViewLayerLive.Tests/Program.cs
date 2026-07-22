using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RE-HOME of G9-043.ViewLayer (retired 2026-07-23 stale-pin teardown). The rehoused card-query view layer
// (CardSource + Permanent members that card predicates read) is re-driven UNCHANGED off live engine state.
// The single stale assertion in the old suite — a continuous +2000 DP fold made observable via the RETIRED
// EffectRegistry registry path (CardEffectFactory.ChangeSelfDPStaticEffect → Permanent.DP) — was dropped: the
// live DP-fold behaviour (continuous DP modifier folds into Permanent.DP via the AS-IS EffectList(None) bucket)
// is covered green by W3c3-DpDeltaGrant and G3.5-N2.ContinuousBattleDp. Adjacent name/color/trait getter
// coverage: G3D-002.Name.color.trait.requirement (exact/contains name, any/all color, exact/contains traits).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CardSource: colors/level/type/name/traits read off the definition", CardSourceViews),
    ("Permanent: TopCard + base DP + level + IsDigimon + sources read off live state", PermanentViews),
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
