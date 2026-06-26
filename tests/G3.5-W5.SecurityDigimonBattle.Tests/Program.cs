using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W5: a revealed security Digimon battles the attacker. The security card is trashed by the check
// regardless; the persistent outcome is the attacker's fate — it is deleted when its DP does not exceed
// the security Digimon's DP (unless protected by PreventBattleDeletion / Jamming). When the attacker is
// deleted the security check stops (AS-IS StopSecurityCheck). Mirrors ISecurityCheck → IBattle.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId SecurityThreeId = new("p2:main:008:P2-M08");
HeadlessEntityId[] Security = { SecurityOneId, SecurityTwoId, SecurityThreeId };

var tests = new (string Name, Func<Task> Body)[]
{
    ("Stronger attacker survives the security Digimon", StrongerAttackerSurvives),
    ("Weaker attacker is deleted by the security Digimon", WeakerAttackerDeleted),
    ("Equal DP deletes the attacker (mutual)", EqualDpDeletesAttacker),
    ("Jamming attacker survives a losing security battle", JammingAttackerSurvives),
    ("Attacker deletion stops the security check", DeletionStopsSecurityCheck),
    ("A non-Digimon security card does not battle", NonDigimonSecurityNoBattle),
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

async Task StrongerAttackerSurvives()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 6000, securityDps: new[] { 3000 });
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertEqual(1, result.SecurityDigimonBattles, "one security Digimon battle");
    AssertFalse(result.AttackerDeletedBySecurity, "attacker not deleted");
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains on field");
    AssertInZone(match, Opponent, ChoiceZone.Trash, SecurityOneId, "security card trashed");
}

async Task WeakerAttackerDeleted()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 2000, securityDps: new[] { 5000 });
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.AttackerDeletedBySecurity, "attacker deleted by security Digimon");
    AssertFalse(InZone(match, Player, ChoiceZone.BattleArea, AttackerId), "attacker left the field");
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertMetadataTrue(match, AttackerId, BattleResolver.DeletedByBattleKey, "attacker marked deleted by battle");
}

async Task EqualDpDeletesAttacker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 4000, securityDps: new[] { 4000 });
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.AttackerDeletedBySecurity, "equal DP deletes the attacker");
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker trashed on equal DP");
}

async Task JammingAttackerSurvives()
{
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 2000,
        securityDps: new[] { 5000 },
        attackerExtra: new Dictionary<string, object?> { [BattleResolver.PreventBattleDeletionKey] = true });
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertEqual(1, result.SecurityDigimonBattles, "battle still occurs");
    AssertFalse(result.AttackerDeletedBySecurity, "Jamming attacker survives");
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker stays on field");
}

async Task DeletionStopsSecurityCheck()
{
    // Strike 2, two security Digimon; the attacker dies on the first, so only one is checked.
    DcgoMatch match = await CreateMatchAsync(attackerDp: 1000, securityDps: new[] { 5000, 5000 }, strike: 2);
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertTrue(result.AttackerDeletedBySecurity, "attacker deleted");
    AssertEqual(1, result.CheckedCardIds.Count, "security check stopped after one card");
    AssertEqual(1, result.SecurityDigimonBattles, "only one battle happened");
    AssertInZone(match, Opponent, ChoiceZone.Security, SecurityTwoId, "second security card untouched");
}

async Task NonDigimonSecurityNoBattle()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 2000, securityDps: new[] { 9000 }, securityCardType: "Option");
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(match.Context);

    AssertEqual(0, result.SecurityDigimonBattles, "non-Digimon security does not battle");
    AssertFalse(result.AttackerDeletedBySecurity, "attacker survives a non-Digimon security");
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains on field");
}

// --- Harness (adapted from G2G-004) --------------------------------------

void DeclareDirectAttack(DcgoMatch match) =>
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);

async Task<DcgoMatch> CreateMatchAsync(
    int attackerDp,
    int[] securityDps,
    int strike = 1,
    string securityCardType = "Digimon",
    IReadOnlyDictionary<string, object?>? attackerExtra = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Definition($"P1-M{index:D2}", "Digimon"));
        cards.Upsert(Definition($"P2-M{index:D2}", index >= 6 ? securityCardType : "Digimon"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    for (int index = 0; index < securityDps.Length; index++)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, Security[index], ChoiceZone.None, ChoiceZone.Security));
        SetMetadata(match, Security[index], new Dictionary<string, object?> { ["dp"] = securityDps[index] });
    }

    var attackerMeta = new Dictionary<string, object?>
    {
        ["isSuspended"] = false,
        [SecurityResolver.StrikeKey] = strike,
        ["dp"] = attackerDp,
    };
    if (attackerExtra is not null)
    {
        foreach (KeyValuePair<string, object?> pair in attackerExtra)
        {
            attackerMeta[pair.Key] = pair.Value;
        }
    }

    SetMetadata(match, AttackerId, attackerMeta);
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });
    return match;
}

static CardRecord Definition(string id, string cardType) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: cardType);

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction[] advance = match.GetLegalActions(playerId)
            .Where(a => a.ActionType == HeadlessActionTypes.AdvancePhase).ToArray();
        AssertEqual(1, advance.Length, "advance phase count");
        await match.ApplyActionAsync(advance[0]);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
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

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId)
{
    return match.Context.ZoneMover is IZoneStateReader reader && reader.GetCards(player, zone).Contains(cardId);
}

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label) =>
    AssertTrue(InZone(match, player, zone, cardId), label);

void AssertMetadataTrue(DcgoMatch match, HeadlessEntityId cardId, string key, string label)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    AssertTrue(record.Metadata.TryGetValue(key, out object? raw) && raw is bool flag && flag, label);
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
