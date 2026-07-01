using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FR2 (G9-051): condition/predicate args that were accepted-but-ignored are now honoured 1:1.
//  (C) special-play `condition` gates availability (not registered unconditionally).
//  (A) Gain1MemoryTamerOwnerDigimonConditionalEffect gains memory ONLY when the owner controls a matching
//      Digimon (permanentCondition), not unconditionally.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Special-play condition=false -> NOT offered", () => SpecialPlayCond(available: false)),
    ("Special-play condition=true -> offered", () => SpecialPlayCond(available: true)),
    ("Gain1Memory: no matching Digimon -> gate false", () => Gain1(match: false)),
    ("Gain1Memory: matching Digimon present -> gate true", () => Gain1(match: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task SpecialPlayCond(bool available)
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var jog = await Place(ctx, P1, "JOG", ChoiceZone.Hand, level: 6);
    // Jogress with two named materials + an availability condition.
    CardEffectFactory.JogressEffectFromNames(new CardSource(ctx, jog, P1), () => available, "MatA", "MatB");
    await Place(ctx, P1, "MatA", ChoiceZone.BattleArea, level: 4);
    await Place(ctx, P1, "MatB", ChoiceZone.BattleArea, level: 4);

    var offered = new SpecialPlayAction().GetLegalActions(ctx, P1);
    AssertTrue(offered.Count == (available ? 1 : 0), $"offered={offered.Count}, expected {(available ? 1 : 0)} (condition honored)");
}

async Task Gain1(bool match)
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(0);
    var tamer = await Place(ctx, P1, "TAMER", ChoiceZone.BattleArea, level: 3);
    // A Digimon that matches only when we want a match (Level 5 vs the predicate Level==5).
    await Place(ctx, P1, "DIGI", ChoiceZone.BattleArea, level: match ? 5 : 4);

    var eff = CardEffectFactory.Gain1MemoryTamerOwnerDigimonConditionalEffect(
        "", p => p.Level == 5, null, new CardSource(ctx, tamer, P1));
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue);
    await ((IHeadlessCardEffect)eff).ResolveAsync(new CardEffectResolveContext(eff.ToBinding("g1").Request), sink);
    await sink.FlushAsync();

    // Gains only when the owner controls a matching (Level-5) Digimon — not unconditionally.
    AssertTrue(ctx.MemoryController.Current.Current == (match ? 1 : 0), $"memory == {(match ? 1 : 0)} (permanentCondition honored, got {ctx.MemoryController.Current.Current})");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 951);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
