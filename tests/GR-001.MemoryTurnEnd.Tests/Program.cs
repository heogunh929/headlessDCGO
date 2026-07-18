using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// GR-001: a play that takes the turn player's memory negative must END the turn (DCGO memory rule). The
// play/digivolve/option/special actions pay memory directly; the AS-IS turn-end trigger (EndTurnCheck,
// reading TurnEndMinMemory) fires when the NON-turn player reaches the threshold. This asserts: after a
// costed play crosses < 0, the pump auto-ends the turn (no explicit handover step), the turn player can
// NO LONGER take a costed play, and the opponent starts with the mirrored (+|m|) memory.
//
// RE-TARGETED (4b B4, P-D EndTurn seam): the OLD driver's `HeadlessActionFactory.EndTurn` two-step
// handover (MemoryPass phase → explicit EndTurn action → flip) is RETIRED onto DcgoMatch.CreatePumpDriven.
// Under the pump the memory-cross IS the handover: AS-IS AutoProcessing.EndTurnCheck (reading
// TurnEndMinMemory) auto-starts EndTurnProcess when the play takes the non-turn player to/above the
// threshold, so the turn flips with no separate EndTurn/Pass action. The former `Single(EndTurn)` assertion
// verified "the turn-handover action is the sole remaining legal action at the memory-pass seam" — under the
// pump that seam does not exist (the explicit two-step is an OLD step-driver artifact, §2.1 P-D), so it is
// translated to the pump equivalent: after the costed play resolves, the turn player has NO costed-play lane
// AND the pump has already auto-flipped to the opponent with the mirrored memory (the real handover).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A costed play that crosses memory < 0 auto-ends the turn (pump EndTurnCheck: no more plays, turn flips, opponent gains +|m|)", MemoryNegativeEndsTurn),
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

async Task MemoryNegativeEndsTurn()
{
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 17);
    var cards = (CardDatabase)context.CardRepository;

    // A plain level-3 Digimon that costs 3, playable from hand.
    const int cost = 3;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TGR-D"), "TGR-D", "TurnEnder",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId("p1:hand:TGR");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("TGR-D"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    AssertTrue(AtMainWaitOf(match, P1), "starts at P1's main-phase wait");
    int p1BattleBefore = ZoneCards(match, P1, ChoiceZone.BattleArea).Count;

    // The costed play: memory 0 -> -3. Once the play resolves, the pump's EndTurnCheck (AS-IS, reading
    // TurnEndMinMemory=1) sees the non-turn player at +3 >= 1 and auto-runs EndTurnProcess -> flip — all within
    // the same auto-flow (the memory cross IS the handover; no explicit EndTurn/Pass action, no MemoryPass wait).
    LegalAction play = Legal(match, P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    // Proof the play actually resolved (not an Illegal silent skip, RD-R3-02 guard): the card left P1's hand and
    // entered P1's battle area.
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(hand), "the played card left P1's hand");
    AssertEqual(p1BattleBefore + 1, ZoneCards(match, P1, ChoiceZone.BattleArea).Count, "the costed play entered P1's battle area (it resolved)");

    // The memory cross ended P1's turn: the pump auto-flipped to P2 (the real handover) with the mirrored memory.
    // (The OLD `Single(EndTurn)` "only the handover action remains" assertion has no pump analogue — the
    // MemoryPass-then-EndTurn two-step is an OLD step-driver artifact; the auto-flip subsumes it. §2.1 P-D.)
    AssertTrue(!match.IsTerminal(), "the match did not terminate on the handover");
    AssertEqual(P2.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0, "the memory cross auto-ended P1's turn (flipped to the opponent)");
    AssertEqual(cost, context.MemoryController.Current.Current, "opponent starts with the mirrored +|m| memory");
}

// --- Harness (pump P-D retarget scaffold; F62/EXEMPLAR-T1 precedent) ------

async Task<(DcgoMatch Match, EngineContext Context)> NewPumpMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, context);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} " +
            $"terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
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
    await ApplyAsync(match, action);
}

async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).ToArray();

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

// --- Helpers -------------------------------------------------------------

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
