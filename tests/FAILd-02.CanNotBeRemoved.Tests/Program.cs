using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: CanNotBeRemoved (AS-IS ICanNotBeRemovedEffect, EX6_044 "can't leave battle area except by deletion")
// was MISSING. CanNotBeRemovedStaticEffect now blocks bounce (return-to-hand) AND deck-bounce, but NOT deletion.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

// Protect kinds: None; Match (predicate matches the target); Other (predicate matches a DIFFERENT card); Conditioned
// (predicate matches but the effect's condition() is false).
var tests = new (string Name, Func<Task> Body)[]
{
    ("no restriction: bounce removes it from the battle area", () => Run(MatchStateMutationSink.ReturnToHandKind, Protect.None, expectStays: false)),
    ("CanNotBeRemoved (predicate matches target): bounce is blocked", () => Run(MatchStateMutationSink.ReturnToHandKind, Protect.Match, expectStays: true)),
    ("CanNotBeRemoved: deck-bounce is blocked", () => Run(MatchStateMutationSink.ReturnToDeckBottomKind, Protect.Match, expectStays: true)),
    ("CanNotBeRemoved does NOT block deletion (the exception)", () => Run(MatchStateMutationSink.DeleteKind, Protect.Match, expectStays: false)),
    ("predicate matches a DIFFERENT card: the target is not protected", () => Run(MatchStateMutationSink.ReturnToHandKind, Protect.Other, expectStays: false)),
    ("effect condition() is false: not blocked", () => Run(MatchStateMutationSink.ReturnToHandKind, Protect.Conditioned, expectStays: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(string kind, Protect protect, bool expectStays)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 922);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("T"), "T", "T", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var target = new HeadlessEntityId("p1:T");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(target, new HeadlessEntityId("T"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));

    var src = new HeadlessEntityId("p2:src");
    cards.Upsert(new CardRecord(new HeadlessEntityId("src"), "src", "src", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("src"), P2));

    if (protect != Protect.None)
    {
        // AS-IS joint... single-arg predicate CanNotBeRemoved(candidate): which permanents are protected.
        Func<CardSource, bool> predicate = protect == Protect.Other
            ? candidate => candidate.InstanceId == new HeadlessEntityId("p1:OTHER")
            : candidate => candidate.InstanceId == target;
        Func<bool>? condition = protect == Protect.Conditioned ? () => false : null;
        // MIGRATION-NOTE (P7 test-fix): CardEffectFactory.CanNotBeRemovedStaticEffect now wraps the NEW-model
        // kind-class CanNotBeRemovedClass (no ToBinding/EffectRegistry bridge — stage-B RED, docs/audit/
        // rebuild_p6_stageA_notes.md), which MatchStateMutationSink.IsRemovalBlockedByScan does not read. The
        // gate this test actually checks (IsRemovalBlockedByScan -> Runtime.RestrictionScan.IsRestricted over
        // RestrictionHelpers.CannotBeRemovedKey) is fed by the OLD-model "true-scan" CanNotBeRemovedEffect class
        // (ContinuousAndRestrictionEffects.cs) — the single-arg-predicate joint-restriction producer built for
        // exactly this remediation — so the test constructs that class directly (same predicate/condition
        // shape the test already assembles) instead of going through the now-dead factory method.
        ctx.EffectRegistry.Register(new CanNotBeRemovedEffect(
            new CardSource(ctx, target, P1), predicate, condition).ToBinding($"cnbr:{target.Value}"));
    }

    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(kind, src,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    await sink.FlushAsync();

    bool stays = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(target);
    if (stays != expectStays)
        throw new InvalidOperationException($"stays-on-battle-area expected {expectStays}, got {stays}");
}

enum Protect { None, Match, Other, Conditioned }
