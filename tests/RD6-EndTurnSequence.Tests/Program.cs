using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RD6 (A-2): the [End of Your Turn] effect window must resolve in the ENDING player's still-live frame, BEFORE
// the turn flip (AS-IS AutoProcessing.EndTurnProcess:699, "step 3" — before the attack loop and the threshold
// re-check at :714). The live bug: headless emitted OnEndTurn AFTER the flip, so a [End of Your Turn] body that
// gates on `TurnPlayerId == Owner` (BT1_021 EoTLose3Memory, TriggeredGainMemoryEffect) NO-OPPED entirely in the
// new turn player's frame — its memory change was silently dropped. This asserts the effect now fires pre-flip
// against the correct owner, so the opponent inherits the mirrored (+|memory - 3|) memory.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[End of Your Turn] lose-3-memory fires in the ENDING player's frame (pre-flip), not no-op post-flip", EoTFiresPreFlip),
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

async Task EoTFiresPreFlip()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // BT1_021 (Red) carries "[End of Your Turn] Lose 3 memory." — place it in P1's battle area and register its
    // effects (the enter-play hook binds the OnEndTurn TriggeredGainMemoryEffect into the scheduler registry).
    cards.Upsert(new CardRecord(new HeadlessEntityId("BT1_021"), "BT1_021", "EoTLoser",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var eot = new HeadlessEntityId("p1:field:BT1_021");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(eot, new HeadlessEntityId("BT1_021"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, eot, ChoiceZone.None, ChoiceZone.BattleArea));
    context.RegisterEnteredCardEffects(eot, P1);

    // A plain level-3 Digimon that costs 3 — playing it crosses memory 0 -> -3, ending P1's turn (MemoryPass).
    const int cost = 3;
    cards.Upsert(new CardRecord(new HeadlessEntityId("VAN-D"), "VAN-D", "Vanilla",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId("p1:hand:VAN");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VAN-D"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);

    AssertEqual(-cost, context.MemoryController.Current.Current, "the costed play took memory to -3");
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "memory < 0 put P1 into MemoryPass (turn ending)");

    // Hand over the turn. If the [End of Your Turn] window fires PRE-flip (the fix), BT1_021 loses 3 more memory
    // in P1's frame (-3 -> -6), so P2 inherits the mirrored +6. If it no-ops post-flip (the bug), P2 gets only +3.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await env.StepAsync(endTurn);

    AssertEqual(P2.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn handed over to P2");
    AssertEqual(cost + 3, context.MemoryController.Current.Current,
        "P2 inherits +6: the pre-flip [End of Your Turn] lose-3 fired in P1's frame (bug would leave +3)");
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

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).First(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
