using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM uniform-activated: the single ActivatedEffect (mirror of AS-IS ActivateClass) expresses shapes that had
// NO factory before — e.g. "draw as a self-scoped [When Attacking] trigger" (draw @ trigger, the top gap).
// Proves: gate (CanUse) self-scopes, precondition (CanActivate) gates, and the composable body applies.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("draw @ trigger: fires when THIS card is the subject (self-scope via CanUse)", DrawTriggerSelf),
    ("draw @ trigger: does NOT fire when another card is the subject", DrawTriggerOther),
    ("CanActivate precondition gates the body (deck empty -> blocked)", PreconditionGate),
    ("memory body: gain 1 memory", MemoryBodyGain),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DrawTriggerSelf()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    await Deck(ctx, P1, 3);
    ActivatedEffect e = DrawTrigger(new CardSource(ctx, self, P1));

    bool canResolve = e.CanResolve(TriggerCtx(self));
    AssertTrue(canResolve, "CanResolve true when this card is the attack subject");
    await Resolve(ctx, e);
    AssertEqual(2, DeckCount(ctx, P1), "deck 3 - 1 drawn = 2 (draw@trigger fired for self)");
}

async Task DrawTriggerOther()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    await Deck(ctx, P1, 3);
    ActivatedEffect e = DrawTrigger(new CardSource(ctx, self, P1));

    AssertTrue(!e.CanResolve(TriggerCtx(new HeadlessEntityId("p1:battle:OTHER"))), "CanResolve false: another card is the subject");
    AssertEqual(3, DeckCount(ctx, P1), "deck unchanged when a different card attacks");
    await Task.CompletedTask;
}

async Task PreconditionGate()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    await Deck(ctx, P1, 3);
    // CanActivate = deck must have >= 5 (fails at 3) -> body must not apply even though CanUse passes.
    var card = new CardSource(ctx, self, P1);
    var e = new ActivatedEffect(card, EffectTiming.OnAllyAttack,
        canUse: c => CardEffectCommons.CanTriggerOnAttack(c, card),
        canActivate: () => DeckCount(ctx, P1) >= 5,
        body: new DrawBody(1), maxCountPerTurn: null, isOptional: false, "draw if deck >= 5");

    AssertTrue(!e.CanResolve(TriggerCtx(self)), "precondition (deck >= 5) blocks CanResolve at deck 3");
    AssertEqual(3, DeckCount(ctx, P1), "deck unchanged (precondition failed)");
    await Task.CompletedTask;
}

async Task MemoryBodyGain()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    ctx.MemoryController.Set(0);
    var card = new CardSource(ctx, self, P1);
    var e = new ActivatedEffect(card, EffectTiming.OnAllyAttack,
        canUse: c => CardEffectCommons.CanTriggerOnAttack(c, card), canActivate: null,
        body: new MemoryBody(1), maxCountPerTurn: null, isOptional: false, "+1 memory");

    AssertTrue(e.CanResolve(TriggerCtx(self)), "CanResolve true for self");
    await Resolve(ctx, e);
    AssertEqual(1, ctx.MemoryController.Current.Current, "+1 memory applied");
}

// --- Helpers ---

ActivatedEffect DrawTrigger(CardSource card) =>
    new(card, EffectTiming.OnAllyAttack,
        canUse: c => CardEffectCommons.CanTriggerOnAttack(c, card), canActivate: null,
        body: new DrawBody(1), maxCountPerTurn: null, isOptional: false, "[When Attacking] Draw 1");

CardEffectResolveContext TriggerCtx(HeadlessEntityId subject)
{
    var ec = new EffectContext(P1, P1, new HeadlessEntityId("src"), triggerEntityId: subject, targetEntityIds: Array.Empty<HeadlessEntityId>());
    return new CardEffectResolveContext(new EffectRequest(new HeadlessEntityId("req"), P1, "OnAllyAttack", ec));
}

async Task Resolve(EngineContext ctx, ActivatedEffect e)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue);
    await e.ResolveBodyAsync(sink, ctx.ChoiceProvider, new[] { P1, P2 });
    await sink.FlushAsync();
}

(EngineContext, HeadlessEntityId) Board()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    var self = new HeadlessEntityId("p1:battle:SELF");
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:SELF"), "SELF", "Self", new Dictionary<string, object?>(), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("DEF:SELF"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea)).Wait();
    return (ctx, self);
}

async Task Deck(EngineContext ctx, HeadlessPlayerId owner, int n)
{
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 0; i < n; i++)
    {
        var id = new HeadlessEntityId($"{owner.Value}:deck:{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:d{i}"), $"d{i}", $"d{i}", new Dictionary<string, object?>(), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:d{i}"), owner));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    }
}

int DeckCount(EngineContext ctx, HeadlessPlayerId owner) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Library).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
