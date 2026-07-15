// W-EoTFIX — the end-of-turn OnEndTurn drain resolves ALL collected SkillInfos (permanent- AND player-scoped,
// sync AND interactive) through the mirror MultipleSkills window.
//
// CONTEXT (RD-CEoT-01, keyword_rehoming_design_2026-07-15.md §5): C-EoT reported that a permanent-scoped OnEndTurn
// ActivateClass was COLLECTED by the mirror window (GetSkillInfos) but "never resolved" when the invented
// EndOfTurnEffectAttack gate was disabled. This batch DIAGNOSED that claim: the drain itself is already
// AS-IS-equivalent to AutoProcessing.EndTurnProcess:699-702 —
//     StackSkillInfos(null, EffectTiming.OnEndTurn)   (== GetSkillInfos 5-region scan -> PutStackedSkill)
//     AutoProcessCheck()                              (== RuleProcess -> RulesTiming stack -> TriggeredSkillProcess
//                                                        -> MultipleSkills.ActivateMultipleSkills)
// — and it DOES resolve permanent-scoped printed ActivateClasses (sync fire-once, and optional/interactive
// suspend->resume). The GameFlowProcessor.AutoProcessAsync emit->ConvertEvent->stack front-end round-trips
// OnEndTurn to exactly this call (SkillWindowSupply handles OnEndTurn with a null payload). So there is NO
// drain drop; the reason C-EoT saw "VortexProcess entered 0 times" is that no card surfaces the printed Vortex
// ActivateClass (VortexSelfEffect / VortexEffect have ZERO callers) — the live <Vortex> fires ONLY via the
// invented EndOfTurnEffectAttack gate (ContinuousKeywordGate.HasKeyword marker). Wiring VortexSelfEffect into
// the card's CardEffects (printed) / GainVortex bucket (granted) is the C-EoT keyword-rehousing work, out of
// this batch's scope. These witnesses LOCK IN that the drain resolves every OnEndTurn scope so that when the
// keyword wiring lands the effect fires through this window (and the gate can retire).
//
// Harness: EngineContext.CreateDefault + TurnController.Initialize(P1,P2) + SetPhase(Main) so DoneStartGame is
// true (F4 finding), all under AmbientMatchContext.Enter — the PRIM-P0 / W1b ambient pattern. Well-formed
// permanents only, so the pre-existing baseline null-TopCard NRE at CEntity_EffectController.GetCardEffects:88
// (which reds the full-match RD6 / GR-001 / GR-006 paths via HasBlitz legal-action validation) is not hit.

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
    ("(a) permanent-scoped SYNC OnEndTurn fires exactly once through the drain, no re-fire on a second drain", PermanentSyncFiresOnce),
    ("(b) permanent-scoped INTERACTIVE OnEndTurn suspends -> pending choice surfaces -> resolve resumes it -> fires once", PermanentInteractiveSuspendResume),
    ("(c) player-scoped OnEndTurn fires through the drain, and the per-duration bucket reset stops a re-fire (RD6 guard)", PlayerScopeFiresAndOneShot),
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
//     SHAPE minus the interactive attack) fires once via the drain and does not re-fire on a second drain.
// ---------------------------------------------------------------------------------------------------------
async Task PermanentSyncFiresOnce()
{
    EngineContext context = NewContext(deferred: false);
    using var scope = AmbientMatchContext.Enter(context);
    Counter.Reset();

    // A printed once-per-turn [End of Your Turn] body (maxCountPerTurn = 1, the Vortex once-per-turn shape).
    PlacePermanentWithOnEndTurn(context, P1, optional: false, interactive: false, maxCount: 1);

    // Collection proof: the mirror window collects the permanent's OnEndTurn effect (GetSkillInfos 5-region scan).
    List<ASSkillInfo> collected = AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn);
    AssertTrue(collected.Count == 1, $"the OnEndTurn window collects the permanent-scoped effect (got {collected.Count})");
    AssertTrue(collected[0].CardEffect is ActivateICardEffect, "the collected effect is an ActivateICardEffect");

    // Drive the AS-IS EndTurnProcess:699-702 shape and assert the body fired exactly once.
    await DriveEndTurnDrain(context);
    AssertEqual(1, Counter.Perm, "the permanent-scoped [End of Your Turn] body fired exactly once through the drain");

    // A second drain (same turn) must NOT re-fire — the once-per-turn use registered before the body
    // (register-before-body) makes CanActivate false on re-collection.
    await DriveEndTurnDrain(context);
    AssertEqual(1, Counter.Perm, "a second drain does not re-fire the once-per-turn permanent-scoped effect");
}

// ---------------------------------------------------------------------------------------------------------
// (b) permanent-scoped INTERACTIVE OnEndTurn body opens an agent choice mid-resolution; the drain suspends
//     (pending choice), and the ResolveChoice-style resume (RecordAnswer-free body replay via
//     ResumeSuspendedWindowsAsync) completes it, firing the body exactly once.
// ---------------------------------------------------------------------------------------------------------
async Task PermanentInteractiveSuspendResume()
{
    EngineContext context = NewContext(deferred: true); // interactive: DeferredChoiceProvider parks the choice
    using var scope = AmbientMatchContext.Enter(context);
    Counter.Reset();

    PlacePermanentWithOnEndTurn(context, P1, optional: false, interactive: true);

    bool suspended = false;
    try
    {
        await DriveEndTurnDrain(context);
    }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException)
    {
        suspended = true;
    }

    AssertTrue(suspended, "the interactive OnEndTurn body suspended the drain (a WindowChoice/DeferredChoice unwound)");
    AssertTrue(context.ChoiceController.Current.IsPending, "a pending choice surfaced (the agent must answer it)");
    AssertEqual(0, Counter.Perm, "the body has NOT completed yet (it is parked on the agent choice)");

    // Resume like MetadataActionProcessor.ResolveChoiceAsync: answer the pending choice, then drive the
    // suspended MultipleSkills chain to completion (ResumeSuspendedWindowsAsync).
    AutoProcessing ap = AutoProcessing.For(context);
    for (int guard = 0; guard < 6 && context.ChoiceController.Current.IsPending; guard++)
    {
        ChoiceRequest? req = context.ChoiceController.PendingRequest;
        ChoiceResult answer = req is { } r && r.Candidates.Count > 0
            ? ChoiceResult.Select(r.Candidates[0].Id)
            : ChoiceResult.Skip();
        context.ChoiceController.ResolveChoice(answer);
        try { await ap.ResumeSuspendedWindowsAsync(); }
        catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parks; loop re-answers */ }
    }

    AssertTrue(!context.ChoiceController.Current.IsPending, "after resolving, no choice remains pending");
    AssertEqual(1, Counter.Perm, "the interactive permanent-scoped body completed exactly once after the resume");
}

// ---------------------------------------------------------------------------------------------------------
// (c) player-scoped OnEndTurn ActivateClass (bucket-stored via the 4-arg AddEffectToPlayer, the BT1_021 shape)
//     fires through the SAME drain (RD6 regression guard), and the AS-IS per-duration bucket reset
//     (HeadlessEndTurnCleanupFlow: player.UntilEachTurnEndEffects = new()) stops a re-fire next turn.
// ---------------------------------------------------------------------------------------------------------
async Task PlayerScopeFiresAndOneShot()
{
    EngineContext context = NewContext(deferred: false);
    using var scope = AmbientMatchContext.Enter(context);
    Counter.Reset();

    var srcId = new HeadlessEntityId("1:battle:PLAYERSRC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, new HeadlessEntityId("DEF:PLAYERSRC"), P1,
        Metadata: new Dictionary<string, object?>()));
    var src = new CardSource(context, srcId, P1, P1);
    StorePlayerCounter(context, src);

    // Storage proof (mirrors PRIM-P0.AddEffectToPlayer): it lands in the owner's OnEndTurn player bucket.
    AssertTrue(new Player(context, P1).EffectList(EffectTiming.OnEndTurn).Count >= 1,
        "the player-scoped effect is in the OnEndTurn player bucket (EffectList(OnEndTurn))");

    await DriveEndTurnDrain(context);
    AssertEqual(1, Counter.Player, "the player-scoped [End of Your Turn] body fired once through the drain (RD6 guard)");

    // AS-IS per-duration bucket reset. A subsequent drain collects nothing -> no re-fire.
    new Player(context, P1).UntilEachTurnEndEffects = new();
    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "after the bucket reset the player-scoped effect is gone");
    await DriveEndTurnDrain(context);
    AssertEqual(1, Counter.Player, "no re-fire after the per-duration bucket reset");
}

// ---------------------------------------------------------------------------------------------------------
// (d) the LIVE-drain front-end: emit OnEndTurn (as DrainEndOfTurnWindowAsync does), convert via the AS-IS
//     supply layer (SkillWindowSupply.ConvertEvent -> a handled null-payload OnEndTurn entry), sequence, then
//     StackSkillInfos + AutoProcessCheck. Proves the whole GameFlowProcessor.AutoProcessAsync path resolves
//     the permanent-scoped OnEndTurn effect (guards against a ConvertEvent/supply regression dropping it).
// ---------------------------------------------------------------------------------------------------------
async Task SupplyRoundTripDrivesDrain()
{
    EngineContext context = NewContext(deferred: false);
    using var scope = AmbientMatchContext.Enter(context);
    Counter.Reset();

    PlacePermanentWithOnEndTurn(context, P1, optional: false, interactive: false);

    AutoProcessing ap = AutoProcessing.For(context);
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1);
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    IReadOnlyList<GameEvent> pending = context.GameEventQueue.DrainPending();

    var entries = new List<SkillWindowSupplyEntry>();
    foreach (GameEvent ev in pending) entries.AddRange(SkillWindowSupply.ConvertEvent(context, ev));
    AssertTrue(entries.Any(e => e.Timing == EffectTiming.OnEndTurn),
        "SkillWindowSupply.ConvertEvent produced a handled OnEndTurn supply entry (AS-IS null payload)");

    IReadOnlyList<IReadOnlyList<SkillWindowSupplyEntry>> passes = SkillWindowSupply.SequenceByMinimumBatch(entries);
    foreach (var pass in passes)
    {
        foreach (var e in pass) await ap.StackSkillInfos(e.Hashtable, e.Timing);
        await ap.AutoProcessCheck();
    }

    AssertEqual(1, Counter.Perm,
        "the full emit -> ConvertEvent -> StackSkillInfos + AutoProcessCheck path resolved the permanent-scoped OnEndTurn effect");
}

// --- Harness -----------------------------------------------------------------------------------------------

EngineContext NewContext(bool deferred)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: deferred);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1); // turn player = owner = P1
    ctx.TurnController.SetPhase(HeadlessPhase.Main);      // past Setup -> DoneStartGame true
    return ctx;
}

// AS-IS EndTurnProcess:699-702 (the OnEndTurn window step): StackSkillInfos(null, OnEndTurn) then AutoProcessCheck.
// This is exactly what DrainEndOfTurnWindowAsync / GameFlowProcessor.AutoProcessAsync drive for OnEndTurn (proven
// equivalent to the emit->convert front-end in test (d)).
async Task DriveEndTurnDrain(EngineContext context)
{
    AutoProcessing ap = AutoProcessing.For(context);
    await ap.StackSkillInfos(null, EffectTiming.OnEndTurn);
    await ap.AutoProcessCheck();
}

void PlacePermanentWithOnEndTurn(EngineContext context, HeadlessPlayerId owner, bool optional, bool interactive, int maxCount = -1)
{
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:PERM"), "DEF:PERM", "PermTag",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var permId = new HeadlessEntityId($"{owner.Value}:battle:PERM");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(permId, new HeadlessEntityId("DEF:PERM"), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, permId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    var src = new CardSource(context, permId, owner, owner);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new OnEndTurnFixture(optional, interactive, maxCount), "DEF:PERM", src);
}

void StorePlayerCounter(EngineContext context, CardSource card)
{
    var ac = new ActivateClass();
    ac.SetUpICardEffect("PlayerCounter", _ => true, card);
    ac.SetUpActivateClass(_ => true, async _ => { Counter.Player++; await Task.CompletedTask; }, -1, false,
        "[End of Your Turn] player counter++.");
    CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, ac, EffectTiming.OnEndTurn);
}

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

// A permanent's printed [End of Your Turn] effect surfaced via its cEntity_Effect (RegisterOnEnterPlay), the
// same surfacing path a real card's CardEffects uses. optional => the AS-IS "you may" confirm (OptionalSkill);
// interactive => the body opens an agent choice mid-resolution (the SelectPermanentEffect / SelectAttackEffect
// SHAPE: a ChoiceProvider.ChooseAsync that parks under the DeferredChoiceProvider).
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
