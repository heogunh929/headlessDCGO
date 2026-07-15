using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using CE = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
using MPermanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;

// C-Atk (Raid): witness that [Raid] fires SOLELY via the AS-IS OnAllyAttack window
// (StackSkillInfos -> MultipleSkills -> RaidProcess), NOT the retired invented RaidAttackSwitch gate. AS-IS
// RaidSelfEffect (an optional OnAllyAttack ActivateClass) -> CanActivateRaid -> RaidProcess -> SwitchDefender.
// The retirement removed the counter-head RaidAttackSwitch.RequestChoice firing-half; keeping it double-fired
// (Permanent.HasRaid reads the SAME ActivateClass the window collects). The grant witness covers that a
// GainRaid-granted [Raid] (AddEffectToPermanent OnAllyAttack bucket) is visible via the permanent-in-play scan.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Printed Raid fires via the OnAllyAttack window (MultipleSkills), not the retired gate; switch offered", PrintedRaidFiresViaWindow),
    ("Declining the window Raid prompt leaves the attack unchanged (no gate re-offer)", DecliningRaidLeavesAttack),
    ("Granted Raid (GainRaid) is visible via the permanent-in-play OnAllyAttack scan and fires via the window", GrantedRaidVisibleAndFires),
};

int failed = 0;
foreach (var (name, body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}\n  {ex.GetType().Name}: {ex.Message}"); }
}
if (failed > 0) { Console.Error.WriteLine($"\n{failed} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PrintedRaidFiresViaWindow()
{
    EngineContext ctx = NewCtx();
    var attacker = await Place(ctx, P1, "TfxRaidOnly", "RAID", "Digimon", dp: 3000);
    var high = await Place(ctx, P2, "FOE1", "FOE1", "Digimon", dp: 9000);
    var defender = await Place(ctx, P2, "FOE2", "FOE2", "Digimon", dp: 1000);

    using var scope = AmbientMatchContext.Enter(ctx);
    var msgs = await DriveAttack(ctx, attacker, defender, answerYes: true);

    AssertTrue(msgs.Any(m => m.Contains("Will you use \"Raid\"")),
        "the OnAllyAttack window opened the AS-IS optional Raid prompt (MultipleSkills OptionalSkill)");
    int switchOffers = msgs.Count(m => m.Contains("attacks to"));
    AssertEqual(1, switchOffers, "RaidProcess offered the switch EXACTLY once (single fire via the window)");
    AssertTrue(msgs.Any(m => m.Contains("attacks to") && m.Contains(high.Value)),
        "the switch offered the highest-DP unsuspended enemy (FOE1)");
    AssertEqual(0, msgs.Count(m => m.Contains("Raid: switch")),
        "the retired RaidAttackSwitch gate opened NO choice (its firing-half is de-wired)");
}

async Task DecliningRaidLeavesAttack()
{
    EngineContext ctx = NewCtx();
    var attacker = await Place(ctx, P1, "TfxRaidOnly", "RAID", "Digimon", dp: 3000);
    await Place(ctx, P2, "FOE1", "FOE1", "Digimon", dp: 9000);
    var defender = await Place(ctx, P2, "FOE2", "FOE2", "Digimon", dp: 1000);

    using var scope = AmbientMatchContext.Enter(ctx);
    var msgs = await DriveAttack(ctx, attacker, defender, answerYes: false);

    AssertTrue(msgs.Any(m => m.Contains("Will you use \"Raid\"")),
        "the window still OFFERED the optional Raid (fires via the window)");
    AssertEqual(0, msgs.Count(m => m.Contains("attacks to")),
        "declining the optional means RaidProcess never offered a switch");
}

async Task GrantedRaidVisibleAndFires()
{
    EngineContext ctx = NewCtx();
    // A plain attacker with NO printed Raid + a granter card.
    var attacker = await Place(ctx, P1, "PLAIN", "PLAIN", "Digimon", dp: 3000);
    var granter = await Place(ctx, P1, "GRANTER", "GRANTER", "Digimon", dp: 2000);
    var high = await Place(ctx, P2, "FOE1", "FOE1", "Digimon", dp: 9000);
    var defender = await Place(ctx, P2, "FOE2", "FOE2", "Digimon", dp: 1000);

    using var scope = AmbientMatchContext.Enter(ctx);

    // Before the grant: no Raid.
    AssertTrue(!new MPermanent(ctx, attacker, P1).HasRaid, "no printed Raid before the grant");

    // Grant [Raid] to the attacker via the AS-IS-signature GainRaid (AddEffectToPermanent OnAllyAttack bucket).
    var granterSource = new CardSource(ctx, granter, P1);
    var grantEffect = new ActivateClass();
    grantEffect.SetUpICardEffect("grant-raid", _ => true, granterSource);
    var targetPermanent = new MPermanent(ctx, attacker, P1);
    await CE.GainRaid(targetPermanent, EffectDuration.UntilOwnerTurnEnd, grantEffect);

    // The permanent-in-play scan (Permanent.HasRaid over EffectList(OnAllyAttack) incl. EffectList_Added) sees it.
    AssertTrue(new MPermanent(ctx, attacker, P1).HasRaid,
        "the granted Raid is visible via the permanent-in-play OnAllyAttack scan (EffectList_Added bucket)");

    // And it fires through the SAME window.
    var msgs = await DriveAttack(ctx, attacker, defender, answerYes: true);
    AssertTrue(msgs.Any(m => m.Contains("Will you use \"Raid\"")),
        "the granted Raid fires via the OnAllyAttack window");
    AssertTrue(msgs.Any(m => m.Contains("attacks to") && m.Contains(high.Value)),
        "the granted Raid offered the switch to the highest-DP enemy");
    AssertEqual(0, msgs.Count(m => m.Contains("Raid: switch")), "no retired-gate choice");
}

// --- harness -------------------------------------------------------------

EngineContext NewCtx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<List<string>> DriveAttack(EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId? target, bool answerYes)
{
    AttackDeclarationCommons.Declare(ctx, P1, attacker, P2, targetId: target, isDirectAttack: target is null);
    var pipeline = new AttackPipeline();
    var msgs = new List<string>();
    for (int guard = 0; guard < 300; guard++)
    {
        bool progressed = true;
        try { progressed = (await pipeline.AdvanceAsync(ctx)).Progressed; }
        catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }

        if (ctx.ChoiceController.Current.IsPending)
        {
            var req = ctx.ChoiceController.PendingRequest!;
            msgs.Add($"[{req.Type}] \"{req.Message}\" [{string.Join(",", req.Candidates.Where(c => c.IsSelectable).Select(c => c.Id.Value))}]");
            ChoiceResult answer;
            if (!answerYes && req.CanSkip) answer = ChoiceResult.Skip();
            else if (req.Candidates.Count(c => c.IsSelectable) > 0) answer = ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id);
            else answer = ChoiceResult.Skip();
            ctx.ChoiceController.ResolveChoice(answer);
            try { await AutoProcessing.For(ctx).ResumeSuspendedWindowsAsync(); }
            catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }
            continue;
        }
        if (!progressed) break;
    }
    return msgs;
}

async Task<HeadlessEntityId> Place(EngineContext c, HeadlessPlayerId owner, string cardNumber, string tag, string cardType, int dp)
{
    var cards = (CardDatabase)c.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    c.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4, ["isSuspended"] = false }));
    await c.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

void AssertTrue(bool cond, string what) { if (!cond) throw new Exception($"expected true: {what}"); }
void AssertEqual(int expected, int actual, string what) { if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}"); }
