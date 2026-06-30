using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G11-004: a once-per-turn (maxCountPerTurn=1) trigger whose GATE (CanResolve condition) is NOT met must
// NOT consume its per-turn use — so a later same-turn firing whose gate IS met still resolves. Driven
// through the real GameFlowProcessor trigger loop (self-scoped OnAllyAttack so the event matches the
// listener), which now evaluates the gate BEFORE the OnceFlag cap.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gate-failing trigger does NOT consume once-per-turn; later gate-passing firing resolves", GateBeforeConsume),
    ("Once-per-turn still caps repeated gate-passing firings in the same turn", OnceStillCaps),
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

async Task GateBeforeConsume()
{
    bool[] gate = { false };
    (EngineContext context, HeadlessEntityId card) = await Setup(gate);

    // Attack #1: gate CLOSED -> condition fails -> must NOT fire and must NOT consume the once-per-turn use.
    await Attack(context, card);
    AssertEqual(5, context.MemoryController.Current.Current, "gate-closed firing did not resolve (memory unchanged)");

    // Attack #2 (same turn): gate OPEN -> fires (proves the once-per-turn was NOT spent by attack #1).
    gate[0] = true;
    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "gate-open firing resolves (-1) since once was not wasted");
}

async Task OnceStillCaps()
{
    bool[] gate = { true };
    (EngineContext context, HeadlessEntityId card) = await Setup(gate);

    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "1st gate-open firing: -1");

    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "2nd same-turn firing blocked by once-per-turn (still 4)");

    context.OnceFlags.ResetForTurn(2, P1);
    await Attack(context, card);
    AssertEqual(3, context.MemoryController.Current.Current, "next turn: once resets, fires again (-1)");
}

// --- Helpers -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId)> Setup(bool[] gate)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1104);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF"), "DEF", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    var card = new HeadlessEntityId("p1:battle:C");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("DEF"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.BattleArea));

    // A MANDATORY once-per-turn [When Attacking] "lose 1 memory" whose gate is the controllable flag.
    // (amount < 0 -> mandatory, so a passing gate auto-resolves instead of surfacing an optional prompt.)
    var source = new CardSource(context, card, P1);
    ICardEffect mem = CardEffectFactory.AddMemoryTriggerEffect(
        EffectTiming.OnAllyAttack, amount: -1, isInheritedEffect: false, card: source,
        condition: () => gate[0], description: "test once+gate", maxCountPerTurn: 1, hash: "g11_004_once");
    context.EffectRegistry.Register(mem.ToBinding($"{card.Value}:test:onallyattack"));

    // Drain any pending zone-entry events so they don't interfere with the gated firings.
    await new GameFlowProcessor().RunToStableAsync(context);
    return (context, card);
}

// Self-scoped OnAllyAttack (subject = the attacking card = the listener) so the collector matches it.
async Task Attack(EngineContext context, HeadlessEntityId card)
{
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnAllyAttack, actor: P1, subject: card);
    await new GameFlowProcessor().RunToStableAsync(context);
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
