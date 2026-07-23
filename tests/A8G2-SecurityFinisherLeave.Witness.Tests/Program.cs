// A8 구조골 GOAL 2 witness (RD-R4B6-P1-2): the SECURITY-battle finisher departure must feed the RunToStable
// OnLeaveFieldAnyone window, exactly like the field-battle finisher (BattleResolver) and the DP-zero sweep
// (GameFlowProcessor) and the effect-delete sink already do.
//
// AS-IS: a security battle exits through the SAME IBattle.Battle → DestroyPermanentsClass(LoserPermanents).Destroy()
// as a field battle (CardController.cs:4179→4705→3736-3756), whose collect-BEFORE-removal StackSkillInfos(OnDeletion /
// OnLeaveFieldAnyone) is the sole thing that opens the anyone-scoped leave window with the dead card still on the
// field. The mirror SecurityResolver.FinalizeSecurityBattleDeletionAsync used to only trash the losing attacker,
// so an uncapped OnLeaveFieldAnyone reactor never fired for a security-battle death (the CardMoved-derived collect
// sees the subject already in the trash). This witness places an UNCAPPED anyone-scoped OnLeaveFieldAnyone reactor
// (TfxOnLeaveFieldCounter, -1 memory) that SURVIVES the security battle and asserts it fires EXACTLY ONCE when the
// attacker dies in a security battle (memory delta -1; a pre-fix run reads 0 = the reactor never fired).
//
// Harness = C5-SecurityPreWindow's pump-driven security-battle driver (verbatim), + one reactor-staging helper.

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId ReactorId = new("p1:main:002:P1-M02");   // survives; the uncapped OnLeaveFieldAnyone reactor
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId[] Security = { SecurityOneId, SecurityTwoId };

var tests = new (string Name, Func<Task> Body)[]
{
    ("security-battle attacker death FIRES the uncapped OnLeaveFieldAnyone reactor exactly once (memory -1)", SecurityDeathFiresLeaveReactor),
    ("CONTROL: attacker SURVIVES the security battle -> reactor does NOT fire (memory unchanged)", SecuritySurvivalDoesNotFire),
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

async Task SecurityDeathFiresLeaveReactor()
{
    // Attacker DP 3000 loses to a 9000-DP security Digimon (strike 1, plain deletion, no replacement) -> reaches
    // FinalizeSecurityBattleDeletionAsync. An uncapped OnLeaveFieldAnyone reactor on P1's field must fire once.
    DcgoMatch match = await CreateMatchAsync(attackerDp: 3000, securityDps: new int?[] { 9000 }, strike: 1);
    StageReactor(match);

    int before = match.Context.MemoryController.Current.Current;
    await DriveDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "attacker deleted by the security battle (reached the finisher)");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "attacker left the field");
    AssertInZone(match, P1, ChoiceZone.BattleArea, ReactorId, "the reactor survived on the field");
    int delta = match.Context.MemoryController.Current.Current - before;
    AssertEqual(-1, delta, "the uncapped OnLeaveFieldAnyone reactor fired EXACTLY ONCE for the security-battle death (delta -1; pre-fix = 0)");
}

async Task SecuritySurvivalDoesNotFire()
{
    // Attacker DP 9000 EXCEEDS the 3000-DP security Digimon -> survives, no departure -> reactor must NOT fire.
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, securityDps: new int?[] { 3000 }, strike: 1);
    StageReactor(match);

    int before = match.Context.MemoryController.Current.Current;
    await DriveDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker survived the security battle");
    int delta = match.Context.MemoryController.Current.Current - before;
    AssertEqual(0, delta, "no departure -> the OnLeaveFieldAnyone reactor did NOT fire (memory unchanged)");
}

// Stage an uncapped anyone-scoped OnLeaveFieldAnyone reactor (TfxOnLeaveFieldCounter) on P1's field that survives
// the battle (it is not the attacker). Registered so the fixture's OnLeaveFieldAnyone ActivateClass is discoverable.
void StageReactor(DcgoMatch match)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId("TfxOnLeaveFieldCounter");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, "TfxOnLeaveFieldCounter", "LeaveReactor",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5, ["dp"] = 8000 }, CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(ReactorId, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false, ["dp"] = 8000 }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, ReactorId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, ReactorId, P1);
}

// --- Harness (C5-SecurityPreWindow verbatim) ------------------------------

async Task DriveDirectAttack(DcgoMatch match)
{
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, targetId: null, isDirectAttack: true);
    }
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

async Task<DcgoMatch> CreateMatchAsync(int attackerDp, int?[] securityDps, int strike = 1)
{
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
    StageInstance(match, P1, AttackerId, dpDef: attackerDp, cardType: "Digimon", attackerMeta, ChoiceZone.BattleArea, register: false);

    for (int index = 0; index < securityDps.Length; index++)
    {
        var meta = securityDps[index] is int dp
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        StageInstance(match, P2, Security[index], dpDef: securityDps[index] ?? 0, cardType: "Digimon", meta, ChoiceZone.Security, register: false);
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
    if (register) HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

static async Task StepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static bool AtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

async Task DriveUntilMainWait(DcgoMatch match, HeadlessPlayerId player)
{
    for (int i = 0; i < 96 && !AtMainWait(match, player); i++)
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

static async Task ApplyAndStep(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
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
