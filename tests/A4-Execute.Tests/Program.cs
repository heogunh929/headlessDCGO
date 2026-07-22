// A군 4단계 witness — <Execute> end-of-turn firing RE-HOUSED to the AS-IS OnEndTurn window (the last of the 18
// window-firing keywords), RE-TARGETED (4b B1-α) onto the DcgoMatch.CreatePumpDriven pump. <Execute> = the
// [End of Your Turn] "this Digimon may attack (the PLAYER or any Digimon incl. UNSUSPENDED); at the end of that
// attack, delete it."
//
// CONTEXT (keyword_rehoming_design_2026-07-15.md §5 C-EoT / Execute; suite_retarget_4b_design §3.1b B1): the
// live <Execute> attack used to fire through the INVENTED EndOfTurnEffectAttack gate (now physically deleted).
// The printed/granted <Execute> fires through the SAME OnEndTurn window that resolves Vortex/Overclock
// (GetSkillInfos → MultipleSkills → ExecuteProcess). DRIVE (4b): the window is driven by the pump's real
// turn-end — an explicit P1 Pass runs EndPhaseAsync → EndTurnProcess → StackSkillInfos(OnEndTurn) +
// AutoProcessCheck; the AS-IS optional "Will you use Execute?" and its ExecuteProcess SelectAttackEffect surface
// at the AGENT SEAT (PolicyChoiceProvider), OBSERVED by capturing the ChoiceRequest (EXEMPLAR-T1/R4S3b
// precedent; the throw-record-replay unwind is retired). GetSkillInfos(OnEndTurn) collection assertions are the
// retained substrate. The self-delete REGISTRATION and the summoning-sickness gate are sub-mechanic assertions
// (direct ExecuteProcess / CanActivateExecute calls) run on the pump-staged board — they never used the window
// drive or the throw contract.
//
// These witnesses assert:
//   * SINGLE-FIRE (window fires exactly once; the retired gate opens nothing — proven structurally).
//   * PLAYER + UNSUSPENDED-Digimon targets offered (isExecute: canAttackPlayer:()=>true unconditional, and a
//     per-attack CanAttackTargetDefendingPermanentClass lifting the unsuspended-defender restriction).
//   * NO summoning-sickness bypass (isExecute does not lift EnteredThisTurn).
//   * SELF-DELETE registered at end of attack (DeleteSelfEffect@OnEndAttack + detail@None) — a PER-ATTACK effect
//     NOT present on a normal Digimon (control).
//   * EoT bucket-reset (fire THEN the REAL cleanup reset) and a plain-Digimon control (false-green guard).

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var execute = Place(match, P1, "TfxExecute", suspended: false, entered: false);
    var foe = Place(match, P2, "FOE", suspended: false, entered: false); // UNSUSPENDED foe
    Cec.CardEffectRegistrar.RegisterCard(match.Context, execute, P1);

    // Collection proof: the OnEndTurn window collects the printed Execute ActivateClass (GetSkillInfos scan).
    AssertEqual(1, CollectOnEndTurn(match), "the OnEndTurn window collects the printed Execute ActivateClass");

    // Drive the pump turn-end drain: the window opens the AS-IS optional "Will you use Execute?" (MultipleSkills).
    (ChoiceRequest? opt, ChoiceRequest? attack) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is not null, "the pump turn-end drain opened the Execute optional (the window fired)");
    AssertEqual(ChoiceType.OptionalEffect, opt!.Type, "the window opened the AS-IS Execute optional (MultipleSkills)");
    AssertTrue(opt.Message.Contains("Execute", StringComparison.Ordinal), "the optional names Execute");

    // Answering "yes" resumed -> ExecuteProcess -> SelectAttackEffect target select.
    AssertTrue(attack is not null, "answering 'yes' opened ExecuteProcess's own attack target select");
    // isExecute: the PLAYER is always attackable (canAttackPlayerCondition:()=>true — the Vortex differentiator).
    AssertTrue(attack!.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "ExecuteProcess offered the PLAYER as an attack target (isExecute: unconditional, unlike Vortex)");
    // isExecute: the per-attack CanAttackTargetDefendingPermanentClass lifts the unsuspended-defender restriction.
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "ExecuteProcess offered the UNSUSPENDED opponent Digimon (isExecute lifts the suspended-defender gate)");

    // (G-clean) Single-fire is proven structurally: the invented EndOfTurnEffectAttack gate is physically
    // deleted, so the OnEndTurn window is the sole <Execute> firing path.
}

async Task ExecuteGrantedFiresThroughWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var host = Place(match, P1, "PLAIN", suspended: false, entered: false);
    Place(match, P2, "FOE", suspended: false, entered: false);
    Cec.CardEffectRegistrar.RegisterCard(match.Context, host, P1);
    GrantExecute(match, host, EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, CollectOnEndTurn(match),
        "GainExecute stored an Execute ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type, "the granted Execute opens the optional through the window");
    AssertTrue(opt!.Message.Contains("Execute", StringComparison.Ordinal), "the granted optional names Execute");
}

async Task ExecuteSelfDeleteRegistered()
{
    // Sub-mechanic (no window drive, no throw contract): run ExecuteProcess DIRECTLY on the pump-staged board and
    // witness the REGISTRATION of the end-of-attack self-delete. The empty ScriptedChoiceProvider fallback answers
    // SelectAttackEffect's skippable "Not Attack" choice, so ExecuteProcess runs to completion and its AFTER-select
    // appends land (a full drain of OnEndAttack to observe the actual deletion is heavier integration; the append
    // is the AS-IS 1:1 structure — Execute.cs:74-93).
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    var host = Place(match, P1, "EXEC", suspended: false, entered: false);
    var plain = Place(match, P1, "PLAIN", suspended: false, entered: false);
    Place(match, P2, "FOE", suspended: false, entered: false);

    using var scope = AmbientMatchContext.Enter(match.Context);
    var source = GrantSource(match, host, "Execute");
    await Cec.CardEffectCommons.ExecuteProcess(new Cec.Permanent(match.Context, host).TopCard, source);

    var effects = new Cec.Permanent(match.Context, host).UntilEndAttackEffects;
    AssertEqual(3, effects.Count,
        "ExecuteProcess appended: the restrict-defender gate (before) + the self-delete + the detail (after)");

    // OnEndAttack: exactly the DeleteSelfEffect ("Delete this Digimon") surfaces (plus the always-on gate).
    var atEndAttack = effects.Select(f => f(Cec.EffectTiming.OnEndAttack)).Where(e => e != null).ToList();
    AssertTrue(atEndAttack.Any(e => e is ActivateClass && e!.EffectName == "Delete this Digimon"),
        "at OnEndAttack the attacker self-deletes (PermanentEffectFactory.DeleteSelfEffect appended)");

    // None: the display detail surfaces.
    var atNone = effects.Select(f => f(Cec.EffectTiming.None)).Where(e => e != null).ToList();
    AssertTrue(atNone.Any(e => e is AddDetailClass),
        "at None the \"At end of attack, delete this Digimon.\" detail surfaces (AddDetailClass appended)");

    // The restrict-defender gate (isExecute unsuspended semantics) is the third append.
    AssertTrue(effects.Any(f => f(Cec.EffectTiming.OnEndAttack) is CanAttackTargetDefendingPermanentClass),
        "the per-attack CanAttackTargetDefendingPermanentClass (attack unsuspended Digimon) is appended");

    // CONTROL: a normal Digimon that never ran ExecuteProcess has NO per-attack self-delete.
    AssertEqual(0, new Cec.Permanent(match.Context, plain).UntilEndAttackEffects.Count,
        "a normal Digimon has no per-attack self-delete (the flag is Execute-only, not on normal attacks)");
}

async Task ExecuteNoSummoningSicknessBypass()
{
    // Sub-mechanic (no window drive): the CanActivateExecute gate on the pump-staged board.
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    Place(match, P2, "FOE", suspended: false, entered: false);
    var settled = Place(match, P1, "SETTLED", suspended: false, entered: false);
    var sick = Place(match, P1, "SICK", suspended: false, entered: true); // entered this turn, no Rush

    using var scope = AmbientMatchContext.Enter(match.Context);
    var source = GrantSource(match, settled, "Execute");
    AssertTrue(Cec.CardEffectCommons.CanActivateExecute(new Cec.Permanent(match.Context, settled).TopCard, source),
        "a settled Execute Digimon CAN activate Execute");

    var sickSource = GrantSource(match, sick, "Execute");
    AssertTrue(!Cec.CardEffectCommons.CanActivateExecute(new Cec.Permanent(match.Context, sick).TopCard, sickSource),
        "a summoning-sick Execute Digimon CANNOT activate Execute (isExecute does not bypass, unlike Rush/isVortex)");
}

async Task ExecuteControlNoWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    Place(match, P1, "PLAIN", suspended: false, entered: false);
    Place(match, P2, "FOE", suspended: false, entered: false);

    AssertEqual(0, CollectOnEndTurn(match), "a plain Digimon surfaces no OnEndTurn effect");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "no window opens for a plain Digimon (false-green guard)");
}

async Task ExecuteGrantedBucketResetOrder()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var host = Place(match, P1, "PLAIN", suspended: false, entered: false);
    Place(match, P2, "FOE", suspended: false, entered: false);
    Cec.CardEffectRegistrar.RegisterCard(match.Context, host, P1);
    GrantExecute(match, host, EffectDuration.UntilOwnerTurnEnd);

    // FIRE first: the pump turn-end drain resolves the bucket effect (window opens the optional) BEFORE the AS-IS
    // per-duration bucket reset (HeadlessEndTurnCleanupFlow, run inside the SAME EndPhaseAsync).
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type,
        "the granted Execute fired through the window (before the bucket reset)");

    // The REAL per-duration bucket reset ran at that turn-end (AS-IS :3191 permanent.UntilOwnerTurnEndEffects):
    // the host's UntilOwnerTurnEnd bucket is now empty -> no re-fire next turn.
    using var scope = AmbientMatchContext.Enter(match.Context);
    AssertEqual(0, new Cec.Permanent(match.Context, host).UntilOwnerTurnEndEffects.Count,
        "after the per-duration bucket reset the granted Execute is gone (no re-fire next turn)");
}

// --- Harness (pump α-cluster retarget scaffold) -----------------------------------------------------------

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewPumpMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    PlayerDeckSetup[] decks =
    {
        new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
        new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    };
    MatchSetupConfig setup = MatchSetupConfig.Create(decks, firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);
    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, policy);
}

async Task<(ChoiceRequest? Optional, ChoiceRequest? Attack)> FireOnEndTurnAsync(DcgoMatch match, PolicyChoiceProvider policy)
{
    ChoiceRequest? optional = null;
    ChoiceRequest? attack = null;
    policy.On(req => req.Type == ChoiceType.OptionalEffect,
        req => { optional = req; return ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    policy.On(req => req.Type is ChoiceType.Card or ChoiceType.Permanent,
        req => { attack ??= req; return req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());
    return (optional, attack);
}

// PREMISE (review1 P2-②): the CollectOnEndTurn counts below (== 1 for the Execute host, == 0 for the plain
// control) rely on the deck filler BT1_028 being INERT for OnEndTurn. The decks are 50x BT1_028 but they sit only
// in library/security (never a battle-area permanent), and GetSkillInfos(OnEndTurn) scans field permanents, so the
// only OnEndTurn effect collected is the one staged by the test — the count is solely the fixture's.
int CollectOnEndTurn(DcgoMatch match)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    return AutoProcessing.GetSkillInfos(new Hashtable(), Cec.EffectTiming.OnEndTurn).Count;
}

void GrantExecute(DcgoMatch match, HeadlessEntityId hostId, EffectDuration duration)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    Cec.CardEffectCommons.GainExecute(new Cec.Permanent(match.Context, hostId), duration, GrantSource(match, hostId, "GrantExecute")).GetAwaiter().GetResult();
}

// A grant-source ICardEffect whose EffectSourceCard is the host's own top card (AS-IS: the granted keyword's
// source is the target permanent — GainExecute passes targetPermanent.TopCard to ExecuteEffect).
Cec.ICardEffect GrantSource(DcgoMatch match, HeadlessEntityId hostId, string name)
{
    var host = new Cec.CardSource(match.Context, hostId, P1, P1);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(name, _ => true, host);
    return ac;
}

HeadlessEntityId Place(DcgoMatch match, HeadlessPlayerId owner, string number, bool suspended, bool entered)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId(number);
    cards.Upsert(new CardRecord(def, number, number,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{number}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended };
    if (entered) meta[MatchStateMutationSink.EnteredThisTurnKey] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

async Task PassTurnAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass = Legal(match, player).First(a => a.ActionType == HeadlessActionTypes.Pass);
    await ApplyAsync(match, pass);
}

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    await ApplyAsync(match, action);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// ═══════════════════════════ providers/context (EXEMPLAR-T1 precedent) ═══════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();
    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));
    public List<string> Seen { get; } = new();
    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            var (applies, answer, oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot) _handlers.RemoveAt(i);
                return Task.FromResult(result);
            }
        }
        return _fallback.ChooseAsync(request, cancellationToken);
    }
}

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));
        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider, randomSource, new CardDatabase(), cardInstanceRepository, zoneMover,
            new InMemoryRuleQueryService(), new InMemoryHeadlessTurnController(), choiceController,
            new InMemoryHeadlessAttackController(), memoryController, logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(), effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
