using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (F1-DEAD) Dead-timing INFRA smoke test. The 6 AS-IS DEAD timings (OnEndAttackPhase / OnEndBlockDesignation /
// OnEndCoinToss / OnEndMainPhase / OnGetDamage / OnKnockOut) are reacted to by ZERO real cards, so there is no
// AS-IS fidelity to assert. These checks prove only that the M0 dead-timing INFRA is coherent:
//   D1 — each enum member exists and its ToTriggerName is string-equal to the AS-IS TriggerTimings value;
//   D2 — the ActivatedBridgeTimings set classification is correct: a TEST FIXTURE (TfxDeadTimingDraw, an activated
//        draw at every DEAD timing) is picked up by CollectActivatedBridgeTriggers in the RIGHT category
//        (subject-scoped subject, boundary scan, broadcast scan) when a synthetic driving event is supplied;
//   behavior-neutral — an INERT card (no effects at the timing) yields NO marker, so the scan cost the Boundary
//        registration adds cannot change any real-card outcome.
// This is an ISOLATION test (same seam as Stage5-ActivatedBridge): the synthetic TimingEvent reproduces exactly
// what the emit sites publish (TriggerEventEmitter sets TriggerTimingKey=<timing>), so it exercises the real
// collect path. The emit sources for OnEndCoinToss/OnGetDamage/OnEndBlockDesignation do not exist yet (design
// items) — the test supplies the event directly to verify the classification, not the (absent) emit.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// --- D1: enum names are string-equal to the AS-IS TriggerTimings values (ToString() == emitted string). ---
Check(EffectTimings.ToTriggerName(EffectTiming.OnEndAttackPhase) == "OnEndAttackPhase", "D1 name: OnEndAttackPhase");
Check(EffectTimings.ToTriggerName(EffectTiming.OnEndBlockDesignation) == "OnEndBlockDesignation", "D1 name: OnEndBlockDesignation");
Check(EffectTimings.ToTriggerName(EffectTiming.OnEndCoinToss) == "OnEndCoinToss", "D1 name: OnEndCoinToss");
Check(EffectTimings.ToTriggerName(EffectTiming.OnEndMainPhase) == "OnEndMainPhase", "D1 name: OnEndMainPhase");
Check(EffectTimings.ToTriggerName(EffectTiming.OnGetDamage) == "OnGetDamage", "D1 name: OnGetDamage");
Check(EffectTimings.ToTriggerName(EffectTiming.OnKnockOut) == "OnKnockOut", "D1 name: OnKnockOut");

// --- D2: set classification. ---
Check(ActivatedBridgeTimings.Boundary.Contains(EffectTiming.OnEndAttackPhase), "D2 set: OnEndAttackPhase -> Boundary");
Check(ActivatedBridgeTimings.Boundary.Contains(EffectTiming.OnEndMainPhase), "D2 set: OnEndMainPhase -> Boundary");
Check(ActivatedBridgeTimings.Boundary.Contains(EffectTiming.OnEndCoinToss), "D2 set: OnEndCoinToss -> Boundary");
Check(ActivatedBridgeTimings.SubjectScoped.Contains(EffectTiming.OnKnockOut), "D2 set: OnKnockOut -> SubjectScoped");
Check(ActivatedBridgeTimings.SubjectScoped.Contains(EffectTiming.OnGetDamage), "D2 set: OnGetDamage -> SubjectScoped");
Check(ActivatedBridgeTimings.EventBroadcast.Contains(EffectTiming.OnEndBlockDesignation), "D2 set: OnEndBlockDesignation -> EventBroadcast");
// OnUseAttack must NOT be bridged (would double-fire with OnAllyAttack on the same attack declaration).
Check(!ActivatedBridgeTimings.SubjectScoped.Contains(EffectTiming.OnUseAttack)
    && !ActivatedBridgeTimings.Boundary.Contains(EffectTiming.OnUseAttack)
    && !ActivatedBridgeTimings.EventBroadcast.Contains(EffectTiming.OnUseAttack),
    "D2 set: OnUseAttack stays UNregistered (OnAllyAttack is the live attack-declaration reactor)");

// --- Subject-scoped bridge: OnKnockOut / OnGetDamage synthesise a marker for the SUBJECT that reacts. ---
foreach (string timing in new[] { "OnKnockOut", "OnGetDamage" })
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7001);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    HeadlessEntityId reactor = await Place(ctx, P1, "TfxDeadTimingDraw", "REACT", ChoiceZone.BattleArea, 3000, 4);
    HeadlessEntityId inert = await Place(ctx, P1, "INERT", "INERT", ChoiceZone.BattleArea, 3000, 4);

    var m = WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent(timing, subject: reactor) });
    Check(m.Count == 1 && IsBridge(m[0], out var c0, out var t0, out var cat0) && c0 == reactor && t0 == timing && cat0 == "Subject",
        $"bridge(subject): a reacting subject yields one {timing}/Subject marker (count={m.Count})");

    var mi = WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent(timing, subject: inert) });
    Check(mi.Count == 0, $"bridge(subject): an INERT subject yields no {timing} marker (behavior-neutral; count={mi.Count})");
}

// --- Boundary bridge: OnEndAttackPhase / OnEndMainPhase / OnEndCoinToss scan every zone card; only the fixture
//     (which reacts) bridges, an inert-only board yields none. Mirrors what PassAction.cs emits for the first two. ---
foreach (string timing in new[] { "OnEndAttackPhase", "OnEndMainPhase", "OnEndCoinToss" })
{
    EngineContext ctxReact = EngineContext.CreateDefault(randomSeed: 7002);
    ctxReact.TurnController.Initialize(new[] { P1, P2 }, P1);
    HeadlessEntityId reactor = await Place(ctxReact, P1, "TfxDeadTimingDraw", "REACT", ChoiceZone.BattleArea, 3000, 4);
    var m = WindowResolverWiring.CollectActivatedBridgeTriggers(ctxReact, new[] { TimingEvent(timing, subject: null, actor: P1) });
    Check(m.Count == 1 && IsBridge(m[0], out var c0, out var t0, out var cat0) && c0 == reactor && t0 == timing && cat0 == "Boundary",
        $"bridge(boundary): the boundary scan yields one {timing}/Boundary marker for the reacting card (count={m.Count})");

    EngineContext ctxInert = EngineContext.CreateDefault(randomSeed: 7003);
    ctxInert.TurnController.Initialize(new[] { P1, P2 }, P1);
    await Place(ctxInert, P1, "A", "A", ChoiceZone.BattleArea, 3000, 4);
    await Place(ctxInert, P2, "B", "B", ChoiceZone.BattleArea, 3000, 4);
    var mi = WindowResolverWiring.CollectActivatedBridgeTriggers(ctxInert, new[] { TimingEvent(timing, subject: null, actor: P1) });
    Check(mi.Count == 0, $"bridge(boundary): an inert-only board yields no {timing} marker (behavior-neutral; count={mi.Count})");
}

// --- Broadcast bridge: OnEndBlockDesignation scans every zone card PER event; the fixture (on a different card
//     than the event subject) bridges, threading the driving event. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 7004);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    HeadlessEntityId reactor = await Place(ctx, P1, "TfxDeadTimingDraw", "REACT", ChoiceZone.BattleArea, 3000, 4);
    HeadlessEntityId subject = await Place(ctx, P2, "SUBJ", "SUBJ", ChoiceZone.BattleArea, 3000, 4);

    var m = WindowResolverWiring.CollectActivatedBridgeTriggers(ctx, new[] { TimingEvent("OnEndBlockDesignation", subject: subject) });
    Check(m.Any(t => IsBridge(t, out var c, out var tn, out var cat) && c == reactor && tn == "OnEndBlockDesignation" && cat == "Broadcast"),
        "bridge(broadcast): a cross-card reactor bridges at OnEndBlockDesignation (threading the driving event)");
    Check(m.Count == 1, $"bridge(broadcast): only the reacting card bridges (subject is inert; count={m.Count})");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall F1 dead-timing infra checks passed.");

// --- Helpers -------------------------------------------------------------

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
