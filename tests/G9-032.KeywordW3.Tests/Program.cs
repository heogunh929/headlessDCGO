using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-032): keyword self-static grants — Batch2 (Blitz/Decode/Progress/Partition) + by-name
// (Iceclad/Decoy/Fragment/Execute/Scapegoat). Verified live via ContinuousKeywordGate.HasKeyword.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // (P7 test-fix) Blitz/Decode/Partition/Decoy/Fragment/Scapegoat now require additional AS-IS parameters
    // that did not exist on the pre-rebuild simplified factories (isWhenDigivolving, decodeStrings/
    // sourceCondition, cardSourceConditions, permanentCondition/effectName/effectDiscription, trashValue,
    // effectName/effectDiscription respectively). Values below are innocuous placeholders (the assertions
    // only check keyword-presence via the gate, not these payloads) — see the MIGRATION-NOTE in Grant()
    // for why the grant itself is not observable yet regardless.
    ("Blitz", () => Grant(c => CardEffectFactory.BlitzSelfEffect(false, c, null, isWhenDigivolving: false), ContinuousKeywordGate.Blitz)),
    // (P7 test-fix) DataBase.DecodeEffectDiscription indexes decodeStrings[0] AND [1] ("Decode {source
    // color}" / "play 1 {target color} Digimon card") — a 1-element array (the prior placeholder) IndexOOB's.
    ("Decode", () => Grant(c => CardEffectFactory.DecodeSelfEffect(c, false, new[] { "Red", "Red" }, null, null), ContinuousKeywordGate.Decode)),
    ("Progress", () => Grant(c => CardEffectFactory.ProgressSelfStaticEffect(false, c, null), ContinuousKeywordGate.Progress)),
    ("Partition", () => Grant(c => CardEffectFactory.PartitionSelfEffect(false, c, null,
        new List<PartitionCondition> { new(4, "Red"), new(4, "Red") }), ContinuousKeywordGate.Partition)),
    ("Iceclad", () => Grant(c => CardEffectFactory.IcecladSelfStaticEffect(false, c, null), ContinuousKeywordGate.Iceclad)),
    ("Decoy", () => Grant(c => CardEffectFactory.DecoySelfEffect(false, c, null, permanentCondition: null,
        effectName: "Decoy", effectDiscription: "Decoy"), ContinuousKeywordGate.Decoy)),
    ("Fragment", () => Grant(c => CardEffectFactory.FragmentSelfEffect(false, c, null, trashValue: 1,
        effectName: "Fragment", effectDiscription: "Fragment"), ContinuousKeywordGate.Fragment)),
    ("Execute", () => Grant(c => CardEffectFactory.ExecuteSelfEffect(false, c, null), ContinuousKeywordGate.Execute)),
    // (P7 stage-B fix) real ScapegoatSelfEffect call sites always pass the literal "<Scapegoat>" (angle
    // brackets — e.g. EX8_061/EX8_071/BT20_080/LM_043), which is what the AS-IS Permanent.HasScapegoat scan
    // matches (Permanent.cs:3142); adjusted from the placeholder "Scapegoat" to that real convention.
    ("Scapegoat", () => Grant(c => CardEffectFactory.ScapegoatSelfEffect(false, c, null,
        effectName: "<Scapegoat>", effectDiscription: "Scapegoat"), ContinuousKeywordGate.Scapegoat)),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Grant(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var id = await PlaceDigimon(context, P1, "KW");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} absent before grant");
    // (P7 stage-B SEAM) these SelfEffect/SelfStaticEffect factories (Blitz/Decode/Progress/Partition/Iceclad/
    // Decoy/Fragment/Execute/Scapegoat) return new-model kind-classes (ActivateClass/CanNotAffectedClass/
    // IcecladClass) that register no substrate binding — the AS-IS-faithful path is the LIVE
    // cEntity_EffectController.GetCardEffects scan NewModelContinuousScan/ContinuousKeywordGate.HasKeyword
    // now performs (docs/audit/rebuild_p6_stageB_notes.md). Attach the built effect to the card's controller
    // via the same seam every ported card definition class uses (`cEntity_Effect` settable property), the
    // proven pattern from tests/FAILd-08.ChangeBaseCardName.Tests.
    var cs = new CardSource(context, id, P1);
    ICardEffect built = build(cs);
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} live after grant");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 932);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup) — a real
    // board is always past those phases when a card effect is evaluated (docs/audit/rebuild_p6_stageB_notes.md).
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
