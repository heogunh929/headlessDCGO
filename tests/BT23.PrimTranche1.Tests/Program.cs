// BT2/BT3 missing-primitive development — Tranche 1 (quick wins) assertion tests.
//   G2 — OnUseOption reactive dispatch: an option-use window reaches a field card's activated effect.
//   G4 — atomic draw-then-discard: DrawThenDiscardEffect draws first (so drawn cards are discardable).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2: OnUseOption broadcast reaches a field card's activated effect (gain 2 memory)", G2_OnUseOptionFires),
    ("G2: a card with no OnUseOption effect is unaffected by the option-use window", G2_ScopedToListener),
    ("G4: draw-then-discard draws FIRST, then discards from the resulting hand (net +draw-discard)", G4_DrawThenDiscard),
    ("G4: a just-drawn card is a legal discard target (atomicity)", G4_DrawnCardDiscardable),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- G2 ----------------------------------------------------------------------

async Task G2_OnUseOptionFires()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    // A Tamer on the battle area whose effect reacts to OnUseOption (fixture: gain 2 memory).
    Battle(ctx, "TfxOnUseOptionMemory", "TfxOnUseOptionMemory", cardType: "Tamer");
    // Simulate an option being used: emit the OnUseOption window (subject = an option card id).
    var optionId = new HeadlessEntityId("p1:opt:X");
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnUseOption, actor: P1, subject: optionId);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(2, ctx.MemoryController.Current.Current, "the field card's [when Option used] gained 2 memory via the G2 broadcast");
}

async Task G2_ScopedToListener()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    // A plain card with no OnUseOption effect.
    Battle(ctx, "PlainDigi", "PlainDigi", cardType: "Digimon");
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnUseOption, actor: P1, subject: new HeadlessEntityId("p1:opt:X"));
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "no reacting effect -> memory unchanged");
}

// ---- G4 ----------------------------------------------------------------------

async Task G4_DrawThenDiscard()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "SELF", "SELF");
    // Empty hand; 2 library cards so a draw of 1 is possible.
    Library(ctx, "lib0"); Library(ctx, "lib1");
    int handBefore = ZoneCount(ctx, ChoiceZone.Hand);

    // (R7 종점) draw 1, then discard 1 (mandatory) via the LIVE substrate coroutine the retired
    // ActivatedDrawThenDiscardEffect merely wrapped (CardEffectCommons.DrawAndDiscardCards — draws+flushes
    // before building the discard pool). Scripted: discard the drawn card (fallback picks minCount).
    Script(ctx); // fallback picks minCount from candidates
    using (AmbientMatchContext.Enter(ctx))
    {
        await CardEffectCommons.DrawAndDiscardCards(
            (P1, P1), drawAmount: 1, trashAmount: 1, new CardSource(ctx, self, P1),
            canTrashTargetCondition: null, canNoSelect: false, canEndNotMax: false, CancellationToken.None);
    }

    AssertEqual(handBefore, ZoneCount(ctx, ChoiceZone.Hand), "draw 1 + discard 1 = net hand unchanged");
    AssertEqual(1, ZoneCount(ctx, ChoiceZone.Trash), "exactly 1 card discarded to trash");
}

async Task G4_DrawnCardDiscardable()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "SELF", "SELF");
    // Hand starts empty; the ONLY discardable card must be the one drawn this activation.
    var drawn = Library(ctx, "drawme");
    Script(ctx, ChoiceResult.Select(drawn)); // explicitly discard the just-drawn card
    using (AmbientMatchContext.Enter(ctx))
    {
        await CardEffectCommons.DrawAndDiscardCards(
            (P1, P1), drawAmount: 1, trashAmount: 1, new CardSource(ctx, self, P1),
            canTrashTargetCondition: null, canNoSelect: false, canEndNotMax: false, CancellationToken.None);
    }

    AssertTrue(InZone(ctx, ChoiceZone.Trash, drawn), "the just-drawn card was a legal discard target and went to trash");
    AssertFalse(InZone(ctx, ChoiceZone.Hand, drawn), "it left the hand");
}

// ---- Harness -----------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

void Script(EngineContext ctx, params ChoiceResult[] choices)
{
    var p = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    p.Clear();
    foreach (var c in choices) { p.Enqueue(c); }
}

HeadlessEntityId Place(EngineContext ctx, string number, string name, ChoiceZone zone, string cardType)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:{zone}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }
    return id;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name, string cardType = "Digimon") =>
    Place(ctx, number, name, ChoiceZone.BattleArea, cardType);

HeadlessEntityId Library(EngineContext ctx, string idSuffix) =>
    Place(ctx, idSuffix, idSuffix, ChoiceZone.Library, "Digimon");

int ZoneCount(EngineContext ctx, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Count;

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
}
