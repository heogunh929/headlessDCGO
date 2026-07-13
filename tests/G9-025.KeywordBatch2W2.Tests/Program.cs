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
    // (P7 stage-B SEAM) all of the SelfEffect/SelfStaticEffect factories under test (Rush/Retaliation/Raid/
    // Barrier/Collision/Fortitude/Evade/Save/ArmorPurge) return new-model kind-classes (RushClass/
    // CollisionClass/ActivateClass/etc.) that register no substrate binding — the AS-IS-faithful path is the
    // LIVE cEntity_EffectController.GetCardEffects scan NewModelContinuousScan/ContinuousKeywordGate.HasKeyword
    // now performs (docs/audit/rebuild_p6_stageB_notes.md). Attach the built effect to the card's controller
    // via the same seam every ported card definition class uses, per tests/FAILd-08.ChangeBaseCardName.Tests.
    var cs = new CardSource(context, id, P1);
    ICardEffect built = build(cs);
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} live after the SelfEffect factory");
}


async Task ScopedToOwnCard()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF");
    var other = await PlaceDigimon(context, P1, "OTHER");
    // (P7 stage-B SEAM) see SelfStaticGoesLive above.
    var cs = new CardSource(context, self, P1);
    ICardEffect built = CardEffectFactory.RetaliationSelfEffect(false, cs, null);
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, self, ContinuousKeywordGate.Retaliation), "self has Retaliation");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, other, ContinuousKeywordGate.Retaliation), "bystander does NOT have Retaliation");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 925);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup).
    context.TurnController.SetPhase(HeadlessPhase.Main);
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

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
