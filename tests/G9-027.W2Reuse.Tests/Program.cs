using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-027): three primitives that reuse existing infra:
//  - ArmorPurgeEffect -> Batch2 keyword grant (HasKeyword).
//  - CanNotAttackSelfStaticEffect -> ContinuousSelfRestrictionEffect(CannotAttack) that
//    ContinuousRestrictionGate.EvaluateAttack (AttackPermanentAction) consults.
//  - DeckBottomBounceEffect -> direct ReturnToDeckBottom of a target list.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ArmorPurgeEffect -> gate sees Armor Purge live", ArmorPurgeGrants),
    ("CanNotAttackSelfStaticEffect -> attack is restricted", CanNotAttackRestricts),
    ("DeckBottomBounceEffect -> targets return to the deck (library)", BounceToDeck),
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

async Task ArmorPurgeGrants()
{
    EngineContext context = Context();
    var id = await PlaceDigimon(context, P1, "AP");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.ArmorPurge), "Armor Purge absent before grant");
    context.EffectRegistry.Register(CardEffectFactory.ArmorPurgeEffect(new CardSource(context, id, P1)).ToBinding($"ap:{id.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.ArmorPurge), "Armor Purge live after grant");
}

async Task CanNotAttackRestricts()
{
    EngineContext context = Context();
    var attacker = await PlaceDigimon(context, P1, "ATK");
    var defender = await PlaceDigimon(context, P2, "DEF");

    AssertTrue(!ContinuousRestrictionGate.EvaluateAttack(context, attacker, defender).IsRestricted, "not restricted before grant");
    context.EffectRegistry.Register(
        CardEffectFactory.CanNotAttackSelfStaticEffect(null, false, new CardSource(context, attacker, P1), null).ToBinding($"cna:{attacker.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(context, attacker, defender).IsRestricted, "attack restricted after CanNotAttackSelf");
}

async Task BounceToDeck()
{
    EngineContext context = Context();
    var src = await PlaceDigimon(context, P1, "SRC");
    var foe = await PlaceDigimon(context, P2, "FOE");

    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    new DeckBottomBounceEffect(new CardSource(context, src, P1), new[] { foe }, "Return to deck bottom.").Apply(sink);
    await sink.FlushAsync();

    var zones = (IZoneStateReader)context.ZoneMover;
    AssertTrue(!zones.GetCards(P2, ChoiceZone.BattleArea).Contains(foe), "foe left the battle area");
    AssertTrue(zones.GetCards(P2, ChoiceZone.Library).Contains(foe), "foe is in the deck (library)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 927);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
