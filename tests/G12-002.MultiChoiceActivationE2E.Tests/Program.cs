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
    var match = new DcgoMatch(EngineContext.CreateDefault(deferredChoice: true), new EngineTrace(), actionLegality: new LegalActionSetValidator());
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

    var a = await PlaceFoe(context, "A");
    var b = await PlaceFoe(context, "B");

    // 1) Activate -> first choice pending, cost paid ONCE (5 -> 3).
    LegalAction activate = match.GetLegalActions(P1).Single(x => x.ActionType == HeadlessActionTypes.ActivateOption);
    RlStepResult r1 = await env.StepAsync(activate);
    AssertTrue(r1.HasPendingChoice, "first choice pending after activation");
    AssertEqual(3, context.MemoryController.Current.Current, "option cost (2) paid once: 5 -> 3");

    // 2) Resolve first choice (select A) -> activation re-suspends for the SECOND choice.
    RlStepResult r2 = await env.StepAsync(ResolveFor(match, a));
    AssertTrue(r2.HasPendingChoice, "second choice pending after the first is resolved");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, a) && InZone(context, P2, ChoiceZone.BattleArea, b),
        "nothing applied yet (commit-once): both still on the battle area");

    // 3) Resolve second choice (select B) -> activation completes; both deletes apply once.
    RlStepResult r3 = await env.StepAsync(ResolveFor(match, b));
    AssertTrue(!r3.HasPendingChoice, "no pending choice after the activation completes");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, a), "first target deleted");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, b), "second target deleted");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, a) && InZone(context, P2, ChoiceZone.Trash, b), "both targets in the trash");
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

LegalAction ResolveFor(DcgoMatch match, HeadlessEntityId target) => match.GetLegalActions(P1)
    .Single(x => x.ActionType == HeadlessActionTypes.ResolveChoice && x.Id.Value.Contains(target.Value, StringComparison.Ordinal));

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

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).First(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }
}

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
