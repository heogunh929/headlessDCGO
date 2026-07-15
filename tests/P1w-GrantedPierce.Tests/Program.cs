using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using CE = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
using MPermanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using ET = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.EffectTiming;
using Bt1091 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red.BT1_091;

// P1w-GrantedPierce (wave2 adversarial P1-1 remediation): a DIRECT witness that a BT1_091-GRANTED <Piercing>
// actually FIRES via the getter path, NOT just via suite-regression drift.
//
// AS-IS anchor (DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_091.cs): BT1_091 is an Option whose [Main] grants
// "1 of your Digimon gains <Piercing> ... for the turn" — its SelectPermanentCoroutine calls
//   CardEffectCommons.GainPierce(permanent, EffectDuration.UntilEachTurnEnd, activateClass)
// where `activateClass` is BT1_091's own printed OptionSkill ActivateClass. Piercing text (AS-IS BT1_091.cs:24):
//   "When this Digimon attacks and deletes an opponent's Digimon and survives the battle, it performs any
//    security checks it normally would."
// So a granted-Piercing attacker that deletes an opponent Digimon in battle and SURVIVES performs the follow-up
// security check that a plain Digimon battle would otherwise skip.
//
// The C-Btl rehousing rewired CardEffectCommons.GainPierce (KeyWordEffects/Pierce.cs:76) off the invented
// GainKeywordToPermanent funnel (a dead ContinuousKeywordGate.Piercing marker that neither the getter nor the
// window ever read) and onto the AS-IS 1:1 path:
//   CardEffectFactory.PierceEffect ActivateClass -> AddEffectToPermanent(OnDetermineDoSecurityCheck) W3 bucket.
// Firing chain the review identified (Pierce.cs / Permanent.cs / HashtableSetting.cs / BattleResolver.cs):
//   bucket load -> Permanent.HasPierce (Permanent.cs:933) scans EffectList(OnDetermineDoSecurityCheck) ->
//   PierceCheckHashtableOfPermanent (HashtableSetting.cs:19) self-builds the synthetic IBattle + battle
//   hashtable -> CanTriggerPierce passes -> BattleResolver.cs:278 (`|| new Permanent(...).HasPierce`) sets
//   triggersPiercingSecurityCheck -> AttackProcess.cs:512 runs the follow-up security check.
// This witness exercises the `.HasPierce` DISJUNCT (NOT the invented hasPiercing metadata flag that
// G3.5-D1.PiercingSecurityBattle drives): the grant is BT1_091's real printed ActivateClass via GainPierce, and
// the fire is observed on the LIVE battle path (full DcgoMatch pipeline).
//
// Scenario note: a Digimon-vs-Digimon TARGET attack is used (attacker deletes the defending Digimon and
// survives) — the exact Piercing trigger. The canonical "attack the player -> blocked -> win" shape reaches the
// IDENTICAL firing point but drives the block-candidate scan (BlockTiming.GetBlockerCandidates ->
// Permanent.HasCollision), which currently trips the documented full-match NRE at CEntity_EffectController.cs:88
// (keyword_rehoming_design_2026-07-15.md §5 W-EoTFIX: "가드 발명 금지, 원인 셋업 추적 필요"; the same artifact
// fails G3.5-005's own BlockedAttack test in the baseline). This is a test-only witness, so it takes the
// equivalent block-free Piercing path rather than fighting an engine-side artifact.

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender / security side
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId Bt1091Id = new("p1:main:091:P1-BT1_091");

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) BT1_091-granted Pierce: attacker deletes the defending Digimon and survives -> the security check CONTINUES (real fire)", GrantedPierceContinuesSecurityCheck),
    ("(b) control (no grant): the same battle win does NOT check security (false-green guard)", NoGrantNoSecurityCheck),
    ("(c) duration: after the UntilEachTurnEnd turn boundary the grant expires -> Pierce does NOT fire", ExpiredGrantDoesNotFire),
};

var failures = new List<string>();
foreach (var (name, body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add(name); Console.Error.WriteLine($"FAIL {name}\n  {ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task GrantedPierceContinuesSecurityCheck()
{
    DcgoMatch match = await Setup();
    HeadlessEntityId topSecurity = TopSecurity(match, P2);
    int securityBefore = SecurityCount(match, P2);

    // Grant [Piercing] to the attacker via BT1_091's real printed ActivateClass (UntilEachTurnEnd), and confirm
    // the granted-keyword getter now sees it (the C-Btl rewire made this getter path live).
    using (AmbientMatchContext.Enter(match.Context))
    {
        AssertTrue(!new MPermanent(match.Context, AttackerId, P1).HasPierce, "no printed/granted Pierce before the grant");
        await GrantPierceFromBt1091(match, AttackerId);
        AssertTrue(new MPermanent(match.Context, AttackerId, P1).HasPierce,
            "the BT1_091-granted Pierce is visible via Permanent.HasPierce (OnDetermineDoSecurityCheck bucket scan)");
    }

    await DriveTargetAttack(match);

    // The battle was won: the defending Digimon is deleted and the attacker survived.
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "the defending Digimon was deleted by the winning attacker");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "the attacker survived the battle");
    // Granted Pierce FIRED: the follow-up security check ran, checking the top security card into the trash.
    AssertInZone(match, P2, ChoiceZone.Trash, topSecurity, "granted Pierce fired: the top security card was checked into trash");
    AssertEqual(securityBefore - 1, SecurityCount(match, P2), "exactly one security card was checked by the piercing follow-up");
}

async Task NoGrantNoSecurityCheck()
{
    DcgoMatch match = await Setup();
    HeadlessEntityId topSecurity = TopSecurity(match, P2);
    int securityBefore = SecurityCount(match, P2);

    // No grant at all — nothing to fire.
    using (AmbientMatchContext.Enter(match.Context))
    {
        AssertTrue(!new MPermanent(match.Context, AttackerId, P1).HasPierce, "no Pierce on the ungranted attacker");
    }

    await DriveTargetAttack(match);

    // Same battle win — but with NO Piercing the Digimon battle performs NO security check.
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "the defending Digimon was still deleted by the winning attacker");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "the attacker still survived the battle");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, topSecurity), "no Pierce -> the top security card was NOT checked");
    AssertEqual(securityBefore, SecurityCount(match, P2), "no Pierce -> the security stack is unchanged (false-green guard)");
}

async Task ExpiredGrantDoesNotFire()
{
    DcgoMatch match = await Setup();
    HeadlessEntityId topSecurity = TopSecurity(match, P2);
    int securityBefore = SecurityCount(match, P2);

    using (AmbientMatchContext.Enter(match.Context))
    {
        await GrantPierceFromBt1091(match, AttackerId);
        AssertTrue(new MPermanent(match.Context, AttackerId, P1).HasPierce, "granted before the turn boundary");
    }

    // AS-IS BT1_091 duration = EffectDuration.UntilEachTurnEnd. The turn-end cleanup (the engine's real
    // HeadlessEndTurnCleanupFlow) drops every field permanent's UntilEachTurnEnd bucket
    // (HeadlessEndTurnCleanupFlow.cs:132) -> the grant expires.
    new HeadlessEndTurnCleanupFlow().Cleanup(match.Context, match.Context.TurnController.Current);

    using (AmbientMatchContext.Enter(match.Context))
    {
        AssertTrue(!new MPermanent(match.Context, AttackerId, P1).HasPierce,
            "after the UntilEachTurnEnd boundary the granted Pierce expired (getter is false again)");
    }

    await DriveTargetAttack(match);

    // Expired grant -> the battle win performs NO security check (same outcome as the ungranted control).
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "the defending Digimon was still deleted");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "the attacker survived");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, topSecurity), "expired Pierce -> the top security card was NOT checked");
    AssertEqual(securityBefore, SecurityCount(match, P2), "expired Pierce -> the security stack is unchanged");
}

// --- Grant (BT1_091's real printed ActivateClass) ------------------------

// Faithful to BT1_091's SelectPermanentCoroutine: GainPierce(permanent, UntilEachTurnEnd, <BT1_091 ActivateClass>).
async Task GrantPierceFromBt1091(DcgoMatch match, HeadlessEntityId attackerId)
{
    var bt1091Source = new CardSource(match.Context, Bt1091Id, P1);
    // BT1_091.CardEffects(OptionSkill, ...) returns exactly its printed OptionSkill ActivateClass (the coroutine
    // is NOT executed here — only the ActivateClass object is used as the grant's root card effect, as AS-IS).
    var grantEffect = new Bt1091().CardEffects(ET.OptionSkill, bt1091Source).Single();
    var attackerPermanent = new MPermanent(match.Context, attackerId, P1);
    await CE.GainPierce(attackerPermanent, EffectDuration.UntilEachTurnEnd, grantEffect);
}

// --- Harness (DcgoMatch full match; direct Digimon battle + piercing security per G3.5-D1) ---------------

async Task<DcgoMatch> Setup()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }
    // BT1_091 card record (Option) so the grant's CardSource resolves its identity.
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:P1:BT1_091"), "BT1_091", "BT1_091",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);

    // Attacker (P1, 9000) beats both the defending Digimon (3000) and the revealed top security (2000), so it
    // survives the field battle AND the piercing security battle.
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    // The BT1_091 grant source lives in P1's hand (an Option — its zone is immaterial to the grant).
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Bt1091Id, new HeadlessEntityId("DEF:P1:BT1_091"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Bt1091Id, ChoiceZone.None, ChoiceZone.Hand));

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = 9000 });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = 3000 });
    // The piercing follow-up reveals P2's TOP security first — make it weaker than the attacker.
    SetMetadata(match, TopSecurity(match, P2), new Dictionary<string, object?> { ["dp"] = 2000 });
    return match;
}

// Drive a REAL target attack through the full pipeline: declare the Digimon-vs-Digimon attack and let the
// battle + (when Piercing fires) the follow-up security check complete.
async Task DriveTargetAttack(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, TargetId, isDirectAttack: false);
    await match.StepAsync();

    AssertFalse(match.HasPendingChoice(), "the attack completed with no pending choice");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after the battle (+ piercing security)");
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

static int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

static bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: not in {zone}.");
}

void AssertEqual<T>(T expected, T actual, string what)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{what}: expected {expected}, got {actual}");
}

void AssertTrue(bool cond, string what) { if (!cond) throw new Exception($"expected true: {what}"); }
void AssertFalse(bool cond, string what) { if (cond) throw new Exception($"expected false: {what}"); }
