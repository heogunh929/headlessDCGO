// D-1 (VR-8 / F1(b)): distinguish INDEPENDENT delete-processes in one drain by BATCH id.
//
// AS-IS: one DestroyPermanentsClass.Destroy() call == one logical deletion batch == one
// StackSkillInfos(OnDeletion / OnLeaveFieldAnyone) == a reactor fires ONCE — no matter how many cards that ONE
// call deletes. TWO independent Destroy() calls in the same pass == TWO stacks == the reactor fires TWICE.
//
// Pre-D1 headless collapsed OnLeaveFieldAnyone/OnDeletion per reactor across the WHOLE DrainPending pass, so two
// independent delete-processes whose CardMoved events drained together UNDER-FIRED (once, not twice). D-1 stamps a
// monotonic batch id (one per effect-resolution sink flush / one per battle finalize / one per DP-zero sweep) onto
// each deleted card's CardMoved event, and the window collapse now keys off (reactor, batch id). This test proves
// both directions: (a) two independent effect deletes => TWO fires (not under-fire), (b) one effect deleting THREE
// cards => ONE fire (not over-fire), (c) a two-loser battle (one Destroy) => ONE fire.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the uncapped OnLeaveFieldAnyone reactor (turn player)
HeadlessPlayerId P2 = new(2);   // the opponent whose Digimon are deleted

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) two INDEPENDENT effect deletes (one card each) => reactor fires TWICE (memory -2, not -1: no under-fire)", IndependentTwoProcesses),
    ("(b) one effect deleting THREE cards (one batch) => reactor fires ONCE (memory -1, not -3: no over-fire)", SingleProcessThreeCards),
    ("(c) a two-loser battle (one Destroy) => reactor fires ONCE (memory -1)", BattleTwoLosersOneBatch),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task IndependentTwoProcesses()
{
    EngineContext ctx = await Setup();
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");

    // TWO separate effect resolutions (two sink flushes) each delete ONE card => two distinct batch ids whose
    // CardMoved events co-drain in ONE window seed. Pre-D1 the (reactor)-keyed collapse fired the reactor ONCE
    // (-1, under-fire); D1's (reactor, batch id) key produces TWO mandatory triggers, ordered by the controller
    // (both resolve). The faithful drive answers that order choice; the observable is TWO fires (memory -2).
    await DeleteViaSink(ctx, d1);
    await DeleteViaSink(ctx, d2);
    await DriveToCompletion(ctx);

    AssertEqual(-2, ctx.MemoryController.Current.Current,
        "two independent delete-processes fired the uncapped reactor TWICE (memory -2); pre-D1 under-fired to -1");
}

async Task SingleProcessThreeCards()
{
    EngineContext ctx = await Setup();
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");
    var d3 = await Foe(ctx, "D3");

    // ONE effect resolution (one sink flush) deletes THREE cards => ONE batch id shared by all three => the
    // reactor collapses to ONE trigger, resolves without an order choice.
    await DeleteViaSink(ctx, d1, d2, d3);
    await DriveToCompletion(ctx);

    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "one N-card delete-process fired the reactor ONCE (memory -1); a per-card batch would over-fire to -3");
}

async Task BattleTwoLosersOneBatch()
{
    EngineContext ctx = await Setup();
    // Equal-DP battle: attacker (P1) and defender (P2) both die = two losers of ONE battle = ONE batch id.
    var attacker = await Combatant(ctx, P1, "ATK", suspended: false, dp: 6000);
    var defender = await Combatant(ctx, P2, "DEF", suspended: true, dp: 6000);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);
    AssertTrue(result.IsSuccess && result.AttackerDeleted && result.DefenderDeleted, "both battle participants deleted");
    await DriveToCompletion(ctx);

    AssertEqual(-1, ctx.MemoryController.Current.Current,
        "the two-loser battle (one Destroy) fired the reactor ONCE (memory -1)");
}

// Drive RunToStable to a fixpoint, answering any WindowChoice (order / optional) by selecting its first
// candidate through the REAL ResolveChoice action — the faithful agent drive for co-drained mandatory triggers.
async Task DriveToCompletion(EngineContext ctx)
{
    var processor = new MetadataActionProcessor();
    for (int guard = 0; guard < 100; guard++)
    {
        HeadlessChoiceState choice = ctx.ChoiceController.Current;
        if (choice.IsPending && choice.Type == ChoiceType.WindowChoice)
        {
            await processor.ProcessAsync(
                HeadlessActionFactory.ResolveChoice(choice.PlayerId!.Value, ChoiceResult.Select(choice.CandidateIds[0])), ctx);
            continue;
        }

        await new GameFlowProcessor().RunToStableAsync(ctx);
        if (!ctx.ChoiceController.Current.IsPending)
        {
            return;
        }
    }

    throw new Exception("window did not drive to completion within the guard bound");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 41);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (C2 harness phase fix) DoneStartGame gate: new-model CanTrigger/AutoProcessCheck need a live phase
    ctx.MemoryController.Set(0);
    var cards = (CardDatabase)ctx.CardRepository;
    // The uncapped [All Turns] OnLeaveFieldAnyone reactor: -1 memory per fire, NO once-per-turn cap so the
    // observable isolates the batch-collapse (a per-event fire would show -N).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnLeaveFieldCounter"), "TfxOnLeaveFieldCounter", "LeaveCounter",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var reactor = new HeadlessEntityId("1:battle:LEAVECTR");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId("TfxOnLeaveFieldCounter"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.RegisterEnteredCardEffects(reactor, P1);
    return ctx;
}

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> Combatant(EngineContext ctx, HeadlessPlayerId owner, string tag, bool suspended, int dp)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = suspended,
        [BattleResolver.DpKey] = dp,
    };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// One effect resolution (one sink flush) that deletes the given cards through the uniform DeleteKind path — the
// same seam a SelectAndDestroy effect body drives. All cards in ONE call share ONE lazily-allocated batch id.
async Task DeleteViaSink(EngineContext ctx, params HeadlessEntityId[] targets)
{
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.GameEventQueue, context: ctx);
    foreach (HeadlessEntityId target in targets)
    {
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            new HeadlessEntityId("effect:deleter"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    }

    await sink.FlushAsync();
}

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
