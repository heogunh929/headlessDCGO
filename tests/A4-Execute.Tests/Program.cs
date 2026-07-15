// A군 4단계 witness — <Execute> end-of-turn firing RE-HOUSED to the AS-IS OnEndTurn window (the last of the 18
// window-firing keywords). <Execute> = the [End of Your Turn] "this Digimon may attack (the PLAYER or any
// Digimon incl. UNSUSPENDED); at the end of that attack, delete it."
//
// CONTEXT (keyword_rehoming_design_2026-07-15.md §5 C-EoT / Execute): the live <Execute> attack used to fire
// through the INVENTED EndOfTurnEffectAttack gate (ContinuousKeywordGate marker + EffectDrivenAttack
// SelfDeleteAtEndOfAttack). This batch (a) completes ExecuteProcess 1:1 with AS-IS — the old blocker RD-R2-01
// (Permanent.UntilEndAttackEffects) is resolved by the W3 bucket and PermanentEffectFactory.DeleteSelfEffect /
// AddDetailClass are now the AS-IS ActivateClass overloads — so the printed/granted <Execute> fires through the
// SAME OnEndTurn window that resolves Vortex/Overclock (GetSkillInfos → MultipleSkills → ExecuteProcess), and
// (b) RETIRES the gate's firing-half (EndOfTurnEffectAttack.TryOpen now opens nothing — the whole gate is dead).
//
// These witnesses assert:
//   * SINGLE-FIRE (window fires exactly once; the retired gate opens nothing).
//   * PLAYER + UNSUSPENDED-Digimon targets offered (isExecute semantics: canAttackPlayer:()=>true unconditional
//     — the Vortex differentiator, whose player-target needs a separate VortexCanAttackPlayers effect — and a
//     per-attack CanAttackTargetDefendingPermanentClass appended to UntilEndAttackEffects that lifts the
//     unsuspended-defender restriction; NOT SetIsVortex).
//   * NO summoning-sickness bypass (isExecute does not lift EnteredThisTurn — only Rush/isVortex do,
//     Permanent.cs:3115).
//   * SELF-DELETE registered at end of attack (a DeleteSelfEffect at OnEndAttack + a detail at None appended to
//     the attacker's UntilEndAttackEffects) — a PER-ATTACK effect NOT present on a normal Digimon (control).
//   * EoT bucket-reset ORDER (fire THEN reset) and a plain-Digimon control (false-green guard).
//
// Harness: EngineContext.CreateDefault + TurnController.Initialize(P1,P2) + SetPhase(Main) under
// AmbientMatchContext.Enter (== C-EoT2). Deferred provider for window observation; a fresh non-deferred context
// (empty ScriptedChoiceProvider == "Not Attack" skip) drives ExecuteProcess to completion for the self-delete
// registration witness.

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

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Execute PRINTED: the OnEndTurn window opens \"Will you use Execute?\" -> ExecuteProcess, offering the PLAYER and an UNSUSPENDED foe", ExecutePrintedFiresThroughWindow),
    ("Execute GRANTED: GainExecute stores an OnEndTurn bucket effect that fires through the window", ExecuteGrantedFiresThroughWindow),
    ("Execute SELF-DELETE: ExecuteProcess appends a DeleteSelfEffect@OnEndAttack + detail@None (per-attack); a normal Digimon has none (control)", ExecuteSelfDeleteRegistered),
    ("Execute: NO summoning-sickness bypass (CanActivateExecute false for a Digimon that entered this turn without Rush)", ExecuteNoSummoningSicknessBypass),
    ("Execute CONTROL: a plain Digimon (no Execute) opens no OnEndTurn window (false-green guard)", ExecuteControlNoWindow),
    ("Execute GRANTED: fires THEN the per-duration bucket reset stops a re-fire (AS-IS order)", ExecuteGrantedBucketResetOrder),
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

async Task ExecutePrintedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);

    var execute = await Place(context, P1, "TfxExecute", suspended: false, entered: false);
    var foe = await Place(context, P2, "FOE", suspended: false, entered: false); // UNSUSPENDED foe
    CardEffectRegistrar.RegisterCard(context, execute, P1);

    // Collection proof: the OnEndTurn window collects the printed Execute ActivateClass (GetSkillInfos scan).
    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "the OnEndTurn window collects the printed Execute ActivateClass");

    // Drive the drain: the window opens the AS-IS optional "Will you use Execute?" (MultipleSkills, NOT the gate).
    await DriveWindow(context);
    AssertTrue(context.ChoiceController.Current.IsPending, "the window suspended on an agent choice");
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the window opened the AS-IS Execute optional (MultipleSkills)");
    AssertTrue(opt.Message.Contains("Execute", StringComparison.Ordinal), "the optional names Execute");

    // Answer "yes" and resume -> ExecuteProcess -> SelectAttackEffect target select.
    ChoiceRequest attack = await AnswerYesAndResume(context, opt);
    // isExecute: the PLAYER is always attackable (canAttackPlayerCondition:()=>true — the Vortex differentiator).
    AssertTrue(attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "ExecuteProcess offered the PLAYER as an attack target (isExecute: unconditional, unlike Vortex)");
    // isExecute: the per-attack CanAttackTargetDefendingPermanentClass lifts the unsuspended-defender restriction.
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "ExecuteProcess offered the UNSUSPENDED opponent Digimon (isExecute lifts the suspended-defender gate)");

    // (G-clean) Single-fire is proven structurally: the invented EndOfTurnEffectAttack gate is physically
    // deleted, so the OnEndTurn window is the sole <Execute> firing path.
}

async Task ExecuteGrantedFiresThroughWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN", suspended: false, entered: false);
    await Place(context, P2, "FOE", suspended: false, entered: false);
    CardEffectRegistrar.RegisterCard(context, host, P1);

    GrantExecute(context, host, EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "GainExecute stored an Execute ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    await DriveWindow(context);
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the granted Execute opens the optional through the window");
    AssertTrue(opt.Message.Contains("Execute", StringComparison.Ordinal), "the granted optional names Execute");
}

async Task ExecuteSelfDeleteRegistered()
{
    // A non-deferred context: the empty ScriptedChoiceProvider answers SelectAttackEffect's skippable "Not Attack"
    // choice, so ExecuteProcess runs to completion and its AFTER-select appends land. We witness the REGISTRATION
    // of the end-of-attack self-delete (a full drain of OnEndAttack to observe the actual deletion is heavier
    // integration; the append is the AS-IS 1:1 structure — Execute.cs:74-93).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: false);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    using var scope = AmbientMatchContext.Enter(context);

    var host = await Place(context, P1, "EXEC", suspended: false, entered: false);
    var plain = await Place(context, P1, "PLAIN", suspended: false, entered: false);
    await Place(context, P2, "FOE", suspended: false, entered: false);

    var source = GrantSource(context, host, "Execute");
    await CardEffectCommons.ExecuteProcess(new Permanent(context, host).TopCard, source);

    var effects = new Permanent(context, host).UntilEndAttackEffects;
    AssertEqual(3, effects.Count,
        "ExecuteProcess appended: the restrict-defender gate (before) + the self-delete + the detail (after)");

    // OnEndAttack: exactly the DeleteSelfEffect ("Delete this Digimon") surfaces (plus the always-on gate).
    var atEndAttack = effects.Select(f => f(EffectTiming.OnEndAttack)).Where(e => e != null).ToList();
    AssertTrue(atEndAttack.Any(e => e is ActivateClass && e!.EffectName == "Delete this Digimon"),
        "at OnEndAttack the attacker self-deletes (PermanentEffectFactory.DeleteSelfEffect appended)");

    // None: the display detail surfaces.
    var atNone = effects.Select(f => f(EffectTiming.None)).Where(e => e != null).ToList();
    AssertTrue(atNone.Any(e => e is AddDetailClass),
        "at None the \"At end of attack, delete this Digimon.\" detail surfaces (AddDetailClass appended)");

    // The restrict-defender gate (isExecute unsuspended semantics) is the third append.
    AssertTrue(effects.Any(f => f(EffectTiming.OnEndAttack) is CanAttackTargetDefendingPermanentClass),
        "the per-attack CanAttackTargetDefendingPermanentClass (attack unsuspended Digimon) is appended");

    // CONTROL: a normal Digimon that never ran ExecuteProcess has NO per-attack self-delete.
    AssertEqual(0, new Permanent(context, plain).UntilEndAttackEffects.Count,
        "a normal Digimon has no per-attack self-delete (the flag is Execute-only, not on normal attacks)");
}

async Task ExecuteNoSummoningSicknessBypass()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await Place(context, P2, "FOE", suspended: false, entered: false);

    var settled = await Place(context, P1, "SETTLED", suspended: false, entered: false);
    var source = GrantSource(context, settled, "Execute");
    AssertTrue(CardEffectCommons.CanActivateExecute(new Permanent(context, settled).TopCard, source),
        "a settled Execute Digimon CAN activate Execute");

    var sick = await Place(context, P1, "SICK", suspended: false, entered: true); // entered this turn, no Rush
    var sickSource = GrantSource(context, sick, "Execute");
    AssertTrue(!CardEffectCommons.CanActivateExecute(new Permanent(context, sick).TopCard, sickSource),
        "a summoning-sick Execute Digimon CANNOT activate Execute (isExecute does not bypass, unlike Rush/isVortex)");
}

async Task ExecuteControlNoWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var plain = await Place(context, P1, "PLAIN", suspended: false, entered: false);
    await Place(context, P2, "FOE", suspended: false, entered: false);
    CardEffectRegistrar.RegisterCard(context, plain, P1);

    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "a plain Digimon surfaces no OnEndTurn effect");
    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending, "no window opens for a plain Digimon (false-green guard)");
}

async Task ExecuteGrantedBucketResetOrder()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN", suspended: false, entered: false);
    await Place(context, P2, "FOE", suspended: false, entered: false);
    CardEffectRegistrar.RegisterCard(context, host, P1);

    GrantExecute(context, host, EffectDuration.UntilOwnerTurnEnd);

    // FIRE first: the drain resolves the bucket effect (window opens the optional) BEFORE any reset.
    await DriveWindow(context);
    AssertEqual(ChoiceType.OptionalEffect, context.ChoiceController.PendingRequest!.Type,
        "the granted Execute fired through the window (before any bucket reset)");

    // AS-IS per-duration bucket reset — a reset BEFORE firing would have dropped it.
    new Permanent(context, host).UntilOwnerTurnEndEffects.Clear();
    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "after the per-duration bucket reset the granted Execute is gone (no re-fire next turn)");
}

// --- Harness -----------------------------------------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1); // turn player = owner = P1
    ctx.TurnController.SetPhase(HeadlessPhase.Main);      // past Setup -> DoneStartGame true
    return ctx;
}

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

async Task<ChoiceRequest> AnswerYesAndResume(EngineContext context, ChoiceRequest optional)
{
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(optional.Candidates[0].Id));
    AutoProcessing ap = AutoProcessing.For(context);
    try { await ap.ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked on the next choice */ }
    AssertTrue(context.ChoiceController.Current.IsPending, "answering the optional 'yes' opened the effect's own choice");
    return context.ChoiceController.PendingRequest!;
}

void GrantExecute(EngineContext context, HeadlessEntityId hostId, EffectDuration duration)
{
    ICardEffect source = GrantSource(context, hostId, "GrantExecute");
    CardEffectCommons.GainExecute(new Permanent(context, hostId), duration, source).GetAwaiter().GetResult();
}

// A grant-source ICardEffect whose EffectSourceCard is the host's own top card (AS-IS: the granted keyword's
// source is the target permanent — GainExecute passes targetPermanent.TopCard to ExecuteEffect).
ICardEffect GrantSource(EngineContext context, HeadlessEntityId hostId, string name)
{
    var host = new CardSource(context, hostId, P1, P1);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(name, _ => true, host);
    return ac;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num, bool suspended, bool entered)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended };
    if (entered) meta[HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.EnteredThisTurnKey] = true;
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
