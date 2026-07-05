// PRIM-P0 B.O.5: CardEffectFactory.AddEffectToPlayer — a delayed one-shot player effect (AS-IS AddEffectToPlayer).
// A "[End of Your Turn] lose 2 memory" delayed effect fires ONCE at OnEndTurn, then its binding self-removes
// (fire-then-clear) so it does NOT fire on subsequent turn ends. Guards the headless CLEAR-then-FIRE turn-end race.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Src = new("1:battle:SRC");

var tests = new (string Name, Func<Task> Body)[]
{
    ("the delayed effect fires once at OnEndTurn (memory 5 -> 3)", FiresOnceAtTiming),
    ("after firing, the binding is removed and does NOT fire again next turn end", OneShotNoRefire),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task FiresOnceAtTiming()
{
    EngineContext context = Setup();
    RegisterDelayedLoseMemory(context);
    context.MemoryController.Set(5);

    await EndTurn(context);

    AssertEqual(3, context.MemoryController.Current.Current, "the delayed [End of Turn] lose-2 fired once");
}

async Task OneShotNoRefire()
{
    EngineContext context = Setup();
    RegisterDelayedLoseMemory(context);
    context.MemoryController.Set(5);

    await EndTurn(context);
    AssertEqual(3, context.MemoryController.Current.Current, "fired on the first turn end");
    AssertTrue(!HasDelayedBinding(context), "the one-shot binding was removed after firing");

    await EndTurn(context);   // a second OnEndTurn — the one-shot must NOT fire again
    AssertEqual(3, context.MemoryController.Current.Current, "did NOT fire again (still 3, no re-fire)");
}

// --- Harness -------------------------------------------------------------

EngineContext Setup()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1 is the turn player (== owner)
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Src, new HeadlessEntityId("DEF:SRC"), P1, Metadata: new Dictionary<string, object?>()));
    return context;
}

void RegisterDelayedLoseMemory(EngineContext context)
{
    var card = new CardSource(context, Src, P1, P1);
    ICardEffect delayed = new TriggeredGainMemoryEffect(card, EffectTiming.OnEndTurn, amount: -2, "[End of Your Turn] Lose 2 memory.");
    CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, delayed, EffectTiming.OnEndTurn);
}

async Task EndTurn(EngineContext context)
{
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1, subject: Src);
    await new GameFlowProcessor().RunToStableAsync(context);
}

bool HasDelayedBinding(EngineContext context)
{
    foreach (EffectRequest req in context.EffectRegistry.GetEffectsForTiming(TriggerTimings.OnEndTurn))
    {
        if (req.Context.Values.TryGetValue(AutoProcessingTriggerCollector.DelayedOneShotKey, out object? v) && v is true)
            return true;
    }
    return false;
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
