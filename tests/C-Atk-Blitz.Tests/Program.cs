using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using CE = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
using MPermanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using MCardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;

// C-Atk (Blitz) — RD-CATK-BLITZ RE-JUDGMENT witnesses. AS-IS Blitz is an [On Play] / [When Digivolving]
// ActivateClass (BlitzSelfEffect) collected by the OnEnterFieldAnyone / OnPlay window (GetSkillInfos ->
// MultipleSkills); on accept its BlitzProcess opens an IMMEDIATE effect-driven attack. The wave2 STOP claimed the
// nested EffectDrivenAttack.RequestChoice is rejected by the ChoiceController IsPending guard INSIDE the window;
// these witnesses DISPROVE that end-to-end: the real BlitzSelfEffect drives the window (optional "Will you use
// Blitz?"), the resume runs BlitzProcess which OPENS the effect-attack choice, and resolving it DECLARES an
// attack (AttackController pending) — the SAME DEFERRED trailing-effect-attack flow Vortex/Overclock use, NOT the
// retired AttackPermanentAction memory-pass phase gate. Consumers = 0 (Blitz cards dormant), so all fixtures Tfx.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Printed Blitz fires via the OnEnterFieldAnyone window: optional -> resume -> nested effect-attack -> declared attack (single fire)", PrintedBlitzFiresViaWindow),
    ("Declining the window Blitz optional opens no effect-attack (no attack declared)", DecliningBlitzLeavesNoAttack),
    ("Granted Blitz (GainBlitz) is visible via the permanent-in-play OnEnterFieldAnyone scan and fires via the window", GrantedBlitzVisibleAndFires),
};

int failed = 0;
foreach (var (name, body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}\n  {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failed > 0) { Console.Error.WriteLine($"\n{failed} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PrintedBlitzFiresViaWindow()
{
    EngineContext ctx = NewCtx();
    using var scope = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Initialize(-1); // opponent-side memory (>=1 for them) -> CanActivateBlitz true on P1's turn

    var attacker = await Place(ctx, P1, "BLITZ1", suspended: false);
    await Place(ctx, P2, "FOE1", suspended: true);

    var src = new MCardSource(ctx, attacker, P1, P1);
    CardEffectRegistrar.RegisterOnEnterPlay(ctx, new TfxBlitzSelf(), src.CardNumber, src);

    // Collection proof: the real BlitzEffect ActivateClass is collected by the OnEnterFieldAnyone window
    // (GetSkillInfos 5-region scan) once the OnPlay hashtable matches (CanTriggerOnPlay).
    Hashtable onPlay = BuildOnPlayHashtable(ctx, attacker, P1);
    var collected = AutoProcessing.GetSkillInfos(onPlay, EffectTiming.OnEnterFieldAnyone);
    AssertEqual(1, collected.Count, "the OnEnterFieldAnyone window collects the printed Blitz ActivateClass");

    var (msgs, attackDeclared) = await DriveOnEnterWindow(ctx, onPlay, answerYes: true);

    AssertTrue(msgs.Any(m => m.Contains("OptionalEffect") && m.Contains("Blitz")),
        "the window opened the AS-IS optional Blitz prompt (\"Will you use Blitz?\")");
    AssertEqual(1, msgs.Count(m => m.Contains("EffectAttack")),
        "BlitzProcess opened the effect-attack choice EXACTLY once from inside the window resolution (wave2 STOP disproven)");
    AssertTrue(attackDeclared,
        "resolving the effect-attack DECLARED an attack (AttackController pending) — window path fires end-to-end");
}

async Task DecliningBlitzLeavesNoAttack()
{
    EngineContext ctx = NewCtx();
    using var scope = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Initialize(-1);

    var attacker = await Place(ctx, P1, "BLITZ2", suspended: false);
    await Place(ctx, P2, "FOE2", suspended: true);
    var src = new MCardSource(ctx, attacker, P1, P1);
    CardEffectRegistrar.RegisterOnEnterPlay(ctx, new TfxBlitzSelf(), src.CardNumber, src);

    Hashtable onPlay = BuildOnPlayHashtable(ctx, attacker, P1);
    var (msgs, attackDeclared) = await DriveOnEnterWindow(ctx, onPlay, answerYes: false);

    AssertTrue(msgs.Any(m => m.Contains("OptionalEffect") && m.Contains("Blitz")),
        "the window still OFFERED the optional Blitz (fires via the window)");
    AssertEqual(0, msgs.Count(m => m.Contains("EffectAttack")),
        "declining the optional means BlitzProcess never opened an effect-attack");
    AssertTrue(!attackDeclared, "no attack was declared when the Blitz optional was declined");
}

async Task GrantedBlitzVisibleAndFires()
{
    EngineContext ctx = NewCtx();
    using var scope = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Initialize(-1);

    // A plain attacker with NO printed Blitz + a granter card.
    var attacker = await Place(ctx, P1, "PLAIN", suspended: false);
    var granter = await Place(ctx, P1, "GRANTER", suspended: false);
    await Place(ctx, P2, "FOE3", suspended: true);

    AssertTrue(!new MPermanent(ctx, attacker, P1).HasBlitz, "no printed Blitz before the grant");

    // Grant [Blitz] to the attacker via the AS-IS-signature GainBlitz (AddEffectToPermanent OnEnterFieldAnyone bucket).
    var granterSource = new MCardSource(ctx, granter, P1, P1);
    var grantEffect = new ActivateClass();
    grantEffect.SetUpICardEffect("grant-blitz", _ => true, granterSource);
    var targetPermanent = new MPermanent(ctx, attacker, P1);
    await CE.GainBlitz(targetPermanent, EffectDuration.UntilOwnerTurnEnd, grantEffect, isWhenDigivolving: false);

    // The permanent-in-play scan (Permanent.HasBlitz over EffectList(OnEnterFieldAnyone) incl. EffectList_Added) sees it.
    AssertTrue(new MPermanent(ctx, attacker, P1).HasBlitz,
        "the granted Blitz is visible via the permanent-in-play OnEnterFieldAnyone scan (EffectList_Added bucket)");

    // And it fires through the SAME window.
    Hashtable onPlay = BuildOnPlayHashtable(ctx, attacker, P1);
    var (msgs, attackDeclared) = await DriveOnEnterWindow(ctx, onPlay, answerYes: true);
    AssertTrue(msgs.Any(m => m.Contains("OptionalEffect") && m.Contains("Blitz")),
        "the granted Blitz fires via the OnEnterFieldAnyone window optional");
    AssertEqual(1, msgs.Count(m => m.Contains("EffectAttack")),
        "the granted Blitz opened the effect-attack choice exactly once");
    AssertTrue(attackDeclared, "the granted Blitz declared an attack (window path, not the retired phase gate)");
}

// --- harness -------------------------------------------------------------

EngineContext NewCtx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

// Drives the OnEnterFieldAnyone window (AS-IS EndTurnProcess-shape: StackSkillInfos + AutoProcessCheck, then
// resume) with the standard external driver: answer the optional (ChoiceController.ResolveChoice + resume), and
// resolve the trailing effect-attack (EffectDrivenAttack.ResolveChoice). Returns the pending prompts seen and
// whether an attack was declared.
async Task<(List<string> Msgs, bool AttackDeclared)> DriveOnEnterWindow(EngineContext ctx, Hashtable onPlay, bool answerYes)
{
    var msgs = new List<string>();
    bool attackDeclared = false;
    AutoProcessing ap = AutoProcessing.For(ctx);
    for (int guard = 0; guard < 30; guard++)
    {
        bool parked = false;
        try
        {
            if (guard == 0) { await ap.StackSkillInfos(onPlay, EffectTiming.OnEnterFieldAnyone); await ap.AutoProcessCheck(); }
            else await ap.ResumeSuspendedWindowsAsync();
        }
        catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { parked = true; }

        if (ctx.ChoiceController.Current.IsPending)
        {
            var req = ctx.ChoiceController.PendingRequest!;
            msgs.Add($"[{req.Type}] \"{req.Message}\"");
            var pick = req.Candidates.FirstOrDefault(c => c.IsSelectable);

            if (req.Type == ChoiceType.EffectAttack)
            {
                EffectDrivenAttack.ResolveChoice(ctx, pick is not null ? ChoiceResult.Select(pick.Id) : ChoiceResult.Skip());
                attackDeclared |= ctx.AttackController.Current.IsPending;
                break;
            }

            // Optional prompt: a select answers YES, a skip answers NO.
            ChoiceResult answer = answerYes && pick is not null ? ChoiceResult.Select(pick.Id) : ChoiceResult.Skip();
            ctx.ChoiceController.ResolveChoice(answer);
            continue;
        }
        if (!parked) break;
    }
    return (msgs, attackDeclared);
}

Hashtable BuildOnPlayHashtable(EngineContext ctx, HeadlessEntityId attackerId, HeadlessPlayerId owner)
{
    // AS-IS OnEnterField hashtable: { "hashtables": [ { "Permanent": <permanent whose cardSources contains the
    // played card> } ], isEvolution=false }. CanTriggerOnEnterField walks this to match the card (CanTriggerOnPlay).
    var permanent = new MPermanent(ctx, attackerId, owner);
    var inner = new Hashtable { ["Permanent"] = permanent };
    return new Hashtable { ["hashtables"] = new List<Hashtable> { inner }, ["isEvolution"] = false };
}

async Task<HeadlessEntityId> Place(EngineContext c, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)c.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, $"DEF:{owner.Value}:{tag}", tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    c.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = suspended }));
    await c.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

void AssertTrue(bool cond, string what) { if (!cond) throw new Exception($"expected true: {what}"); }
void AssertEqual(int expected, int actual, string what) { if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}"); }

// Returns the REAL BlitzSelfEffect at OnEnterFieldAnyone (the printed-card shape, AS-IS BT6_014).
internal sealed class TfxBlitzSelf : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(CardEffectFactory.BlitzSelfEffect(isInheritedEffect: false, card: card, condition: null, isWhenDigivolving: false));
        }
        return effects;
    }
}
