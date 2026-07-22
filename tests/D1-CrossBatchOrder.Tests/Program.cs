// D-1 (adversarial-review P1 remediation): CROSS-batch deletion triggers process SEQUENTIALLY by batch-id, with
// NO cross-batch order choice; the order choice stays SAME-batch only.
//
// Background: D-1 stamped a batch id onto each deletion's CardMoved and made the OnDeletion/OnLeaveFieldAnyone
// collapse key off (reactor, batch id), so N cards of ONE Destroy() collapse to one fire while two INDEPENDENT
// Destroy()s fire the reactor twice. But when two independent batches' CardMoved events CO-DRAIN into one window
// seed (a RuleProcess sweep finalizing a declined would-be-deleted AND the DP-zero deaths, or two sink flushes
// queued before a drain), the reactor produced TWO simultaneous mandatory triggers, which the window offered to
// the agent as an ORDER CHOICE. AS-IS never does that: each DestroyPermanentsClass.Destroy() is its OWN
// StackSkillInfos window, resolved fully before the next Destroy — two independent processes are SEQUENTIAL, not a
// co-stacked order choice. (An order choice appears in AS-IS only when 2+ reactors co-stack WITHIN one Destroy's
// window — MultipleSkills, skillInfos_active.Count > 1.)
//
// This test proves:
//   (a) two INDEPENDENT batches co-drained => the window fires the reactor twice IN BATCH-ID ORDER with NO order
//       choice presented (zero WindowChoice prompts).
//   (b) ONE batch with TWO DISTINCT reacting cards => an order choice IS presented (same-batch order preserved).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the uncapped OnLeaveFieldAnyone reactor(s) (turn player)
HeadlessPlayerId P2 = new(2);   // the opponent whose Digimon are deleted

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) two INDEPENDENT batches co-drain => fire TWICE, batch-id order, NO cross-batch order choice", CrossBatchSequential),
    ("(b) ONE batch, TWO distinct reactors => an order choice IS presented (same-batch order preserved)", SameBatchOrderChoice),
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

async Task CrossBatchSequential()
{
    EngineContext ctx = await Setup(reactorCount: 1);
    var d1 = await Foe(ctx, "D1");
    var d2 = await Foe(ctx, "D2");

    // Two separate sink flushes => two distinct batch ids whose CardMoved (leave) events co-drain in ONE seed.
    await DeleteViaSink(ctx, d1);
    await DeleteViaSink(ctx, d2);
    int prompts = await DriveToCompletion(ctx);

    AssertEqual(-2, ctx.MemoryController.Current.Current,
        "the single reactor fired ONCE per batch (memory -2) — both independent batches resolved");
    AssertEqual(0, prompts,
        "cross-batch triggers resolved SEQUENTIALLY with NO order choice presented (0 WindowChoice prompts)");
}

async Task SameBatchOrderChoice()
{
    EngineContext ctx = await Setup(reactorCount: 2);   // TWO distinct reactor cards on P1's field
    var v = await Foe(ctx, "V");

    // ONE sink flush deletes ONE card => ONE batch id. BOTH reactors react to that single leave in the SAME batch:
    // two simultaneous mandatory triggers of the same batch => AS-IS MultipleSkills offers an order choice.
    await DeleteViaSink(ctx, v);
    int prompts = await DriveToCompletion(ctx);

    AssertEqual(-2, ctx.MemoryController.Current.Current,
        "both same-batch reactors fired (memory -2)");
    AssertTrue(prompts >= 1,
        "two distinct reactors in ONE batch presented an order choice (>=1 WindowChoice prompt) — same-batch order preserved");
}

// Drive RunToStable to a fixpoint, answering any WindowChoice (order / optional) by selecting its first candidate.
// Returns the NUMBER of WindowChoice prompts answered (the observable that distinguishes a cross-batch order
// choice — which must be ZERO — from a same-batch one).
async Task<int> DriveToCompletion(EngineContext ctx)
{
    int prompts = 0;
    var processor = new MetadataActionProcessor();
    for (int guard = 0; guard < 100; guard++)
    {
        HeadlessChoiceState choice = ctx.ChoiceController.Current;
        if (choice.IsPending && choice.Type == ChoiceType.WindowChoice)
        {
            prompts++;
            await processor.ProcessAsync(
                HeadlessActionFactory.ResolveChoice(choice.PlayerId!.Value, ChoiceResult.Select(choice.CandidateIds[0])), ctx);
            continue;
        }

        await new GameFlowProcessor().RunToStableAsync(ctx);
        if (!ctx.ChoiceController.Current.IsPending)
        {
            return prompts;
        }
    }

    throw new Exception("window did not drive to completion within the guard bound");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Setup(int reactorCount)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 41);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (C2 harness phase fix) DoneStartGame gate
    ctx.MemoryController.Set(0);
    var cards = (CardDatabase)ctx.CardRepository;
    // Uncapped [All Turns] OnLeaveFieldAnyone reactor: -1 memory per fire, NO once-per-turn cap so the observable
    // isolates the fire count (a per-event / duplicated fire would show extra).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnLeaveFieldCounter"), "TfxOnLeaveFieldCounter", "LeaveCounter",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    for (int i = 0; i < reactorCount; i++)
    {
        var reactor = new HeadlessEntityId($"1:battle:LEAVECTR{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId("TfxOnLeaveFieldCounter"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
        ctx.RegisterEnteredCardEffects(reactor, P1);
    }

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
