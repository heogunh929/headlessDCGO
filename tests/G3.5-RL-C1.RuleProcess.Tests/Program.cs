using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
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

    // End P1's first turn (draw is skipped on the very first turn), then advance P2 into its draw.
    match.Context.TurnController.SetPhase(HeadlessPhase.End);
    await Apply(match, HeadlessActionFactory.EndTurn(P1));

    for (int i = 0; i < 10 && !match.IsTerminal(); i++)
    {
        HeadlessPlayerId? turnPlayer = match.GetObservation().Turn.TurnPlayerId;
        if (turnPlayer is not { } tp)
        {
            break;
        }

        LegalAction? advance = match.GetLegalActions(tp)
            .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        if (advance is null)
        {
            break;
        }

        await Apply(match, advance);
    }

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

    DcgoMatch match = new(context);
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

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await Apply(match, advance);
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static async Task Apply(DcgoMatch match, LegalAction action)
{
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
