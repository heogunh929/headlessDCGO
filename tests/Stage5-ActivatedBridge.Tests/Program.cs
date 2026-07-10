using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (Stage 5, Phase 3b-ii) ACTIVATED-EFFECT BRIDGE unification for the window loop. The one window seed folds in
// a card's ACTIVATED effects (draw/trash/delete/select via ActivatedEffectResolver) as MARKER-bearing triggers
// alongside the scheduler (mutation) triggers. These checks prove, in ISOLATION (no live AutoProcessAsync flip
// yet — that is 3b-iii):
//   1. a marker trigger DISPATCHES to ActivatedEffectResolver, and an interactive body suspend is NORMALISED to
//      WindowResolveOutcome.Suspended + DeferredActivations.Suspend (the same resume seam as the batch bridge);
//   2. a normal (marker-less) scheduler trigger still routes to the scheduler through the SAME main-loop deps;
//   3. the OnUnTappedAnyone caller-cap is consumed at COMMIT (its synthetic key) and other timings are uncapped;
//   4/5. CollectActivatedBridgeTriggers synthesises the right markers (subject-scoped / boundary), mirroring the
//      batch BridgeActivatedTriggersAsync scan.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// --- 1. Dispatch: a WhenDigivolving marker for the real EX8_074 routes to ActivatedEffectResolver; under a
//        DeferredChoiceProvider its first choice suspends, normalised to Suspended + DeferredActivations. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3211, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.MemoryController.Set(5);

    HeadlessEntityId ex8 = await Place(ctx, P1, "EX8_074", "EX8", ChoiceZone.BattleArea, dp: 6000, level: 5);
    await Place(ctx, P1, "ALLY", "ALLY", ChoiceZone.BattleArea, dp: 4000, level: 4);
    await Place(ctx, P2, "FOE", "FOE", ChoiceZone.BattleArea, dp: 7000, level: 4);

    TimingWindowTrigger marker = WindowResolverWiring.MakeActivatedBridgeTrigger(
        ex8, EffectTiming.WhenDigivolving, P1, drivingEvent: null,
        WindowResolverWiring.ActivatedBridgeCategory.Subject, sequence: 0);

    WindowResolverDeps deps = WindowResolverWiring.BuildMainLoopDeps(ctx);
    WindowRunResult r = await new WindowResolver().DriveAsync(WindowResolver.CreateContinuation(new[] { marker }), deps);

    Check(r == WindowRunResult.Suspended, "dispatch: an activated body that suspends -> WindowRunResult.Suspended");
    Check(ctx.DeferredActivations.HasPending, "dispatch: the suspend was recorded in DeferredActivations (same seam as the batch bridge)");
    Check(ctx.DeferredActivations.Pending?.CardId == ex8, "dispatch: the suspended activation is EX8_074");
    Check(ctx.DeferredActivations.Pending?.Timing == EffectTiming.WhenDigivolving, "dispatch: suspended at the WhenDigivolving timing");
    Check(ctx.ChoiceController.Current.IsPending, "dispatch: the interactive choice surfaced to the agent");
}

// --- 2. Dispatch: a marker-less scheduler trigger still routes to the scheduler through the main-loop deps. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3212);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var e = new StubEffect("SCHED1");
    ctx.EffectRegistry.Register(new EffectBinding(SchedRequest(P1, "SCHED1"), effect: e));

    WindowResolverDeps deps = WindowResolverWiring.BuildMainLoopDeps(ctx);
    WindowRunResult r = await new WindowResolver().DriveAsync(
        WindowResolver.CreateContinuation(new[] { SchedTrigger(P1, "SCHED1") }), deps);

    Check(r == WindowRunResult.Completed && e.Resolved == 1,
        $"dispatch: a non-marker trigger resolves through the scheduler (resolved={e.Resolved})");
}

// --- 3. Cap: an OnUnTappedAnyone marker consumes its synthetic once-key at COMMIT; a non-capped timing does
//        not. (Gate re-checks the SAME CanActivate predicate, so a consumed key blocks a re-fire this turn.) ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3213);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // A generic (non-dispatch) card: the resolver no-ops (no activated effects), but Commit still runs — so the
    // synthetic cap is observable without needing a real OnUnTapped card.
    HeadlessEntityId dummy = await Place(ctx, P1, "DUMMY", "DUMMY", ChoiceZone.BattleArea, dp: 3000, level: 4);

    EffectRequest untapKey = BridgeOnceKey(dummy, EffectTiming.OnUnTappedAnyone, P1);
    Check(ctx.OnceFlags.CanActivate(untapKey, 1), "cap: the OnUnTapped synthetic key starts available");

    TimingWindowTrigger untapMarker = WindowResolverWiring.MakeActivatedBridgeTrigger(
        dummy, EffectTiming.OnUnTappedAnyone, P1, null, WindowResolverWiring.ActivatedBridgeCategory.Subject, 0);
    WindowResolverDeps deps = WindowResolverWiring.BuildMainLoopDeps(ctx);
    WindowRunResult r1 = await new WindowResolver().DriveAsync(WindowResolver.CreateContinuation(new[] { untapMarker }), deps);

    Check(r1 == WindowRunResult.Completed, "cap: the OnUnTapped window completes (resolver no-op)");
    Check(!ctx.OnceFlags.CanActivate(untapKey, 1),
        "cap: Commit consumed the OnUnTapped caller-cap -> Gate (same CanActivate) now blocks a re-fire this turn");

    // A non-OnUnTapped bridge timing carries NO caller-cap: its synthetic key is never created/consumed.
    EffectRequest battleKey = BridgeOnceKey(dummy, EffectTiming.OnEndBattle, P1);
    TimingWindowTrigger battleMarker = WindowResolverWiring.MakeActivatedBridgeTrigger(
        dummy, EffectTiming.OnEndBattle, P1, null, WindowResolverWiring.ActivatedBridgeCategory.Broadcast, 1);
    await new WindowResolver().DriveAsync(WindowResolver.CreateContinuation(new[] { battleMarker }), deps);
    Check(ctx.OnceFlags.CanActivate(battleKey, 1), "cap: a non-OnUnTapped timing consumes no caller-cap (resolver's own MaxCountPerTurn caps it)");
}

// --- 4. Collect (subject scan + HasEffectsAt filter): a subject-scoped event (OnDestroyedAnyone) synthesises a
//        marker ONLY for a subject that ACTUALLY reacts at that timing (BT2_034 has an OnDestroyedAnyone effect);
//        an effect-less subject yields none, so a no-op marker never competes for the player's order choice. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3214);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    HeadlessEntityId reactor = await Place(ctx, P1, "BT2_034", "REACTOR", ChoiceZone.BattleArea, dp: 3000, level: 4);
    HeadlessEntityId inert = await Place(ctx, P1, "INERT", "INERT", ChoiceZone.BattleArea, dp: 3000, level: 4);

    IReadOnlyList<TimingWindowTrigger> reactorMarkers =
        WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent("OnDestroyedAnyone", subject: reactor) });
    Check(reactorMarkers.Count == 1 && IsBridge(reactorMarkers[0], out HeadlessEntityId c0, out string t0, out string cat0)
        && c0 == reactor && t0 == "OnDestroyedAnyone" && cat0 == "Subject",
        $"collect(subject): an effect-bearing subject yields one OnDestroyedAnyone/Subject marker (count={reactorMarkers.Count})");

    IReadOnlyList<TimingWindowTrigger> inertMarkers =
        WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent("OnDestroyedAnyone", subject: inert) });
    Check(inertMarkers.Count == 0, $"collect(subject): an effect-LESS subject yields no marker (HasEffectsAt filter; count={inertMarkers.Count})");
}

// --- 5. Collect (boundary scan + filter): a boundary event (OnEndTurn) visits every battle-area card, but the
//        HasEffectsAt filter yields markers ONLY for effect-bearing cards — two inert cards produce none. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3215);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    await Place(ctx, P1, "A", "A", ChoiceZone.BattleArea, dp: 3000, level: 4);
    await Place(ctx, P2, "B", "B", ChoiceZone.BattleArea, dp: 3000, level: 4);

    IReadOnlyList<TimingWindowTrigger> markers =
        WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent("OnEndTurn", subject: null, actor: P1) });
    Check(markers.Count == 0,
        $"collect(boundary): the scan visits every battle-area card but only effect-bearing ones bridge (inert -> 0; count={markers.Count})");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall Stage-5 activated-bridge checks passed.");

// --- Helpers -------------------------------------------------------------

EffectRequest SchedRequest(HeadlessPlayerId p, string id) => new(
    new HeadlessEntityId(id), p, "OnTest",
    new EffectContext(p, p, new HeadlessEntityId($"src:{id}"), triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>()));

TimingWindowTrigger SchedTrigger(HeadlessPlayerId p, string id) =>
    new(SchedRequest(p, id), EffectResolutionMode.MainStack, TimingWindowTriggerKind.Mandatory, priority: 0, sequence: 0);

// The synthetic OnceFlag key the wiring uses for the OnUnTappedAnyone caller-cap (must match exactly).
EffectRequest BridgeOnceKey(HeadlessEntityId card, EffectTiming timing, HeadlessPlayerId owner) =>
    new(new HeadlessEntityId($"{card.Value}:bridgeOnce:{timing}"), owner, timing.ToString(), new EffectContext(owner, card));

// Read a marker off a bridge trigger via the public marker keys (the internal reader is private).
static bool IsBridge(TimingWindowTrigger t, out HeadlessEntityId card, out string timing, out string category)
{
    card = t.Request.Context.SourceEntityId;
    timing = t.Request.Timing;
    category = t.Request.Context.Values.TryGetValue(WindowResolverWiring.ActivatedBridgeCategoryKey, out object? c) ? c as string ?? "" : "";
    return t.Request.Context.Values.TryGetValue(WindowResolverWiring.ActivatedBridgeKey, out object? m) && m is true;
}

GameEvent TimingEvent(string timing, HeadlessEntityId? subject, HeadlessPlayerId? actor = null) => new(
    Sequence: 0, Type: GameEventType.Unknown, Message: timing,
    Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["triggerTiming"] = timing })
{
    Subject = subject,
    Actor = actor,
};

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, ChoiceZone zone, int dp, int level)
{
    var cards = (HeadlessDCGO.Engine.Headless.DataLoading.CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// A real IHeadlessCardEffect: records resolutions (proves the scheduler path still fires under the main-loop deps).
sealed class StubEffect : IHeadlessCardEffect
{
    public int Resolved { get; private set; }
    public CardEffectDefinition Definition { get; }

    public StubEffect(string id) =>
        Definition = new CardEffectDefinition(new HeadlessEntityId(id), new HeadlessEntityId($"src:{id}"), id, "OnTest");

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        Resolved++;
        return ValueTask.FromResult(EffectResult.Success($"resolved {Definition.EffectId.Value}"));
    }
}
