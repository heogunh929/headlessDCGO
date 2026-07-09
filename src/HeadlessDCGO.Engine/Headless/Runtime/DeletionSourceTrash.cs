namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (RD-4, partial) AS-IS <c>Permanent.DiscardEvoRoots</c> (CardController.cs:3846 / Permanent.cs:106-142): when
/// a permanent is deleted, ALL its digivolution (under-)sources are trashed alongside the top card — not just
/// the top. The headless port trashed only the top and left the sources dead in <see cref="ChoiceZone.None"/>,
/// so the trash count / trash queries were short.
///
/// SCOPE (safe subset): this trashes the sources ONLY when the deleted card carries none of the deletion-
/// replacement / replay keywords that CONSUME its sources AFTER the deletion — Save / Decode / Partition play a
/// source from <see cref="ChoiceZone.None"/>, and Fortitude reads the source COUNT to judge its replay. For
/// those, the sources are kept (current behaviour) so the POST window still works; the full AS-IS "trash the
/// REMAINING sources after the PRE/POST window consumes some" is the larger RD-4 sequence redesign (deferred).
/// ACE-overflow on a leaving un-flipped ACE SOURCE (TODO-98) is likewise deferred.
/// </summary>
public static class DeletionSourceTrash
{
    /// <summary>Trash the deleted card's digivolution sources unless a source-consuming replacement/replay
    /// keyword (Save/Decode/Partition/Fortitude) is pending on it. Call after the top card is (being) trashed.</summary>
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

        // Keep the sources when a POST source-consumer (Save/Decode/Partition) or Fortitude (reads source count
        // to replay) is on the card — those run after the deletion and need the sources present.
        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasSaveKey)
            || ReadFlag(record.Metadata, DeletionReplacementGate.HasDecodeKey)
            || ReadFlag(record.Metadata, DeletionReplacementGate.HasPartitionKey)
            || ReadFlag(record.Metadata, DeletionReplacementGate.HasFortitudeKey))
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
