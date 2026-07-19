using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W5: a revealed security Digimon battles the attacker. The security card is trashed by the check
// regardless; the persistent outcome is the attacker's fate — it is deleted when its DP does not exceed
// the security Digimon's DP (unless protected by PreventBattleDeletion / Jamming). When the attacker is
// deleted the security check stops (AS-IS StopSecurityCheck). Mirrors ISecurityCheck → IBattle.
//
// (4b A-1) RE-TARGETED to the pump (G3.5-005 c5 F68 idiom): the OLD-ctor `new DcgoMatch` + AdvanceToMain
// (AdvancePhase currency) + manual `new SecurityResolver().ResolveAsync` seam is replaced by
// CreatePumpDriven + the pump DeclareAttack direct legal lane, driven to auto-resolution. The manual
// SecurityResolutionResult diagnostics are re-sourced to the equivalent, stronger ZONE / metadata
// observations of the persistent outcome (each translation is noted inline). AdvancePhase/EndTurn removed.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId SecurityThreeId = new("p2:main:008:P2-M08");
HeadlessEntityId[] Security = { SecurityOneId, SecurityTwoId, SecurityThreeId };

var tests = new (string Name, Func<Task> Body)[]
{
    ("Stronger attacker survives the security Digimon", StrongerAttackerSurvives),
    ("Weaker attacker is deleted by the security Digimon", WeakerAttackerDeleted),
    ("A deleted attacker's digivolution sources are trashed (P0-4 wiring)", DeletedAttackerSourcesAreTrashed),
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
    await DriveDirectAttack(match);

    // (re-source) result.IsSuccess -> the attack drove to completion (AttackPhase.None).
    AssertAttackCleared(match, "resolve success");
    // (re-source) result.SecurityDigimonBattles == 1 -> the single security Digimon was checked & trashed (battled).
    AssertInZone(match, Opponent, ChoiceZone.Trash, SecurityOneId, "one security Digimon battle (checked & trashed)");
    // (re-source) result.AttackerDeletedBySecurity == false -> the attacker stays on the field.
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker not deleted");
}

async Task WeakerAttackerDeleted()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 2000, securityDps: new[] { 5000 });
    await DriveDirectAttack(match);

    AssertAttackCleared(match, "resolve success");
    // (re-source) result.AttackerDeletedBySecurity == true -> the attacker left the field into trash.
    AssertFalse(InZone(match, Player, ChoiceZone.BattleArea, AttackerId), "attacker left the field");
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertMetadataTrue(match, AttackerId, BattleResolver.DeletedByBattleKey, "attacker marked deleted by battle");
}

async Task DeletedAttackerSourcesAreTrashed()
{
    // (P0-4/RD-4) a security-battle loser is deleted through SecurityResolver's own path (not the sink/battle
    // resolver). Verify that path now trashes the attacker's digivolution sources like AS-IS DiscardEvoRoots.
    HeadlessEntityId src0 = new("p1:src:00");
    HeadlessEntityId src1 = new("p1:src:01");
    DcgoMatch match = await CreateMatchAsync(attackerDp: 2000, securityDps: new[] { 5000 },
        attackerExtra: new Dictionary<string, object?> { ["sourceIds"] = new[] { src0.Value, src1.Value } });
    CardDatabase cards = (CardDatabase)match.Context.CardRepository;
    cards.Upsert(Definition("SRCDEF", "Digimon"));
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(src0, new HeadlessEntityId("SRCDEF"), Player));
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(src1, new HeadlessEntityId("SRCDEF"), Player));
    await DriveDirectAttack(match);

    // (re-source) result.AttackerDeletedBySecurity == true -> the attacker top and its sources landed in trash.
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker top trashed");
    AssertInZone(match, Player, ChoiceZone.Trash, src0, "attacker source 0 trashed via SecurityResolver wiring");
    AssertInZone(match, Player, ChoiceZone.Trash, src1, "attacker source 1 trashed via SecurityResolver wiring");
}

async Task EqualDpDeletesAttacker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 4000, securityDps: new[] { 4000 });
    await DriveDirectAttack(match);

    // (re-source) result.AttackerDeletedBySecurity == true (equal DP deletes the attacker).
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker trashed on equal DP");
}

async Task JammingAttackerSurvives()
{
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 2000,
        securityDps: new[] { 5000 },
        attackerExtra: new Dictionary<string, object?> { [BattleResolver.PreventBattleDeletionKey] = true });
    await DriveDirectAttack(match);

    // (re-source) result.SecurityDigimonBattles == 1 -> the security Digimon still battled (was checked & trashed).
    AssertInZone(match, Opponent, ChoiceZone.Trash, SecurityOneId, "battle still occurs (security checked & trashed)");
    // (re-source) result.AttackerDeletedBySecurity == false -> the Jamming attacker survives on the field.
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker stays on field");
}

async Task DeletionStopsSecurityCheck()
{
    // Strike 2, two security Digimon; the attacker dies on the first, so only one is checked.
    DcgoMatch match = await CreateMatchAsync(attackerDp: 1000, securityDps: new[] { 5000, 5000 }, strike: 2);
    await DriveDirectAttack(match);

    // (re-source) result.AttackerDeletedBySecurity == true.
    AssertInZone(match, Player, ChoiceZone.Trash, AttackerId, "attacker deleted");
    // (re-source) result.CheckedCardIds.Count == 1 / SecurityDigimonBattles == 1 -> exactly the first security was
    // checked (trashed) and the check STOPPED before the second, which remains in the security stack.
    AssertInZone(match, Opponent, ChoiceZone.Trash, SecurityOneId, "first security checked (battle happened)");
    AssertInZone(match, Opponent, ChoiceZone.Security, SecurityTwoId, "second security card untouched");
}

async Task NonDigimonSecurityNoBattle()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 2000, securityDps: new[] { 9000 }, securityCardType: "Option");
    await DriveDirectAttack(match);

    // (re-source) result.SecurityDigimonBattles == 0 / AttackerDeletedBySecurity == false -> a non-Digimon security
    // does not battle, so the (weaker) attacker survives unscathed on the field.
    AssertInZone(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker survives a non-Digimon security");
}

// --- Harness (pump, G3.5-005 F68 idiom) ----------------------------------

async Task DriveDirectAttack(DcgoMatch match)
{
    await ExpApply(match, DirectAttackLane(match, AttackerId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());
}

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
        cards.Upsert(Definition($"P2-M{index:D2}", "Digimon"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, Player));

    // Stage the attacker as a live battle-area synthetic Digimon.
    var attackerMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = false,
        [SecurityResolver.StrikeKey] = strike,
        [BattleResolver.DpKey] = attackerDp,
    };
    if (attackerExtra is not null)
    {
        foreach (KeyValuePair<string, object?> pair in attackerExtra) attackerMeta[pair.Key] = pair.Value;
    }

    StageInstance(match, Player, AttackerId, dpDef: attackerDp, cardType: "Digimon", attackerMeta, ChoiceZone.BattleArea, register: true);

    // Clear the pump-dealt security stack, then stage exactly the test's security cards (top-down = index order).
    var reader = (IZoneStateReader)context.ZoneMover;
    foreach (HeadlessEntityId dealt in reader.GetCards(Opponent, ChoiceZone.Security).ToArray())
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, dealt, ChoiceZone.Security, ChoiceZone.Library));
    }

    for (int index = 0; index < securityDps.Length; index++)
    {
        var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = securityDps[index] };
        StageInstance(match, Opponent, Security[index], dpDef: securityDps[index], cardType: securityCardType, meta, ChoiceZone.Security, register: false);
    }

    return match;
}

void StageInstance(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int dpDef, string cardType,
    IReadOnlyDictionary<string, object?> instMeta, ChoiceZone zone, bool register)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5, ["dp"] = dpDef }, CardType: cardType));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(instMeta, StringComparer.Ordinal)));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    if (register) CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

LegalAction DirectAttackLane(DcgoMatch match, HeadlessEntityId attacker) =>
    ExpLegal(match, Player)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) is null)
        ?? throw new InvalidOperationException("no direct-attack lane for " + attacker.Value);

static IReadOnlyList<LegalAction> ExpLegal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static HeadlessEntityId? ExpParamId(LegalAction action, string key) =>
    action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id ? id : null;

static bool ExpAtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task ExpStepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

async Task ExpApply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task ExpDriveUntil(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await ExpStepOnce(match); }
            else { await ExpApply(match, resolve); }
        }
        else
        {
            await ExpStepOnce(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"EXP drive did not reach state — phase:{t.Phase}/{t.StepCursor} attackPhase:{match.Context.AttackController.Current.Phase} " +
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static CardRecord Definition(string id, string cardType) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: cardType);

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    match.Context.ZoneMover is IZoneStateReader reader && reader.GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label) =>
    AssertTrue(InZone(match, player, zone, cardId), label);

void AssertAttackCleared(DcgoMatch match, string label) =>
    AssertTrue(match.Context.AttackController.Current.Phase == AttackPhase.None, label);

void AssertMetadataTrue(DcgoMatch match, HeadlessEntityId cardId, string key, string label)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    AssertTrue(record.Metadata.TryGetValue(key, out object? raw) && raw is bool flag && flag, label);
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
