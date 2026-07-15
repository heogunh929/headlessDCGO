using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-002: the per-keyword SELF-STATIC factories the original CardEffectFactory exposes
// (RebootSelfStaticEffect / AllianceSelfEffect / OverclockSelfEffect / VortexSelfEffect) now exist in the
// headless mirror (structural-fidelity standard: an original `<Keyword>SelfEffect` must have a same-named
// headless entry point). This test drives the REAL card path — CardEffectFactory.X(card,...).ToBinding()
// -> register -> the live consumer recognises the keyword — proving the grant is no longer inert.
//
// Reboot is a Batch1 self-static; Alliance/Overclock/Vortex are Batch2 (new SelfKeywordBatch2Effect). Each
// is verified by the same pull-pattern gate consumers use (ContinuousKeywordGate), and Vortex additionally
// by its live end-of-turn attack window (EndOfTurnEffectAttack), the path GR-006 wired.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("RebootSelfStaticEffect -> gate sees Reboot live (Batch1)", () => SelfStaticGoesLive(
        (card) => CardEffectFactory.RebootSelfStaticEffect(false, card, null), ContinuousKeywordGate.Reboot)),
    ("AllianceSelfEffect -> gate sees Alliance live (Batch2)", () => SelfStaticGoesLive(
        (card) => CardEffectFactory.AllianceSelfEffect(false, card, null), ContinuousKeywordGate.Alliance)),
    ("OverclockSelfEffect -> gate sees Overclock live (Batch2)", () => SelfStaticGoesLive(
        (card) => CardEffectFactory.OverclockSelfEffect("", false, card, null), ContinuousKeywordGate.Overclock)),
    ("VortexSelfEffect -> gate sees Vortex live (Batch2)", () => SelfStaticGoesLive(
        (card) => CardEffectFactory.VortexSelfEffect(false, card, null), ContinuousKeywordGate.Vortex)),
    ("VortexSelfEffect opens the live end-of-turn attack window (real card path)", VortexSelfEffectOpensWindow),
    ("Self-static keyword is scoped to its own card, not a bystander", ScopedToOwnCard),
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

async Task SelfStaticGoesLive(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var id = await PlaceDigimon(context, P1, "KW", dp: 4000, suspended: false);

    // Before registering the self-static, the gate must NOT report the keyword (no false positive).
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} not present before the factory registers it");

    var source = new CardSource(context, id, P1);
    // SEAM (post-stage-B): the SelfEffect factories under test (RebootSelfStaticEffect / AllianceSelfEffect /
    // OverclockSelfEffect / VortexSelfEffect) return new-model kind-classes with no ToBinding/EffectRegistry
    // bridge — they are observed instead via the AS-IS live scan (NewModelContinuousScan.HasKeyword, unioned
    // into ContinuousKeywordGate.HasKeyword). That scan reads cEntity_EffectController.GetCardEffects, so the
    // built effect must be attached to the card's controller the same way a real `CEntity_Effect`-derived card
    // definition class does.
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var effect = build(source);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} is live after the SelfEffect factory registers it");
}

async Task VortexSelfEffectOpensWindow()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);

    var source = new CardSource(context, vortex, P1);
    // SEAM (post-stage-B): VortexSelfEffect returns ActivateClass, a new-model kind-class observed via the
    // unioned NewModelContinuousScan.HasSelfEndTurnKeyword scan; attach it to the controller.
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var effect = CardEffectFactory.VortexSelfEffect(false, source, null);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    // (C-EoT-2) <Vortex> firing is RE-HOUSED to the AS-IS OnEndTurn window: the VortexSelfEffect ActivateClass
    // surfaced by the card's cEntity is collected by AutoProcessing.GetSkillInfos and resolved by MultipleSkills
    // -> VortexProcess. The invented EndOfTurnEffectAttack gate no longer fires <Vortex> (single-fire: window XOR
    // gate). Live window firing is witnessed by tests/C-EoT2 / GR-006.
    AssertTrue(AutoProcessing.GetSkillInfos(new System.Collections.Hashtable(), EffectTiming.OnEndTurn)
        .Any(si => si.CardEffect is ActivateICardEffect),
        "the OnEndTurn window collects the card's VortexSelfEffect ActivateClass");
    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1),
        "the retired gate no longer opens a <Vortex> window (the window resolves it — see C-EoT2)");
}

async Task ScopedToOwnCard()
{
    EngineContext context = Context();
    var alliance = await PlaceDigimon(context, P1, "ALLY", dp: 4000, suspended: false);
    var bystander = await PlaceDigimon(context, P1, "BYST", dp: 4000, suspended: false);

    var source = new CardSource(context, alliance, P1);
    // SEAM (post-stage-B): AllianceSelfEffect returns a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasAlliance scan; attach it to the GRANTING card's controller only (the
    // bystander's controller is untouched, proving the scope is per-card, not global).
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var effect = CardEffectFactory.AllianceSelfEffect(false, source, null);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, alliance, ContinuousKeywordGate.Alliance), "the granting card HAS Alliance");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, bystander, ContinuousKeywordGate.Alliance), "a bystander does NOT get Alliance");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended }));
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
