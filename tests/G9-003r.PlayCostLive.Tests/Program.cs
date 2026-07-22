using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// RE-HOME of G9-003.PlayCostFactory (retired 2026-07-23 stale-pin teardown). The old suite's two structural
// pins were retired with it (the ResolvePlayCost `canReduceCost:` legacy-param guard — the live cost-reduction
// immunity rule is covered green by PRIM-P0.CannotReduceCost / FAILa-05.CannotReduceCostScope / G3E-001
// "Cost reduction permission blocks up down reductions"; and setFixedCost:true→throws, a guard on the invented
// factory contract, not a game rule). The BEHAVIORAL assertions below — continuous play-cost ± modifiers fold
// into the paid cost, the reduction floors at 0, a dynamic (Func) amount folds, and a false condition makes the
// grant inert — are re-driven UNCHANGED through the live cost pipeline (the exact ResolvePlayCost call
// PlayCardAction makes; W3c cost-fold retirement). They pass on the live surface. Adjacent coverage:
// G3.5-D8.CostReductionPipeline (folds ±play/digivolution cost via the AS-IS ChangeCostClass path).

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MandatorySelfPlayCostReduction(4) reduces a 6-cost card to 2 via ResolvePlayCost", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, null), baseCost: 6, expected: 2)),
    // ChangePlayCostStaticEffect gates on CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent) — AS-IS
    // CardSource.cs:92-103 `PermanentsCondition`, requiring >=1 matching battle-area permanent, else it is a
    // no-op. withTargetPermanent places one for the card's owner and threads its id through ResolvePlayCost.
    ("ChangePlayCostStaticEffect(-3) reduces a 5-cost card to 2", () => CostResolvesTo(
        (card) => CardEffectFactory.ChangePlayCostStaticEffect(-3, null, false, card, null, false), baseCost: 5, expected: 2, withTargetPermanent: true)),
    ("ChangePlayCostStaticEffect(+2) increases a 3-cost card to 5", () => CostResolvesTo(
        (card) => CardEffectFactory.ChangePlayCostStaticEffect(2, null, false, card, null, false), baseCost: 3, expected: 5, withTargetPermanent: true)),
    ("Reduction never drops below 0 (a 2-cost card minus 4 = 0)", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, null), baseCost: 2, expected: 0)),
    ("Dynamic MandatorySelfPlayCostReduction(()=>2) reduces 5 to 3", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(() => 2, card, null), baseCost: 5, expected: 3)),
    ("condition:false makes the reduction inert (cost unchanged)", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, () => false), baseCost: 6, expected: 6)),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CostResolvesTo(Func<CardSource, ICardEffect> build, int baseCost, int expected, bool withTargetPermanent = false)
{
    EngineContext context = Context();
    var id = await PlaceInHand(context, P1, "CARD", playCost: baseCost);
    var source = new CardSource(context, id, P1);
    // SEAM: ChangeCostClass (returned by MandatorySelfPlayCostReduction / ChangePlayCostStaticEffect) is a
    // new-model kind-class observed via the unioned NewModelContinuousScan.FoldPlayCost (AS-IS
    // CardSource.GetChangedCostItselef/GetChangedPayingCost) — attach it to the card's own controller (the card
    // is a hand card, not yet a permanent, so AS-IS scans its OWN EffectList too).
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = build(source);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    HeadlessEntityId[]? targetPermanentIds = null;
    if (withTargetPermanent)
    {
        HeadlessEntityId battlerId = await PlaceDigimon(context, P1, "TGT", dp: 3000);
        targetPermanentIds = new[] { battlerId };
    }

    int resolved = ContinuousModifierGate.ResolvePlayCost(context, id, baseCost, targetPermanentIds: targetPermanentIds);
    AssertEqual(expected, resolved, $"resolved play cost (base {baseCost})");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceInHand(EngineContext context, HeadlessPlayerId owner, string tag, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class uses to surface its
// printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
