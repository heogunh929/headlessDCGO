using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-C1: common rule processing (state-based actions).
//  - Deck-out is now a LOSS for the decking player (correct winner verdict, not a draw).
//  - RuleProcess sweeps cards flagged for deletion off the field into the trash.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId FieldCard = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Deck-out marks the decking player as the loser", DeckOutMarksLoser),
    ("Deck-out produces a terminal verdict with the opponent as winner", DeckOutWinnerIsOpponent),
    ("RuleProcess sweeps a pending-deletion card off the field to trash", RuleProcessSweepsDeletion),
    ("RuleProcess leaves un-flagged field cards untouched", RuleProcessLeavesNormalCards),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Deck-out ------------------------------------------------------------

async Task DeckOutMarksLoser()
{
    DcgoMatch match = await DriveToDeckOutAsync();

    AssertTrue(match.IsTerminal(), "deck-out is terminal");
    AssertTrue(match.Context.PlayerStatusController.IsLose(P2), "decking player (P2) marked lose");
    AssertFalse(match.Context.PlayerStatusController.IsLose(P1), "opponent (P1) not marked lose");
}

async Task DeckOutWinnerIsOpponent()
{
    DcgoMatch match = await DriveToDeckOutAsync();

    MatchResult result = match.GetResult();
    AssertFalse(result.IsDraw, "deck-out is a loss, not a draw");
    AssertEqual(P1, result.WinnerId, "winner is the non-decking player");
}

// --- RuleProcess deletion sweep -----------------------------------------

async Task RuleProcessSweepsDeletion()
{
    DcgoMatch match = await CreateFieldMatchAsync();
    SetMetadata(match, FieldCard, new Dictionary<string, object?> { [GameFlowProcessor.PendingDeletionKey] = true });

    await match.StepAsync(); // empty queue -> RunToStable -> RuleProcess sweeps the flagged card

    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, FieldCard), "flagged card left the battle area");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, FieldCard), "flagged card moved to trash");

    match.Context.CardInstanceRepository.TryGetInstance(FieldCard, out CardInstanceRecord? record);
    AssertEqual(false, record!.Metadata[GameFlowProcessor.PendingDeletionKey], "pending-deletion flag cleared");
}

async Task RuleProcessLeavesNormalCards()
{
    DcgoMatch match = await CreateFieldMatchAsync();

    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, FieldCard), "un-flagged card stays on the field");
}

// --- Harness -------------------------------------------------------------

// mainDeckCount 10 -> 5 hand + 5 security + 0 library, so the player decks out on their first real draw.
async Task<DcgoMatch> DriveToDeckOutAsync()
{
    DcgoMatch match = await CreateMatchAsync(mainDeckCount: 10);

    // mainDeckCount 10 -> pump StartGame deals 5 hand + 5 security, leaving 0 library. P1's first turn
    // skips its draw; P1 passes, the pump flips to P2, and P2's first real draw exhausts the empty
    // library -> deck-out. (OLD SetPhase(End)/EndTurn + AdvancePhase step-loop retired; P-D Pass seam.)
    await AdvanceToMainAsync(match);
    await PassAsync(match, P1);
    await DriveUntilAsync(match, m => m.IsTerminal());

    AssertTrue(match.IsTerminal(), "reached deck-out terminal");
    return match;
}

async Task<DcgoMatch> CreateFieldMatchAsync()
{
    DcgoMatch match = await CreateMatchAsync(mainDeckCount: 12);
    await AdvanceToMainAsync(match);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, FieldCard, ChoiceZone.Hand, ChoiceZone.BattleArea));
    return match;
}

async Task<DcgoMatch> CreateMatchAsync(int mainDeckCount)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= mainDeckCount; index++)
    {
        cards.Upsert(CreateDigimon($"P1-M{index:D2}"));
        cards.Upsert(CreateDigimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1", mainDeckCount), BuildDeck(P2, "P2", mainDeckCount) },
        firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    return match;
}

static CardRecord CreateDigimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount) =>
    new(playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Phase driving (pump auto-flow, F62/C1-Witness precedent, 4b B3 RL re-aim) ---
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to P1's main wait; the OLD AdvancePhase
// step currency is retired (4b B6 gate). Breeding/Mulligan decisions are declined.
async Task AdvanceToMainAsync(DcgoMatch match)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
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

// Pass the turn player's Main action; the pump owns EndTurnCheck + the turn flip (P-D EndTurn seam:
// the OLD SetPhase(End)/EndTurn action + AdvancePhase step-loop cadence is retired).
static async Task PassAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction? pass;
    using (AmbientMatchContext.Enter(match.Context))
    {
        pass = match.GetLegalActions(player).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.Pass);
    }
    if (pass is null) throw new InvalidOperationException("no Pass lane at the main wait");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(pass);
    await match.StepAsync();
}

static async Task Apply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
