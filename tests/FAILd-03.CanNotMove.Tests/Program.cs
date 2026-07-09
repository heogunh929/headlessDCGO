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
    var cards = (CardDatabase)ctx.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("B"), "B", "B",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var b = new HeadlessEntityId("p1:B");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(b, new HeadlessEntityId("B"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, b, ChoiceZone.None, ChoiceZone.BreedingArea)).GetAwaiter().GetResult();

    if (restrict)
    {
        // AS-IS joint predicate CanNotMove(candidate, causing): the breeding Digimon B cannot move (causing is null).
        ctx.EffectRegistry.Register(CardEffectFactory.CanNotMoveStaticEffect(
            (candidate, _) => candidate.InstanceId == b, new CardSource(ctx, b, P1), condition: null).ToBinding($"cnm:{b.Value}"));
    }

    var dispatcher = new HeadlessLegalActionDispatcher();
    return dispatcher.GetLegalActions(ctx, P1).Any(a => a.ActionType == HeadlessActionTypes.MoveBreedingToBattle);
}
