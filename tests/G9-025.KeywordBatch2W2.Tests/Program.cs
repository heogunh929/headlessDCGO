using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-025): the Batch2 self-static keyword factories Rush / Retaliation now exist (their kind is in
// KeywordBaseBatch2Kind and ContinuousKeywordGate has the name), mirroring the same one-liner as
// Vortex/Alliance/Overclock. Verified by the gate consumers use (ContinuousKeywordGate.HasKeyword).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("RushSelfStaticEffect -> gate sees Rush live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.RushSelfStaticEffect(false, c, null), ContinuousKeywordGate.Rush)),
    ("RetaliationSelfEffect -> gate sees Retaliation live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.RetaliationSelfEffect(false, c, null), ContinuousKeywordGate.Retaliation)),
    ("RaidSelfEffect -> gate sees Raid live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.RaidSelfEffect(false, c, null), ContinuousKeywordGate.Raid)),
    ("BarrierSelfEffect -> gate sees Barrier live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.BarrierSelfEffect(false, c, null), ContinuousKeywordGate.Barrier)),
    ("CollisionSelfStaticEffect -> gate sees Collision live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.CollisionSelfStaticEffect(false, c, null), ContinuousKeywordGate.Collision)),
    ("FortitudeSelfEffect -> gate sees Fortitude live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.FortitudeSelfEffect(false, c, null), ContinuousKeywordGate.Fortitude)),
    ("EvadeSelfEffect -> gate sees Evade live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.EvadeSelfEffect(false, c, null), ContinuousKeywordGate.Evade)),
    ("SaveEffect -> gate sees Save live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.SaveEffect(c), ContinuousKeywordGate.Save)),
    ("ArmorPurgeEffect -> gate sees Armor Purge live", () => SelfStaticGoesLive(
        (c) => CardEffectFactory.ArmorPurgeEffect(c), ContinuousKeywordGate.ArmorPurge)),
    ("Self-static keyword is scoped to its own card", ScopedToOwnCard),
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

async Task SelfStaticGoesLive(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var id = await PlaceDigimon(context, P1, "KW");

    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} not present before grant");
    context.EffectRegistry.Register(build(new CardSource(context, id, P1)).ToBinding($"kw:{keyword}:{id.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} live after the SelfEffect factory");
}


async Task ScopedToOwnCard()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF");
    var other = await PlaceDigimon(context, P1, "OTHER");
    context.EffectRegistry.Register(
        CardEffectFactory.RetaliationSelfEffect(false, new CardSource(context, self, P1), null).ToBinding($"kw:ret:{self.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, self, ContinuousKeywordGate.Retaliation), "self has Retaliation");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, other, ContinuousKeywordGate.Retaliation), "bystander does NOT have Retaliation");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 925);
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
