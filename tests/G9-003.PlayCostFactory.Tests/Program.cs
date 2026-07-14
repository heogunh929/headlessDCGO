using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-003: the headless mirror of the original CardEffectFactory play-cost factories
// (ChangePlayCostStaticEffect / MandatorySelfPlayCostReduction). The headless play-cost engine already
// pulls continuous cost modifiers from the EffectRegistry at play time (PlayCardAction ->
// ContinuousModifierGate.ResolvePlayCost); these factories were the missing card-facing entry points.
// This test drives the REAL resolution path: Factory.X(card,...).ToBinding() -> register -> the same
// ResolvePlayCost call PlayCardAction makes returns the reduced cost, and the CanReduceCost guard holds.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MandatorySelfPlayCostReduction(4) reduces a 6-cost card to 2 via ResolvePlayCost", () => CostResolvesTo(
        (card) => CardEffectFactory.MandatorySelfPlayCostReduction(4, card, null), baseCost: 6, expected: 2)),
    // ChangePlayCostStaticEffect gates on CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent) — AS-IS
    // CardSource.cs:92-103 `PermanentsCondition`, requiring >=1 matching battle-area permanent, else it is a
    // no-op (there is no real AS-IS card that plays it with an empty target list). withTargetPermanent places one
    // for the card's owner and threads its id through ResolvePlayCost's targetPermanentIds.
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
    ("CanReduceCost:false blocks the reduction (the original CanReduceCost guard)", CanReduceCostGuardHolds),
    ("setFixedCost:true throws rather than silently treating set as add", FixedCostThrows),
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
    // SEAM (post-stage-B): ChangeCostClass (returned by MandatorySelfPlayCostReduction /
    // ChangePlayCostStaticEffect) is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.FoldPlayCost (AS-IS CardSource.GetChangedCostItselef/GetChangedPayingCost) —
    // attach it to the card's own controller (the card is a hand card, not yet a permanent, so AS-IS scans
    // its OWN EffectList too).
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

async Task CanReduceCostGuardHolds()
{
    EngineContext context = Context();
    var id = await PlaceInHand(context, P1, "CARD", playCost: 6);
    var source = new CardSource(context, id, P1);
    // SEAM (post-stage-B): MandatorySelfPlayCostReduction returns ChangeCostClass, observed via the unioned
    // FoldPlayCost fold; attach it to the card's controller.
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = CardEffectFactory.MandatorySelfPlayCostReduction(4, source, null);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    int withReduction = ContinuousModifierGate.ResolvePlayCost(context, id, basePlayCost: 6, canReduceCost: true);
    int noReduction = ContinuousModifierGate.ResolvePlayCost(context, id, basePlayCost: 6, canReduceCost: false);
    AssertEqual(2, withReduction, "reduction applies when CanReduceCost");
    AssertEqual(6, noReduction, "reduction is blocked when CanReduceCost is false");
}

Task FixedCostThrows()
{
    EngineContext context = Context();
    var source = new CardSource(context, new HeadlessEntityId("1:hand:X"), P1);
    bool threw = false;
    try { CardEffectFactory.ChangePlayCostStaticEffect(1, null, false, source, null, setFixedCost: true); }
    catch (NotSupportedException) { threw = true; }
    AssertTrue(threw, "setFixedCost:true throws NotSupportedException (no silent set-as-add)");
    return Task.CompletedTask;
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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
