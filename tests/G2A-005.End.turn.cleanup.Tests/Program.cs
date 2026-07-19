using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G2A-005 — end-turn cleanup scoping matrix.
//
// (4b B6 re-pin) The OLD step driver's EndTurn ACTION (MetadataActionProcessor.EndTurnAsync) is retired;
// `HeadlessEndTurnCleanupFlow` itself is RETAINED SUBSTRATE — the pump's mirror turn end calls the SAME
// flow (TurnStateMachine EndPhase :670), so the cleanup rules survive verbatim as direct substrate-unit
// assertions (C-Del retained-substrate precedent): the OLD action-metadata reads become the flow's own
// EndTurnCleanupResult reads (same observables: Applied / RemovedKeys / CleanedCardIds / ResetAttackCount).
//
// Retired with their verification target (4b B6 disposition table):
//   - goal-row CSV metadata + AS-IS source sniff + TODO sniff (invented test-infra assertions, F62 precedent)
//   - "Memory pass end turn also applies cleanup" — the OLD MemoryPass-phase EndTurn seam is retired; under
//     the pump there is ONE unconditional cleanup seat (TurnStateMachine EndPhase :670, covered by
//     R4P2a-PhaseBodies EndResetList), so the memory-pass variant has no distinct rule left to test.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("End turn cleanup removes turn scoped field metadata", EndTurnCleanupRemovesTurnScopedFieldMetadata),
    ("End turn cleanup resets attack count and pending attack state", EndTurnCleanupResetsAttackState),
    ("End turn cleanup preserves persistent and out of scope metadata", EndTurnCleanupPreservesPersistentMetadata),
    ("End turn cleanup keeps hand card turn metadata untouched", EndTurnCleanupKeepsHandCardMetadataUntouched),
    ("Turn scoped metadata remains before end turn", TurnScopedMetadataRemainsBeforeEndTurn),
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
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

async Task EndTurnCleanupRemovesTurnScopedFieldMetadata()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AddBattleCardAsync(match, P1, "turn-card", P1, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "all",
        ["untilOwnerTurnEndEffects"] = "owner",
        ["oncePerTurnUsed"] = true,
        ["persistentKeyword"] = "Blocker"
    });
    await AddBattleCardAsync(match, P2, "opponent-card", P2, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "all",
        ["untilOpponentTurnEndEffects"] = "opponent",
        ["persistentKeyword"] = "Reboot"
    });

    EndTurnCleanupResult cleanup = RunCleanup(match);

    AssertTrue(cleanup.Applied, "cleanup applied");
    AssertEqual(5, cleanup.RemovedKeys.Count, "removed key count");
    AssertStringSet(
        new[] { "turn-card", "opponent-card" },
        cleanup.CleanedCardIds.ToArray(),
        "cleaned card ids");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("untilEachTurnEndEffects"), "turn each effect removed");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("untilOwnerTurnEndEffects"), "owner effect removed");
    AssertFalse(CardMetadata(match, "turn-card").ContainsKey("oncePerTurnUsed"), "once flag removed");
    AssertFalse(CardMetadata(match, "opponent-card").ContainsKey("untilOpponentTurnEndEffects"), "opponent effect removed");
    AssertEqual("Blocker", CardMetadata(match, "turn-card")["persistentKeyword"], "persistent keyword");
    AssertEqual("Reboot", CardMetadata(match, "opponent-card")["persistentKeyword"], "opponent persistent keyword");
}

async Task EndTurnCleanupResetsAttackState()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AddBattleCardAsync(match, P1, "attacker", P1, new Dictionary<string, object?>());

    // Establish a pending attack directly on the controller (isolates the end-turn attack-state reset from
    // the common loop's auto-advance, as before).
    match.Context.AttackController.DeclareAttack(P1, new HeadlessEntityId("attacker"), P2);

    EndTurnCleanupResult cleanup = RunCleanup(match);

    AssertEqual(1, cleanup.ResetAttackCount, "reset attack count");
    AssertEqual(0, match.Context.AttackController.Current.AttackCount, "attack count");
    AssertFalse(match.Context.AttackController.Current.IsPending, "attack pending");
    AssertFalse(match.Context.AttackController.Current.IsResolved, "attack resolved");
}

async Task EndTurnCleanupPreservesPersistentMetadata()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AddBattleCardAsync(match, P1, "persistent-card", P1, new Dictionary<string, object?>
    {
        ["isSuspended"] = true,
        ["persistentKeyword"] = "SecurityAttackPlus",
        ["untilEndTurnEffects"] = "temporary"
    });

    RunCleanup(match);
    IReadOnlyDictionary<string, object?> metadata = CardMetadata(match, "persistent-card");

    AssertEqual(true, metadata["isSuspended"], "suspended preserved");
    AssertEqual("SecurityAttackPlus", metadata["persistentKeyword"], "persistent keyword preserved");
    AssertFalse(metadata.ContainsKey("untilEndTurnEffects"), "turn scoped effect removed");
}

async Task EndTurnCleanupKeepsHandCardMetadataUntouched()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AddHandCardAsync(match, P1, "hand-card", P1, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "hand-selection-marker",
        ["persistentKeyword"] = "HandOnly"
    });

    EndTurnCleanupResult cleanup = RunCleanup(match);

    AssertEqual(0, cleanup.CleanedCardIds.Count, "cleaned card count");
    AssertEqual("hand-selection-marker", CardMetadata(match, "hand-card")["untilEachTurnEndEffects"], "hand metadata remains");
    AssertEqual("HandOnly", CardMetadata(match, "hand-card")["persistentKeyword"], "hand persistent metadata");
}

async Task TurnScopedMetadataRemainsBeforeEndTurn()
{
    DcgoMatch match = await CreateInitializedMatchAsync();
    await AddBattleCardAsync(match, P1, "pre-end-card", P1, new Dictionary<string, object?>
    {
        ["untilEachTurnEndEffects"] = "still-active"
    });

    // No cleanup call: the turn-scoped marker must survive until the turn actually ends.
    AssertEqual("still-active", CardMetadata(match, "pre-end-card")["untilEachTurnEndEffects"], "metadata before end turn");
}

static EndTurnCleanupResult RunCleanup(DcgoMatch match)
{
    // The retained substrate seat: the SAME flow the pump's mirror turn end drives (TurnStateMachine
    // EndPhase :670), called with the ending player's turn state.
    return new HeadlessEndTurnCleanupFlow().Cleanup(match.Context, match.Context.TurnController.Current);
}

static async Task<DcgoMatch> CreateInitializedMatchAsync(int mainDeckCount = 12)
{
    DcgoMatch match = new();
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1", mainDeckCount), BuildDeck(new HeadlessPlayerId(2), "P2", mainDeckCount) },
        firstPlayerId: new HeadlessPlayerId(1));
    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 17, setup: setup));
    return match;
}

static PlayerDeckSetup BuildDeck(
    HeadlessPlayerId playerId,
    string prefix,
    int mainCount = 12,
    int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount)
            .Select(index => new HeadlessEntityId($"{prefix}-M{index:D2}"))
            .ToArray(),
        Enumerable.Range(1, digitamaCount)
            .Select(index => new HeadlessEntityId($"{prefix}-D{index:D2}"))
            .ToArray());
}

static async Task AddBattleCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    IReadOnlyDictionary<string, object?> metadata)
{
    await AddCardAsync(match, zonePlayer, cardId, owner, ChoiceZone.BattleArea, metadata);
}

static async Task AddHandCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    IReadOnlyDictionary<string, object?> metadata)
{
    await AddCardAsync(match, zonePlayer, cardId, owner, ChoiceZone.Hand, metadata);
}

static async Task AddCardAsync(
    DcgoMatch match,
    HeadlessPlayerId zonePlayer,
    string cardId,
    HeadlessPlayerId owner,
    ChoiceZone zone,
    IReadOnlyDictionary<string, object?> metadata)
{
    HeadlessEntityId id = new(cardId);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(
        id,
        new HeadlessEntityId($"{cardId}-def"),
        owner,
        Metadata: metadata));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(zonePlayer, id, ChoiceZone.None, zone));
}

static IReadOnlyDictionary<string, object?> CardMetadata(DcgoMatch match, string cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(cardId), out CardInstanceRecord? record) ||
        record is null)
    {
        throw new InvalidOperationException($"Card instance '{cardId}' was not found.");
    }

    return record.Metadata;
}

static void AssertStringSet(string[] expected, string[] actual, string label)
{
    var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
    var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
    if (!expectedSet.SetEquals(actualSet))
    {
        throw new InvalidOperationException(
            $"{label}: expected {{{string.Join(", ", expectedSet)}}}, actual {{{string.Join(", ", actualSet)}}}.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
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
