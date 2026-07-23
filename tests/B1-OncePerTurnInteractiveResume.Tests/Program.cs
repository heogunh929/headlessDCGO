using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-1 (P1-3, reworked per the 2026-07-11 adversarial review): a CAPPED ([Once Per Turn]) uniform activated effect
// with an INTERACTIVE body must survive a suspend/resume. The resolver registers the use BEFORE the body (AS-IS
// register-before-body) but STAGES it in the OnceFlags uniform-cycle transaction: a suspend keeps it staged, and
// the resumed re-invocation's own gate reads its replay-cursor view (not capped-out — the original B-1 bug), while
// the staged use commits exactly once on completion. This proves: (a) no game mutation lands while suspended,
// (b) the effect COMPLETES on resume, (c) the cap is consumed exactly once (a same-turn re-resolve no longer
// fires). Also covers B-4 (refund = PER-CARD OPT-IN, AS-IS default consumes on a skipped body) and the
// multi-effect suspend replay (P0-3: an earlier effect's immediate mutation must not double-apply).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a capped interactive activated effect suspends, resumes to completion, and consumes its cap exactly once", CappedInteractiveSurvivesResume),
    ("(B-4) an OPT-IN refund card's SKIPPED selection does nothing and REFUNDS the per-turn cap (re-resolve fires again)", SkippedInteractiveRefundsCap),
    ("(B-4 default) a NON-refund card's SKIPPED selection keeps the use CONSUMED (AS-IS default — no RemoveUse)", SkippedInteractiveDefaultConsumes),
    ("(P0-3) multi-effect suspend: an earlier capped effect's immediate mutation applies EXACTLY once across the replay", MultiEffectSuspendRepaysOnce),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CappedInteractiveSurvivesResume()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 13, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (uniform-사멸 flip) the fixtures are now AS-IS ActivateClass — ICardEffect.CanTrigger gates on
    // DoneStartGame (phase past None/Setup), so advance to Main (PRIM-P0/G9-009 harness precedent).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)context.CardRepository;

    // The [Once Per Turn] interactive-trash card in P1's battle area.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOncePerTurnInteractiveTrash"), "TfxOncePerTurnInteractiveTrash", "OPTI",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var self = new HeadlessEntityId("p1:battle:OPTI");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("TfxOncePerTurnInteractiveTrash"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = true }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));

    // Two hand cards to choose from — one this turn, one to prove the cap is spent (a same-turn re-resolve won't fire).
    var hand1 = await PlaceHand(context, "H1");
    var hand2 = await PlaceHand(context, "H2");

    // (1) First resolve: the interactive body suspends at the hand-select choice.
    bool suspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended, "the interactive body suspended (DeferredChoicePendingException)");
    AssertTrue(context.ChoiceController.Current.IsPending, "a hand-select choice is pending");
    AssertTrue(InZone(context, hand1, ChoiceZone.Hand) && InZone(context, hand2, ChoiceZone.Hand),
        "nothing trashed while suspended (pending mutations are discarded; the staged use commits only on completion — B-1)");

    // (2) Answer + resume: the resolver is re-invoked; the effect must COMPLETE (not vanish on the CanActivate re-check).
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(hand1));
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(InZone(context, hand1, ChoiceZone.Trash),
        "the chosen hand card was trashed after resume — the capped interactive effect COMPLETED (B-1: without consume-after-body the resume vanishes it)");

    // (3) The cap was consumed exactly once: a same-turn re-resolve does NOT fire again (no second suspend, hand2 stays).
    bool reSuspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { reSuspended = true; }
    AssertTrue(!reSuspended && !context.ChoiceController.Current.IsPending,
        "a same-turn re-resolve does NOT open a choice — the [Once Per Turn] cap was consumed on completion");
    AssertTrue(InZone(context, hand2, ChoiceZone.Hand), "the second hand card is untouched (cap spent, effect capped out)");
}

async Task SkippedInteractiveRefundsCap()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 21, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (uniform-사멸 flip) the fixtures are now AS-IS ActivateClass — ICardEffect.CanTrigger gates on
    // DoneStartGame (phase past None/Setup), so advance to Main (PRIM-P0/G9-009 harness precedent).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOncePerTurnOptionalTrash"), "TfxOncePerTurnOptionalTrash", "OPTI",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var self = new HeadlessEntityId("p1:battle:OPTI");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("TfxOncePerTurnOptionalTrash"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = true }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));
    var hand1 = await PlaceHand(context, "H1");

    // (1) Resolve suspends at the (skippable) hand-select choice.
    bool suspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended && context.ChoiceController.Current.IsPending, "the interactive body suspended at its choice");

    // (2) SKIP the selection: the body does nothing (ResolveBodyAsync returns executed=false), so the resolver
    // REFUNDS the per-turn use instead of consuming it (AS-IS `if (!executed) RemoveUse()`).
    context.ChoiceController.ResolveChoice(ChoiceResult.Skip());
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(InZone(context, hand1, ChoiceZone.Hand), "the skipped selection trashed nothing");

    // (3) The cap was REFUNDED: a same-turn re-resolve fires AGAIN (opens the choice), proving the use was not spent.
    bool reSuspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { reSuspended = true; }
    AssertTrue(reSuspended && context.ChoiceController.Current.IsPending,
        "a same-turn re-resolve fires again — the [Once Per Turn] use was REFUNDED on the skip (B-4 opt-in card)");
}

async Task SkippedInteractiveDefaultConsumes()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 34, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (uniform-사멸 flip) the fixtures are now AS-IS ActivateClass — ICardEffect.CanTrigger gates on
    // DoneStartGame (phase past None/Setup), so advance to Main (PRIM-P0/G9-009 harness precedent).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)context.CardRepository;

    // The DEFAULT (no-refund) fixture — like AS-IS BT2_078 (canNoSelect body, no RemoveUse): selecting nothing
    // still spends the [Once Per Turn] use.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOncePerTurnOptionalTrashNoRefund"), "TfxOncePerTurnOptionalTrashNoRefund", "OPTN",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var self = new HeadlessEntityId("p1:battle:OPTN");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("TfxOncePerTurnOptionalTrashNoRefund"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = true }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));
    var hand1 = await PlaceHand(context, "H1");

    // (1) Resolve suspends at the (skippable) hand-select choice.
    bool suspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended && context.ChoiceController.Current.IsPending, "the interactive body suspended at its choice");

    // (2) SKIP the selection: the body does nothing, but the registered use STAYS consumed — the AS-IS default
    // (only ~38 cards call RemoveUse; this card does not).
    context.ChoiceController.ResolveChoice(ChoiceResult.Skip());
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(InZone(context, hand1, ChoiceZone.Hand), "the skipped selection trashed nothing");

    // (3) The use was CONSUMED: a same-turn re-resolve does NOT fire again.
    bool reSuspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { reSuspended = true; }
    AssertTrue(!reSuspended && !context.ChoiceController.Current.IsPending,
        "a same-turn re-resolve does NOT fire — a skipped body on a non-refund card keeps its use consumed (AS-IS default)");
}

async Task MultiEffectSuspendRepaysOnce()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 55, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (uniform-사멸 flip) the fixtures are now AS-IS ActivateClass — ICardEffect.CanTrigger gates on
    // DoneStartGame (phase past None/Setup), so advance to Main (PRIM-P0/G9-009 harness precedent).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.MemoryController.Set(0);
    var cards = (CardDatabase)context.CardRepository;

    // TWO capped effects at the same timing: [0] +2 memory (immediate mutation), [1] interactive hand-trash.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxCappedMemoryThenSelectTrash"), "TfxCappedMemoryThenSelectTrash", "CMST",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var self = new HeadlessEntityId("p1:battle:CMST");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId("TfxCappedMemoryThenSelectTrash"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));
    var hand1 = await PlaceHand(context, "H1");

    // (1) First resolve: effect [0] applies +2 memory (immediately) and stages its use; effect [1] suspends.
    bool suspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended && context.ChoiceController.Current.IsPending, "effect [1] suspended at its hand-select choice");
    AssertEqual(2, context.MemoryController.Current.Current, "effect [0]'s +2 memory applied before the suspend (AS-IS mid-resolution view)");

    // (2) Answer + resume: the replay must NOT re-apply [0]'s memory (mutation journal) nor cap [0] out against
    // its own staged use (replay cursor); [1] completes with the chosen trash.
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(hand1));
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone);
    AssertEqual(2, context.MemoryController.Current.Current,
        "memory is +2 exactly once across the suspend/replay (P0-3: the immediate mutation must not double-apply)");
    AssertTrue(InZone(context, hand1, ChoiceZone.Trash), "effect [1]'s chosen hand card was trashed on resume");

    // (3) Both uses committed exactly once: a same-turn re-resolve neither gains memory nor opens a choice.
    bool reSuspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OnEnterFieldAnyone); }
    catch (DeferredChoicePendingException) { reSuspended = true; }
    AssertTrue(!reSuspended && !context.ChoiceController.Current.IsPending, "no effect re-fires — both caps committed");
    AssertEqual(2, context.MemoryController.Current.Current, "memory unchanged on the capped-out re-resolve");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceHand(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext context, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

static void AssertEqual(int expected, int actual, string label)
{
    if (expected != actual) { throw new InvalidOperationException($"{label}: expected {expected}, got {actual}."); }
}
