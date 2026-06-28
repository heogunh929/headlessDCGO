// C-12 Iceclad — battle-comparison keyword. AS-IS CardController.CompareStats: when EITHER combatant
// has Iceclad the battle is decided by digivolution-source count (DigivolutionCards.Count) instead of
// DP; otherwise by DP. Engine consumption is in BattleResolver.CompareBattleStats; the grant maps
// GrantIceclad -> hasIceclad (MatchStateMutationSink.KindToFlag). No new subsystem — a comparison branch
// plus a source count. This suite drives a real battle and asserts who is deleted under each branch,
// including the regression that with no Iceclad source counts are ignored (DP still governs).
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Iceclad attacker with more sources beats a higher-DP defender", IcecladAttackerWinsBySources),
    ("Iceclad defender with more sources beats a higher-DP attacker", IcecladDefenderWinsBySources),
    ("Iceclad with equal source counts deletes both (tie ignores DP)", IcecladEqualSourcesTie),
    ("Iceclad attacker with fewer sources loses despite higher DP", IcecladFewerSourcesLoses),
    ("No Iceclad: source counts are ignored and DP governs", NoIcecladUsesDp),
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

// Attacker: low DP but 3 sources + Iceclad. Defender: high DP, 1 source. By DP the attacker loses;
// Iceclad switches the comparison to sources (3 > 1) so the defender is deleted instead.
async Task IcecladAttackerWinsBySources()
{
    BattleResolutionResult result = await ResolveBattle(
        attackerDp: 1000, attackerSources: 3, attackerIceclad: true,
        targetDp: 9000, targetSources: 1, targetIceclad: false);

    AssertFalse(result.AttackerDeleted, "Iceclad attacker survives on source count");
    AssertTrue(result.DefenderDeleted, "defender is deleted (fewer sources)");
}

// Either combatant having Iceclad triggers the source comparison: here the defender carries it.
async Task IcecladDefenderWinsBySources()
{
    BattleResolutionResult result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 1, attackerIceclad: false,
        targetDp: 1000, targetSources: 3, targetIceclad: true);

    AssertTrue(result.AttackerDeleted, "attacker is deleted (fewer sources)");
    AssertFalse(result.DefenderDeleted, "Iceclad defender survives on source count");
}

// Equal sources => clamp(0) tie => both deleted, even though DP differs (DP is not consulted).
async Task IcecladEqualSourcesTie()
{
    BattleResolutionResult result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 2, attackerIceclad: true,
        targetDp: 1000, targetSources: 2, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "tie deletes the attacker");
    AssertTrue(result.DefenderDeleted, "tie deletes the defender");
}

// Iceclad does not guarantee a win — fewer sources still loses regardless of DP advantage.
async Task IcecladFewerSourcesLoses()
{
    BattleResolutionResult result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 0, attackerIceclad: true,
        targetDp: 1000, targetSources: 2, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "Iceclad attacker with fewer sources is deleted");
    AssertFalse(result.DefenderDeleted, "defender with more sources survives");
}

// Regression: with neither combatant Iceclad, source counts are irrelevant and DP decides.
async Task NoIcecladUsesDp()
{
    BattleResolutionResult result = await ResolveBattle(
        attackerDp: 1000, attackerSources: 5, attackerIceclad: false,
        targetDp: 9000, targetSources: 0, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "no Iceclad: lower-DP attacker is deleted (sources ignored)");
    AssertFalse(result.DefenderDeleted, "higher-DP defender survives");
}

// --- Harness -------------------------------------------------------------

async Task<BattleResolutionResult> ResolveBattle(
    int attackerDp, int attackerSources, bool attackerIceclad,
    int targetDp, int targetSources, bool targetIceclad)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(Player, "P1"), Deck(Opponent, "P2") },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, Player);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, BattleMetadata(attackerDp, attackerSources, attackerIceclad, suspended: false));
    SetMetadata(match, TargetId, BattleMetadata(targetDp, targetSources, targetIceclad, suspended: true));
    // A blocker keeps the block window open after the attack is declared, so the attack stays pending for
    // the manual BattleResolver.ResolveAsync (mirrors G2G-003); the block itself is never selected.
    SetMetadata(match, BlockerId, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = false,
        [BattleResolver.DpKey] = 5000,
        [BlockTiming.HasBlockerKey] = true,
    });

    LegalAction declare = match.GetLegalActions(Player).Single(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        (a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) ? raw?.ToString() : null) == TargetId.Value);
    await match.ApplyActionAsync(declare);
    await match.StepAsync();

    return await new BattleResolver().ResolveAsync(match.Context);
}

Dictionary<string, object?> BattleMetadata(int dp, int sources, bool iceclad, bool suspended)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = suspended,
        [BattleResolver.DpKey] = dp,
        [BattleResolver.SourceIdsKey] = Enumerable.Range(1, sources).Select(i => $"src{i:D2}").ToArray(),
    };
    if (iceclad)
    {
        metadata[BattleResolver.HasIcecladKey] = true;
    }

    return metadata;
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
