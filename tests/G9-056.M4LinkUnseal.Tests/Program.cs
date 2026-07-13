using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-4 (G9-056): link-subsystem un-seal. ChangeSelfLinkMax (linkedMaxDelta) and GrantedReduceLinkCost
// (linkCostDelta) registered continuous modifiers that were emitted as NO modifier and read by NOTHING.
// Now LinkHelpers.ResolveLinkedMax / ResolveLinkCost fold them.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeSelfLinkMax(+2) raises the effective link max (1 -> 3)", LinkMax),
    ("No effect -> base link max (control)", LinkMaxControl),
    ("GrantedReduceLinkCost(2) lowers the effective link cost (3 -> 1)", LinkCost),
    ("Link cost reduction clamps at 0", LinkCostClamp),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task LinkMax()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, "HOST");
    // MIGRATION-NOTE (P7 test-fix): ChangeLinkMaxClass (Script/CardEffects/ChangeLinkMaxClass.cs) is a new-model
    // kind-class with no ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md).
    // The gate this test checks (LinkHelpers.ResolveLinkedMax, via ContinuousScopeEvaluation over the substrate
    // EffectRegistry) reads only registered EffectRequest bindings, not the AS-IS live CardSource.EffectList scan,
    // so there is no buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED
    // TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.ChangeSelfLinkMaxStaticEffect(2, false, new CardSource(ctx, host, P1), null);
    AssertTrue(LinkHelpers.ResolveLinkedMax(ctx, host) == LinkHelpers.DefaultLinkedMax + 2, $"effective link max == {LinkHelpers.DefaultLinkedMax + 2}");
}

async Task LinkMaxControl()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, "HOST");
    AssertTrue(LinkHelpers.ResolveLinkedMax(ctx, host) == LinkHelpers.DefaultLinkedMax, "base link max with no effect");
}

async Task LinkCost()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, "CARD");
    // MIGRATION-NOTE (P7 test-fix): ChangeLinkCostClass (Script/CardEffects/ChangeLinkCostClass.cs) is a
    // new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md).
    // The gate this test checks (LinkHelpers.ResolveLinkCost, via ContinuousScopeEvaluation over the substrate
    // EffectRegistry) reads only registered EffectRequest bindings, not the AS-IS live CardSource.EffectList scan,
    // so there is no buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED
    // TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.GrantedReduceLinkCostClass(new CardSource(ctx, card, P1), 2, null, null, null);
    AssertTrue(LinkHelpers.ResolveLinkCost(ctx, card, 3) == 1, "effective link cost 3 - 2 == 1");
}

async Task LinkCostClamp()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, "CARD");
    // MIGRATION-NOTE (P7 test-fix): ChangeLinkCostClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). See LinkCost() above for the full rationale.
    // Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.GrantedReduceLinkCostClass(new CardSource(ctx, card, P1), 5, null, null, null);
    AssertTrue(LinkHelpers.ResolveLinkCost(ctx, card, 3) == 0, "3 - 5 clamps to 0");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 956);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
