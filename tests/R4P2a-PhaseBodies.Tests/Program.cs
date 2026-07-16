using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using GameContext = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.GameContext;
using TurnStateMachine = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.TurnStateMachine;

// (R4 batch P2a) DORMANT phase-body witnesses. Each drives one AS-IS-region mirror method DIRECTLY
// (gate-invisible: no driver, no action queue) and asserts the deterministic body order/outcome:
//   * ActivePhaseAsync : reset (UntilOwnerTurnStartEffects) -> start-of-turn window -> unsuspend -> phase-end
//     bucket reset (TurnCount is a read-only view over the substrate turn number, R4 P3).
//   * DrawPhaseAsync   : deck-out loss judgment (empty library, TurnCount != 1) marks the turn player.
//   * EndPhaseAsync    : the end-of-turn reset LIST (every mirrored Until* bucket) is cleared.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ActivePhaseAsync runs reset -> start-turn window -> unsuspend -> bucket reset (TurnCount views substrate)", ActiveOrder),
    ("DrawPhaseAsync judges deck-out as a loss for the empty-library turn player", DrawDeckOut),
    ("EndPhaseAsync clears the full end-of-turn reset list", EndResetList),
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

async Task ActiveOrder()
{
    EngineContext context = Board(turnPlayer: P1, phase: HeadlessPhase.Active);
    using var scope = AmbientMatchContext.Enter(context);
    var permA = Place(context, P1, "A");
    permA.IsSuspended = true;

    // Seed the buckets the body resets so an "emptied" post-state proves each reset ran.
    permA.UntilOwnerTurnStartEffects = OneEffect();
    permA.UntilNextUntapEffects = OneEffect();
    var turnPlayer = new Player(context, P1);
    turnPlayer.UntilOwnerActivePhaseEffects = OneEffect();

    var tsm = TurnStateMachine.For(context);
    // (R4 P3) TurnCount is now a read-only view over the substrate turn counter (HeadlessTurnState.TurnNumber);
    //   the AS-IS :550 in-body increment is the substrate turn-advance (turn-controller today, S3 relocates it
    //   here), so it is no longer a dormant-body mutation. The reset-ORDER below is this witness's real subject.
    AssertEqual(context.TurnController.Current.TurnNumber, tsm.TurnCount, "TurnCount views substrate TurnNumber");

    await tsm.ActivePhaseAsync();

    // reset (:534) ran BEFORE the start-turn window + unsuspend (both below observed) — order preserved.
    AssertEqual(0, new Permanent(context, permA.InstanceId, P1).UntilOwnerTurnStartEffects.Count,
        "UntilOwnerTurnStartEffects reset (AS-IS :534-537)");
    // unsuspend (:622) ran (after reset + window) — the suspended turn-player permanent is now unsuspended.
    AssertTrue(!new Permanent(context, permA.InstanceId, P1).IsSuspended, "permanent unsuspended (AS-IS :586-622)");
    // phase-end buckets reset (:643-647).
    AssertEqual(0, new Player(context, P1).UntilOwnerActivePhaseEffects.Count,
        "UntilOwnerActivePhaseEffects reset (AS-IS :644)");
    AssertEqual(0, new Permanent(context, permA.InstanceId, P1).UntilNextUntapEffects.Count,
        "UntilNextUntapEffects reset (AS-IS :646)");
}

async Task DrawDeckOut()
{
    // (R4 P3) TurnCount now views the substrate turn number, so advance the turn-controller to turn 3 (two
    //   EndTurns keep P1 the turn player: turn1=P1, turn2=P2, turn3=P1) — AS-IS :669 draws only when TurnCount != 1.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 6162);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.EndTurn();   // turn 2, P2
    context.TurnController.EndTurn();   // turn 3, P1
    ((InMemoryHeadlessTurnController)context.TurnController).SetPhase(HeadlessPhase.Draw);
    using var scope = AmbientMatchContext.Enter(context);
    // P1 library is empty (no cards placed there); the turn player P1 must draw -> deck-out.
    var tsm = TurnStateMachine.For(context);
    AssertEqual(3, tsm.TurnCount, "TurnCount == 3 (substrate, != 1 so DrawPhase draws)");

    await tsm.DrawPhaseAsync();

    AssertTrue(tsm.endGame, "endGame set (AS-IS EndGame :3325)");
    AssertTrue(context.PlayerStatusController.IsLose(P1), "turn player P1 loses on deck-out (AS-IS :674)");
    AssertTrue(!context.PlayerStatusController.IsLose(P2), "non-turn player P2 does not lose");
}

async Task EndResetList()
{
    EngineContext context = Board(turnPlayer: P1, phase: HeadlessPhase.End);
    using var scope = AmbientMatchContext.Enter(context);
    var permA = Place(context, P1, "A");
    var permB = Place(context, P2, "B");

    var p1 = new Player(context, P1);
    var p2 = new Player(context, P2);

    // Seed every bucket the reset list clears.
    p1.UntilEachTurnEndEffects = OneEffect();
    p2.UntilEachTurnEndEffects = OneEffect();
    p1.UntilCalculateFixedCostEffect = OneEffect();
    p2.UntilCalculateFixedCostEffect = OneEffect();
    p1.UntilOwnerTurnEndEffects = OneEffect();
    p2.UntilOpponentTurnEndEffects = OneEffect();
    permA.UntilEachTurnEndEffects = OneEffect();
    permA.UntilOwnerTurnEndEffects = OneEffect();
    permB.UntilEachTurnEndEffects = OneEffect();
    permB.UntilOpponentTurnEndEffects = OneEffect();

    var tsm = TurnStateMachine.For(context);
    await tsm.EndPhaseAsync();

    AssertTrue(!tsm.isFirstPlayerFirstTurn, "isFirstPlayerFirstTurn cleared (AS-IS :3165)");
    AssertEqual(0, new Player(context, P1).UntilEachTurnEndEffects.Count, "P1 UntilEachTurnEndEffects (:3177)");
    AssertEqual(0, new Player(context, P2).UntilEachTurnEndEffects.Count, "P2 UntilEachTurnEndEffects (:3177)");
    AssertEqual(0, new Player(context, P1).UntilCalculateFixedCostEffect.Count, "P1 UntilCalculateFixedCostEffect (:3179)");
    AssertEqual(0, new Player(context, P2).UntilCalculateFixedCostEffect.Count, "P2 UntilCalculateFixedCostEffect (:3179)");
    AssertEqual(0, new Player(context, P1).UntilOwnerTurnEndEffects.Count, "P1 UntilOwnerTurnEndEffects (:3196)");
    AssertEqual(0, new Player(context, P2).UntilOpponentTurnEndEffects.Count, "P2 UntilOpponentTurnEndEffects (:3194)");
    AssertEqual(0, new Permanent(context, permA.InstanceId, P1).UntilEachTurnEndEffects.Count, "permA UntilEachTurnEndEffects (:3185)");
    AssertEqual(0, new Permanent(context, permA.InstanceId, P1).UntilOwnerTurnEndEffects.Count, "permA UntilOwnerTurnEndEffects (:3191)");
    AssertEqual(0, new Permanent(context, permB.InstanceId, P2).UntilEachTurnEndEffects.Count, "permB UntilEachTurnEndEffects (:3185)");
    AssertEqual(0, new Permanent(context, permB.InstanceId, P2).UntilOpponentTurnEndEffects.Count, "permB UntilOpponentTurnEndEffects (:3200)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board(HeadlessPlayerId turnPlayer, HeadlessPhase phase)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 6162);
    context.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    ((InMemoryHeadlessTurnController)context.TurnController).SetPhase(phase);
    return context;
}

Permanent Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return new Permanent(context, id, owner);
}

static List<Func<EffectTiming, ICardEffect>> OneEffect() =>
    new() { _ => null! };

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
