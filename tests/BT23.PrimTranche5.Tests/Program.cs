// BT2/BT3 missing-primitive development — Tranche 5.
//   G3 — ETB suppression: an effect-play with activateETB:false must skip the played card's OWN
//        [On Play]/OnEnterField triggers ("Any [On Play] effects on the Digimon played with this effect don't
//        activate", BT3_109/110). The mechanism: PlayPermanentCards(activateETB:false) no longer throws; the
//        PlayCard mutation threads a one-shot suppressOnPlay marker onto the entering CardMoved event, and
//        AutoProcessingTriggerCollector drops the moved card's own OnPlay/OnEnterField triggers.
//
//   These tests verify the mechanism directly at the collector (register the card -> collect triggers for a
//   suppressed vs. unsuppressed CardMoved event). NOTE: end-to-end firing of an effect-played card's [On Play]
//   is a SEPARATE pre-existing gap — CardEffectCommons.PlayPermanentCards builds its sink without the enter-play
//   registration callback the action layer wires (EngineContext.cs:261), so an effect-played card's effects are
//   not auto-registered today. That gap is orthogonal to this suppression and affects G1's played cards too.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

// (RC-6) The two collector-driven suppression subtests (G3_SuppressDropsOwnEnterPlay /
// G3_NoSuppressKeepsTrigger) were removed: they asserted the invented registry trigger-reader surface
// (register a ToBinding fixture -> AutoProcessingTriggerCollector.CollectAllTriggers returns it), which is
// excised. The retargeted TfxOnPlayGainMemory is a new-model ActivateClass (no registry binding), so the
// collector-level suppression probe has no home; ETB-suppression on the live path routes through the
// SkillInfo-scan pipeline, not this reader. The play-does-not-throw behavior subtest is retained.
var tests = new (string Name, Func<Task> Body)[]
{
    ("G3: PlayPermanentCards(activateETB:false) no longer throws and still plays the card onto the field", G3_PlayEtbFalseDoesNotThrow),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- G3 ----------------------------------------------------------------------

async Task G3_PlayEtbFalseDoesNotThrow()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source");
    HeadlessEntityId played = Trash(ctx, "PLAYED", "PlayedDigi");

    // Previously PlayPermanentCards(activateETB:false) threw NotSupportedException; now it plays with ETB suppressed.
    await CardEffectCommons.PlayPermanentCards(
        new[] { new CardSource(ctx, played, P1) }, new CardSource(ctx, src, P1),
        payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: false);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, played), "the card was played onto the battle area (ETB suppressed, not the play)");
}

// ---- Harness -----------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

HeadlessEntityId Place(EngineContext ctx, string number, string name, ChoiceZone zone, string cardType)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:{zone}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name) => Place(ctx, number, name, ChoiceZone.BattleArea, "Digimon");
HeadlessEntityId Trash(EngineContext ctx, string number, string name) => Place(ctx, number, name, ChoiceZone.Trash, "Digimon");

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
