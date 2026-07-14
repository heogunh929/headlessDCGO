using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W4 batch 1d (G9-040): protection restrictions wired behavior-live through new sink / battle-gate
// consults: CantSuspend, CannotReturnToHand, CannotReturnToDeck, CanNotBeDestroyedByBattle (battle-only),
// ImmuneStackTrashing.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CantSuspend -> Suspend mutation is blocked", CantSuspend),
    ("CannotReturnToHand -> ReturnToHand blocked (stays in play)", CannotReturnToHand),
    ("CannotReturnToDeck -> ReturnToDeckBottom blocked (stays in play)", CannotReturnToDeck),
    ("CanNotBeDestroyedByBattle -> battle prevented but effect deletion still works", CanNotBeDestroyedByBattle),
    ("ImmuneStackTrashing -> effect source-trash blocked (sources remain)", ImmuneStackTrashing),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CantSuspend()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    // SEAM (post-stage-B): CanNotSuspendClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.CanNotSuspend (RD-P6B-11, MatchStateMutationSink's SuspendKind case) — attach it
    // to the card's own controller.
    var source = new CardSource(context, id, P1);
    ICardEffect effect = CardEffectFactory.CantSuspendStaticEffect(null, false, source, null, $"cs:{id.Value}");
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    await ApplyKind(context, id, MatchStateMutationSink.SuspendKind);
    AssertTrue(!ReadBool(context, id, "isSuspended"), "not suspended (blocked)");
}

async Task CannotReturnToHand()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    // SEAM (post-stage-B): CannotReturnToHandClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasCannotReturnToHand (RD-P6B-12, MatchStateMutationSink.IsRestrictedFromCause) —
    // attach it to the card's own controller.
    var source = new CardSource(context, id, P1);
    ICardEffect effect = CardEffectFactory.CannotReturnToHandStaticEffect(null, null, false, source, null, $"crh:{id.Value}");
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    await ApplyKind(context, id, MatchStateMutationSink.ReturnToHandKind);
    AssertTrue(InBattle(context, P1, id), "stays in play (bounce blocked)");
}

async Task CannotReturnToDeck()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    // SEAM (post-stage-B): CannotReturnToLibraryClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasCannotReturnToLibrary (RD-P6B-12, MatchStateMutationSink.IsRestrictedFromCause)
    // — attach it to the card's own controller.
    var source = new CardSource(context, id, P1);
    ICardEffect effect = CardEffectFactory.CannotReturnToDeckStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false, card: source, condition: null, effectName: $"crd:{id.Value}");
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    await ApplyKind(context, id, MatchStateMutationSink.ReturnToDeckBottomKind);
    AssertTrue(InBattle(context, P1, id), "stays in play (deck-return blocked)");
}

async Task CanNotBeDestroyedByBattle()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    // SEAM (post-stage-B): CanNotBeDestroyedByBattleClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasCanNotBeDestroyedByBattle (RD-P6B-10, BattleDeletionGate.PreventsBattleDeletion)
    // — attach it to the card's own controller.
    // TEST-BUG FIX: CanNotBeDestroyedByBattleClass.CanNotBeDestroyedByBattle (AS-IS 1:1,
    // CanNotBeDestroyedByBattleClass.cs) is gated by `if (_canNotBeDestroyedByBattleCondition != null)` FIRST —
    // unlike most other factories' PermanentCondition, a NULL condition here means "never protects" (the whole
    // predicate short-circuits to false), not "accept unconditionally". A real card always supplies a concrete
    // predicate; pass an explicit accept-all `(p, a, d, dc) => true` instead of null.
    var source = new CardSource(context, id, P1);
    ICardEffect effect = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        (p, a, d, dc) => true, null, false, source, null, $"cbdb:{id.Value}");
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(context, id), "battle deletion prevented");
    // effect deletion still applies
    await ApplyKind(context, id, MatchStateMutationSink.DeleteKind);
    AssertTrue(!InBattle(context, P1, id), "effect deletion still deletes it");
}

async Task ImmuneStackTrashing()
{
    EngineContext context = Context();
    var host = await Place(context, P1, "HOST");
    var mat = await PlaceOffField(context, P1, "MAT");
    context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r);
    context.CardInstanceRepository.Upsert(r! with { Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { ["sourceIds"] = new[] { mat.Value } } });
    var immuneEffect = CardEffectFactory.ImmuneStackTrashingClass(false, new CardSource(context, host, P1), null);
    if (!LegacyBindingBridge.TryToBinding(immuneEffect, $"ist:{host.Value}", out var immuneBinding) || immuneBinding is null)
        throw new InvalidOperationException($"{immuneEffect.GetType().Name} has no ToBinding bridge.");
    context.EffectRegistry.Register(immuneBinding);

    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashDigivolutionCardsKind, host,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = host.Value, ["count"] = 1 }));
    await sink.FlushAsync();

    context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? after);
    bool stillHasSource = after!.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids && ids.Contains(mat.Value);
    AssertTrue(stillHasSource, "digivolution source not trashed (immune)");
}

// --- Helpers -------------------------------------------------------------

async Task ApplyKind(EngineContext context, HeadlessEntityId target, string kind)
{
    var sink = Sink(context);
    sink.Apply(new EffectMutation(kind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = target.Value }));
    await sink.FlushAsync();
}

// TEST-BUG FIX: the sink's `context:` parameter was omitted, so `_context` stayed null and every new-model
// union in MatchStateMutationSink that gates on `_context is not null` (SuspendKind -> CanNotSuspend,
// IsRestrictedFromCause -> HasCannotReturnToHand/HasCannotReturnToLibrary) silently never fired — passing it
// costs nothing for the legacy-only paths this suite ALSO exercises (they never read `_context`).
MatchStateMutationSink Sink(EngineContext context) => new(
    context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue,
    context: context);

bool ReadBool(EngineContext context, HeadlessEntityId id, string key) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null &&
    r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

bool InBattle(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id) =>
    context.ZoneMover is IZoneStateReader r && r.GetCards(owner, ChoiceZone.BattleArea).Contains(id);

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 940);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    HeadlessEntityId id = await Register(context, owner, tag);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

Task<HeadlessEntityId> PlaceOffField(EngineContext context, HeadlessPlayerId owner, string tag) => Register(context, owner, tag);

async Task<HeadlessEntityId> Register(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await Task.CompletedTask;
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
