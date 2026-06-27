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

    sink.Apply(Delete(Target));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, Target), "continuous-protected target stays on the field");
    AssertFalse(InZone(context, P2, ChoiceZone.Trash, Target), "continuous-protected target not trashed");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

void RegisterPreventDeletion(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{cardId.Value}"),
        triggerEntityId: null, targetEntityIds: new[] { cardId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["preventDeletion"] = true });
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"prevent:{cardId.Value}"), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }));
}

async Task<EngineContext> SetupTargetOnField()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
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
