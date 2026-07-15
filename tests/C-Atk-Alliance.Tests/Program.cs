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

// C-Atk (Alliance): witness that [Alliance] fires SOLELY via the AS-IS OnAllyAttack window
// (StackSkillInfos -> MultipleSkills -> AllianceProcess), NOT the retired invented AllianceAttackBoost gate.
// AS-IS AllianceSelfEffect (a NON-optional OnAllyAttack ActivateClass) -> CanActivateAlliance -> AllianceProcess
// ("Select 1 Digimon to suspend."; suspend an ally -> attacker gains its DP + 1 SA UntilEndAttack). The
// retirement removed the counter-head AllianceAttackBoost.RequestChoice firing-half (Permanent.HasAlliance
// reads the SAME ActivateClass the window collects, so keeping it double-fired). The grant witness covers that a
// GainAlliance-granted [Alliance] (AddEffectToPermanent OnAllyAttack bucket) is visible via the in-play scan.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Printed Alliance fires via the OnAllyAttack window (MultipleSkills), not the retired gate; suspend offered", PrintedAllianceFiresViaWindow),
    ("Alliance with no suspendable ally opens no window prompt", AllianceNoAllyNoPrompt),
    ("Granted Alliance (GainAlliance) is visible via the permanent-in-play OnAllyAttack scan and fires via the window", GrantedAllianceVisibleAndFires),
};

int failed = 0;
foreach (var (name, body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}\n  {ex.GetType().Name}: {ex.Message}"); }
}
if (failed > 0) { Console.Error.WriteLine($"\n{failed} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PrintedAllianceFiresViaWindow()
{
    EngineContext ctx = NewCtx();
    var attacker = await Place(ctx, P1, "TfxAllianceOnly", "ALLY", "Digimon", dp: 3000);
    var ally = await Place(ctx, P1, "MATE", "MATE", "Digimon", dp: 5000);   // suspendable ally
    var defender = await Place(ctx, P2, "FOE", "FOE", "Digimon", dp: 1000);

    using var scope = AmbientMatchContext.Enter(ctx);
    var msgs = await DriveAttack(ctx, attacker, defender);

    int suspendOffers = msgs.Count(m => m.Contains("Select 1 Digimon to suspend"));
    AssertEqual(1, suspendOffers, "AllianceProcess offered the suspend-an-ally choice EXACTLY once (single fire via the window)");
    AssertTrue(msgs.Any(m => m.Contains("Select 1 Digimon to suspend") && m.Contains(ally.Value)),
        "the suspend choice offered the unsuspended ally");
    AssertEqual(0, msgs.Count(m => m.Contains("Alliance: suspend an ally")),
        "the retired AllianceAttackBoost gate opened NO choice (its firing-half is de-wired)");
    AssertTrue(IsSuspended(ctx, ally), "the selected ally was suspended (AllianceProcess cost)");
}

async Task AllianceNoAllyNoPrompt()
{
    EngineContext ctx = NewCtx();
    var attacker = await Place(ctx, P1, "TfxAllianceOnly", "ALLY", "Digimon", dp: 3000);
    var defender = await Place(ctx, P2, "FOE", "FOE", "Digimon", dp: 1000);  // no ally to suspend

    using var scope = AmbientMatchContext.Enter(ctx);
    var msgs = await DriveAttack(ctx, attacker, defender);

    AssertEqual(0, msgs.Count(m => m.Contains("Select 1 Digimon to suspend")),
        "with no suspendable ally the window opens no Alliance prompt (CanActivateAlliance false)");
    AssertEqual(0, msgs.Count(m => m.Contains("Alliance: suspend an ally")), "no retired-gate choice either");
}

async Task GrantedAllianceVisibleAndFires()
{
    EngineContext ctx = NewCtx();
    var attacker = await Place(ctx, P1, "PLAIN", "PLAIN", "Digimon", dp: 3000);
    var granter = await Place(ctx, P1, "GRANTER", "GRANTER", "Digimon", dp: 2000);
    var ally = await Place(ctx, P1, "MATE", "MATE", "Digimon", dp: 5000);
    var defender = await Place(ctx, P2, "FOE", "FOE", "Digimon", dp: 1000);

    using var scope = AmbientMatchContext.Enter(ctx);
    AssertTrue(!new MPermanent(ctx, attacker, P1).HasAlliance, "no printed Alliance before the grant");

    var granterSource = new CardSource(ctx, granter, P1);
    var grantEffect = new ActivateClass();
    grantEffect.SetUpICardEffect("grant-alliance", _ => true, granterSource);
    var targetPermanent = new MPermanent(ctx, attacker, P1);
    await CE.GainAlliance(targetPermanent, EffectDuration.UntilOwnerTurnEnd, grantEffect);

    AssertTrue(new MPermanent(ctx, attacker, P1).HasAlliance,
        "the granted Alliance is visible via the permanent-in-play OnAllyAttack scan (EffectList_Added bucket)");

    var msgs = await DriveAttack(ctx, attacker, defender);
    AssertTrue(msgs.Any(m => m.Contains("Select 1 Digimon to suspend") && m.Contains(ally.Value)),
        "the granted Alliance fires via the OnAllyAttack window and offers the ally");
    AssertEqual(0, msgs.Count(m => m.Contains("Alliance: suspend an ally")), "no retired-gate choice");
}

// --- harness -------------------------------------------------------------

EngineContext NewCtx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<List<string>> DriveAttack(EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId? target)
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
            // Always accept: for a suspend choice select the ally, else the first selectable, else skip.
            ChoiceResult answer = req.Candidates.Count(c => c.IsSelectable) > 0
                ? ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id)
                : ChoiceResult.Skip();
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

bool IsSuspended(EngineContext c, HeadlessEntityId id) =>
    c.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

void AssertTrue(bool cond, string what) { if (!cond) throw new Exception($"expected true: {what}"); }
void AssertEqual(int expected, int actual, string what) { if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}"); }
