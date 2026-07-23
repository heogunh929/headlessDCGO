using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D1: a Piercing attacker's follow-up security check now runs the SAME loop as a direct attack
// (SecurityResolver.RunSecurityCheckLoopAsync) — including the W5 security-Digimon battle and the W4
// OnSecurityCheck window. Before the fix the piercing path only moved cards to trash, so a revealed
// security Digimon never battled the attacker and security effects never fired.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Piercing into a stronger security Digimon deletes the attacker", PiercingIntoStrongerSecurityDeletesAttacker),
    ("Piercing into a weaker security Digimon leaves the attacker alive", PiercingIntoWeakerSecuritySurvives),
    ("Piercing fires the revealed security card's OnSecurityCheck effect", PiercingFiresSecurityEffect),
    ("(A1) Piercing into EMPTY security does nothing — no invented game loss", PiercingIntoEmptySecurityNoLoss),
    ("(B2) battle triggers drain BEFORE the piercing check: a knock-out trigger that kills the attacker cancels it", TriggerKillsAttackerBeforePiercing),
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

async Task PiercingIntoStrongerSecurityDeletesAttacker()
{
    // Attacker 5000 beats target 3000 (piercing triggers), then the revealed security Digimon (7000)
    // out-DPs the attacker -> attacker is deleted by the security battle.
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 7000, piercing: true);
    await DeclareTargetAttackAsync(match);

    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "attacker left the battle area");
    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "attacker deleted by the security Digimon");
}

async Task PiercingIntoWeakerSecuritySurvives()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 2000, piercing: true);
    HeadlessEntityId topSecurity = TopSecurity(match, P2);
    await DeclareTargetAttackAsync(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker survives the weaker security Digimon");
    AssertInZone(match, P2, ChoiceZone.Trash, topSecurity, "security card was still checked into trash");
}

async Task PiercingFiresSecurityEffect()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 2000, piercing: true);

    // (B6-Dc D1 re-target — W4 stale-probe removed) The invented `EffectRegistry.Register(new EffectBinding(
    // RecordingFakeEffect))` probe is gone: the live OnSecurityCheck window reads card-registered ActivateClasses
    // through AutoProcessing.GetSkillInfos, NEVER the registry binding (the same W4 finding). Witness the window
    // with a SURVIVING P2 battle-area reactor (TfxOnSecurityCheckDraw, owner-scoped): when piercing checks P2's
    // security, the reactor's [When your security is checked] draws 1. A live surface, no invented logic.
    await AddSecurityCheckReactorAsync(match.Context, P2);
    int p2HandBefore = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Hand).Count;

    await DeclareTargetAttackAsync(match);

    int p2HandAfter = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Hand).Count;
    AssertEqual(p2HandBefore + 1, p2HandAfter,
        "the revealed security card's OnSecurityCheck window fired via piercing (the P2 field reactor drew 1)");
}

// (A1) AS-IS CanActivatePierce: Pierce fires only with >= 1 security; with 0 security nothing happens.
// The empty-security game loss belongs only to the DIRECT-attack path.
async Task PiercingIntoEmptySecurityNoLoss()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 1000, piercing: true);
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    foreach (HeadlessEntityId securityCard in zones.GetCards(P2, ChoiceZone.Security).ToArray())
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, securityCard, ChoiceZone.Security, ChoiceZone.Trash));
    }

    await DeclareTargetAttackAsync(match);

    AssertFalse(match.Context.PlayerStatusController.IsLose(P2), "the defending player did NOT lose (Pierce no-op at 0 security)");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker survives (battle won, no security battle)");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "the defending Digimon was deleted by the field battle");
}

// (B2) AS-IS AttackProcess: battle → TriggeredSkillProcess (drain) → `if (AttackingPermanent.TopCard ==
// null) End` → security check. A battle-generated trigger that removes the attacker must cancel Piercing.
async Task TriggerKillsAttackerBeforePiercing()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 1000, piercing: true);
    int before = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;

    // (B6-Dc D1 re-target — W4 stale-probe removed) The invented `EffectRegistry.Register(new EffectBinding(
    // AttackerKillingEffect))` probe is gone: the live BattleResolver OnKnockOut window reads card-registered
    // ActivateClasses via GetSkillInfos(OnKnockOut), NEVER a registry binding. Retype the (about-to-be-knocked-out)
    // target to a card-registered OnKnockOut reactor (TfxOnKnockOutDeleteOpponent: [When knocked out] delete all
    // opponent battle-area Digimon, via the same DestroyPermanentsClass primitive real cards use). It stacks at the
    // KO window (BattleResolver:248) and drains at the shared main-loop AutoProcessCheck BEFORE PierceProcess flips
    // DoSecurityCheck (BattleResolver:262-267) — so the attacker dies and Piercing is cancelled. The attacker's
    // deletion + the unchanged security count ARE the witness the KO trigger drained pre-check.
    GiveKnockOutDeleteReactor(match.Context, TargetId, P2);

    await DeclareTargetAttackAsync(match);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "the attacker was deleted by the drained OnKnockOut trigger");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "the attacker left the battle area");
    AssertEqual(before, ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count,
        "NO security was checked — Piercing was cancelled by the pre-check survival test");
}

// --- Harness (from C2) ---------------------------------------------------

async Task<DcgoMatch> Setup(int attackerDp, int targetDp, int topSecurityDp, bool piercing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, P1));

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var attackerMeta = new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp };
    SetMetadata(match, AttackerId, attackerMeta);
    // (RD-CBTL-01) Piercing now fires only through the AS-IS OnDetermineDoSecurityCheck window — drive it with
    // the REAL printed BT1_022 <Piercing> (retype + register), not the retired HasPiercingKey metadata flag.
    if (piercing) GivePierce(context, AttackerId, P1);
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = targetDp });

    // The piercing check reveals the top of P2's security first — give that card the test DP.
    SetMetadata(match, TopSecurity(match, P2), new Dictionary<string, object?> { ["dp"] = topSecurityDp });
    return match;
}

// (RD-CBTL-01) retype the attacker to the REAL printed BT1_022 <Piercing> and register it, so its
// PierceSelfEffect surfaces in EffectList(OnDetermineDoSecurityCheck) — the window firing path.
void GivePierce(EngineContext context, HeadlessEntityId card, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:BT1_022");
    cards.Upsert(new CardRecord(defId, "BT1_022", "BT1_022",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(context, card, owner);
}

// (B6-Dc D1 re-target) Stage a SURVIVING battle-area reactor for `owner` carrying the live OnSecurityCheck
// ActivateClass (TfxOnSecurityCheckDraw fixture: [When your security is checked] Draw 1, owner-scoped). Retype to
// the fixture def + register through CardEffectRegistrar so the ActivateClass surfaces in AutoProcessing
// .GetSkillInfos — the surface the SecurityResolver window reads (mirrors W4.RegisterReactor).
async Task<HeadlessEntityId> AddSecurityCheckReactorAsync(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:TfxOnSecurityCheckDraw");
    cards.Upsert(new CardRecord(defId, "TfxOnSecurityCheckDraw", "TfxOnSecurityCheckDraw",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    var reactorId = new HeadlessEntityId($"{owner.Value}:battle:SECREACTOR");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(reactorId, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false, ["dp"] = 1000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, reactorId, ChoiceZone.None, ChoiceZone.BattleArea));
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(context, reactorId, owner);
    return reactorId;
}

// (B6-Dc D1 re-target) Retype `card` to the live OnKnockOut reactor fixture (TfxOnKnockOutDeleteOpponent) and
// register through CardEffectRegistrar, so its OnKnockOut ActivateClass surfaces in GetSkillInfos(OnKnockOut) —
// the surface the BattleResolver KO window reads (NOT the retired EffectRegistry binding). Keeps the instance's
// battle metadata (dp/isSuspended) via the def-only retype (mirrors GivePierce).
void GiveKnockOutDeleteReactor(EngineContext context, HeadlessEntityId card, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:TfxOnKnockOutDeleteOpponent");
    cards.Upsert(new CardRecord(defId, "TfxOnKnockOutDeleteOpponent", "TfxOnKnockOutDeleteOpponent",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(context, card, owner);
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

async Task DeclareTargetAttackAsync(DcgoMatch match)
{
    // (pump re-pin) The targeted (piercing) DeclareAttack legal lane — keep AttackTargetId (targeted, non-direct
    // attack). Drive via ExpApply + ExpDriveUntil to the attack's resolution as the assertions require.
    LegalAction attack = ExpLegal(match, P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await ExpApply(match, attack);
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());
}

// --- Pump harness (Exp* helpers copied verbatim from G3.5-W5) -------------

static IReadOnlyList<LegalAction> ExpLegal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

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

static HeadlessEntityId TopSecurity(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security)[0];

static bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: not in {zone}.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

// (B6-Dc D1 re-target) The former test-local IHeadlessCardEffect probes (AttackerKillingEffect / RecordingFakeEffect,
// registered through the invented EffectRegistry.Register(EffectBinding) surface) are RETIRED — both subtests now
// witness the live card-registered windows (TfxOnSecurityCheckDraw / TfxOnKnockOutDeleteOpponent).
