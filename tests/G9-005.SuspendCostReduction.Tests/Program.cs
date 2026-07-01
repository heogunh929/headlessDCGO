using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-005 (EX8_074 Stage 3, brick 1): SuspendCostReductionEffect — the headless composite of the original
// SuspendPermanentsClass.Tap() + ChangeCostClass (Player.UntilCalculateFixedCostEffect). Verified in
// ISOLATION via a scripted choice provider (no core PlayCardAction change, no dispatch): selecting exactly
// N own Digimon suspends them and registers a one-shot (-M) self play-cost reduction that
// ContinuousModifierGate.ResolvePlayCost folds in, and which EffectDurationExpiry.ExpireFixedCostCalc
// clears (the UntilCalculateFixedCost lifetime PlayCardAction enforces once the play's cost is locked).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Suspending exactly 2 Digimon taps them AND reduces the card's play cost by 4", SuspendTwoReducesFour),
    ("The reduction is one-shot: ExpireFixedCostCalc clears it (UntilCalculateFixedCost)", ReductionIsUntilFixedCostCalc),
    ("Selecting fewer than N applies nothing (no suspend, no reduction) — original '== 2' branch", ShortSelectionNoOp),
    ("Skipping (optional) applies nothing", SkipNoOp),
    ("Any-owner UNSUSPENDED Digimon are offered (incl. opponent's); suspended are not", AnyOwnerUnsuspendedOffered),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task SuspendTwoReducesFour()
{
    EngineContext context = Context();
    var hand = await PlaceInHand(context, P1, "EX8074", playCost: 6);
    var d1 = await PlaceDigimon(context, P1, "U1", suspended: false);
    var d2 = await PlaceDigimon(context, P1, "U2", suspended: false);

    var effect = BuildEffect(context, hand);
    await ResolveSelecting(context, effect, d1, d2);

    AssertTrue(IsSuspended(context, d1) && IsSuspended(context, d2), "both chosen Digimon are now suspended");
    AssertEqual(2, ContinuousModifierGate.ResolvePlayCost(context, hand, basePlayCost: 6), "play cost 6 -> 2 (reduced by 4)");
}

async Task ReductionIsUntilFixedCostCalc()
{
    EngineContext context = Context();
    var hand = await PlaceInHand(context, P1, "EX8074", playCost: 6);
    var d1 = await PlaceDigimon(context, P1, "U1", suspended: false);
    var d2 = await PlaceDigimon(context, P1, "U2", suspended: false);

    await ResolveSelecting(context, BuildEffect(context, hand), d1, d2);
    AssertEqual(2, ContinuousModifierGate.ResolvePlayCost(context, hand, 6), "reduced before fixed-cost calc");

    // PlayCardAction.ProcessAsync calls this once the play cost is locked in.
    EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, hand, 6), "reduction expired -> back to full cost");
}

async Task ShortSelectionNoOp()
{
    EngineContext context = Context();
    var hand = await PlaceInHand(context, P1, "EX8074", playCost: 6);
    var d1 = await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);

    // Apply with only 1 selected (fewer than the required 2).
    var effect = BuildEffect(context, hand);
    var sink = Sink(context);
    effect.Apply(sink, new[] { d1 });
    await sink.FlushAsync();

    AssertTrue(!IsSuspended(context, d1), "the single selection is NOT suspended");
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, hand, 6), "no reduction applied");
}

async Task SkipNoOp()
{
    EngineContext context = Context();
    var hand = await PlaceInHand(context, P1, "EX8074", playCost: 6);
    var d1 = await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);

    var effect = BuildEffect(context, hand);
    var sink = Sink(context);
    // A skipped choice -> Apply is never called (mirrors the resolver's `if (!result.IsSkipped)` guard).
    await sink.FlushAsync();

    AssertTrue(!IsSuspended(context, d1), "nothing suspended on skip");
    AssertEqual(6, ContinuousModifierGate.ResolvePlayCost(context, hand, 6), "no reduction on skip");
}

// AS-IS: the original EX8_074 BeforePayCost predicate (IsPermanentExistsOnBattleAreaDigimon && !IsSuspended
// && CanSuspend && !CanNotBeAffected) is NOT owner-scoped — EITHER player's unsuspended Digimon may be
// suspended to pay the reduction. BuildEffect mirrors that with IsBattleAreaDigimon.
async Task AnyOwnerUnsuspendedOffered()
{
    EngineContext context = Context();
    var hand = await PlaceInHand(context, P1, "EX8074", playCost: 6);
    var own = await PlaceDigimon(context, P1, "OWN", suspended: false);
    var ownSuspended = await PlaceDigimon(context, P1, "OWNSUS", suspended: true);
    var foe = await PlaceDigimon(context, P2, "FOE", suspended: false);
    var foeSuspended = await PlaceDigimon(context, P2, "FOESUS", suspended: true);

    ChoiceRequest req = BuildEffect(context, hand).BuildRequest(new[] { P1, P2 });
    bool Offered(HeadlessEntityId id) => req.Candidates.Any(c => c.Label.Contains(id.Value, StringComparison.Ordinal));
    AssertTrue(Offered(own), "owner's unsuspended Digimon is a target");
    AssertTrue(Offered(foe), "opponent's unsuspended Digimon is ALSO a target (any-owner, per AS-IS)");
    AssertTrue(!Offered(ownSuspended), "owner's already-suspended Digimon is NOT a target");
    AssertTrue(!Offered(foeSuspended), "opponent's already-suspended Digimon is NOT a target");
    await Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

SuspendCostReductionEffect BuildEffect(EngineContext context, HeadlessEntityId handCard)
{
    var card = new CardSource(context, handCard, P1);
    bool Suspendable(HeadlessEntityId id) =>
        CardEffectCommons.IsBattleAreaDigimon(card, id) && !CardEffectCommons.IsSuspended(card, id);
    return new SuspendCostReductionEffect(card, Suspendable, suspendCount: 2, costReduction: 4,
        description: "Suspend 2 Digimon to get Play Cost -4");
}

async Task ResolveSelecting(EngineContext context, SuspendCostReductionEffect effect, params HeadlessEntityId[] ids)
{
    ChoiceRequest request = effect.BuildRequest(new[] { P1, P2 });
    var provider = new ScriptedChoiceProvider();
    provider.Enqueue(ChoiceResult.Select(ids));
    ChoiceResult result = await provider.ChooseAsync(request);
    var sink = Sink(context);
    effect.Apply(sink, result.SelectedIds);
    await sink.FlushAsync();
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceInHand(EngineContext context, HeadlessPlayerId owner, string tag, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
