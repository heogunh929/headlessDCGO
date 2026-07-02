using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// A3 (G9-061): view-layer continuous folds. AS-IS folds IChangeCardLevelEffect / IChangePermanentLevelEffect /
// IChange(Base)CardColorEffect / IChangeTraitsEffect into CardSource.Level / Permanent.Level / CardColors /
// CardTraits — the port previously read printed metadata only (the five adapter classes were skeletons).
// The transforms are stored VERBATIM (accumulator Funcs) and evaluated live, mirroring the AS-IS scan scopes.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeCardLevel (BT17_068 shape): the card's Level folds; condition gates it live; HasLevel stays printed", CardLevelFold),
    ("ChangePermanentLevel: the permanent's Level folds; closure targeting spares other permanents", PermanentLevelFold),
    ("Colors: base-change runs BEFORE change (two-stage) and the result is Distinct; HasCardColor consumes it", ColorTwoStageFold),
    ("ChangeTraits: granted trait reaches ContainsTraits/EqualsTraits", TraitsFold),
    ("(A2 integration) the added-digivolve level gate reads the FOLDED level", LevelGateReadsFoldedLevel),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task CardLevelFold()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "SELF", level: 4);
    var self = new CardSource(ctx, id, P1);
    bool active = true;

    var effect = new ChangeCardLevelClass();
    effect.SetUpICardEffect("Also treated as level 6", () => active, self);
    effect.SetUpChangeCardLevelClass((cardSource, level) => cardSource.InstanceId == id ? 6 : level);
    ctx.EffectRegistry.Register(effect.ToBinding($"ccl:{id.Value}"));

    AssertTrue(new CardSource(ctx, id, P1).Level == 6, "the folded Level is 6");
    AssertTrue(new CardSource(ctx, id, P1).HasLevel, "HasLevel stays true (printed 4)");
    active = false;
    AssertTrue(new CardSource(ctx, id, P1).Level == 4, "condition off -> printed level (live gate)");
}

async Task PermanentLevelFold()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", level: 4);
    var other = await Place(ctx, P1, "OTHER", level: 4);
    var source = new CardSource(ctx, target, P1);

    var effect = new ChangePermanentLevelClass();
    effect.SetUpICardEffect("This Digimon is also treated as level 5", null, source);
    effect.SetUpChangePermanentLevelClass((permanent, level) => permanent.InstanceId == target ? 5 : level);
    ctx.EffectRegistry.Register(effect.ToBinding($"cpl:{target.Value}"));

    AssertTrue(new Permanent(ctx, target, P1).Level == 5, "the permanent's folded Level is 5");
    AssertTrue(new Permanent(ctx, other, P1).Level == 4, "closure targeting spares the other permanent");
}

async Task ColorTwoStageFold()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "SELF", level: 4, colors: new[] { "Red" });
    var self = new CardSource(ctx, id, P1);

    // base-change: Red -> Blue (replaces the base). change: adds Red back on top of the BASE result.
    var baseEffect = new ChangeBaseCardColorClass();
    baseEffect.SetUpICardEffect("base becomes Blue", null, self);
    baseEffect.SetUpChangeBaseCardColorClass((cs, colors) => cs.InstanceId == id ? new List<string> { "Blue" } : colors);
    ctx.EffectRegistry.Register(baseEffect.ToBinding($"cbc:{id.Value}"));

    var changeEffect = new ChangeCardColorClass();
    changeEffect.SetUpICardEffect("also Red", null, self);
    changeEffect.SetUpChangeCardColorClass((cs, colors) =>
    {
        if (cs.InstanceId == id) { colors.Add("Red"); colors.Add("Blue"); }   // duplicate Blue -> Distinct
        return colors;
    });
    ctx.EffectRegistry.Register(changeEffect.ToBinding($"ccc:{id.Value}"));

    var view = new CardSource(ctx, id, P1);
    AssertTrue(view.BaseCardColors.SequenceEqual(new[] { "Blue" }), "base-change replaced the printed base");
    AssertTrue(view.CardColors.Count == 2 && view.HasCardColor("Blue") && view.HasCardColor("Red"),
        "change ran over the BASE result and the list is Distinct");
}

async Task TraitsFold()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "SELF", level: 4, traits: new[] { "Dragon" });
    var self = new CardSource(ctx, id, P1);

    var effect = new ChangeTraitsClass();
    effect.SetUpICardEffect("gains [Royal Knight]", null, self);
    effect.SetUpChangeTraitsClass((cs, traits) => { if (cs.InstanceId == id) traits.Add("Royal Knight"); return traits; });
    ctx.EffectRegistry.Register(effect.ToBinding($"ct:{id.Value}"));

    var view = new CardSource(ctx, id, P1);
    AssertTrue(view.ContainsTraits("Royal Knight"), "the granted trait is visible");
    AssertTrue(view.EqualsTraits("Dragon"), "the printed trait stays");
}

async Task LevelGateReadsFoldedLevel()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await Place(ctx, P1, "BASE", level: 4, colors: new[] { "Blue" });
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@7", cost: 2);

    // The added requirement demands exact level 5; the target is printed Lv4 but a fold treats it as 5.
    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: p => true, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
        card: new CardSource(ctx, evo, P1), condition: null, level: 5).ToBinding($"asdr:{evo.Value}"));

    var before = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
    AssertTrue(!before.IsSuccess, "printed Lv4 fails the level:5 gate");

    var fold = new ChangeCardLevelClass();
    fold.SetUpICardEffect("treated as level 5", null, new CardSource(ctx, target, P1));
    fold.SetUpChangeCardLevelClass((cs, level) => cs.InstanceId == target ? 5 : level);
    ctx.EffectRegistry.Register(fold.ToBinding($"ccl:{target.Value}"));

    var after = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
    AssertTrue(after.IsSuccess, $"the folded Lv5 passes the gate ({after.Message})");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 961);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level, string[]? colors = null, string[]? traits = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = level };
    if (colors is not null) meta["colors"] = colors;
    if (traits is not null) meta["traits"] = traits;
    cards.Upsert(new CardRecord(defId, tag, tag, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext ctx, string tag, string requirement, int cost)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 6, ["fixedDigivolutionCost"] = cost },
        CardType: "Digimon", EvolutionCondition: requirement));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = cost }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
