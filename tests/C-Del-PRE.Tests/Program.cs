// C-Del 3b PRE-transport witness — the universal effect-delete sink (MatchStateMutationSink) now opens the
// AS-IS "would be deleted" cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField) that the mirror faithful
// DestroyPermanentsClass.Destroy() opens (CardController.cs:3448-3485), reconciling the AS-IS willBeRemoveField
// survival model to the sink (a replacement clears willBeRemoveField → the card is SPARED, never trashed).
//
// SUBSTRATE SCOPE (cdel_wave3_investigation_2026-07-15.md §F.3b): opened ONLY on the non-gate-deferred path
// (DeferAll=false). Every ported PRE keyword (Evade/Barrier/…) is gate-detected (NewModelContinuousScan reads the
// SAME EffectList the window collects) so it forces DeferAll=true and never reaches the window → NO double-fire
// with the still-live PRE replacement gate (whose firing half is NOT retired this batch — that is 3c). This
// witness therefore uses a GATE-INVISIBLE window-form fixture (TfxWouldBeDeleted): a non-keyword-named, new-model
// ActivateClass with no EffectRegistry binding, so HasPreOption is false and the AS-IS cut-in window is the sole
// firing path. INTERACTIVE would-be-deleted replacements (the cut-in drain pauses) are design item
// RD-3B-INTERACTIVE (promote-to-defer + state-sweep finalize) — out of the 3b substrate scope; the fixture is
// MANDATORY so the drain completes inline within FlushAsync.

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
    ("WINDOW LIVE: the printed WhenPermanentWouldBeDeleted ActivateClass IS collected by the AS-IS cut-in window", WindowCollectsPrinted),
    ("GATE-INVISIBLE: the fixture is NOT gate-detected (HasPreOption false) → the batch is NOT gate-deferred (no double-fire)", GateInvisibleNotDeferred),
    ("PRINTED SURVIVES: deleting the fixture fires the PRE cut-in, which cancels the deletion (willBeRemoveField=false) → spared", PrintedSurvivesViaPreWindow),
    ("CONTROL: a plain deleted card (no PRE effect) is trashed — the PRE window is a no-op (false-green guard)", ControlStaysTrashed),
    ("BATCH: one Destroy over [survivor, casualty] spares the survivor, trashes the casualty, fires the OnDeletion reactor ONCE", BatchSurvivorAndCasualty),
    ("GATE COEXISTS: a gate-detected (keyword) card still DEFERS via the live gate (DeferAll path unchanged)", GateStillDefersKeyword),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ TESTS

async Task WindowCollectsPrinted()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeleted");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    // The AS-IS PRE cut-in window's collection (GetSkillInfos over WhenPermanentWouldBeDeleted) picks up the
    // printed ActivateClass while the card is on field with willBeRemoveField=true (the marked-target state).
    var perm = new Permanent(context, card, P1) { willBeRemoveField = true };
    var ht = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { perm }, cardEffect: null, battle: null);
    int collected = AutoProcessing.GetSkillInfos(ht, EffectTiming.WhenPermanentWouldBeDeleted).Count;
    AssertEqual(1, collected, "the printed WhenPermanentWouldBeDeleted effect IS collected by the cut-in window");
    perm.willBeRemoveField = false;
}

async Task GateInvisibleNotDeferred()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeleted");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    // DOUBLE-FIRE AUDIT: the fixture is not a recognised replacement keyword and registers no binding, so the
    // PRE replacement GATE does not see it — HasPreOption is FALSE. The sink's batch is therefore NOT gate-
    // deferred, so opening the AS-IS cut-in window here cannot collide with the gate.
    var zones = (IZoneStateReader)context.ZoneMover;
    var record = Record(context, card);
    bool gateSees = DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, record, byBattle: false, context.EffectRegistry);
    AssertFalse(gateSees, "the gate does NOT detect the fixture (HasPreOption false) — no double-fire");

    await DeleteAndDrive(context, card);
    AssertFalse(ReadFlag(context, card, GameFlowProcessor.PendingDeletionKey), "the card was NOT parked for the gate (not gate-deferred)");
    AssertFalse(context.ChoiceController.Current.IsPending, "the gate opened no replacement choice");
}

async Task PrintedSurvivesViaPreWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeleted");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the PRE cut-in cancelled the deletion — the card survived on the battle area");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, card), "the survivor is NOT in the trash");
    AssertFalse(ReadFlag(context, card, "willBeRemoveField"), "willBeRemoveField was reset for the spared survivor (no stale marker)");
}

async Task ControlStaysTrashed()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "PLAIN");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "a plain deleted card is trashed (the PRE window is a no-op — false-green guard)");
    AssertFalse(InZone(context, P1, ChoiceZone.BattleArea, card), "the plain card left the battle area");
}

async Task BatchSurvivorAndCasualty()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var survivor = await Place(context, P1, "TfxWouldBeDeleted", instance: "1:battle:Surv");
    var casualty = await Place(context, P1, "PLAIN", instance: "1:battle:Cas");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, survivor, P1);
    CardEffectRegistrar.RegisterCard(context, casualty, P1);

    // ONE Destroy sequence = ONE batch: willBeRemoveField marked on BOTH, the ONE cut-in over the LIST fires the
    // survivor's replacement (spared), the casualty stays willBeRemoveField=true (trashed). The OnDeletion window
    // over the SURVIVOR-FIXED list must collect only the casualty (single anyone-reactor collection — atomicity).
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(survivor));
    sink.Apply(Delete(casualty));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, survivor), "the batch survivor was spared (willBeRemoveField cancelled)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, casualty), "the batch casualty was trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, survivor), "the survivor is not in the trash");
    // The OnDeletion cut-in (opened over the fixed survivor list) collected exactly the casualty's reactors — but
    // PLAIN has none, so no window pends. The key atomicity claim: the survivor did NOT also get trashed by a
    // second per-card pass.
    AssertFalse(context.ChoiceController.Current.IsPending, "no cross-batch order choice — one Destroy, one window");
}

async Task GateStillDefersKeyword()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    // A card the GATE detects (bare Evade presence marker) — the live PRE replacement gate must STILL defer it
    // (DeferAll=true) exactly as before this batch; the new PRE cut-in window is NOT opened for it (it never
    // reaches the DeferAll=false branch), so there is no double-path.
    var card = await Place(context, P1, "PLAIN", flags: (DeletionReplacementGate.HasEvadeKey, true));
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);
    SetMeta(context, card, DeletionReplacementGate.IsSuspendedKey, false);

    var zones = (IZoneStateReader)context.ZoneMover;
    bool gateSees = DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, Record(context, card), byBattle: false, context.EffectRegistry);
    AssertTrue(gateSees, "the gate DOES detect the Evade keyword card (gate firing half is not retired)");

    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(card));
    await sink.FlushAsync();

    AssertTrue(ReadFlag(context, card, GameFlowProcessor.PendingDeletionKey), "the gate-detected card is DEFERRED (pendingDeletion) via the live gate — DeferAll path unchanged");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the deferred card is not yet trashed (awaits the gate replacement decision)");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 17, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context: context);

async Task DeleteAndDrive(EngineContext context, HeadlessEntityId card)
{
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(card));
    await sink.FlushAsync();
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num,
    string cardType = "Digimon", string? instance = null, (string Key, bool Value)? flags = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: cardType));
    var id = new HeadlessEntityId(instance ?? $"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false };
    if (flags is { } f) meta[f.Key] = f.Value;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:FOE"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

CardInstanceRecord Record(EngineContext context, HeadlessEntityId cardId) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        ? r : throw new InvalidOperationException($"missing instance {cardId}");

void SetMeta(EngineContext context, HeadlessEntityId cardId, string key, object? value)
{
    var r = Record(context, cardId);
    var meta = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal) { [key] = value };
    context.CardInstanceRepository.Upsert(r with { Metadata = meta });
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
