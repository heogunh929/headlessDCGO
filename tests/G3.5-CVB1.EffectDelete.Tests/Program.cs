using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// CV-B1 / B-1: effect-driven Delete. The MatchStateMutationSink "Delete" kind destroys a target Digimon
// (moves it to trash, stamps deletedByEffect) BUT honours deletion-prevention — the static
// `cannotBeDeleted` flag and continuous Delete/Prevent replacements (the same source BattleDeletionGate
// consults). Unlike the raw TrashCard kind, Delete can be prevented.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Target = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Delete moves the target to trash and marks deletedByEffect", DeleteTrashesTarget),
    ("Delete is prevented by the static cannotBeDeleted flag", StaticFlagPrevents),
    ("Delete is prevented by a continuous Delete/Prevent replacement", ContinuousReplacementPrevents),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task DeleteTrashesTarget()
{
    EngineContext context = await SetupTargetOnField();
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(Target));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, Target), "target moved to trash");
    AssertFalse(InZone(context, P2, ChoiceZone.BattleArea, Target), "target left the battle area");
    AssertTrue(ReadFlag(context, Target, MatchStateMutationSink.DeletedByEffectKey), "deletedByEffect stamped");
}

async Task StaticFlagPrevents()
{
    EngineContext context = await SetupTargetOnField();
    SetFlag(context, Target, MatchStateMutationSink.CannotBeDeletedFlagKey, true);
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(Target));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, Target), "protected target stays on the field");
    AssertFalse(InZone(context, P2, ChoiceZone.Trash, Target), "protected target not trashed");
    AssertTrue(sink.SkippedCount > 0, "delete recorded as skipped/prevented");
}

async Task ContinuousReplacementPrevents()
{
    EngineContext context = await SetupTargetOnField();
    RegisterPreventDeletion(context, Target, owner: P2);
    MatchStateMutationSink sink = Sink(context);

    // (④) The effect-delete immunity scan (Permanent.CanBeDestroyed → ICardEffect.CanUse → CheckEffectDisabledClass)
    // reads GManager.instance (AmbientMatchContext) — production drives deletes inside the game loop's scope, so
    // enter it here (the sibling live scans self-enter; the Permanent getter relies on the caller's scope).
    using (AmbientMatchContext.Enter(context))
    {
        sink.Apply(Delete(Target));
        await sink.FlushAsync();
    }

    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, Target), "continuous-protected target stays on the field");
    AssertFalse(InZone(context, P2, ChoiceZone.Trash, Target), "continuous-protected target not trashed");
}

// --- Helpers -------------------------------------------------------------

// (④) pass the EngineContext so the effect-delete path's new-model deletion-immunity scan
// (Permanent.CanBeDestroyed(), guarded by `_context is not null`) is consulted — the live seam that
// replaces the retired registry `preventDeletion` binding.
MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context: context);

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

// (④ harness rewire) The invented EffectRegistry continuous "preventDeletion" binding is deleted; the
// effect-delete path now reads the AS-IS-literal Permanent.CanBeDestroyed() / NewModelContinuousScan
// .HasCanNotBeDestroyed scan (ICanNotBeDestroyedEffect over field permanents — the same seam the battle
// resolvers consult, exercised end-to-end by G3.5-R2-1). Grant the immunity the way a real card does: attach a
// live CanNotBeDestroyedClass kind-class (CardEffectFactory.CanNotBeDestroyedStaticEffect, scoped to this card)
// to the card's live effect list.
void RegisterPreventDeletion(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var holder = new CardSource(context, cardId, owner);
    using (AmbientMatchContext.Enter(context))
    {
        holder.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(
            CardEffectFactory.CanNotBeDestroyedStaticEffect(
                permanentCondition: p => p is not null && p.InstanceId == cardId,
                isInheritedEffect: false, card: holder, condition: () => true, effectName: "cannot-be-deleted"));
    }
}

async Task<EngineContext> SetupTargetOnField()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    // (④) The live immunity scan (ICardEffect.CanTrigger) gates on DoneStartGame (== turn phase past None) and
    // scans gameContext.Players_ForTurnPlayer — both empty in a raw context. Seat the players and enter Main so
    // the continuous kind-class the effect-delete path now reads is live (no pump / full match needed here).
    context.TurnController.Initialize(new[] { P1, P2 }, firstPlayerId: P2);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Target, new HeadlessEntityId("P2-M01"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, Target, ChoiceZone.None, ChoiceZone.BattleArea));
    return context;
}

void SetFlag(EngineContext context, HeadlessEntityId cardId, string key, bool value)
{
    CardInstanceRecord record = context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null
        ? r : throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> meta = new(record.Metadata, StringComparer.Ordinal) { [key] = value };
    context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }

// (④) attaches a built kind-class to a card's live effect list (the seam a ported card uses); no timing key
// so it surfaces at EffectTiming.None (the continuous-scan read point).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
