using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D5: the OnDeletion (AS-IS OnDestroyedAnyone) timing window must open only when a FIELD card is
// destroyed to the trash. A hand discard, deck mill, or security-check trash is NOT a deletion. Before
// the fix, TriggerTimingMap opened OnDeletion for ANY move into the trash, so "when destroyed" effects
// could mis-fire on discards/mill/security.

var tests = new (string Name, Func<Task> Body)[]
{
    ("BattleArea->Trash derives OnDeletion", () => Pure(() => AssertDeletion(ChoiceZone.BattleArea, true))),
    ("BreedingArea->Trash derives OnDeletion", () => Pure(() => AssertDeletion(ChoiceZone.BreedingArea, true))),
    ("Hand->Trash (discard) does NOT derive OnDeletion", () => Pure(() => AssertDeletion(ChoiceZone.Hand, false))),
    ("Library->Trash (mill) does NOT derive OnDeletion", () => Pure(() => AssertDeletion(ChoiceZone.Library, false))),
    ("Security->Trash (security check) does NOT derive OnDeletion", () => Pure(() => AssertDeletion(ChoiceZone.Security, false))),
    ("An OnDeletion effect fires on field destruction but not on a hand discard", () => Pure(OnDeletionEffectScopedToField)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Derivation matrix ---------------------------------------------------

static void AssertDeletion(ChoiceZone from, bool expectDeletion)
{
    var timings = TriggerTimingMap.Derive(Moved(from, ChoiceZone.Trash));
    bool has = timings.Contains(TriggerTimings.OnDeletion);
    if (has != expectDeletion)
    {
        throw new InvalidOperationException(
            $"{from}->Trash: expected OnDeletion={expectDeletion}, got {has} (timings: {string.Join(", ", timings)}).");
    }
}

// --- Behavioral E2E ------------------------------------------------------

void OnDeletionEffectScopedToField()
{
    // A card bound to OnDeletion: it must be collected when destroyed from the battle area,
    // and NOT collected when discarded from hand.
    AssertEqual(1, EnqueuedFor(ChoiceZone.BattleArea), "OnDeletion enqueued on field destruction");
    AssertEqual(0, EnqueuedFor(ChoiceZone.Hand), "OnDeletion NOT enqueued on a hand discard");
}

static int EnqueuedFor(ChoiceZone from)
{
    var query = new InMemoryEffectQueryService();
    query.Register(new EffectRequest(
        new HeadlessEntityId("del-fx"), new HeadlessPlayerId(1), TriggerTimings.OnDeletion,
        new EffectContext(new HeadlessPlayerId(1), new HeadlessPlayerId(1), new HeadlessEntityId("src"),
            triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())));

    var scheduler = new EffectScheduler();
    var collector = new AutoProcessingTriggerCollector(query);
    TriggerCollectionResult result = collector.CollectAndEnqueueAll(Moved(from, ChoiceZone.Trash), scheduler);
    return result.EnqueuedCount;
}

// --- Helpers -------------------------------------------------------------

static GameEvent Moved(ChoiceZone from, ChoiceZone to) =>
    new(1, GameEventType.CardMoved, "moved", new Dictionary<string, object?>())
    {
        ZoneFrom = from,
        ZoneTo = to,
    };

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
