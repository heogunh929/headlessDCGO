using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5-e (G9-047): tail wrappers. ChangeCardNames is behavior-live (folds into CardSource.CardNames so
// EqualsCardName sees the added name). CanNotAffected registers the EffectMutation/Immune replacement.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeCardNames -> added name visible via EqualsCardName", ChangeCardNames),
    ("CanNotAffected -> ImmuneFromEffects replacement registered", CanNotAffected),
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

async Task ChangeCardNames()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "GREY", "Greymon");
    var cs = new CardSource(ctx, id, P1);
    AssertTrue(cs.EqualsCardName("Greymon") && !cs.EqualsCardName("Agumon"), "printed name only before");
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeCardNamesStaticEffect("Agumon", false, new CardSource(ctx, id, P1), null).ToBinding($"ccn:{id.Value}"));
    AssertTrue(cs.EqualsCardName("Agumon") && cs.EqualsCardName("Greymon"), "added name folded into CardNames");
}

async Task CanNotAffected()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "SELF", "Self");
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAffectedStaticEffect(null, false, new CardSource(ctx, id, P1), null).ToBinding($"cna:{id.Value}"));
    ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(
        ctx.EffectRegistry, new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: id));
    bool immune = result.Replacements.Any(r => r.EventKind == ReplacementEventKind.EffectMutation && r.ActionKind == ReplacementActionKind.Immune);
    AssertTrue(immune, "ImmuneFromEffects (EffectMutation/Immune) replacement registered");
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 947);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string name)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
