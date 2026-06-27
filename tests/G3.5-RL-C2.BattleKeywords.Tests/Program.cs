using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-C2: battle keywords on top of the B1 DP model — PreventBattleDeletion, Jamming, Piercing.
// Builds on the integrated attack pipeline (declare -> block -> combat -> resolve).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Plain battle deletes the loser only (regression baseline)", PlainBattleDeletesLoser),
    ("PreventBattleDeletion: a losing defender survives", PreventBattleDeletionSurvives),
    ("Jamming flag does NOT cause mutual deletion (correct semantics)", JammingIsNotMutualDeletion),
    ("Piercing: a winning attacker also checks the defender's security", PiercingChecksSecurity),
    ("Piercing with strike 2 checks two security cards", PiercingStrikeChecksTwo),
    ("No piercing leaves the defender's security untouched", NoPiercingNoSecurityCheck),
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

async Task PlainBattleDeletesLoser()
{
    DcgoMatch match = await BattleAsync(attackerDp: 5000, targetDp: 3000);
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, TargetId), "defender (lower DP) deleted");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "attacker survives");
}

async Task PreventBattleDeletionSurvives()
{
    DcgoMatch match = await BattleAsync(
        attackerDp: 5000, targetDp: 3000,
        targetFlags: new Dictionary<string, object?> { [BattleResolver.PreventBattleDeletionKey] = true });

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, TargetId), "flagged defender survives a losing battle");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, TargetId), "flagged defender not trashed");
}

async Task JammingIsNotMutualDeletion()
{
    // Correct Jamming protects the ATTACKER only versus a Security Digimon (not modeled yet), so a
    // "hasJamming" flag must NOT turn a normal lost battle into a mutual deletion.
    DcgoMatch match = await BattleAsync(
        attackerDp: 3000, targetDp: 5000,
        attackerFlags: new Dictionary<string, object?> { ["hasJamming"] = true });

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, AttackerId), "losing attacker is deleted (normal DP)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, TargetId), "defender survives — no mutual deletion");
}

async Task PiercingChecksSecurity()
{
    int before = SecurityCount(await Setup(5000, 3000), P2);
    DcgoMatch match = await BattleAsync(
        attackerDp: 5000, targetDp: 3000,
        attackerFlags: new Dictionary<string, object?> { [BattleResolver.HasPiercingKey] = true });

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, TargetId), "defender deleted in battle");
    AssertEqual(before - 1, SecurityCount(match, P2), "piercing checked one security card");
}

async Task PiercingStrikeChecksTwo()
{
    int before = SecurityCount(await Setup(5000, 3000), P2);
    DcgoMatch match = await BattleAsync(
        attackerDp: 5000, targetDp: 3000,
        attackerFlags: new Dictionary<string, object?>
        {
            [BattleResolver.HasPiercingKey] = true,
            [SecurityResolver.StrikeKey] = 2
        });

    AssertEqual(before - 2, SecurityCount(match, P2), "piercing with strike 2 checked two security cards");
}

async Task NoPiercingNoSecurityCheck()
{
    int before = SecurityCount(await Setup(5000, 3000), P2);
    DcgoMatch match = await BattleAsync(attackerDp: 5000, targetDp: 3000);
    AssertEqual(before, SecurityCount(match, P2), "no piercing leaves security untouched");
}

// --- Battle harness (from B1/G2G-003) ------------------------------------

async Task<DcgoMatch> BattleAsync(
    int attackerDp,
    int targetDp,
    IReadOnlyDictionary<string, object?>? attackerFlags = null,
    IReadOnlyDictionary<string, object?>? targetFlags = null)
{
    DcgoMatch match = await Setup(attackerDp, targetDp);
    if (attackerFlags is not null) SetMetadata(match, AttackerId, attackerFlags);
    if (targetFlags is not null) SetMetadata(match, TargetId, targetFlags);
    await DeclareTargetAttackAsync(match);
    return match;
}

async Task<DcgoMatch> Setup(int attackerDp, int targetDp)
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
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = targetDp });
    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

async Task DeclareTargetAttackAsync(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

static string? ReadId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId entityId ? entityId.Value : raw.ToString();
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
