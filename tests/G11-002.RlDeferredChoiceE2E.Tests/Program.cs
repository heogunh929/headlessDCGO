using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G11-002: the activation full loop end-to-end through HeadlessRlEnvironment. Activating an Option whose
// [Main] needs a target select SUSPENDS (the agent gets a pending choice); a follow-up ResolveChoice
// RESUMES the activation and applies the effect — WITHOUT re-paying the option cost (commit-once).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Activate option -> pending choice (cost paid once) -> ResolveChoice resumes (no re-pay)", DeferredOptionE2E),
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

async Task DeferredOptionE2E()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(deferredChoice: true), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    // ST2_16 [Main] "Return 1 of your opponent's Digimon to its owner's hand" — an Option needing a select.
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST2_16"), "ST2_16", "OptionBounce",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 0 }, CardType: "Option", PlayCost: 3));
    var opt = new HeadlessEntityId("p1:hand:ST2_16");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(opt, new HeadlessEntityId("ST2_16"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, opt, ChoiceZone.None, ChoiceZone.Hand));

    cards.Upsert(new CardRecord(new HeadlessEntityId("FOE"), "FOE", "Greymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var foe = new HeadlessEntityId("p2:battle:FOE");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, new HeadlessEntityId("FOE"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));

    // 1) Activate the option. Its [Main] select suspends -> pending choice, cost paid ONCE (5 -> 2).
    LegalAction activate = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.ActivateOption);
    RlStepResult afterActivate = await env.StepAsync(activate);
    AssertTrue(afterActivate.HasPendingChoice, "activating the option suspended for a target choice");
    AssertEqual(2, context.MemoryController.Current.Current, "option cost (3) paid once: 5 -> 2");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, foe), "target still on the battle area before the choice resolves");

    // 2) Resolve the choice selecting the opponent Digimon -> activation resumes, bounce applies, NO re-pay.
    LegalAction resolve = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.Contains(foe.Value, StringComparison.Ordinal));
    RlStepResult afterResolve = await env.StepAsync(resolve);

    AssertTrue(!afterResolve.HasPendingChoice, "no pending choice after the activation resumes");
    AssertTrue(InZone(context, P2, ChoiceZone.Hand, foe), "opponent Digimon was bounced to its owner's hand");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, foe), "opponent Digimon left the battle area");
    AssertEqual(2, context.MemoryController.Current.Current, "memory NOT re-paid on resume (still 2)");
}

// --- Helpers -------------------------------------------------------------

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
}

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
