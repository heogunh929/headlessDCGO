using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-1 (P1-3): a CAPPED ([Once Per Turn]) uniform activated effect with an INTERACTIVE body must survive a
// suspend/resume. ActivatedEffectResolver consumes the per-turn cap AFTER the body completes (not before): the
// interactive body suspends mid-choice (DeferredChoicePendingException) and the resolver is RE-INVOKED on resume,
// re-running its uniform case incl. the CanActivate re-check. Consuming before the body made that re-check read the
// already-spent cap as false → the effect BREAK-vanished with its use wasted (latent P0). This proves: (a) the cap
// is NOT consumed while suspended, (b) the effect COMPLETES on resume, and (c) the cap is consumed exactly once
// (a same-turn re-resolve no longer fires).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a capped interactive activated effect suspends, resumes to completion, and consumes its cap exactly once", CappedInteractiveSurvivesResume),
    ("(B-4) a SKIPPED interactive selection does nothing and REFUNDS the per-turn cap (re-resolve fires again)", SkippedInteractiveRefundsCap),
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
        "nothing trashed while suspended (the cap is NOT consumed before the body — B-1)");

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
        "a same-turn re-resolve fires again — the [Once Per Turn] use was REFUNDED on the skip (B-4)");
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
