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
    // MIGRATION-NOTE (P7 test-fix): CanNotSuspendClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The suspend mutation gate this test checks
    // reads only the substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this
    // grant observable yet. Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked,
    // not silently weakened.
    CardEffectFactory.CantSuspendStaticEffect(null, false, new CardSource(context, id, P1), null, $"cs:{id.Value}");
    await ApplyKind(context, id, MatchStateMutationSink.SuspendKind);
    AssertTrue(!ReadBool(context, id, "isSuspended"), "not suspended (blocked)");
}

async Task CannotReturnToHand()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // MIGRATION-NOTE (P7 test-fix): CannotReturnToHandClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The bounce mutation
    // gate this test checks reads only the substrate EffectRegistry, not the AS-IS live scan, so there is no
    // buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED TO FAIL until
    // stage B lands — tracked, not silently weakened.
    CardEffectFactory.CannotReturnToHandStaticEffect(null, null, false, new CardSource(context, id, P1), null, $"crh:{id.Value}");
    await ApplyKind(context, id, MatchStateMutationSink.ReturnToHandKind);
    AssertTrue(InBattle(context, P1, id), "stays in play (bounce blocked)");
}

async Task CannotReturnToDeck()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // MIGRATION-NOTE (P7 test-fix): CannotReturnToLibraryClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The deck-return
    // mutation gate this test checks reads only the substrate EffectRegistry, not the AS-IS live scan, so there
    // is no buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED TO FAIL
    // until stage B lands — tracked, not silently weakened.
    CardEffectFactory.CannotReturnToDeckStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false, card: new CardSource(context, id, P1), condition: null, effectName: $"crd:{id.Value}");
    await ApplyKind(context, id, MatchStateMutationSink.ReturnToDeckBottomKind);
    AssertTrue(InBattle(context, P1, id), "stays in play (deck-return blocked)");
}

async Task CanNotBeDestroyedByBattle()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // MIGRATION-NOTE (P7 test-fix): CanNotBeDestroyedByBattleClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test
    // checks (BattleDeletionGate.PreventsBattleDeletion) reads only the substrate EffectRegistry, not the AS-IS
    // live scan, so there is no buildable way to make this grant observable yet. Assertion below is UNCHANGED
    // and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(null, null, false, new CardSource(context, id, P1), null, $"cbdb:{id.Value}");
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

MatchStateMutationSink Sink(EngineContext context) => new(
    context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);

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
