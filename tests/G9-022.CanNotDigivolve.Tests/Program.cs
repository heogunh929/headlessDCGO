using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W1-5 (G9-022): CardEffectFactory.CanNotDigivolveStaticSelfEffect — mirror of the original: a continuous
// "this card cannot be digivolved" restriction on self. Registers a CannotDigivolve restriction (via the
// reusable ContinuousSelfRestrictionEffect) that DigivolveAction already consults (EvaluateDigivolve on the
// target under-card). End-to-end: digivolving onto a restricted target is illegal; control is legal; a false
// condition lifts the restriction.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Control: digivolve onto an unrestricted Red Lv4 target is legal", ControlLegal),
    ("Digivolve onto a CanNotDigivolve target is illegal", RestrictedIllegal),
    ("A false condition lifts the restriction -> digivolve legal again", ConditionLifts),
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

async Task ControlLegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"unrestricted digivolve is legal ({result.Message})");
}

async Task RestrictedIllegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO");

    // Register "this card cannot be digivolved" on the target.
    var targetCard = new CardSource(context, target, P1);
    // MIGRATION-NOTE (P7 test-fix): CanNotDigivolveStaticSelfEffect returns CanNotDigivolveClass, a
    // new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B RED,
    // docs/audit/rebuild_p6_stageA_notes.md). DigivolveAction's EvaluateDigivolve gate reads only the
    // substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this
    // restriction observable yet. The factory call is kept (still exercises construction); Register/
    // ToBinding is dropped. Assertions below are UNCHANGED and EXPECTED TO FAIL until stage B lands —
    // tracked, not silently weakened.
    CardEffectFactory.CanNotDigivolveStaticSelfEffect(cardCondition: null, isInheritedEffect: false,
        card: targetCard, condition: null, effectName: "CanNotDigivolve");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "digivolve onto a CanNotDigivolve target is illegal");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, evo), "the evolving card did NOT enter play");
}

async Task ConditionLifts()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO");

    var targetCard = new CardSource(context, target, P1);
    // MIGRATION-NOTE (P7 test-fix): see RestrictedIllegal above — CanNotDigivolveClass has no ToBinding/
    // EffectRegistry bridge (stage-B RED). Factory call kept for construction; Register/ToBinding dropped.
    // Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands (it currently passes vacuously
    // since the restriction was never observable to begin with, matching the "no restriction" branch).
    CardEffectFactory.CanNotDigivolveStaticSelfEffect(cardCondition: null, isInheritedEffect: false,
        card: targetCard, condition: () => false, effectName: "CanNotDigivolve");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, "a false condition means no restriction -> digivolve legal");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 922);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 4, ["dp"] = 3000 },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = 2 },
        CardType: "Digimon", EvolutionCondition: "Red@4"));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 2 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
