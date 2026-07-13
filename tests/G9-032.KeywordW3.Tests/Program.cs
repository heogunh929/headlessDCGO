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
    ("Decode", () => Grant(c => CardEffectFactory.DecodeSelfEffect(c, false, new[] { "Red" }, null, null), ContinuousKeywordGate.Decode)),
    ("Progress", () => Grant(c => CardEffectFactory.ProgressSelfStaticEffect(false, c, null), ContinuousKeywordGate.Progress)),
    ("Partition", () => Grant(c => CardEffectFactory.PartitionSelfEffect(false, c, null,
        new List<PartitionCondition> { new(4, "Red"), new(4, "Red") }), ContinuousKeywordGate.Partition)),
    ("Iceclad", () => Grant(c => CardEffectFactory.IcecladSelfStaticEffect(false, c, null), ContinuousKeywordGate.Iceclad)),
    ("Decoy", () => Grant(c => CardEffectFactory.DecoySelfEffect(false, c, null, permanentCondition: null,
        effectName: "Decoy", effectDiscription: "Decoy"), ContinuousKeywordGate.Decoy)),
    ("Fragment", () => Grant(c => CardEffectFactory.FragmentSelfEffect(false, c, null, trashValue: 1,
        effectName: "Fragment", effectDiscription: "Fragment"), ContinuousKeywordGate.Fragment)),
    ("Execute", () => Grant(c => CardEffectFactory.ExecuteSelfEffect(false, c, null), ContinuousKeywordGate.Execute)),
    ("Scapegoat", () => Grant(c => CardEffectFactory.ScapegoatSelfEffect(false, c, null,
        effectName: "Scapegoat", effectDiscription: "Scapegoat"), ContinuousKeywordGate.Scapegoat)),
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
    // MIGRATION-NOTE (P7 test-fix): all of these SelfEffect/SelfStaticEffect factories (Blitz/Decode/
    // Progress/Partition/Iceclad/Decoy/Fragment/Execute/Scapegoat) return new-model kind-classes
    // (ActivateClass/CanNotAffectedClass/IcecladClass) with no ToBinding/EffectRegistry bridge (stage-B
    // RED, docs/audit/rebuild_p6_stageA_notes.md). ContinuousKeywordGate.HasKeyword reads only the
    // substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this grant
    // observable yet. The factory call is kept (still exercises construction); Register/ToBinding is
    // dropped. Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not
    // silently weakened.
    build(new CardSource(context, id, P1));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, keyword), $"{keyword} live after grant");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 932);
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
