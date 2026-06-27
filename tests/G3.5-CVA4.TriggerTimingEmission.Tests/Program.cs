using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// CV-A4 / F-6: trigger-timing emission. New timing windows so ported cards have a firing site:
//   - OnMove (EffectTiming.OnMove) — derived from a BreedingArea→BattleArea promotion only.
//   - OnTapped / OnUntapped (OnTappedAnyone/OnUnTappedAnyone) — emitted by the mutation sink on
//     Suspend/Unsuspend (non-zone-move state changes, so not CardMoved-derived).
//   - OnEndBattle — emitted by BattleResolver after the battle resolves.
//   - OnDeletion-for-Delete lock: a field→Trash move (what effect Delete produces) opens OnDeletion.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:main:C1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("OnMove opens for a breeding→battle promotion only", () => Pure(OnMovePromotion)),
    ("OnMove does not open for a hand→battle play", () => Pure(OnMoveNotForPlay)),
    ("OnDeletion opens for a field→Trash move (effect Delete path)", () => Pure(OnDeletionForFieldTrash)),
    ("Suspend emits an OnTapped timing window", SuspendEmitsOnTapped),
    ("Unsuspend emits an OnUntapped timing window", UnsuspendEmitsOnUntapped),
    ("Sink without a queue does not throw on suspend", SinkWithoutQueueIsSafe),
    ("BattleResolver emits OnEndBattle after resolving", BattleEmitsOnEndBattle),
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

void OnMovePromotion()
{
    IReadOnlyList<string> timings = TriggerTimingMap.Derive(Moved(ChoiceZone.BreedingArea, ChoiceZone.BattleArea));
    AssertTrue(timings.Contains(TriggerTimings.OnMove), "promotion opens OnMove");
}

void OnMoveNotForPlay()
{
    IReadOnlyList<string> timings = TriggerTimingMap.Derive(Moved(ChoiceZone.Hand, ChoiceZone.BattleArea));
    AssertFalse(timings.Contains(TriggerTimings.OnMove), "a hand→battle play is not an OnMove");
    AssertTrue(timings.Contains(TriggerTimings.OnPlay), "but it is an OnPlay");
}

void OnDeletionForFieldTrash()
{
    IReadOnlyList<string> timings = TriggerTimingMap.Derive(Moved(ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(timings.Contains(TriggerTimings.OnDeletion), "field→Trash opens OnDeletion");
}

async Task SuspendEmitsOnTapped()
{
    EngineContext context = await SetupCardOnField();
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(new EffectMutation(MatchStateMutationSink.SuspendKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.Value }));
    await sink.FlushAsync();

    AssertTrue(QueueOpens(context, TriggerTimings.OnTapped), "OnTapped window opened");
}

async Task UnsuspendEmitsOnUntapped()
{
    EngineContext context = await SetupCardOnField();
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(new EffectMutation(MatchStateMutationSink.UnsuspendKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.Value }));
    await sink.FlushAsync();

    AssertTrue(QueueOpens(context, TriggerTimings.OnUntapped), "OnUntapped window opened");
}

async Task SinkWithoutQueueIsSafe()
{
    EngineContext context = await SetupCardOnField();
    // No GameEventQueue passed — emission must be a no-op, not a crash.
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.SuspendKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.Value }));
    await sink.FlushAsync();
    AssertTrue(sink.AppliedCount > 0, "suspend still applied without a queue");
}

async Task BattleEmitsOnEndBattle()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 31);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    HeadlessEntityId atk = new("p1:main:A");
    HeadlessEntityId def = new("p2:main:B");
    cards.Upsert(new CardRecord(new HeadlessEntityId("A"), "A", "A", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("B"), "B", "B", new Dictionary<string, object?>(), CardType: "Digimon"));

    await PlaceWithDp(context, P1, atk, "A", dp: 9000, suspended: false);
    await PlaceWithDp(context, P2, def, "B", dp: 5000, suspended: true);
    context.AttackController.DeclareAttack(P1, atk, P2, def, isDirectAttack: false);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(context);
    AssertTrue(result.IsSuccess, "battle resolved");

    AssertTrue(QueueOpens(context, TriggerTimings.OnEndBattle), "OnEndBattle window opened");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);

GameEvent Moved(ChoiceZone from, ChoiceZone to) =>
    new(1, GameEventType.CardMoved, $"{from}->{to}", new Dictionary<string, object?>(StringComparer.Ordinal))
    {
        ZoneFrom = from,
        ZoneTo = to,
    };

bool QueueOpens(EngineContext context, string timing) =>
    context.GameEventQueue.DrainPending().Any(e => TriggerTimingMap.Derive(e).Contains(timing));

async Task<EngineContext> SetupCardOnField()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 19);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending(); // discard the placement CardMoved so only the mutation event remains
    return context;
}

async Task PlaceWithDp(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, string defId, int dp, bool suspended)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(defId), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BattleResolver.DpKey] = dp,
            ["isSuspended"] = suspended,
        }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending();
}

static Task Pure(Action body) { body(); return Task.CompletedTask; }

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
