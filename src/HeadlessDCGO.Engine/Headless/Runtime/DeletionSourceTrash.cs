namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (RD-4, partial) AS-IS <c>Permanent.DiscardEvoRoots</c> (CardController.cs:3846 / Permanent.cs:106-142): when
/// a permanent is deleted, ALL its digivolution (under-)sources are trashed alongside the top card — not just
/// the top. The headless port trashed only the top and left the sources dead in <see cref="ChoiceZone.None"/>,
/// so the trash count / trash queries were short.
///
/// AS-IS trashes the sources UNCONDITIONALLY (Permanent.cs:117-128 — no keyword check). Save moves only the
/// TOP card under a Tamer (its sources are already in the trash), and Fortitude judges its replay off a
/// deletion-time source-count SNAPSHOT (<see cref="DeletionReplacementGate.SourceCountAtDeletionKey"/>), not
/// the live stack — so both work with the sources trashed, matching AS-IS. The ONLY reason a card's sources
/// are still held back here is Decode / Partition, which in the current headless model PLAY a source from
/// <see cref="ChoiceZone.None"/> in a POST window (AS-IS plays them in a PRE window, then DiscardEvoRoots
/// trashes the remainder — the PRE move is the deferred RD-4 restructure, TODO-96). ACE-overflow on a leaving
/// un-flipped ACE SOURCE (TODO-98) and LinkedCards are likewise deferred.
/// </summary>
public static class DeletionSourceTrash
{
    /// <summary>Trash the deleted card's digivolution sources. Skipped only for Decode/Partition, whose POST
    /// window still plays a source from <see cref="ChoiceZone.None"/> (their remaining-source trash awaits the
    /// PRE restructure, TODO-96). Call before/as the top card is trashed (AS-IS sources-then-top).</summary>
    public static async Task TrashEvoSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (cardId.IsEmpty || !repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        // Hold the sources back ONLY for Decode/Partition — the headless POST window plays one from
        // ChoiceZone.None. Save (top-only) and Fortitude (reads the count snapshot) do NOT need them, so their
        // sources are trashed unconditionally like AS-IS DiscardEvoRoots.
        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasDecodeKey)
            || ReadFlag(record.Metadata, DeletionReplacementGate.HasPartitionKey))
        {
            return;
        }

        int count = SourceCount(record.Metadata);
        if (count > 0)
        {
            await DigivolutionStackHelpers.TrashSourcesAsync(
                repository, zoneMover, cardId, count, fromBottom: true, cancellationToken, gameEventQueue).ConfigureAwait(false);
        }
    }

    private static int SourceCount(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
            ? ids.Count()
            : 0;

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}
