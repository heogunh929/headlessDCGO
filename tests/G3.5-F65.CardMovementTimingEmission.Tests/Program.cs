using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.5: card-movement timing windows derived from the CardMoved zone transition. Discards (a card
// trashed from a NON-field zone — distinct from the field-only OnDeletion), trash recoveries, and the
// field-leave / permanent-return synonyms.

var tests = new (string Name, Action Body)[]
{
    ("Hand→Trash opens OnDiscardHand, not OnDeletion", () =>
    {
        Opens(ChoiceZone.Hand, ChoiceZone.Trash, TriggerTimings.OnDiscardHand);
        NotOpens(ChoiceZone.Hand, ChoiceZone.Trash, TriggerTimings.OnDeletion);
    }),
    ("Security→Trash opens OnDiscardSecurity", () => Opens(ChoiceZone.Security, ChoiceZone.Trash, TriggerTimings.OnDiscardSecurity)),
    ("Library→Trash opens OnDiscardLibrary", () => Opens(ChoiceZone.Library, ChoiceZone.Trash, TriggerTimings.OnDiscardLibrary)),
    // (R2-P1-4) a genuine deletion carries the delete-batch marker; unmarked field→Trash is a top-swap.
    ("BattleArea→Trash (deletion-marked) still opens OnDeletion, not a discard", () =>
    {
        Opens(ChoiceZone.BattleArea, ChoiceZone.Trash, TriggerTimings.OnDeletion, deletionMarked: true);
        NotOpens(ChoiceZone.BattleArea, ChoiceZone.Trash, TriggerTimings.OnDiscardHand, deletionMarked: true);
        NotOpens(ChoiceZone.BattleArea, ChoiceZone.Trash, TriggerTimings.OnDeletion);
    }),
    ("Trash→Hand opens OnReturnCardsToHandFromTrash", () => Opens(ChoiceZone.Trash, ChoiceZone.Hand, TriggerTimings.OnReturnCardsToHandFromTrash)),
    ("Trash→Library opens OnReturnCardsToLibraryFromTrash", () => Opens(ChoiceZone.Trash, ChoiceZone.Library, TriggerTimings.OnReturnCardsToLibraryFromTrash)),
    ("BattleArea→Hand opens OnPermanentReturnedToHand (and OnReturnToHand)", () =>
    {
        Opens(ChoiceZone.BattleArea, ChoiceZone.Hand, TriggerTimings.OnPermanentReturnedToHand);
        Opens(ChoiceZone.BattleArea, ChoiceZone.Hand, TriggerTimings.OnReturnToHand);
    }),
    ("Hand→BattleArea does NOT open OnPermanentReturnedToHand", () => NotOpens(ChoiceZone.Hand, ChoiceZone.BattleArea, TriggerTimings.OnPermanentReturnedToHand)),
    ("BattleArea→Trash (deletion-marked) opens OnRemovedField alongside WhenRemoveField", () =>
    {
        Opens(ChoiceZone.BattleArea, ChoiceZone.Trash, TriggerTimings.OnRemovedField, deletionMarked: true);
        Opens(ChoiceZone.BattleArea, ChoiceZone.Trash, TriggerTimings.WhenRemoveField, deletionMarked: true);
    }),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// & instead of && so both assertions in a pair always run (no short-circuit); each throws on failure.
bool Opens(ChoiceZone from, ChoiceZone to, string timing, bool deletionMarked = false)
{
    if (!Derive(from, to, deletionMarked).Contains(timing))
        throw new InvalidOperationException($"{from}->{to} should open {timing}.");
    return true;
}

bool NotOpens(ChoiceZone from, ChoiceZone to, string timing, bool deletionMarked = false)
{
    if (Derive(from, to, deletionMarked).Contains(timing))
        throw new InvalidOperationException($"{from}->{to} should NOT open {timing}.");
    return true;
}

static IReadOnlyList<string> Derive(ChoiceZone from, ChoiceZone to, bool deletionMarked = false)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (deletionMarked)
    {
        // (R2-P1-4) the delete-batch id every deletion finisher stamps on the move.
        metadata[MatchStateMutationSink.DeletionBatchIdKey] = 1L;
    }

    return TriggerTimingMap.Derive(new GameEvent(1, GameEventType.CardMoved, $"{from}->{to}", metadata) { ZoneFrom = from, ZoneTo = to });
}
