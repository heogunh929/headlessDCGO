using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-044): AddSelfDigivolutionRequirementStaticEffect — a predicate-based alternative digivolution
// source. The printed requirement (Red@4) fails against a Blue Lv5 target, but the added predicate
// (p => p.Level == 5, honored by the new view layer) makes the digivolve legal. Controls: no effect / a
// non-matching predicate stay illegal.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Added predicate (Level==5) makes an off-color target legal", AddedPredicateLegal),
    ("Without the added source, the off-color target is illegal (control)", NoEffectIllegal),
    ("A non-matching predicate (Level==6) stays illegal (control)", NonMatchingIllegal),
    ("costEquation (dynamic cost) applies over the fixed cost", DynamicCost),
    ("(A2) exact level gate: Lv5 source accepted, Lv4/Lv6 rejected even when the predicate matches", ExactLevelGate),
    ("(A2) minLevel gate (EX6_073 shape): trait-only predicate no longer digivolves at ANY level", MinLevelGate),
    ("(A2) a source WITHOUT a level never passes a configured gate", NoLevelSourceRejected),
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

async Task AddedPredicateLegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    Register(ctx, evo, p => p.Level == 5);

    // (FR2/M-3) the printed condition (Red@4) fails, so the ADDED requirement's own cost (digivolutionCost: 3)
    // applies — NOT the printed cost of 2.
    var atPrinted = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(!atPrinted.IsSuccess, "printed cost (2) is rejected — the added path's cost applies");

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
    AssertTrue(result.IsSuccess, $"legal via added predicate at the added cost 3 ({result.Message})");
    AssertTrue(InZone(ctx, P1, evo), "evolving card became the new top");
}

async Task NoEffectIllegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(!result.IsSuccess, "illegal without the added source");
}

async Task NonMatchingIllegal()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    Register(ctx, evo, p => p.Level == 6); // does not match a Lv5 target

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), ctx);
    AssertTrue(!result.IsSuccess, "illegal when the predicate does not match");
}

// (A2) AS-IS GetEvoCost: level/minLevel/maxLevel is a HARD gate on the digivolving-FROM permanent,
// OUTSIDE the predicate — a matching predicate alone must not pass.

async Task ExactLevelGate()
{
    foreach ((int sourceLevel, bool legal) in new[] { (5, true), (4, false), (6, false) })
    {
        EngineContext ctx = Ctx();
        ctx.MemoryController.Set(5);
        var target = await PlaceBase(ctx, "BLUE", color: "Blue", level: sourceLevel);
        var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
        RegisterLeveled(ctx, evo, p => true, level: 5);   // predicate always true — the gate decides

        var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
        AssertTrue(result.IsSuccess == legal, $"source Lv{sourceLevel} with level:5 gate -> legal={legal} ({result.Message})");
    }
}

async Task MinLevelGate()
{
    foreach ((int sourceLevel, bool legal) in new[] { (5, true), (6, true), (4, false) })
    {
        EngineContext ctx = Ctx();
        ctx.MemoryController.Set(5);
        var target = await PlaceBase(ctx, "BLUE", color: "Blue", level: sourceLevel);
        var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@7", cost: 2);
        RegisterLeveled(ctx, evo, p => true, minLevel: 5); // EX6_073: predicate = trait only, minLevel is the sole level constraint

        var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
        AssertTrue(result.IsSuccess == legal, $"source Lv{sourceLevel} with minLevel:5 gate -> legal={legal} ({result.Message})");
    }
}

async Task NoLevelSourceRejected()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var target = await PlaceBase(ctx, "NOLEVEL", color: "Blue", level: -1); // no printed level
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    RegisterLeveled(ctx, evo, p => true, minLevel: 3);

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
    AssertTrue(!result.IsSuccess, "AS-IS: a configured gate requires HasLevel — a no-level source is rejected");
}

// SEAM (RD-P6B-15): AddSelfDigivolutionRequirementStaticEffect returns AddDigivolutionRequirementClass
// (IAddDigivolutionRequirementEffect — GetEvoCost(Permanent, CardSource, IgnoreRequirement, bool)), the REAL
// AS-IS mechanism (color / level-range / permanentCondition / cardCondition / cost all gated in the closure).
// It registers no binding, so the mirror observes it via the AS-IS live interface scan CardSource.EvoCosts/
// CostList (mirror CardSource.AddedDigivolutionCosts, DCGO CardSource.cs:534-627), which DigivolveAction now
// UNIONs into MatchesAddedDigivolutionRequirement / TryGetAddedDigivolutionCost. Attach the built effect to the
// evolving card's controller (AS-IS EvoCosts' "effects of itself" region scans EffectList(None) on the still-in-
// hand card). The added path's cost (digivolutionCost / costEquation) then applies over the printed cost.
void RegisterLeveled(EngineContext ctx, HeadlessEntityId evo, Func<Permanent, bool> predicate, int level = -1, int minLevel = -1, int maxLevel = -1)
{
    var card = new CardSource(ctx, evo, P1);
    ICardEffect eff = CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: predicate, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
        card: card, condition: null, level: level, minLevel: minLevel, maxLevel: maxLevel);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
}

// --- Helpers -------------------------------------------------------------

async Task DynamicCost()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(9);
    var target = await PlaceBase(ctx, "BLUE5", color: "Blue", level: 5);
    var evo = await PlaceEvolve(ctx, "EVO", requirement: "Red@4", cost: 2);
    // Added path with a DYNAMIC cost (costEquation) that overrides the fixed digivolutionCost: 3.
    // (RD-P6B-15 seam) attach to the evolving card's controller so the new-model GetEvoCost scan observes it.
    var evoCard = new CardSource(ctx, evo, P1);
    ICardEffect dynEff = CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: p => p.Level == 5, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
        card: evoCard, condition: null, costEquation: () => 6);
    evoCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(dynEff);

    var atFixed = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), ctx);
    AssertTrue(!atFixed.IsSuccess, "fixed cost (3) is rejected — costEquation() overrides it");

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 6), ctx);
    AssertTrue(result.IsSuccess, $"legal at the dynamic cost 6 ({result.Message})");
}

// (RD-P6B-15 seam) attach to the evolving card's controller so the new-model GetEvoCost scan observes it.
void Register(EngineContext ctx, HeadlessEntityId evo, Func<Permanent, bool> predicate)
{
    var card = new CardSource(ctx, evo, P1);
    ICardEffect eff = CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: predicate, digivolutionCost: 3, ignoreDigivolutionRequirement: false,
        card: card, condition: null);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 944);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (RD-P6B-15 seam) live phase so ICardEffect.CanUse's DoneStartGame gate passes for the new-model scan.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext ctx, string tag, string color, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color }, ["level"] = level, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
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

bool InZone(EngineContext ctx, HeadlessPlayerId p, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the seam every ported card definition class uses to surface its printed
// effect list to CardSource.EffectList → CEntity_Effect.CardEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
