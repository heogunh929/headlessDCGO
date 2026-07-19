using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-5 / VR-6 (RD-7 Part B) — the SECURITY battle loss rides the PRE would-be-deleted window.
// AS-IS: a security battle exits through the SAME path as a field battle — IBattle.Battle →
// DestroyPermanentsClass.Destroy (CardController.cs:4165→4705→3696) — whose PRE cut-in
// (WhenPermanentWouldBeDeleted) lets Evade/Barrier/Fragment/Scapegoat cancel the deletion
// (willBeRemoveField=false). Barrier is by-battle (IsByBattle, OnDeletion.cs:82) and the battle hashtable
// is present for a DefendingCard battle too (CardController.cs:4700), so it may fire on a security-battle
// loss. After a survival the AS-IS ISecurityCheck while-loop CONTINUES the remaining checks
// (StopSecurityCheck only breaks when the attacker is gone, CardController.cs:3893-3908).
//
// Headless: the loss is flagged pendingDeletion+deletedByBattle (the same pending/defer/finalize machine
// as BattleResolver), the check loop parks (AttackPhase.DeletionReplacement + a remaining-checks marker),
// the common loop opens the DeletionReplacement choice, and SecurityResolver.FinalizeDeferredSecurityCheckAsync
// settles the attacker — decline finalizes (cleanup → evo-source trash → top trash → Fortitude), survival
// resumes the remaining checks.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId DefenderId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId OwnSecurityId = new("p1:main:006:P1-M06");
HeadlessEntityId[] Security = { SecurityOneId, SecurityTwoId };

var tests = new (string Name, Func<Task> Body)[]
{
    ("Evade attacker losing a security battle gets the PRE window; accepting suspends it, survives, and the check resumes", EvadeOfferAcceptResumesCheck),
    ("Declining the PRE window finalizes the deletion (sources then top to trash) and stops the check", DeclineFinalizesDeletionAndStopsCheck),
    ("A by-battle-required WhenPermanentWouldBeDeleted replacement survives a security-battle loss (byBattle cause threaded)", BarrierSurvivesSecurityBattle),
    ("CONTROL: with NO registered replacement, the security-battle loser gets NO window and is deleted outright (StopSecurityCheck)", SuspendedEvadeNoOffer),
    ("A Piercing follow-up check's security battle loss also parks and resumes through the same window", PiercingDeferAndResume),
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

async Task EvadeOfferAcceptResumesCheck()
{
    // Strike 2: check #1 is a 9000 DP security Digimon (attacker 3000 loses -> window), check #2 is a
    // 1000 DP security Digimon (the resumed check: attacker wins).
    // (수리-2 re-aim) The retired HasEvadeKey metadata gate-key is replaced by the current-model canon: a
    // card-registered OPTIONAL [WhenPermanentWouldBeDeleted] survival replacement (TfxWouldBeDeletedInteractive —
    // the window form of the "you may" Evade keyword). Its "will you use it?" pause is what parks the security
    // check; accepting cancels the deletion and the remaining check resumes. The retired 'evaded' marker and the
    // Evade keyword's suspend-cost are dropped (invented-keyword expression); the park/survive/resume transport
    // rule assertions are preserved.
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 3000,
        securityDps: new int?[] { 9000, 1000 },
        strike: 2);
    GiveWouldBeDeleted(match.Context, AttackerId, P1, "TfxWouldBeDeletedInteractive");

    await DriveDirectAttack(match);

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE would-be-deleted window is open");
    // (수리-2 re-aim) the current PRE cut-in surfaces as the OptionalEffect "will you use it?" ForCutIn choice
    // (the old invented DeletionReplacement gate type is retired).
    AssertEqual(ChoiceType.OptionalEffect, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertEqual(P1, match.Context.ChoiceController.PendingRequest!.PlayerId, "the ATTACKER'S owner decides");
    AssertTrue(ReadFlag(match, AttackerId, GameFlowProcessor.PendingDeletionKey), "attacker deletion deferred");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker still on the field while parked");

    LegalAction activate = AcceptWindow(match, P1, AttackerId);
    await ResolveAndDrive(match, activate);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "the replacement cancels the deletion (field survival)");
    AssertFalse(ReadFlag(match, AttackerId, GameFlowProcessor.PendingDeletionKey), "pendingDeletion cleared");
    AssertInZone(match, P2, ChoiceZone.Trash, SecurityTwoId, "the SECOND security card was checked (loop resumed)");
    AssertEqual(0, SecurityCount(match, P2), "no security left after the resumed check");
    AssertAttackEnded(match, "attack completed after the resumed check");
    AssertFalse(HasMarker(match, AttackerId, SecurityResolver.SecurityCheckRemainingKey), "park marker cleared");
}

async Task DeclineFinalizesDeletionAndStopsCheck()
{
    // (수리-2 re-aim) HasEvadeKey → a card-registered OPTIONAL [WhenPermanentWouldBeDeleted] replacement
    // (TfxWouldBeDeletedInteractive). DECLINING it leaves willBeRemoveField set, so the deletion finalizes; the
    // finalize-order (sources BEFORE top) and StopSecurityCheck transport rules are preserved unchanged.
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 3000,
        securityDps: new int?[] { 9000, 1000 },
        strike: 2);
    GiveWouldBeDeleted(match.Context, AttackerId, P1, "TfxWouldBeDeletedInteractive");

    // Give the attacker digivolution sources so the finalize order (sources BEFORE top) is observable.
    HeadlessEntityId src0 = new("p1:src:00");
    HeadlessEntityId src1 = new("p1:src:01");
    CardDatabase cards = (CardDatabase)match.Context.CardRepository;
    cards.Upsert(Definition("SRCDEF", "Digimon"));
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(src0, new HeadlessEntityId("SRCDEF"), P1));
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(src1, new HeadlessEntityId("SRCDEF"), P1));
    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["sourceIds"] = new[] { src0.Value, src1.Value } });

    await DriveDirectAttack(match);

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window is open");
    LegalAction decline = ResolveActions(match, P1).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await ResolveAndDrive(match, decline);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "declined attacker is deleted to the trash");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "attacker left the field");
    AssertInZone(match, P1, ChoiceZone.Trash, src0, "digivolution source 0 trashed");
    AssertInZone(match, P1, ChoiceZone.Trash, src1, "digivolution source 1 trashed");
    // Trash inserts at the top (index 0 = most recent, AS-IS TrashCards.Insert(0)): the sources were
    // trashed BEFORE the top card (AS-IS DiscardEvoRoots order), so the top card sits ABOVE them.
    var trash = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P1, ChoiceZone.Trash).ToList();
    AssertTrue(trash.IndexOf(AttackerId) < trash.IndexOf(src0) && trash.IndexOf(AttackerId) < trash.IndexOf(src1),
        "sources trashed BEFORE the top card (AS-IS DiscardEvoRoots order; trash is newest-first)");
    AssertTrue(ReadFlag(match, AttackerId, BattleResolver.DeletedByBattleKey), "deleted-by-battle marker");
    AssertInZone(match, P2, ChoiceZone.Security, SecurityTwoId, "the SECOND check never happened (StopSecurityCheck)");
    AssertAttackEnded(match, "attack completed after the finalized deletion");
    AssertFalse(HasMarker(match, AttackerId, SecurityResolver.SecurityCheckRemainingKey), "park marker cleared");
}

async Task BarrierSurvivesSecurityBattle()
{
    // (수리-2 re-aim) The retired HasBarrierKey metadata gate-key is replaced by the current-model canon: a
    // card-registered [WhenPermanentWouldBeDeleted] survival replacement that REQUIRES IsByBattle
    // (TfxWouldBeDeletedByBattle — the window form of the by-battle Barrier keyword). Its very availability on a
    // SECURITY-battle loss asserts the security cut-in threads the byBattle cause (AS-IS IsByBattle), the rule the
    // old HasBarrierKey fixture stood in for. The retired 'barriered' marker and the keyword's own-security-trash
    // cost are dropped (invented-keyword expression); survival + the loop resuming are the preserved rule witness.
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 3000,
        securityDps: new int?[] { 9000, 1000 },
        strike: 2);
    GiveWouldBeDeleted(match.Context, AttackerId, P1, "TfxWouldBeDeletedByBattle");

    await DriveDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "by-battle replacement survives the security battle");
    AssertInZone(match, P2, ChoiceZone.Trash, SecurityTwoId, "the SECOND security card was checked (loop resumed)");
    AssertAttackEnded(match, "attack completed after the resumed check");
}

async Task SuspendedEvadeNoOffer()
{
    // (수리-2 re-aim) CONTROL / false-green guard: with NO card-registered [WhenPermanentWouldBeDeleted] replacement
    // (the current-model canon that replaced the retired HasEvadeKey gate-key), a plain attacker that loses the
    // security battle gets NO PRE window and is deleted outright, and StopSecurityCheck halts the remaining checks.
    // This proves the window opens ONLY when a replacement exists — the rule the "unpayable cost" case stood for.
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 3000,
        securityDps: new int?[] { 9000, 1000 },
        strike: 2);

    await DriveDirectAttack(match);

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no PRE window without a registered replacement");
    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "attacker deleted outright");
    AssertInZone(match, P2, ChoiceZone.Security, SecurityTwoId, "the second check never happened (StopSecurityCheck)");
    AssertAttackEnded(match, "attack completed");
}

async Task PiercingDeferAndResume()
{
    // Non-direct attack: the Piercing attacker wins the field battle, the follow-up security check's
    // security Digimon beats it, and the SAME park/resume seam (DeletionReplacement) handles the window.
    DcgoMatch match = await CreateMatchAsync(
        attackerDp: 3000,
        securityDps: new int?[] { 9000 },
        strike: 1,
        defenderOnField: true);
    // (수리-2 re-aim) Piercing fires only through the AS-IS OnDetermineDoSecurityCheck window (the retired
    // HasPiercingKey flag is gone), and the survival replacement is a card-registered OPTIONAL
    // [WhenPermanentWouldBeDeleted] effect (the retired HasEvadeKey gate-key is gone). Both are carried by ONE
    // registered fixture (TfxPierceWouldBeDeletedInteractive) that composes the real PierceSelfEffect with the
    // interactive survival, so a single card drives the piercing follow-up check AND parks on its loss.
    GiveWouldBeDeleted(match.Context, AttackerId, P1, "TfxPierceWouldBeDeletedInteractive");

    await DriveTargetedAttack(match, DefenderId);

    AssertInZone(match, P2, ChoiceZone.Trash, DefenderId, "the field-battle defender was deleted");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window opened from the piercing check");
    // (수리-2 re-aim) the current PRE cut-in surfaces as the OptionalEffect ForCutIn choice (retired gate type).
    AssertEqual(ChoiceType.OptionalEffect, match.Context.ChoiceController.PendingRequest!.Type, "choice type");

    LegalAction activate = AcceptWindow(match, P1, AttackerId);
    await ResolveAndDrive(match, activate);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "the replacement survives the piercing security battle");
    AssertInZone(match, P2, ChoiceZone.Trash, SecurityOneId, "the checked security card is in the trash");
    AssertAttackEnded(match, "attack completed after the deferred piercing check");
    AssertFalse(HasMarker(match, AttackerId, SecurityResolver.SecurityCheckRemainingKey), "park marker cleared");
}

// --- Harness (adapted from G3.5-W5 / G3.5-F68) ----------------------------

// (4b B6 re-pin) The OLD `new DcgoMatch(context)` + AdvanceToMain (AdvancePhase currency) driver is replaced by
// CreatePumpDriven + the pump reach-main + DeclareAttack legal-lane (G3.5-W5 / G3.5-005 idiom). The direct
// AttackController.DeclareAttack seam is replaced by the pump's DeclareAttack legal lane driven to the PRE window
// (or resolution). The pump owns the security deal, so the pump-dealt security is cleared before the fixture's own
// security is staged. Assertions are unchanged.

async Task DriveDirectAttack(DcgoMatch match)
{
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, targetId: null, isDirectAttack: true);
    }
    await DriveToChoiceOrAttackEnd(match);
}

async Task DriveTargetedAttack(DcgoMatch match, HeadlessEntityId target)
{
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, targetId: target, isDirectAttack: false);
    }
    await DriveToChoiceOrAttackEnd(match);
}

async Task ResolveAndDrive(DcgoMatch match, LegalAction action)
{
    await ApplyAndStep(match, action);
    await DriveToChoiceOrAttackEnd(match);
}

async Task DriveToChoiceOrAttackEnd(DcgoMatch match)
{
    for (int i = 0; i < 96; i++)
    {
        if (match.HasPendingChoice() || match.IsTerminal())
        {
            return;
        }
        HeadlessAttackState attack = match.Context.AttackController.Current;
        if (attack.Phase == AttackPhase.None && !attack.IsPending)
        {
            return;
        }
        await StepOnce(match);
    }
}

async Task<DcgoMatch> CreateMatchAsync(
    int attackerDp,
    int?[] securityDps,
    int strike = 1,
    IReadOnlyDictionary<string, object?>? attackerExtra = null,
    bool ownSecurity = false,
    bool defenderOnField = false)
{
    // (수리-2 re-aim) deferredChoice: the interactive [WhenPermanentWouldBeDeleted] replacement (Tfx…Interactive)
    // parks its "will you use it?" cut-in as an agent ResolveChoice instead of the enqueued-result fallback — the
    // same context flag the green sibling C-Del-3C1C and C5-Witness use to surface the PRE cut-in window.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 75, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Definition($"P1-M{index:D2}", "Digimon"));
        cards.Upsert(Definition($"P2-M{index:D2}", "Digimon"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1"), BuildDeck(P2, "P2") },
        firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 75, setup: setup));
    await StepOnce(match);
    await DriveUntilMainWait(match, P1);

    // Clear the pump-dealt security stack for both players, then stage exactly the test's cards.
    var reader = (IZoneStateReader)context.ZoneMover;
    foreach (HeadlessPlayerId owner in new[] { P1, P2 })
    {
        foreach (HeadlessEntityId dealt in reader.GetCards(owner, ChoiceZone.Security).ToArray())
        {
            await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, ChoiceZone.Security, ChoiceZone.Library));
        }
    }

    var attackerMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
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

    StageInstance(match, P1, AttackerId, dpDef: attackerDp, cardType: "Digimon", attackerMeta, ChoiceZone.BattleArea, register: false);

    if (defenderOnField)
    {
        StageInstance(match, P2, DefenderId, dpDef: 1000, cardType: "Digimon",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000, ["isSuspended"] = true },
            ChoiceZone.BattleArea, register: false);
    }

    for (int index = 0; index < securityDps.Length; index++)
    {
        var meta = securityDps[index] is int dp
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        StageInstance(match, P2, Security[index], dpDef: securityDps[index] ?? 0, cardType: "Digimon", meta, ChoiceZone.Security, register: false);
    }

    if (ownSecurity)
    {
        StageInstance(match, P1, OwnSecurityId, dpDef: 0, cardType: "Digimon",
            new Dictionary<string, object?>(StringComparer.Ordinal), ChoiceZone.Security, register: false);
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
    if (register) HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

static async Task StepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ApplyAndStep(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
}

static bool AtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task DriveUntilMainWait(DcgoMatch match, HeadlessPlayerId player)
{
    for (int i = 0; i < 96 && !AtMainWait(match, player); i++)
    {
        if (match.HasPendingChoice())
        {
            // Resolve pump start-of-game choices (mulligan etc.) via the skip lane so reach-main can proceed.
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await StepOnce(match); }
            else { await ApplyAndStep(match, resolve); }
        }
        else
        {
            await StepOnce(match);
        }
    }

    if (!AtMainWait(match, player))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump did not reach {player.Value}'s main wait — phase:{t.Phase}/{t.StepCursor} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static CardRecord Definition(string id, string cardType) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: cardType);

// (수리-2 re-aim) Give the attacker a card-registered [WhenPermanentWouldBeDeleted] survival replacement — the
// current-model canon for the retired HasEvadeKey/HasBarrierKey metadata gate-key fixtures. `tfxNumber` selects a
// dispatch-discoverable TestFixtures ActivateClass (TfxWouldBeDeletedInteractive = optional; TfxWouldBeDeletedByBattle
// = mandatory, IsByBattle-required — the window form of the retired by-battle Barrier keyword). Same retype+register
// shape as GivePierce: the effect surfaces through RegisterCard, NOT through a metadata flag.
void GiveWouldBeDeleted(EngineContext context, HeadlessEntityId card, HeadlessPlayerId owner, string tfxNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:" + tfxNumber);
    cards.Upsert(new CardRecord(defId, tfxNumber, tfxNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(context, card, owner);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

// (수리-2 re-aim) Accept the OptionalEffect PRE would-be-deleted window: the non-skip candidate keyed by the
// replacement holder's own instance id (the Candidates[0].Id convention the green sibling C-Del-3C1C resolves
// against) — replaces the torn-down invented "#<keyword>" gate ids.
LegalAction AcceptWindow(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId holder) =>
    ResolveActions(match, player).Single(a =>
        a.Id.Value.Contains(holder.Value, StringComparison.Ordinal)
        && !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));

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

int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    match.Context.ZoneMover is IZoneStateReader reader && reader.GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label) =>
    AssertTrue(InZone(match, player, zone, cardId), label);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

bool HasMarker(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.ContainsKey(key);

void AssertAttackEnded(DcgoMatch match, string label)
{
    HeadlessAttackState attack = match.Context.AttackController.Current;
    AssertTrue(attack.Phase == AttackPhase.None && !attack.IsPending, $"{label} (phase={attack.Phase}, pending={attack.IsPending})");
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
