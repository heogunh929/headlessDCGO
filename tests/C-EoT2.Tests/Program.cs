// C-EoT-2 witness — <Vortex>/<Overclock> end-of-turn firing RE-HOUSED to the AS-IS OnEndTurn window,
// RE-TARGETED (4b B1-α) onto the DcgoMatch.CreatePumpDriven pump. The retired invented EndOfTurnEffectAttack
// gate is gone; these witnesses drive the SAME OnEndTurn drain the live turn cadence now runs.
//
// CONTEXT (keyword_rehoming_design_2026-07-15.md §2 C-EoT / §5 W-EoTFIX; suite_retarget_4b_design §3.1b B1):
// the live <Vortex>/<Overclock> attack fires through the mirror MultipleSkills window off the OnEndTurn drain
// (AutoProcessing.StackSkillInfos(OnEndTurn) + AutoProcessCheck == AS-IS EndTurnProcess:699/1511). Since the R4
// cutover (decision 3=B) that drain is owned by the pump's EndPhaseAsync -> EndTurnProcess: an explicit P1 Pass
// runs the turn-end, the drain opens the AS-IS optional "Will you use Vortex/Overclock?" at the AGENT SEAT (the
// PolicyChoiceProvider — the R4S3b/EXEMPLAR precedent; OnEndTurn optionals/selects are provider-seat choices
// under the pump, NOT ChoiceController-pending, so they are OBSERVED by capturing the ChoiceRequest at the seat).
// The throw-record-replay contract (the OnEndTurn window's pending-exception unwind + suspended-window resume)
// is RETIRED — the pump await-mode replaces it. GetSkillInfos(OnEndTurn) collection assertions are the retained
// substrate (query surface), unchanged.
//
//   * PRINTED  — the card's CardEffects(OnEndTurn) returns VortexSelfEffect / OverclockSelfEffect (fixtures
//                TfxVortex / TfxOverclock, dispatch-registered by card number).
//   * GRANTED  — CardEffectCommons.GainVortex / GainOverclock store a Vortex/Overclock ActivateClass in the
//                target's OnEndTurn duration bucket (AS-IS 1:1), collected by GetSkillInfos.
// SINGLE-FIRE is proven structurally: the EndOfTurnEffectAttack gate is physically deleted (the class no longer
// exists), so the OnEndTurn window is the sole firing path. The presence markers (ContinuousKeywordGate) stay
// live. Control groups (no keyword) open nothing (false-green guard). The granted path asserts the AS-IS EoT
// bucket-reset happens (the REAL HeadlessEndTurnCleanupFlow ran at the turn-end that fired the effect — a
// fidelity upgrade over the old manual bucket clear).

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using ActivateClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Vortex PRINTED: the OnEndTurn window opens \"Will you use Vortex?\" -> VortexProcess attack target select", VortexPrintedFiresThroughWindow),
    ("Vortex: the presence marker stays live while the invented EoT gate is deleted (single-fire: window only)", VortexGateRetired),
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
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var vortex = PlaceTfx(match, P1, "TfxVortex", suspended: false);
    var foe = PlaceTfx(match, P2, "FOE", suspended: true);

    // Collection proof: the OnEndTurn window collects the printed Vortex ActivateClass (GetSkillInfos scan).
    AssertEqual(1, CollectOnEndTurn(match), "the OnEndTurn window collects the printed Vortex ActivateClass");

    // Drive the pump turn-end drain: the window opens the AS-IS optional "Will you use Vortex?" (MultipleSkills).
    (ChoiceRequest? opt, ChoiceRequest? target) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is not null, "the pump turn-end drain opened the Vortex optional (the window fired)");
    AssertEqual(ChoiceType.OptionalEffect, opt!.Type, "the window opened the AS-IS Vortex optional (MultipleSkills)");
    AssertTrue(opt.Message.Contains("Vortex", StringComparison.Ordinal), "the optional names Vortex");

    // Answering "yes" resumed -> VortexProcess -> SelectAttackEffect target select, with the opponent Digimon
    // offered (AS-IS defenderCondition _ => true + SetIsVortex).
    AssertTrue(target is not null, "answering the optional 'yes' opened VortexProcess's own attack target select");
    AssertTrue(target!.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "VortexProcess offered the opponent Digimon as an attack target");
}

async Task VortexGateRetired()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    var vortex = PlaceTfx(match, P1, "TfxVortex", suspended: false);
    PlaceTfx(match, P2, "FOE", suspended: false);

    using var scope = AmbientMatchContext.Enter(match.Context);
    AssertTrue(ContinuousKeywordGate.HasKeyword(match.Context, vortex, ContinuousKeywordGate.Vortex),
        "the Vortex presence marker is still live (only the gate FIRING is retired)");
    // (G-clean) The invented EndOfTurnEffectAttack gate is physically deleted — single-fire is proven
    // structurally (the gate class no longer exists); <Vortex> fires only through the OnEndTurn window.
}

async Task VortexGrantedFiresThroughWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var host = PlaceTfx(match, P1, "PLAIN", suspended: false);
    var foe = PlaceTfx(match, P2, "FOE", suspended: true);
    GrantVortex(match, host, EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, CollectOnEndTurn(match),
        "GainVortex stored a Vortex ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    (ChoiceRequest? opt, ChoiceRequest? target) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type, "the granted Vortex opens the optional through the window");
    AssertTrue(target is not null, "answering 'yes' opened the granted VortexProcess attack target select");
    AssertTrue(target!.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the granted VortexProcess offered the opponent Digimon");
}

async Task VortexGrantedBucketResetOrder()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var host = PlaceTfx(match, P1, "PLAIN", suspended: false);
    PlaceTfx(match, P2, "FOE", suspended: true);
    GrantVortex(match, host, EffectDuration.UntilOwnerTurnEnd);

    // FIRE first: the pump turn-end drain resolves the bucket effect (window opens the optional) BEFORE the
    // AS-IS per-duration bucket reset (HeadlessEndTurnCleanupFlow, run inside the SAME EndPhaseAsync). A reset
    // before the drain would have dropped the effect and no optional would have surfaced.
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type,
        "the granted effect fired through the window (before the bucket reset)");

    // The REAL per-duration bucket reset ran at that turn-end (AS-IS :3191 permanent.UntilOwnerTurnEndEffects):
    // the host's UntilOwnerTurnEnd bucket is now empty -> no re-fire next turn.
    using var scope = AmbientMatchContext.Enter(match.Context);
    AssertEqual(0, new Cec.Permanent(match.Context, host).UntilOwnerTurnEndEffects.Count,
        "after the per-duration bucket reset the granted Vortex is gone (no re-fire next turn)");
}

async Task VortexControlNoWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    PlaceTfx(match, P1, "PLAIN", suspended: false);
    PlaceTfx(match, P2, "FOE", suspended: false);

    AssertEqual(0, CollectOnEndTurn(match), "a plain Digimon surfaces no OnEndTurn effect");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "no window opens for a plain Digimon (false-green guard)");
}

// ---------------------------------------------------------------------------------------------------------
// OVERCLOCK
// ---------------------------------------------------------------------------------------------------------

async Task OverclockPrintedFiresThroughWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var oc = PlaceTfx(match, P1, "TfxOverclock", suspended: false);
    var ally = PlaceTfxTrait(match, P1, "PUPPETALLY", "Puppet");
    PlaceTfx(match, P2, "FOE", suspended: false);

    AssertEqual(1, CollectOnEndTurn(match), "the OnEndTurn window collects the printed Overclock ActivateClass");

    (ChoiceRequest? opt, ChoiceRequest? target) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type, "the window opened the AS-IS Overclock optional (MultipleSkills)");
    AssertTrue(opt!.Message.Contains("Overclock", StringComparison.Ordinal), "the optional names Overclock");

    // Answering "yes" -> OverclockProcess -> SelectPermanent "delete a trait/token ally", offering the Puppet ally.
    AssertTrue(target is not null, "answering the optional 'yes' opened OverclockProcess's own ally select");
    AssertTrue(target!.Candidates.Any(c => c.Id == ally || c.Label.Contains(ally.Value, StringComparison.Ordinal)),
        "OverclockProcess offered the trait ally to delete");
}

async Task OverclockGateRetired()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    var oc = PlaceTfx(match, P1, "TfxOverclock", suspended: false);
    PlaceTfxTrait(match, P1, "PUPPETALLY", "Puppet");

    using var scope = AmbientMatchContext.Enter(match.Context);
    AssertTrue(ContinuousKeywordGate.HasKeyword(match.Context, oc, ContinuousKeywordGate.Overclock),
        "the Overclock presence marker is still live (only the gate FIRING is retired)");
    // (G-clean) The invented EndOfTurnEffectAttack gate is physically deleted — single-fire is proven
    // structurally (the gate class no longer exists); <Overclock> fires only through the OnEndTurn window.
}

async Task OverclockGrantedFiresThroughWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    var host = PlaceTfx(match, P1, "PLAIN", suspended: false);
    PlaceTfxTrait(match, P1, "PUPPETALLY", "Puppet");
    PlaceTfx(match, P2, "FOE", suspended: false);
    GrantOverclock(match, host, "Puppet", EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(1, CollectOnEndTurn(match),
        "GainOverclock stored an Overclock ActivateClass in the host's OnEndTurn bucket (collected by the window)");

    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertEqual(ChoiceType.OptionalEffect, opt?.Type, "the granted Overclock opens the optional through the window");
}

async Task OverclockControlNoWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    PlaceTfx(match, P1, "PLAIN", suspended: false);
    PlaceTfxTrait(match, P1, "PUPPETALLY", "Puppet");

    AssertEqual(0, CollectOnEndTurn(match), "a plain Digimon surfaces no OnEndTurn effect");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "no window opens for a plain Digimon (false-green guard)");
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

// Register the agent-seat handlers, then run the pump turn-end (P1 Pass -> EndPhaseAsync -> EndTurnProcess ->
// StackSkillInfos(OnEndTurn) + AutoProcessCheck) and drive to the opponent's main wait. Returns the captured
// OnEndTurn optional ("Will you use <keyword>?") and the effect's own follow-up select (attack/ally target).
async Task<(ChoiceRequest? Optional, ChoiceRequest? Target)> FireOnEndTurnAsync(DcgoMatch match, PolicyChoiceProvider policy)
{
    ChoiceRequest? optional = null;
    ChoiceRequest? target = null;
    policy.On(req => req.Type == ChoiceType.OptionalEffect,
        req => { optional = req; return ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    policy.On(req => req.Type is ChoiceType.Card or ChoiceType.Permanent,
        req => { target ??= req; return req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());
    return (optional, target);
}

int CollectOnEndTurn(DcgoMatch match)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    return AutoProcessing.GetSkillInfos(new Hashtable(), Cec.EffectTiming.OnEndTurn).Count;
}

HeadlessEntityId PlaceTfx(DcgoMatch match, HeadlessPlayerId owner, string number, bool suspended)
    => PlaceCore(match, owner, number, suspended, trait: null);

HeadlessEntityId PlaceTfxTrait(DcgoMatch match, HeadlessPlayerId owner, string number, string trait)
    => PlaceCore(match, owner, number, suspended: false, trait: trait);

HeadlessEntityId PlaceCore(DcgoMatch match, HeadlessPlayerId owner, string number, bool suspended, string? trait)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId(number);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    if (trait != null) defMeta["traits"] = trait;
    cards.Upsert(new CardRecord(def, number, number, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{number}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended };
    if (trait != null) meta["traits"] = trait;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

void GrantVortex(DcgoMatch match, HeadlessEntityId hostId, EffectDuration duration)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    Cec.CardEffectCommons.GainVortex(new Cec.Permanent(match.Context, hostId), duration, GrantSource(match, hostId, "GrantVortex")).GetAwaiter().GetResult();
}

void GrantOverclock(DcgoMatch match, HeadlessEntityId hostId, string trait, EffectDuration duration)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    Cec.CardEffectCommons.GainOverclock(trait, new Cec.Permanent(match.Context, hostId), duration, GrantSource(match, hostId, "GrantOverclock")).GetAwaiter().GetResult();
}

// A grant-source ICardEffect whose EffectSourceCard is the host's own top card (AS-IS: the granted keyword's
// source is the target permanent — GainVortex passes targetPermanent.TopCard to VortexEffect).
Cec.ICardEffect GrantSource(DcgoMatch match, HeadlessEntityId hostId, string name)
{
    var host = new Cec.CardSource(match.Context, hostId, P1, P1);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(name, _ => true, host);
    return ac;
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
        var effectRegistry = new InMemoryEffectRegistry();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                effectRegistry,
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, effectRegistry, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));
        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider, randomSource, new CardDatabase(), cardInstanceRepository, zoneMover,
            new InMemoryRuleQueryService(), new InMemoryHeadlessTurnController(), choiceController,
            new InMemoryHeadlessAttackController(), memoryController, logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(), effectScheduler,
            effectRegistry: effectRegistry, gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
