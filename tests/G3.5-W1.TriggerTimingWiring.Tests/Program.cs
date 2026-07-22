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
    // (RC-6) EffectFiresOnDeletion + MultiTimingDedup removed — they registered EffectRequests into an
    // InMemoryEffectQueryService and asserted the invented AutoProcessingTriggerCollector enqueued/de-duplicated
    // them (the excised registry trigger-reader surface). The timing DERIVATION wiring (TriggerTimingMap.Derive,
    // incl. the multi-timing set a field->Trash opens) — the actual W1 concern — is retained above.
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
    // (R2-P1-4) a field->Trash move is a DELETION only when the deleting path stamped the delete-batch id;
    // an unmarked field->Trash move is a top-swap / no-trigger trash (AS-IS ArmorPurge willBeRemoveField=false,
    // TrashNoDPPermanentProcess) and derives NONE of the deletion/leave timings.
    var timings = TriggerTimingMap.Derive(Deleted(ChoiceZone.BattleArea));
    AssertContains(timings, TriggerTimings.OnDeletion, "OnDeletion");
    AssertContains(timings, TriggerTimings.WhenRemoveField, "WhenRemoveField");
    AssertContains(timings, TriggerTimings.OnLeaveField, "OnLeaveField");

    var unmarked = TriggerTimingMap.Derive(Moved(ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertDoesNotContain(unmarked, TriggerTimings.OnDeletion, "unmarked field->Trash (top-swap) is not a deletion");
    AssertDoesNotContain(unmarked, TriggerTimings.OnLeaveField, "unmarked field->Trash derives no leave timing");
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
    // D-5: trashing a security card is NOT a field deletion -> OnDeletion must NOT be opened.
    AssertDoesNotContain(timings, TriggerTimings.OnDeletion, "OnDeletion not opened by security trash");
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

// --- Helpers -------------------------------------------------------------

GameEvent Moved(ChoiceZone from, ChoiceZone to) =>
    new(1, GameEventType.CardMoved, $"{from}->{to}", Empty()) { ZoneFrom = from, ZoneTo = to };

// (R2-P1-4) a DELETION move carries the delete-batch id marker every deletion finisher stamps.
GameEvent Deleted(ChoiceZone from) =>
    new(1, GameEventType.CardMoved, $"{from}->Trash", new Dictionary<string, object?>
    {
        [MatchStateMutationSink.DeletionBatchIdKey] = 1L,
    })
    { ZoneFrom = from, ZoneTo = ChoiceZone.Trash };

EffectRequest EffectFor(string effectId, string timing) =>
    new(new HeadlessEntityId(effectId), P1, timing,
        new EffectContext(P1, P1, new HeadlessEntityId($"src-{effectId}"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

static IReadOnlyDictionary<string, object?> Empty() => new Dictionary<string, object?>();

static void AssertDoesNotContain(IReadOnlyList<string> timings, string unexpected, string label)
{
    if (timings.Contains(unexpected))
    {
        throw new InvalidOperationException($"{label}: did not expect '{unexpected}' in [{string.Join(", ", timings)}].");
    }
}

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
