using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// R2-1 / N-2 (consumption side): battle deletion now consults continuous deletion-prevention
// REPLACEMENTS (BattleDeletionGate), not just the static preventBattleDeletion flag. A card protected
// by another card's continuous "cannot be deleted" replacement survives a battle it would otherwise
// lose — both a field battle (BattleResolver) and a security-Digimon battle (SecurityResolver).
// This is the engine wiring; a Jamming keyword would register such a replacement in Phase 4.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Field battle: a continuous prevent-deletion replacement saves the loser", FieldBattleReplacementSaves),
    ("Field battle control: without the replacement the loser is deleted", FieldBattleControlDeleted),
    ("Security battle: the replacement saves the attacker from a stronger security Digimon", SecurityBattleReplacementSaves),
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

async Task FieldBattleReplacementSaves()
{
    // Attacker 5000 beats target 3000 -> target would be deleted; a continuous prevent-deletion
    // replacement on the target saves it.
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    RegisterPreventDeletion(match.Context, TargetId, owner: P2);
    await DeclareTargetAttack(match);

    AssertInZone(match, P2, ChoiceZone.BattleArea, TargetId, "protected target survived the battle");
}

async Task FieldBattleControlDeleted()
{
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    await DeclareTargetAttack(match);

    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, TargetId), "unprotected target left the field");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "unprotected target was deleted");
}

async Task SecurityBattleReplacementSaves()
{
    // Attacker 2000 vs a 7000 security Digimon -> attacker would be deleted; the replacement saves it.
    DcgoMatch match = await SecuritySetup(attackerDp: 2000, topSecurityDp: 7000);
    RegisterPreventDeletion(match.Context, AttackerId, owner: P1);
    await DeclareDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "protected attacker survived the security Digimon");
}

// --- Continuous prevent-deletion replacement registration ----------------

void RegisterPreventDeletion(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var effectId = new HeadlessEntityId($"prevent-del:{cardId.Value}");
    var effectContext = new EffectContext(
        owner,
        owner,
        new HeadlessEntityId($"src:{cardId.Value}"),
        triggerEntityId: null,
        targetEntityIds: new[] { cardId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["preventDeletion"] = true });

    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(effectId, owner, "Continuous", effectContext),
        keywords: null,
        EffectQueryRole.Continuous,
        new[] { ContinuousRestrictionGate.Scope }));
}

// --- Harness (field battle, from C2) -------------------------------------

async Task<DcgoMatch> FieldSetup(int attackerDp, int targetDp)
{
    DcgoMatch match = await BaseMatch();
    EngineContext context = match.Context;
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = targetDp });
    return match;
}

async Task DeclareTargetAttack(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

// --- Harness (security battle, from W5) ----------------------------------

async Task<DcgoMatch> SecuritySetup(int attackerDp, int topSecurityDp)
{
    DcgoMatch match = await BaseMatch(initialSecurity: 0);
    EngineContext context = match.Context;
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, new HeadlessEntityId("p2:main:006:P2-M06"), ChoiceZone.None, ChoiceZone.Security));

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp, [SecurityResolver.StrikeKey] = 1 });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });
    SetMetadata(match, new HeadlessEntityId("p2:main:006:P2-M06"), new Dictionary<string, object?> { ["dp"] = topSecurityDp });
    return match;
}

async Task DeclareDirectAttack(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, targetId: null, isDirectAttack: true);
    await new AttackPipeline().AdvanceAsync(match.Context); // Declared -> (counter/block) -> ... drive to combat/security
    await match.StepAsync();
}

// --- Shared --------------------------------------------------------------

async Task<DcgoMatch> BaseMatch(int initialSecurity = 5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, initialSecuritySize: initialSecurity);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));

    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static string? ReadId(IReadOnlyDictionary<string, object?> p, string key)
{
    if (!p.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId id ? id.Value : raw.ToString();
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

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: not in {zone}.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
