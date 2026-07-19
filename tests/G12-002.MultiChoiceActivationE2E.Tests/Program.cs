using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G12-002: an activation that needs TWO choices drives the deferred resume loop across two ResolveChoice
// rounds. Each ResolveChoice re-invokes the resolver, replaying prior answers; the option cost is paid
// ONCE and both selections apply once (commit-once / no re-pay). The TfxMultiSelect fixture's [Main]
// returns two select-and-destroy effects.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Two-choice activation: 2 ResolveChoice rounds, cost paid once, both targets deleted", MultiChoiceE2E),
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

async Task MultiChoiceE2E()
{
    // (4b B6) Pump re-pin (G12-004 pattern): CreatePumpDriven installs the LegalActionSetValidator boundary
    // by default; the OLD AdvancePhase reach-main preamble is retired onto the pump auto-flow drive below.
    var match = DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(deferredChoice: true), new EngineTrace());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    // The fixture Option whose [Main] needs TWO selects (delete first, delete second).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxMultiSelect"), "TfxMultiSelect", "TwoSelect",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 0 }, CardType: "Option", PlayCost: 2));
    var opt = new HeadlessEntityId("p1:hand:TFX");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(opt, new HeadlessEntityId("TfxMultiSelect"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, opt, ChoiceZone.None, ChoiceZone.Hand));

    // THREE foes: the AS-IS forced-selection shortcut (SelectPermanentEffect :529, exactly-max pool
    // auto-selects) would silently answer a single-candidate second select — with three staged foes BOTH
    // selects are genuine agent choices (select1 {A,B,C}, select2 {B,C}) and C is the survivor control.
    var a = await PlaceFoe(context, "A");
    var b = await PlaceFoe(context, "B");
    var c = await PlaceFoe(context, "C");

    // 1) Activate -> first choice pending, cost paid ONCE (5 -> 3).
    // (4b B6) Under the pump the ActivateOption action is queued as a MainPhaseAction packet and the pump
    // consumes it on its next step(s) — drive the bounded pump steps to the pending choice (the assertion
    // stays: the activation must SUSPEND on the first select).
    LegalAction activate;
    using (AmbientMatchContext.Enter(context))
    {
        activate = match.GetLegalActions(P1).Single(x => x.ActionType == HeadlessActionTypes.ActivateOption);
    }
    await env.StepAsync(activate);
    await DrivePumpUntilPendingAsync(match);
    AssertTrue(match.HasPendingChoice(), "first choice pending after activation");
    AssertEqual(3, context.MemoryController.Current.Current, "option cost (2) paid once: 5 -> 3");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, a) && InZone(context, P2, ChoiceZone.BattleArea, b) && InZone(context, P2, ChoiceZone.BattleArea, c),
        "nothing applied while the first select is pending: all three still on the battle area");

    // 2) Resolve first choice (select A) -> the first destroy applies IN PLACE (AS-IS: the parked pump
    // coroutine consumes the deposited answer — no record-replay), then the activation re-suspends for the
    // SECOND choice. (The OLD "nothing applied yet (commit-once)" claim was the OLD throw-record-replay
    // contract; the pump's in-place consume applies each select's destroy as it resolves, and commit-once
    // is asserted as: cost paid once + each destroy applied exactly once + the unchosen survivor lives.)
    await env.StepAsync(ResolveFor(match, a));
    await DrivePumpUntilPendingAsync(match);
    AssertTrue(match.HasPendingChoice(), "second choice pending after the first is resolved");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, a) && InZone(context, P2, ChoiceZone.Trash, a),
        "first chosen target deleted in place when its select resolved");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, b) && InZone(context, P2, ChoiceZone.BattleArea, c),
        "the second select is still pending: B and C untouched");
    AssertEqual(3, context.MemoryController.Current.Current, "memory NOT re-paid for the second select (still 3)");

    // 3) Resolve second choice (select B) -> activation completes; both deletes applied exactly once.
    await env.StepAsync(ResolveFor(match, b));
    await DrivePumpToQuietAsync(match);
    AssertTrue(!match.HasPendingChoice(), "no pending choice after the activation completes");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, a), "first target deleted");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, b), "second target deleted");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, a) && InZone(context, P2, ChoiceZone.Trash, b), "both targets in the trash");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, c), "the unchosen survivor control (C) was NOT deleted");
    AssertEqual(3, context.MemoryController.Current.Current, "memory NOT re-paid across the two choices (still 3)");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceFoe(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p2:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

LegalAction ResolveFor(DcgoMatch match, HeadlessEntityId target)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.ResolveChoice && x.Id.Value.Contains(target.Value, StringComparison.Ordinal));
}

// Bounded pump drain to the next pending choice (the activation packet resolves inside the pump auto-flow).
static async Task DrivePumpUntilPendingAsync(DcgoMatch match)
{
    for (int i = 0; i < 8 && !match.HasPendingChoice(); i++)
    {
        await StepOnceDriveAsync(match);
    }
}

// Bounded pump drain to a quiet (no-pending) state after the final resolve.
static async Task DrivePumpToQuietAsync(DcgoMatch match)
{
    for (int i = 0; i < 8 && match.HasPendingChoice(); i++)
    {
        await StepOnceDriveAsync(match);
    }
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3) =>
    new(playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Phase driving (pump auto-flow, G12-004 pattern, 4b B6) --------------
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; assertion strength unchanged.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
