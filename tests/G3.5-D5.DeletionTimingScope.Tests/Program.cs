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
    // (RC-6) OnDeletionEffectScopedToField removed — it registered an EffectRequest into an
    // InMemoryEffectQueryService and asserted the invented AutoProcessingTriggerCollector enqueued it (the excised
    // registry trigger-reader surface). The field-vs-non-field DERIVATION scoping (TriggerTimingMap.Derive matrix
    // above) — the actual D-5 rule — is retained.
    ("An UNMARKED field->Trash move (top-swap / no-DP trash) does NOT derive OnDeletion (R2-P1-4)", () => Pure(UnmarkedFieldTrashIsNotDeletion)),
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
    // (R2-P1-4) a genuine deletion move carries the delete-batch marker (all deletion finishers stamp it);
    // the non-field sources must NOT derive OnDeletion even WITH a marker-bearing metadata (field-only rule).
    var timings = TriggerTimingMap.Derive(Moved(from, ChoiceZone.Trash, deletionMarked: true));
    bool has = timings.Contains(TriggerTimings.OnDeletion);
    if (has != expectDeletion)
    {
        throw new InvalidOperationException(
            $"{from}->Trash: expected OnDeletion={expectDeletion}, got {has} (timings: {string.Join(", ", timings)}).");
    }
}

static void UnmarkedFieldTrashIsNotDeletion()
{
    // AS-IS: Armor Purge / Burst / De-Digivolve trash the TOP while the permanent survives
    // (willBeRemoveField=false — only WhenTopCardTrashed), and TrashNoDPPermanentProcess is a direct
    // no-trigger trash. Neither stacks OnDestroyedAnyone, so a marker-less field->Trash derives nothing.
    var timings = TriggerTimingMap.Derive(Moved(ChoiceZone.BattleArea, ChoiceZone.Trash, deletionMarked: false));
    if (timings.Contains(TriggerTimings.OnDeletion) || timings.Contains(TriggerTimings.OnLeaveField))
    {
        throw new InvalidOperationException(
            $"unmarked BattleArea->Trash must derive neither OnDeletion nor OnLeaveField (timings: {string.Join(", ", timings)}).");
    }
}

// --- Helpers -------------------------------------------------------------

static GameEvent Moved(ChoiceZone from, ChoiceZone to, bool deletionMarked = false)
{
    var metadata = new Dictionary<string, object?>();
    if (deletionMarked)
    {
        // (R2-P1-4) the delete-batch id every deletion finisher stamps on the move.
        metadata[MatchStateMutationSink.DeletionBatchIdKey] = 1L;
    }

    return new GameEvent(1, GameEventType.CardMoved, "moved", metadata)
    {
        ZoneFrom = from,
        ZoneTo = to,
    };
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
