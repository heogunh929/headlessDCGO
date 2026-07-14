using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W1-3 (G9-021): CardEffectFactory.ChangeDigivolutionCostStaticEffect — the headless mirror of the
// original same-named factory (delta case). It registers a continuous digivolution-cost modifier on self
// that ContinuousModifierGate.ResolveDigivolutionCost folds into the card's evolution cost (D-8). Verified in
// isolation: register the binding, then resolve the cost.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Static -2 reduces the digivolution cost (5 -> 3)", StaticReduces),
    ("Dynamic (Func<int>) -3 reduces the cost (5 -> 2)", DynamicReduces),
    ("A false condition applies no change (stays 5)", ConditionGates),
    ("Positive +2 raises the cost (5 -> 7)", PositiveRaises),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

Task StaticReduces()
{
    EngineContext context = Context();
    var card = Place(context, "EVO");
    HeadlessEntityId target = PlaceBattleTarget(context, "TGT");
    // SEAM (post-stage-B): ChangeDigivolutionCostStaticEffect returns ChangeCostClass, a new-model kind-class
    // observed via the unioned NewModelContinuousScan.FoldPlayCost (already unioned into
    // ContinuousModifierGate.ResolveDigivolutionCost) — attach it to the card's own controller. Its
    // PermanentsCondition needs >=1 real matching battle/breeding-area permanent (CardEffectCommons.
    // IsPermanentExistsOnField), threaded through as the digivolveTargetPermanentId.
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = CardEffectFactory.ChangeDigivolutionCostStaticEffect(-2, permanentCondition: null, cardCondition: null,
        rootCondition: null, isInheritedEffect: false, card: card, condition: null, setFixedCost: false);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(context, card.InstanceId, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(3, cost, "digivolution cost 5 - 2 = 3");
    return Task.CompletedTask;
}

Task DynamicReduces()
{
    EngineContext context = Context();
    var card = Place(context, "EVO");
    HeadlessEntityId target = PlaceBattleTarget(context, "TGT");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = CardEffectFactory.ChangeDigivolutionCostStaticEffect(() => -3, permanentCondition: null, cardCondition: null,
        rootCondition: null, isInheritedEffect: false, card: card, condition: null, setFixedCost: false);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(context, card.InstanceId, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(2, cost, "digivolution cost 5 - 3 = 2 (dynamic)");
    return Task.CompletedTask;
}

Task ConditionGates()
{
    EngineContext context = Context();
    var card = Place(context, "EVO");
    HeadlessEntityId target = PlaceBattleTarget(context, "TGT");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = CardEffectFactory.ChangeDigivolutionCostStaticEffect(-2, permanentCondition: null, cardCondition: null,
        rootCondition: null, isInheritedEffect: false, card: card, condition: () => false, setFixedCost: false);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(context, card.InstanceId, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(5, cost, "false condition -> no reduction");
    return Task.CompletedTask;
}

Task PositiveRaises()
{
    EngineContext context = Context();
    var card = Place(context, "EVO");
    HeadlessEntityId target = PlaceBattleTarget(context, "TGT");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    ICardEffect effect = CardEffectFactory.ChangeDigivolutionCostStaticEffect(2, permanentCondition: null, cardCondition: null,
        rootCondition: null, isInheritedEffect: false, card: card, condition: null, setFixedCost: false);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(context, card.InstanceId, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(7, cost, "digivolution cost 5 + 2 = 7");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 921);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

CardSource Place(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1));
    return new CardSource(context, id, P1);
}

// A real battle-area permanent for ChangeDigivolutionCostStaticEffect's PermanentsCondition (AS-IS requires
// >=1 matching CardEffectCommons.IsPermanentExistsOnField target — the "digivolving FROM" permanent).
HeadlessEntityId PlaceBattleTarget(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

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
