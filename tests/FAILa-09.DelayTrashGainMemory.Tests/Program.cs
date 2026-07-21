using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #9 (mapping remediation): Gain2MemoryOptionDelayEffect must mirror AS-IS — a [Main] <Delay> that TRASHES
// this card's own permanent to activate, and gains 2 memory ONLY IF the trash succeeded. The port previously
// mapped it to an UNCONDITIONAL start-of-turn +2 (wrong trigger and no self-trash cost/gate).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Own permanent present: trashes self AND gains 2 memory", TrashAndGain),
    ("No own permanent to trash: gains NOTHING (gain gated on the trash)", NoTrashNoGain),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task TrashAndGain()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(0);
    var card = await Place(ctx, ChoiceZone.BattleArea);

    // (이연③-d) drive the AS-IS inline ActivateClass (retires the invented TrashSelfThenGainMemoryDelayEffect cast).
    await Drive(ctx, CardEffectFactory.Gain2MemoryOptionDelayEffect(new CardSource(ctx, card, P1)));

    AssertTrue(!((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(card), "the Delay option was trashed (left the battle area)");
    AssertEqual(2, ctx.MemoryController.Current.Current, "gained 2 memory after the self-trash succeeded");
}

async Task NoTrashNoGain()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(0);
    // Card is in HAND, not on the battle area — there is no own permanent to trash.
    var card = await Place(ctx, ChoiceZone.Hand);

    // (이연③-d) the AS-IS CanDeclareOptionDelayEffect gate (on battle area) blocks activation from hand — nothing trashed, no gain.
    await Drive(ctx, CardEffectFactory.Gain2MemoryOptionDelayEffect(new CardSource(ctx, card, P1)));

    AssertEqual(0, ctx.MemoryController.Current.Current, "no memory gained when there was nothing to trash");
}

// --- Helpers ---

// (이연③-d) Drive the AS-IS [Main] <Delay> ActivateClass the way the live window does: under an ambient match
// scope, gate on CanTrigger (CanUse = CanDeclareOptionDelayEffect) then Activate (the self-trash + gain coroutine).
async Task Drive(EngineContext ctx, ICardEffect effect)
{
    using var scope = AmbientMatchContext.Enter(ctx);
    var ht = new System.Collections.Hashtable();
    if (effect is ActivateICardEffect ae && effect.CanTrigger(ht) && effect.CanActivate(ht))
    {
        await ae.Activate(ht);
    }
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 909);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (이연③-d) past Setup -> DoneStartGame true, so the ActivateClass CanTrigger/CanActivate window gate passes
    // (the AS-IS live-window drive now used instead of the retired direct ResolveAsync cast).
    ctx.TurnController.SetPhase(HeadlessDCGO.Engine.Headless.Runtime.HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, ChoiceZone zone)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("OPT"), "OPT", "DelayOption",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var id = new HeadlessEntityId("p1:opt:OPT");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("OPT"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
