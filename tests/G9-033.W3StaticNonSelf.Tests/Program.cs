using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-033): non-self static effects reusing the player-scope infra:
//  - RushStaticEffect / RebootStaticEffect -> player-scope keyword grant (owner's Digimon).
//  - CanNotAttackStaticEffect -> player-scope CannotAttack restriction (AttackPermanentAction consults it).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("RushStaticEffect -> owner's ally has Rush (player-scope)", RushStatic),
    ("RebootStaticEffect -> owner's ally has Reboot (player-scope)", RebootStatic),
    ("CanNotAttackStaticEffect (scope P2) -> P2's Digimon attack is restricted", CanNotAttackStatic),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task RushStatic()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    context.EffectRegistry.Register(CardEffectFactory.RushStaticEffect(null, false, new CardSource(context, src, P1), null).ToBinding($"rush:{src.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, ally, ContinuousKeywordGate.Rush), "owner's ally has Rush");
}

async Task RebootStatic()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    context.EffectRegistry.Register(CardEffectFactory.RebootStaticEffect(null, false, new CardSource(context, src, P1), null).ToBinding($"reboot:{src.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, ally, ContinuousKeywordGate.Reboot), "owner's ally has Reboot");
}

async Task CanNotAttackStatic()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var foe = await Place(context, P2, "FOE");
    var target = await Place(context, P1, "TGT");

    AssertTrue(!ContinuousRestrictionGate.EvaluateAttack(context, foe, target).IsRestricted, "not restricted before grant");
    context.EffectRegistry.Register(CardEffectFactory.CanNotAttackStaticEffect(P2, false, new CardSource(context, src, P1), null).ToBinding($"cna:{src.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(context, foe, target).IsRestricted, "P2's Digimon cannot attack (player-scope)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 933);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
