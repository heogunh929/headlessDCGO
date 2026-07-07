using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
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
    ("BT1_003 LIVE: [When Attacking] conditional draw fires via bridge when this card attacks + opponent qualifies", BT1_003_Live),
    ("BT1_003 LIVE: once-per-turn — a 2nd attack same turn does NOT draw again", BT1_003_OncePerTurn),
    ("BT1_003 LIVE: no draw when the opponent has no no-source Digimon (CanActivate fails)", BT1_003_NoCondition),
    ("BT1_003 LIVE: no draw when a DIFFERENT ally attacks (self-scope)", BT1_003_OtherAlly),
    ("select body (interactive): select + destroy the chosen opponent Digimon", SelectDestroyBody),
    ("grant-continuous body: a [Main] skill grants a restriction (registers the continuous binding)", GrantRestrictionBody),
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

async Task GrantRestrictionBody()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    var card = new CardSource(ctx, self, P1);
    AssertTrue(!ContinuousRestrictionGate.EvaluateUnsuspend(ctx, self).IsRestricted, "not restricted before the grant");

    var e = new ActivatedEffect(card, EffectTiming.OptionSkill, canUse: null, canActivate: null,
        body: new GrantContinuousBody(CardEffectFactory.CantUnsuspendStaticEffect(false, card, null)),
        maxCountPerTurn: null, isOptional: false, "[Main] grant: this Digimon can't unsuspend");

    await Resolve(ctx, e);
    AssertTrue(ContinuousRestrictionGate.EvaluateUnsuspend(ctx, self).IsRestricted, "restricted after the [Main] grant resolved (continuous binding registered)");
}

async Task SelectDestroyBody()
{
    (EngineContext ctx, HeadlessEntityId self) = Board();
    var cards = (CardDatabase)ctx.CardRepository;
    var foe = new HeadlessEntityId("p2:battle:FOE");
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:FOE"), "FOE", "Foe", new Dictionary<string, object?>(), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, new HeadlessEntityId("DEF:FOE"), P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));

    var card = new CardSource(ctx, self, P1);
    var e = new ActivatedEffect(card, EffectTiming.OptionSkill, canUse: null, canActivate: null,
        body: new SelectBody(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id), maxCount: 1,
                             canNoSelect: false, canEndNotMax: false, SelectPermanentEffect.Mode.Destroy, "destroy 1"),
        maxCountPerTurn: null, isOptional: false, "select + destroy 1 opponent Digimon");

    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(foe));
    await Resolve(ctx, e);
    AssertTrue(!((IZoneStateReader)ctx.ZoneMover).GetCards(P2, ChoiceZone.BattleArea).Contains(foe), "chosen opponent Digimon destroyed");
}

// --- BT1_003 live (real card via the bridge) ---

async Task BT1_003_Live()
{
    (EngineContext ctx, HeadlessEntityId atk) = BoardBT1_003(opponentNoSourceDigimon: true, deck: 3);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnAllyAttack", actor: P1, subject: atk);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(2, DeckCount(ctx, P1), "deck 3 - 1 = 2 (conditional draw@trigger fired live via the uniform primitive)");
}

async Task BT1_003_OncePerTurn()
{
    (EngineContext ctx, HeadlessEntityId atk) = BoardBT1_003(opponentNoSourceDigimon: true, deck: 3);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnAllyAttack", actor: P1, subject: atk);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnAllyAttack", actor: P1, subject: atk);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(2, DeckCount(ctx, P1), "only 1 draw this turn (once-per-turn cap): deck stays at 2");
}

async Task BT1_003_NoCondition()
{
    (EngineContext ctx, HeadlessEntityId atk) = BoardBT1_003(opponentNoSourceDigimon: false, deck: 3);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnAllyAttack", actor: P1, subject: atk);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(3, DeckCount(ctx, P1), "no qualifying opponent Digimon -> CanActivate fails -> no draw");
}

async Task BT1_003_OtherAlly()
{
    (EngineContext ctx, HeadlessEntityId atk) = BoardBT1_003(opponentNoSourceDigimon: true, deck: 3);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, "OnAllyAttack", actor: P1, subject: new HeadlessEntityId("p1:battle:OTHER"));
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(3, DeckCount(ctx, P1), "a different ally attacking does not fire BT1_003 (self-scope)");
}

(EngineContext, HeadlessEntityId) BoardBT1_003(bool opponentNoSourceDigimon, int deck)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    // BT1_003 on P1's battle area (definition cardNumber = "BT1_003" so its effect class is dispatched).
    var atk = new HeadlessEntityId("p1:battle:BT1_003");
    cards.Upsert(new CardRecord(new HeadlessEntityId("BT1_003"), "BT1_003", "Betamon", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(atk, new HeadlessEntityId("BT1_003"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, atk, ChoiceZone.None, ChoiceZone.BattleArea)).Wait();

    if (opponentNoSourceDigimon)
    {
        var foe = new HeadlessEntityId("p2:battle:FOE");
        cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:FOE"), "FOE", "Foe", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, new HeadlessEntityId("DEF:FOE"), P2));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea)).Wait();
    }

    for (int i = 0; i < deck; i++)
    {
        var id = new HeadlessEntityId($"1:deck:{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:d{i}"), $"d{i}", $"d{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:d{i}"), P1));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library)).Wait();
    }

    return (ctx, atk);
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
