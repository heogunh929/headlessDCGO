using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W1: trigger-timing wiring. A single structured game event opens the canonical timings card
// effects bind to, so Phase 4 card bodies fire without further engine wiring.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Play (Hand->BattleArea) derives OnPlay + OnEnterField", () => Pure(PlayDerivesOnPlay)),
    ("Field->Trash derives OnDeletion + leave-field timings", () => Pure(DeletionDerivesTimings)),
    ("Field->Hand derives return-to-hand timings", () => Pure(ReturnToHandDerivesTimings)),
    ("Security->Trash derives OnLoseSecurity + OnDeletion", () => Pure(SecurityLossDerivesTimings)),
    ("AttackDeclared derives OnAttack", () => Pure(AttackDerivesTimings)),
    ("Explicit metadata timing overrides derivation", () => Pure(ExplicitOverrideWins)),
    ("An effect bound to OnDeletion fires when a card is trashed", () => Pure(EffectFiresOnDeletion)),
    ("An effect matching two derived timings is enqueued once (dedup)", () => Pure(MultiTimingDedup)),
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

// --- Derivation ----------------------------------------------------------

void PlayDerivesOnPlay()
{
    var timings = TriggerTimingMap.Derive(Moved(ChoiceZone.Hand, ChoiceZone.BattleArea));
    AssertContains(timings, TriggerTimings.OnPlay, "OnPlay");
    AssertContains(timings, TriggerTimings.OnEnterField, "OnEnterField");
}

void DeletionDerivesTimings()
{
    var timings = TriggerTimingMap.Derive(Moved(ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertContains(timings, TriggerTimings.OnDeletion, "OnDeletion");
    AssertContains(timings, TriggerTimings.WhenRemoveField, "WhenRemoveField");
    AssertContains(timings, TriggerTimings.OnLeaveField, "OnLeaveField");
}

void ReturnToHandDerivesTimings()
{
    var timings = TriggerTimingMap.Derive(Moved(ChoiceZone.BattleArea, ChoiceZone.Hand));
    AssertContains(timings, TriggerTimings.OnAddToHand, "OnAddToHand");
    AssertContains(timings, TriggerTimings.OnReturnToHand, "OnReturnToHand");
}

void SecurityLossDerivesTimings()
{
    var timings = TriggerTimingMap.Derive(Moved(ChoiceZone.Security, ChoiceZone.Trash));
    AssertContains(timings, TriggerTimings.OnLoseSecurity, "OnLoseSecurity");
    AssertContains(timings, TriggerTimings.OnDeletion, "OnDeletion");
}

void AttackDerivesTimings()
{
    var timings = TriggerTimingMap.Derive(new GameEvent(1, GameEventType.AttackDeclared, "atk", Empty()));
    AssertContains(timings, TriggerTimings.OnAttack, "OnAttack");
}

void ExplicitOverrideWins()
{
    GameEvent e = new(1, GameEventType.CardMoved, "x", new Dictionary<string, object?>
    {
        [AutoProcessingTriggerCollector.TriggerTimingKey] = "CustomTiming"
    })
    {
        ZoneFrom = ChoiceZone.Hand,
        ZoneTo = ChoiceZone.BattleArea
    };

    var timings = TriggerTimingMap.Derive(e);
    AssertEqual(1, timings.Count, "explicit override yields exactly one timing");
    AssertEqual("CustomTiming", timings[0], "override value");
}

// --- Live collection -----------------------------------------------------

void EffectFiresOnDeletion()
{
    var query = new InMemoryEffectQueryService();
    query.Register(EffectFor("del-effect", TriggerTimings.OnDeletion));

    var scheduler = new EffectScheduler();
    var collector = new AutoProcessingTriggerCollector(query);

    TriggerCollectionResult result = collector.CollectAndEnqueueAll(
        Moved(ChoiceZone.BattleArea, ChoiceZone.Trash), scheduler);

    AssertEqual(1, result.EnqueuedCount, "OnDeletion effect enqueued when a card is trashed");
    AssertEqual(1, scheduler.PendingCount, "scheduler holds the trigger");
}

void MultiTimingDedup()
{
    var query = new InMemoryEffectQueryService();
    // Same effect id registered under two timings that a field->Trash move both opens.
    query.Register(EffectFor("dual", TriggerTimings.OnDeletion));
    query.Register(EffectFor("dual", TriggerTimings.OnLeaveField));

    var scheduler = new EffectScheduler();
    var collector = new AutoProcessingTriggerCollector(query);

    TriggerCollectionResult result = collector.CollectAndEnqueueAll(
        Moved(ChoiceZone.BattleArea, ChoiceZone.Trash), scheduler);

    AssertEqual(1, result.EnqueuedCount, "effect matching two timings is enqueued once");
}

// --- Helpers -------------------------------------------------------------

GameEvent Moved(ChoiceZone from, ChoiceZone to) =>
    new(1, GameEventType.CardMoved, $"{from}->{to}", Empty()) { ZoneFrom = from, ZoneTo = to };

EffectRequest EffectFor(string effectId, string timing) =>
    new(new HeadlessEntityId(effectId), P1, timing,
        new EffectContext(P1, P1, new HeadlessEntityId($"src-{effectId}"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

static IReadOnlyDictionary<string, object?> Empty() => new Dictionary<string, object?>();

static void AssertContains(IReadOnlyList<string> timings, string expected, string label)
{
    if (!timings.Contains(expected))
    {
        throw new InvalidOperationException($"{label}: expected timing '{expected}' in [{string.Join(",", timings)}].");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
