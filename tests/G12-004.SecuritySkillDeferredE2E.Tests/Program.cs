using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G12-004: a revealed [Security] card whose effect needs the agent's choice drives the deferred resume path.
// SecurityResolver resolves the SecuritySkill effect; the interactive (deferred) provider suspends it, so
// SecurityResolver records the suspended activation (DeferredActivations) and stops the check with the
// choice pending on the controller. The agent's ResolveChoice then resumes the effect through the real
// action pipeline (re-resolving the SecuritySkill, replaying the answer), deleting the chosen target exactly
// once. The TfxSecuritySelect fixture's [Security] returns one select-and-destroy.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Security skill needing a choice: suspends on reveal, ResolveChoice resumes and deletes the target", SecurityDeferredE2E),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task SecurityDeferredE2E()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(deferredChoice: true), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    CardDatabase cards = (CardDatabase)context.CardRepository;

    // Clear the dealt security stack so our fixture card is the one revealed (security[0]).
    foreach (var dealt in ((IZoneStateReader)context.ZoneMover).GetCards(P2, ChoiceZone.Security).ToArray())
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, dealt, ChoiceZone.Security, ChoiceZone.None));

    // P2 owns a [Security] Option whose security skill deletes one opponent Digimon (a choice).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxSecuritySelect"), "TfxSecuritySelect", "SecuritySelect",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 0 }, CardType: "Option"));
    var sec = new HeadlessEntityId("p2:security:TFXSEC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(sec, new HeadlessEntityId("TfxSecuritySelect"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, sec, ChoiceZone.None, ChoiceZone.Security));

    // The attacker / select target: a P1 Digimon (the opponent of the security player P2).
    var target = await PlaceFoe(context, "ATK");

    // 1) Run the security check. The revealed [Security] effect asks P2 to choose -> the deferred provider
    //    suspends it; SecurityResolver records the activation and stops with the choice pending.
    await new SecurityResolver().RunSecurityCheckLoopAsync(
        context, (IZoneStateReader)context.ZoneMover, P1, target, P2, strike: 1);

    AssertTrue(context.ChoiceController.Current.IsPending, "a choice is pending after the security skill suspended");
    AssertTrue(context.DeferredActivations.HasPending, "the suspended security activation is recorded");
    AssertEqual(EffectTiming.SecuritySkill, context.DeferredActivations.Pending!.Timing, "the suspended activation is the SecuritySkill timing");
    AssertEqual(sec.Value, context.DeferredActivations.Pending!.CardId.Value, "the suspended activation is the revealed security card");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, target), "nothing applied yet (commit-once): the target is still on the battle area");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, sec), "the security card was revealed exactly once (now in the trash)");

    // 2) The security player (P2) resolves the choice -> the activation resumes through the action pipeline,
    //    re-resolving the SecuritySkill (replaying the answer) and deleting the chosen target.
    LegalAction resolve = match.GetLegalActions(P2)
        .Single(x => x.ActionType == HeadlessActionTypes.ResolveChoice && x.Id.Value.Contains(target.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(resolve);
    await match.StepAsync();

    AssertTrue(!context.ChoiceController.Current.IsPending, "no pending choice after the security skill resumes");
    AssertTrue(!context.DeferredActivations.HasPending, "the suspended activation was cleared after it resumed");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, target), "the chosen target was deleted");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, target), "the chosen target is in the trash");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceFoe(EngineContext context, string tag)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
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
