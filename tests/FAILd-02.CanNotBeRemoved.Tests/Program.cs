using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
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
    // (이연④-c) DoneStartGame gate for the new-model kind-class CanUse(null) — the live Permanent.CanBeRemoved()
    // scan gates each ICanNotBeRemovedEffect on cardEffect.CanUse(null), which is false before the game starts.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
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
        // (이연④-c) RE-AIMED off the registry direct-read onto the LIVE chokepoint. The old-model
        // CanNotBeRemovedEffect (which wrote CannotBeRemovedKey via ToBinding) was census-0 and DELETED; the real
        // card EX6_044 and the CanNotBeRemovedStaticEffect factory build the new-model kind-class
        // CanNotBeRemovedClass (an ICanNotBeRemovedEffect, no ToBinding). MatchStateMutationSink.IsRemovalBlockedByScan
        // now UNIONs the live AS-IS Permanent.CanBeRemoved() interface scan alongside the registry scan, so this
        // test REAL-DRIVES the factory's own output through that chokepoint: place the factory's CanNotBeRemovedClass
        // on the target's own effect list (the seam CanBeRemoved() enumerates over every field permanent), then
        // exercise the bounce / deck-bounce / delete mutations. AS-IS Permanent.CanNotBeRemoved is a per-permanent
        // predicate (Func<Permanent,bool>); Protect.Other points it at a DIFFERENT id, Protect.Conditioned fails CanUse.
        HeadlessEntityId matchId = protect == Protect.Other ? new HeadlessEntityId("p1:OTHER") : target;
        Func<bool>? condition = protect == Protect.Conditioned ? () => false : null;
        ICardEffect cnbr = CardEffectFactory.CanNotBeRemovedStaticEffect(
            permanentCondition: perm => perm.InstanceId == matchId,
            isInheritedEffect: false,
            card: new CardSource(ctx, target, P1),
            condition: condition!,
            effectName: "Can't leave battle area except by deletion effect");
        new CardSource(ctx, target, P1).cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(cnbr);
    }

    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    using (AmbientMatchContext.Enter(ctx))
    {
        sink.Apply(new EffectMutation(kind, src,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
        await sink.FlushAsync();
    }

    bool stays = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(target);
    if (stays != expectStays)
        throw new InvalidOperationException($"stays-on-battle-area expected {expectStays}, got {stays}");
}

enum Protect { None, Match, Other, Conditioned }

// (이연④-c) Minimal effect-list provider — the seam Permanent.EffectList(None) enumerates (backed by the
// per-instance CEntity_EffectControllerStore), so the placed CanNotBeRemovedClass is visible to the live
// Permanent.CanBeRemoved() scan the removal chokepoint now consults.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
