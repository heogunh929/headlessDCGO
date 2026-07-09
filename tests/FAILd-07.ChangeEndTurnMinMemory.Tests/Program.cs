using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: ChangeEndTurnMinMemory (AS-IS BT14_081/BT17_069 "set the turn-end min memory to 3") was MISSING. The
// turn auto-ends when the opponent reaches the min-memory threshold (default 1); the effect raises it to 3, so at
// memory -1 the turn no longer passes.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<bool> Body)[]
{
    ("default threshold 1: memory -1 passes the turn", () => Passes(memory: -1, minMemory: null) == true),
    ("ChangeEndTurnMinMemory(3): memory -1 does NOT pass the turn", () => Passes(memory: -1, minMemory: 3) == false),
    ("ChangeEndTurnMinMemory(3): memory -3 passes the turn", () => Passes(memory: -3, minMemory: 3) == true),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

bool Passes(int memory, int? minMemory)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 927);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);

    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var card = new HeadlessEntityId("p1:C");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("C"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();

    if (minMemory is int m)
    {
        context.EffectRegistry.Register(CardEffectFactory.ChangeEndTurnMinMemoryStaticEffect(
            m, isInheritedEffect: false, new CardSource(context, card, P1), condition: null).ToBinding("cetmm"));
    }

    HeadlessMemoryState state = context.MemoryController.Set(memory);
    var action = HeadlessActionFactory.AdvancePhase(P1);
    MainPhaseMemoryResult result = new HeadlessMainPhaseFlow()
        .EvaluateAfterMemoryMutation(context, action, state, state, "test");
    return result.MemoryPassTriggered;
}
