using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using GameContext = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.GameContext;
using TurnStateMachine = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.TurnStateMachine;

// (MIG6 goal-6 surface) Mirror GameContext accessors (Players/Players_ForTurnPlayer/TurnPlayer/TurnPhase/
// PermanentsForTurnPlayer) returning mirror Player/Permanent, delegating to TurnController + zone reads.
// Smoke coverage for the accessors (additive surface, no current caller).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Players_ForTurnPlayer lists both seats with the turn player first", TurnPlayerFirst),
    ("TurnPlayer/NonTurnPlayer resolve to the seated players", TurnAndNonTurn),
    ("TurnPhase maps HeadlessPhase to the AS-IS phase enum", PhaseMapping),
    ("PermanentsForTurnPlayer returns both players' battle-area permanents, turn first", PermanentsTurnFirst),
    ("TurnStateMachine.For(context).gameContext exposes the same GameContext", AccessPath),
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

Task TurnPlayerFirst()
{
    EngineContext context = Board(turnPlayer: P2);
    var gc = new GameContext(context);
    var order = gc.Players_ForTurnPlayer.Select(p => p.PlayerId).ToList();
    AssertEqual(2, order.Count, "two seats");
    AssertEqual(P2, order[0], "turn player (P2) first");
    AssertEqual(P1, order[1], "non-turn player second");
    return Task.CompletedTask;
}

Task TurnAndNonTurn()
{
    EngineContext context = Board(turnPlayer: P1);
    var gc = new GameContext(context);
    AssertEqual(P1, gc.TurnPlayer!.PlayerId, "turn player P1");
    AssertEqual(P2, gc.NonTurnPlayer!.PlayerId, "non-turn player P2");
    return Task.CompletedTask;
}

Task PhaseMapping()
{
    EngineContext context = Board(turnPlayer: P1);
    ((InMemoryHeadlessTurnController)context.TurnController).SetPhase(HeadlessPhase.Main);
    AssertTrue(new GameContext(context).TurnPhase == GameContext.phase.Main, "Main maps to phase.Main");
    ((InMemoryHeadlessTurnController)context.TurnController).SetPhase(HeadlessPhase.Breeding);
    AssertTrue(new GameContext(context).TurnPhase == GameContext.phase.Breeding, "Breeding maps to phase.Breeding");
    return Task.CompletedTask;
}

Task PermanentsTurnFirst()
{
    EngineContext context = Board(turnPlayer: P2);
    Place(context, P1, "A");
    Place(context, P2, "B");
    var perms = new GameContext(context).PermanentsForTurnPlayer;
    AssertEqual(2, perms.Count, "both battle-area permanents");
    AssertEqual(P2, perms[0].OwnerId, "turn player (P2) permanent first");
    return Task.CompletedTask;
}

Task AccessPath()
{
    EngineContext context = Board(turnPlayer: P1);
    var gc = TurnStateMachine.For(context).gameContext;
    AssertEqual(P1, gc.TurnPlayer!.PlayerId, "gameContext via TurnStateMachine.For");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

EngineContext Board(HeadlessPlayerId turnPlayer)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 61);
    context.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    return context;
}

void Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
