// C-EoT-2 witness — <Vortex>/<Overclock> end-of-turn firing RE-HOUSED to the AS-IS OnEndTurn window.
//
// CONTEXT (keyword_rehoming_design_2026-07-15.md §2 C-EoT / §5 W-EoTFIX): the live <Vortex>/<Overclock> attack
// used to fire through the INVENTED EndOfTurnEffectAttack gate (ContinuousKeywordGate marker). W-EoTFIX proved
// the OnEndTurn drain (AutoProcessing.StackSkillInfos(OnEndTurn) + AutoProcessCheck == AS-IS EndTurnProcess:699)
// resolves EVERY collected OnEndTurn scope through the mirror MultipleSkills window. This batch wires the
// keyword to that window:
//   * PRINTED   — the card's CardEffects(OnEndTurn) returns CardEffectFactory.VortexSelfEffect /
//                 OverclockSelfEffect (already the AS-IS shape for EX8_074; fixtures TfxVortex/TfxOverclock).
//   * GRANTED   — CardEffectCommons.GainVortex / GainOverclock store a VortexEffect/OverclockEffect ActivateClass
//                 in the target's OnEndTurn duration bucket (AS-IS 1:1, W3 live), collected by GetSkillInfos.
// and RETIRES the gate's <Vortex>/<Overclock> firing-half (EndOfTurnEffectAttack.TryOpen is <Execute>-only now).
//
// These witnesses assert SINGLE-FIRE (window XOR gate): the window opens the AS-IS optional "Will you use
// Vortex/Overclock?" (StackedSkillInfos -> MultipleSkills -> Vortex/OverclockProcess), AND the retired gate
// (EndOfTurnEffectAttack.TryOpen) returns FALSE for a Vortex/Overclock Digimon. Control groups (no keyword)
// open nothing (false-green guard). The granted path also asserts the AS-IS EoT bucket-reset ORDER (fire, THEN
// reset — a reset before the drain would drop the effect).
//
// Harness: EngineContext.CreateDefault + TurnController.Initialize(P1,P2) + SetPhase(Main) so DoneStartGame is
// true (F4), under AmbientMatchContext.Enter (PRIM-P0 / W1b pattern). Deferred provider so the window's agent
// choices PARK and are observable; well-formed permanents only (avoids the baseline CEntity_EffectController:88
// null-TopCard NRE).

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
using ASSkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Vortex PRINTED: the OnEndTurn window opens \"Will you use Vortex?\" -> VortexProcess attack target select", VortexPrintedFiresThroughWindow),
    ("Vortex: the retired gate (EndOfTurnEffectAttack.TryOpen) does NOT open for a Vortex Digimon (single-fire)", VortexGateRetired),
    ("Vortex GRANTED: GainVortex stores an OnEndTurn bucket effect that fires through the window", VortexGrantedFiresThroughWindow),
    ("Vortex GRANTED: fires THEN the per-duration bucket reset stops a re-fire (AS-IS order)", VortexGrantedBucketResetOrder),
    ("Vortex CONTROL: a plain Digimon (no Vortex) opens no OnEndTurn window (false-green guard)", VortexControlNoWindow),
    ("Overclock PRINTED: the OnEndTurn window opens \"Will you use Overclock?\" -> OverclockProcess ally select", OverclockPrintedFiresThroughWindow),
    ("Overclock: the retired gate does NOT open for an Overclock Digimon (single-fire)", OverclockGateRetired),
    ("Overclock GRANTED: GainOverclock stores an OnEndTurn bucket effect that fires through the window", OverclockGrantedFiresThroughWindow),
    ("Overclock CONTROL: a plain Digimon (no Overclock) opens no OnEndTurn window (false-green guard)", OverclockControlNoWindow),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---------------------------------------------------------------------------------------------------------
// VORTEX
// ---------------------------------------------------------------------------------------------------------

async Task VortexPrintedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);

    var vortex = await Place(context, P1, "TfxVortex", suspended: false, trait: null);
    var foe = await Place(context, P2, "FOE", suspended: true, trait: null);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    // Collection proof: the OnEndTurn window collects the printed Vortex ActivateClass (GetSkillInfos scan).
    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "the OnEndTurn window collects the printed Vortex ActivateClass");

    // Drive the drain: the window opens the AS-IS optional "Will you use Vortex?" (MultipleSkills, NOT the gate).
    await DriveWindow(context);
    AssertTrue(context.ChoiceController.Current.IsPending, "the window suspended on an agent choice");
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the window opened the AS-IS Vortex optional (MultipleSkills)");
    AssertTrue(opt.Message.Contains("Vortex", StringComparison.Ordinal), "the optional names Vortex");

    // Answer "yes" and resume -> VortexProcess -> SelectAttackEffect target select, with the opponent Digimon
    // offered (AS-IS defenderCondition _ => true + SetIsVortex).
    ChoiceRequest attack = await AnswerYesAndResume(context, opt);
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "VortexProcess offered the opponent Digimon as an attack target");
}

async Task VortexGateRetired()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false, trait: null);
    await Place(context, P2, "FOE", suspended: false, trait: null);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, vortex, ContinuousKeywordGate.Vortex),
        "the Vortex presence marker is still live (only the gate FIRING is retired)");
    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1),
        "the retired gate opens NO window for a Vortex Digimon (single-fire: window only)");
}

async Task VortexGrantedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN", suspended: false, trait: null);
    var foe = await Place(context, P2, "FOE", suspended: true, trait: null);
    CardEffectRegistrar.RegisterCard(context, host, P1);

    GrantVortex(context, host, EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "GainVortex stored a Vortex ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    await DriveWindow(context);
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the granted Vortex opens the optional through the window");
    ChoiceRequest attack = await AnswerYesAndResume(context, opt);
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the granted VortexProcess offered the opponent Digimon");
}

async Task VortexGrantedBucketResetOrder()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN", suspended: false, trait: null);
    await Place(context, P2, "FOE", suspended: true, trait: null);
    CardEffectRegistrar.RegisterCard(context, host, P1);

    GrantVortex(context, host, EffectDuration.UntilOwnerTurnEnd);

    // FIRE first: the drain resolves the bucket effect (window opens the optional) BEFORE any reset.
    await DriveWindow(context);
    AssertEqual(ChoiceType.OptionalEffect, context.ChoiceController.PendingRequest!.Type,
        "the granted effect fired through the window (before any bucket reset)");

    // AS-IS per-duration bucket reset (HeadlessEndTurnCleanupFlow) — a reset BEFORE firing would have dropped it.
    new Permanent(context, host).UntilOwnerTurnEndEffects.Clear();
    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "after the per-duration bucket reset the granted Vortex is gone (no re-fire next turn)");
}

async Task VortexControlNoWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var plain = await Place(context, P1, "PLAIN", suspended: false, trait: null);
    await Place(context, P2, "FOE", suspended: false, trait: null);
    CardEffectRegistrar.RegisterCard(context, plain, P1);

    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "a plain Digimon surfaces no OnEndTurn effect");
    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending, "no window opens for a plain Digimon (false-green guard)");
}

// ---------------------------------------------------------------------------------------------------------
// OVERCLOCK
// ---------------------------------------------------------------------------------------------------------

async Task OverclockPrintedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var oc = await Place(context, P1, "TfxOverclock", suspended: false, trait: null);
    var ally = await Place(context, P1, "PUPPETALLY", suspended: false, trait: "Puppet");
    await Place(context, P2, "FOE", suspended: false, trait: null);
    CardEffectRegistrar.RegisterCard(context, oc, P1);

    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "the OnEndTurn window collects the printed Overclock ActivateClass");

    await DriveWindow(context);
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the window opened the AS-IS Overclock optional (MultipleSkills)");
    AssertTrue(opt.Message.Contains("Overclock", StringComparison.Ordinal), "the optional names Overclock");

    // Answer "yes" -> OverclockProcess -> SelectPermanent "delete a trait/token ally", offering the Puppet ally.
    ChoiceRequest select = await AnswerYesAndResume(context, opt);
    AssertTrue(select.Candidates.Any(c => c.Id == ally || c.Label.Contains(ally.Value, StringComparison.Ordinal)),
        "OverclockProcess offered the trait ally to delete");
}

async Task OverclockGateRetired()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var oc = await Place(context, P1, "TfxOverclock", suspended: false, trait: null);
    await Place(context, P1, "PUPPETALLY", suspended: false, trait: "Puppet");
    CardEffectRegistrar.RegisterCard(context, oc, P1);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, oc, ContinuousKeywordGate.Overclock),
        "the Overclock presence marker is still live (only the gate FIRING is retired)");
    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1),
        "the retired gate opens NO window for an Overclock Digimon (single-fire: window only)");
}

async Task OverclockGrantedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN", suspended: false, trait: null);
    await Place(context, P1, "PUPPETALLY", suspended: false, trait: "Puppet");
    await Place(context, P2, "FOE", suspended: false, trait: null);
    CardEffectRegistrar.RegisterCard(context, host, P1);

    GrantOverclock(context, host, "Puppet", EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "GainOverclock stored an Overclock ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    await DriveWindow(context);
    AssertEqual(ChoiceType.OptionalEffect, context.ChoiceController.PendingRequest!.Type,
        "the granted Overclock opens the optional through the window");
}

async Task OverclockControlNoWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var plain = await Place(context, P1, "PLAIN", suspended: false, trait: null);
    await Place(context, P1, "PUPPETALLY", suspended: false, trait: "Puppet");
    CardEffectRegistrar.RegisterCard(context, plain, P1);

    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "a plain Digimon surfaces no OnEndTurn effect");
    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending, "no window opens for a plain Digimon (false-green guard)");
}

// --- Harness -----------------------------------------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1); // turn player = owner = P1
    ctx.TurnController.SetPhase(HeadlessPhase.Main);      // past Setup -> DoneStartGame true
    return ctx;
}

// Drive the AS-IS EndTurnProcess:699-702 OnEndTurn window step; a parked agent choice unwinds via the deferred
// provider (WindowChoice/DeferredChoice pending), leaving the pending choice on the controller.
async Task DriveWindow(EngineContext context)
{
    AutoProcessing ap = AutoProcessing.For(context);
    try
    {
        await ap.StackSkillInfos(null, EffectTiming.OnEndTurn);
        await ap.AutoProcessCheck();
    }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* parked */ }
}

// Answer the pending optional "yes" (select its candidate), resume the suspended MultipleSkills chain, and return
// the NEXT pending choice (VortexProcess's attack target select / OverclockProcess's ally select).
async Task<ChoiceRequest> AnswerYesAndResume(EngineContext context, ChoiceRequest optional)
{
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(optional.Candidates[0].Id));
    AutoProcessing ap = AutoProcessing.For(context);
    try { await ap.ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked on the next choice */ }
    AssertTrue(context.ChoiceController.Current.IsPending, "answering the optional 'yes' opened the effect's own choice");
    return context.ChoiceController.PendingRequest!;
}

void GrantVortex(EngineContext context, HeadlessEntityId hostId, EffectDuration duration)
{
    ICardEffect source = GrantSource(context, hostId, "GrantVortex");
    CardEffectCommons.GainVortex(new Permanent(context, hostId), duration, source).GetAwaiter().GetResult();
}

void GrantOverclock(EngineContext context, HeadlessEntityId hostId, string trait, EffectDuration duration)
{
    ICardEffect source = GrantSource(context, hostId, "GrantOverclock");
    CardEffectCommons.GainOverclock(trait, new Permanent(context, hostId), duration, source).GetAwaiter().GetResult();
}

// A grant-source ICardEffect whose EffectSourceCard is the host's own top card (AS-IS: the granted keyword's
// source is the target permanent — GainVortex passes targetPermanent.TopCard to VortexEffect).
ICardEffect GrantSource(EngineContext context, HeadlessEntityId hostId, string name)
{
    var host = new CardSource(context, hostId, P1, P1);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(name, _ => true, host);
    return ac;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num, bool suspended, string? trait)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    if (trait != null) defMeta["traits"] = trait;
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended };
    if (trait != null) meta["traits"] = trait;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
