// W-EoTFIX — the end-of-turn OnEndTurn drain resolves ALL collected SkillInfos (permanent- AND player-scoped,
// sync AND interactive), RE-TARGETED (4b B1-α) onto the DcgoMatch.CreatePumpDriven pump. The drain is no longer
// hand-driven (AutoProcessing.StackSkillInfos + AutoProcessCheck + throw-record-replay); it is now the pump's
// REAL turn-end: an explicit P1 Pass runs EndPhaseAsync -> EndTurnProcess -> StackSkillInfos(OnEndTurn) +
// AutoProcessCheck (AS-IS EndTurnProcess:1511) followed by the REAL HeadlessEndTurnCleanupFlow bucket reset.
//
// CONTEXT (RD-CEoT-01, keyword_rehoming_design_2026-07-15.md §5; suite_retarget_4b_design §3.1b B1): these
// witnesses LOCK IN that the OnEndTurn drain resolves every collected scope — permanent-scoped sync fire-once,
// permanent-scoped interactive suspend/resume (now: the effect-internal choice surfaces at the AGENT SEAT under
// the pump and completes when answered), and player-scoped bucket-stored — and that the AS-IS supply front-end
// (SkillWindowSupply.ConvertEvent -> a handled null-payload OnEndTurn entry) still produces the entry the drain
// feeds on. GetSkillInfos / EffectList / ConvertEvent query assertions are the retained substrate, unchanged;
// the firing DRIVE is the pump.
//
// Harness: DcgoMatch.CreatePumpDriven + a fixture permanent staged onto the pump board at P1's main wait, then a
// pump turn-end. The old throw-record-replay unwind is retired.

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using ASSkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) permanent-scoped SYNC OnEndTurn fires exactly once through the pump turn-end drain", PermanentSyncFiresOnce),
    ("(b) permanent-scoped INTERACTIVE OnEndTurn suspends on an agent choice -> answering it completes the body once", PermanentInteractiveSuspendResume),
    ("(c) player-scoped OnEndTurn fires through the drain, and the REAL per-duration bucket reset stops a re-fire (RD6 guard)", PlayerScopeFiresAndOneShot),
    ("(d) the AS-IS supply front-end round-trips OnEndTurn (emit -> ConvertEvent -> StackSkillInfos + AutoProcessCheck) and resolves the permanent-scoped effect", SupplyRoundTripDrivesDrain),
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
// (a) permanent-scoped SYNC OnEndTurn ActivateClass (a printed [End of Your Turn] body, the Vortex/Overclock
//     SHAPE minus the interactive attack) fires exactly once through the pump's turn-end drain.
// ---------------------------------------------------------------------------------------------------------
async Task PermanentSyncFiresOnce()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    Counter.Reset();

    // A printed once-per-turn [End of Your Turn] body (maxCountPerTurn = 1, the Vortex once-per-turn shape).
    PlacePermanentWithOnEndTurn(match, P1, optional: false, interactive: false, maxCount: 1);

    // Collection proof: the mirror window collects the permanent's OnEndTurn effect (GetSkillInfos 5-region scan).
    List<ASSkillInfo> collected = CollectSkillInfos(match);
    AssertTrue(collected.Count == 1, $"the OnEndTurn window collects the permanent-scoped effect (got {collected.Count})");
    AssertTrue(collected[0].CardEffect is ActivateICardEffect, "the collected effect is an ActivateICardEffect");

    // Drive the AS-IS pump turn-end (P1 Pass -> EndPhaseAsync -> EndTurnProcess:1511) and assert the body fired
    // exactly once. The pump runs the OnEndTurn drain exactly once per turn-end, and the once-per-turn cap
    // (register-before-body) makes CanActivate false on any re-collection within that drain, so Counter == 1 is
    // the AS-IS "fires exactly once per turn-end" guarantee (the old manual second-drain was a hand-drive artifact).
    await FirePumpTurnEndAsync(match);
    AssertEqual(1, Counter.Perm, "the permanent-scoped [End of Your Turn] body fired exactly once through the pump turn-end drain");
}

// ---------------------------------------------------------------------------------------------------------
// (b) permanent-scoped INTERACTIVE OnEndTurn body opens an agent choice mid-resolution. Under the pump the choice
//     surfaces at the AGENT SEAT (PolicyChoiceProvider); the body is PARKED (Counter still 0) until the seat
//     answers, then it completes exactly once.
// ---------------------------------------------------------------------------------------------------------
async Task PermanentInteractiveSuspendResume()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 11);
    Counter.Reset();

    PlacePermanentWithOnEndTurn(match, P1, optional: false, interactive: true);

    ChoiceRequest? interactiveSeen = null;
    int counterAtChoice = -1;
    policy.On(req => req.Type == ChoiceType.Card && req.Message.Contains("OnEndTurn body select", StringComparison.Ordinal),
        req => { interactiveSeen = req; counterAtChoice = Counter.Perm; return ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertTrue(interactiveSeen is not null,
        "the interactive OnEndTurn body opened an agent choice mid-resolution (the drain suspended on the seat)");
    AssertEqual(0, counterAtChoice,
        "the body had NOT completed when it suspended on the agent choice (parked before the increment)");
    AssertEqual(1, Counter.Perm, "the interactive permanent-scoped body completed exactly once after the seat answered");
}

// ---------------------------------------------------------------------------------------------------------
// (c) player-scoped OnEndTurn ActivateClass (bucket-stored via the 4-arg AddEffectToPlayer, the BT1_021 shape)
//     fires through the SAME pump turn-end drain (RD6 regression guard); the AS-IS per-duration bucket reset
//     (the REAL HeadlessEndTurnCleanupFlow: player.UntilEachTurnEndEffects = new()) runs at that turn-end and
//     stops a re-fire.
// ---------------------------------------------------------------------------------------------------------
async Task PlayerScopeFiresAndOneShot()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    Counter.Reset();

    var srcId = new HeadlessEntityId("1:battle:PLAYERSRC");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, new HeadlessEntityId("DEF:PLAYERSRC"), P1,
        Metadata: new Dictionary<string, object?>()));
    using (AmbientMatchContext.Enter(match.Context))
    {
        var src = new CardSource(match.Context, srcId, P1, P1);
        StorePlayerCounter(src);
    }

    // Storage proof (mirrors PRIM-P0.AddEffectToPlayer): it lands in the owner's OnEndTurn player bucket.
    AssertTrue(PlayerOnEndTurnBucketCount(match) >= 1,
        "the player-scoped effect is in the OnEndTurn player bucket (EffectList(OnEndTurn))");

    await FirePumpTurnEndAsync(match);
    AssertEqual(1, Counter.Player, "the player-scoped [End of Your Turn] body fired once through the pump turn-end drain (RD6 guard)");

    // The REAL per-duration bucket reset ran at that turn-end (AS-IS :3177 player.UntilEachTurnEndEffects): the
    // OnEndTurn player bucket is now empty -> no re-fire (this replaces the old manual bucket clear + second drain).
    AssertEqual(0, PlayerOnEndTurnBucketCount(match),
        "after the REAL per-duration bucket reset the player-scoped effect is gone (no re-fire)");
}

// ---------------------------------------------------------------------------------------------------------
// (d) the LIVE-drain front-end: emit OnEndTurn, convert via the AS-IS supply layer (SkillWindowSupply.ConvertEvent
//     -> a handled null-payload OnEndTurn entry). That ConvertEvent query is the retained substrate (read-only
//     here). The FULL pump turn-end (its own emit -> ConvertEvent -> StackSkillInfos + AutoProcessCheck path) then
//     resolves the permanent-scoped OnEndTurn effect (guards against a ConvertEvent/supply regression dropping it).
// ---------------------------------------------------------------------------------------------------------
async Task SupplyRoundTripDrivesDrain()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 11);
    Counter.Reset();

    PlacePermanentWithOnEndTurn(match, P1, optional: false, interactive: false);

    // Substrate query (read-only): ConvertEvent produces a handled OnEndTurn supply entry (AS-IS null payload).
    using (AmbientMatchContext.Enter(match.Context))
    {
        TriggerEventEmitter.Emit(match.Context.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1);
        match.Context.GameEventQueue.SyncFrom(match.Context.ZoneMover.Events);
        IReadOnlyList<GameEvent> pending = match.Context.GameEventQueue.DrainPending();
        var entries = new List<SkillWindowSupplyEntry>();
        foreach (GameEvent ev in pending) entries.AddRange(SkillWindowSupply.ConvertEvent(match.Context, ev));
        AssertTrue(entries.Any(e => e.Timing == EffectTiming.OnEndTurn),
            "SkillWindowSupply.ConvertEvent produced a handled OnEndTurn supply entry (AS-IS null payload)");
    }
    AssertEqual(0, Counter.Perm, "the read-only ConvertEvent inspection did not itself fire the effect");

    // The full pump turn-end front-end resolves the permanent-scoped OnEndTurn effect.
    await FirePumpTurnEndAsync(match);
    AssertEqual(1, Counter.Perm,
        "the full pump turn-end front-end (emit -> ConvertEvent -> StackSkillInfos + AutoProcessCheck) resolved the permanent-scoped OnEndTurn effect");
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

// Run the pump turn-end (P1 Pass -> EndPhaseAsync -> EndTurnProcess:1511 OnEndTurn drain + the REAL cleanup) and
// drive to P2's main wait. Effect-internal choices (if any) fall to the PolicyChoiceProvider seat.
async Task FirePumpTurnEndAsync(DcgoMatch match)
{
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());
}

List<ASSkillInfo> CollectSkillInfos(DcgoMatch match)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    return AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn);
}

int PlayerOnEndTurnBucketCount(DcgoMatch match)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    return new Player(match.Context, P1).EffectList(EffectTiming.OnEndTurn).Count;
}

void PlacePermanentWithOnEndTurn(DcgoMatch match, HeadlessPlayerId owner, bool optional, bool interactive, int maxCount = -1)
{
    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:PERM"), "DEF:PERM", "PermTag",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var permId = new HeadlessEntityId($"{owner.Value}:battle:PERM");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(permId, new HeadlessEntityId("DEF:PERM"), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, permId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    using var scope = AmbientMatchContext.Enter(context);
    var src = new CardSource(context, permId, owner, owner);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new OnEndTurnFixture(optional, interactive, maxCount), "DEF:PERM", src);
}

void StorePlayerCounter(CardSource card)
{
    var ac = new ActivateClass();
    ac.SetUpICardEffect("PlayerCounter", _ => true, card);
    ac.SetUpActivateClass(_ => true, async _ => { Counter.Player++; await Task.CompletedTask; }, -1, false,
        "[End of Your Turn] player counter++.");
    CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, ac, EffectTiming.OnEndTurn);
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

// Shared body counters (top-level statements can't hold static fields directly).
internal static class Counter
{
    public static int Perm;
    public static int Player;
    public static void Reset() { Perm = 0; Player = 0; }
}

// A permanent's printed [End of Your Turn] effect surfaced via its cEntity_Effect (RegisterOnEnterPlay), the same
// surfacing path a real card's CardEffects uses. optional => the AS-IS "you may" confirm (OptionalSkill);
// interactive => the body opens an agent choice mid-resolution (the SelectPermanentEffect / SelectAttackEffect
// SHAPE: a ChoiceProvider.ChooseAsync that, under the pump, resolves at the agent seat).
internal sealed class OnEndTurnFixture : CEntity_Effect
{
    private readonly bool _optional;
    private readonly bool _interactive;
    private readonly int _maxCount;
    public OnEndTurnFixture(bool optional, bool interactive, int maxCount) { _optional = optional; _interactive = interactive; _maxCount = maxCount; }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndTurn)
        {
            var ac = new ActivateClass();
            ac.SetUpICardEffect("PermOnEndTurn", _ => true, card);
            ac.SetUpActivateClass(_ => true, async _ =>
            {
                if (_interactive)
                {
                    EngineContext ctx = AmbientMatchContext.Require();
                    var cand = new ChoiceCandidate(card.InstanceId, "target", ChoiceZone.Custom, IsSelectable: true, ownerId: card.Owner);
                    var req = new ChoiceRequest(ChoiceType.Card, card.Owner, "OnEndTurn body select", 0, 1,
                        canSkip: true, ChoiceZone.Custom, new[] { cand });
                    await ctx.ChoiceProvider.ChooseAsync(req);
                }
                Counter.Perm++;
            }, _maxCount, _optional, "[End of Your Turn] perm counter++.");
            effects.Add(ac);
        }
        return effects;
    }
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
