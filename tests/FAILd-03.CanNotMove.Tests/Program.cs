using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: CanNotMove (AS-IS ICanNotMoveEffect / Permanent.CanMove) was MISSING. CanNotMoveStaticEffect now
// registers a continuous restriction the legal-action dispatcher's move gate honours.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<bool> Body)[]
{
    ("no restriction: the breeding Digimon may move", () => MoveOffered(restrict: false)),
    ("CanNotMove: the move is NOT offered", () => !MoveOffered(restrict: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

bool MoveOffered(bool restrict)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 923);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Breeding);
    // (R3-W3c-4c D-1) the live move-gate ICanNotMoveEffect scan gates each effect with CanUse (→ IsDisabled →
    // GManager), so build legal actions under an ambient match scope (FAILd-06 precedent).
    using var _ambient = AmbientMatchContext.Enter(ctx);
    var cards = (CardDatabase)ctx.CardRepository;

    var b = new HeadlessEntityId("p1:B");
    // (R3-W3c-4c D-1) the move restriction is now the AS-IS kind-class CanNotMoveClass carried on B's LIVE
    // EffectList (no registry binding); the dispatcher's move gate is the AS-IS-literal live ICanNotMoveEffect
    // scan (AS-IS Permanent.CanMove, causing effect null). B dispatches to the TfxCanNotMove fixture by CardNumber,
    // whose static Predicate slot the harness sets to the joint AS-IS predicate CanNotMove(candidate, causing).
    TfxCanNotMove.Predicate = restrict ? (candidate, _) => candidate.InstanceId == b : null;

    cards.Upsert(new CardRecord(new HeadlessEntityId("B"), restrict ? "TfxCanNotMove" : "B", "B",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(b, new HeadlessEntityId("B"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, b, ChoiceZone.None, ChoiceZone.BreedingArea)).GetAwaiter().GetResult();

    var dispatcher = new HeadlessLegalActionDispatcher();
    return dispatcher.GetLegalActions(ctx, P1).Any(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
}
