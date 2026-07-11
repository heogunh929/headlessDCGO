namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
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
/// the live stack — so both work with the sources trashed, matching AS-IS. (C-1/TODO-96 done) Decode/Partition
/// used to be held back here for a POST window; they now fire in the PRE (would-be-deleted) window and pull
/// the played source(s) out of <c>sourceIds</c> BEFORE the finalize reaches here, so the remainder is trashed
/// with no special-case — exactly AS-IS (play in the cut-in, then DiscardEvoRoots trashes the rest).
///
/// (C-2/TODO-98 done) The full AS-IS <c>DiscardEvoRoots</c> order is now mirrored: snapshot the evo roots
/// (<c>DigivolutionCards</c>) and the link roots (<c>LinkedCards</c>), apply the ACE-Overflow penalty to BOTH
/// (evo THEN link — Permanent.cs:113-114) while the host is still on the field, THEN trash the evo sources and
/// finally detach/trash the link cards (Permanent.cs:117-141). A leaving un-flipped ACE SOURCE now costs its
/// owner memory just like a leaving top card, and a deleted Digimon's link cards route to the trash instead of
/// being stranded.
/// </summary>
public static class DeletionSourceTrash
{
    /// <summary>Trash the deleted card's digivolution sources UNCONDITIONALLY (AS-IS DiscardEvoRoots —
    /// Permanent.cs:117-128, no keyword check) and its link cards (Permanent.cs:130-141), applying the ACE
    /// Overflow penalty to any leaving un-flipped ACE source/link first (Permanent.cs:111-115). Call before/as
    /// the top card is trashed (AS-IS sources-then-top) — the host is still on the field, so the source's
    /// overflow existence test (AS-IS IsExistOnBattleArea/IsExistOnBreedingAreaDigimon, evaluated via its
    /// permanent) holds.
    /// (C-1/TODO-96) Decode/Partition no longer hold sources back here: they now fire in the PRE
    /// (would-be-deleted) window and pull the source(s) they play OUT of <c>sourceIds</c> before the finalize
    /// reaches here, so the remainder is trashed naturally — exactly AS-IS (play in the cut-in, then
    /// DiscardEvoRoots trashes what is left).</summary>
    /// <param name="memory">The memory controller for the ACE-Overflow penalty; null in bare contexts (no penalty).</param>
    /// <param name="turnPlayer">The current turn player, for the turn-relative sign of the overflow penalty.</param>
    /// <param name="ignoreOverflow">AS-IS <c>DiscardEvoRoots(ignoreOverflow)</c> — skip the overflow penalty
    /// entirely (a re-parent move, not a field-leave). False on every headless deletion path (all mirror the
    /// default <c>DiscardEvoRoots()</c>).</param>
    public static async Task TrashEvoSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default,
        IHeadlessMemoryController? memory = null,
        HeadlessPlayerId? turnPlayer = null,
        bool ignoreOverflow = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (cardId.IsEmpty || !repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        // AS-IS DiscardEvoRoots (Permanent.cs:108-109): snapshot BOTH root lists up front (a Clone), so the
        // overflow pass sees the full stack before any card is trashed.
        IReadOnlyList<HeadlessEntityId> evoRoots = ReadSources(record.Metadata);
        IReadOnlyList<HeadlessEntityId> linkRoots = LinkHelpers.ReadLinkedCardIds(record.Metadata);

        // (C-2/TODO-98) Overflow FIRST — evoRoots fully, THEN linkRoots (Permanent.cs:113-114) — while the host
        // is still on the field (AceOverflowClass.Overflow keeps only sources whose permanent IsExistOnBattleArea
        // / IsExistOnBreedingAreaDigimon; this runs BEFORE the top card leaves in every deletion path). Doing all
        // overflow before any trash matters: interleaving a trash would drop the host off the field and change the
        // existence test for later sources.
        if (!ignoreOverflow && memory is not null && IsHostOnField(zoneMover, record.OwnerId, cardId))
        {
            ApplyAceOverflow(repository, evoRoots, memory, turnPlayer);
            ApplyAceOverflow(repository, linkRoots, memory, turnPlayer);
        }

        // Trash the evo sources (AS-IS AddTrashCard — direct trash, no OnDigivolutionCardDiscarded trigger, so
        // gameEventQueue stays out of the overflow/trash). Unconditional, ALL sources, from the bottom.
        int count = evoRoots.Count;
        if (count > 0)
        {
            await DigivolutionStackHelpers.TrashSourcesAsync(
                repository, zoneMover, cardId, count, fromBottom: true, cancellationToken, gameEventQueue,
                // (C-3) AS-IS Permanent.DiscardEvoRoots trashes sources UNCONDITIONALLY (no
                // CanNotTrashFromDigivolutionCards check) — the deletion path bypasses trash protection entirely.
                honorProtection: false).ConfigureAwait(false);
        }

        // Detach + trash the link cards (AS-IS RemoveLinkedCard, Permanent.cs:130-135).
        foreach (HeadlessEntityId linkCardId in linkRoots)
        {
            await LinkHelpers.RemoveLinkCardAsync(
                repository, zoneMover, cardId, linkCardId, trash: true, gameEventQueue, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>AceOverflowClass.Overflow</c> (CardController.cs:5836-5851): keep the un-flipped ACE
    /// cards (<see cref="AceOverflowGate.OverflowFor"/> &gt; 0), order the turn player's own first (owner ==
    /// TurnPlayer sorts ahead), then subtract each owner's printed Overflow — the turn-relative sign moves an
    /// off-turn owner's loss toward the turn player. Non-ACE / flipped cards have overflow 0 and drop out, which
    /// also collapses AS-IS's breeding-area OR-branch (a non-ACE source's <c>OverflowMemory</c> is 0, a no-op).
    /// (R3-P1-5) internal: the sink's link-card trash (<c>ApplyTrashLinkCards</c>) runs the same
    /// <c>AceOverflowClass(trashTargets).Overflow()</c> pass (AS-IS ITrashLinkCards, CardController.cs:5331).</summary>
    internal static void ApplyAceOverflow(
        ICardInstanceRepository repository,
        IReadOnlyList<HeadlessEntityId> cardIds,
        IHeadlessMemoryController memory,
        HeadlessPlayerId? turnPlayer)
    {
        if (cardIds.Count == 0)
        {
            return;
        }

        var penalties = new List<(HeadlessPlayerId Owner, int Overflow)>();
        foreach (HeadlessEntityId cardId in cardIds)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            int overflow = AceOverflowGate.OverflowFor(card);
            if (overflow > 0)
            {
                penalties.Add((card.OwnerId, overflow));
            }
        }

        // OrderBy is a STABLE sort (mirrors AS-IS OrderBy) — the turn player's own ACE sources take their memory
        // penalty first, off-turn owners after, each group keeping stack order.
        foreach ((HeadlessPlayerId owner, int overflow) in penalties
            .OrderBy(penalty => turnPlayer is { } tp && penalty.Owner == tp ? 0 : 1))
        {
            memory.Add(AceOverflowGate.MemoryDelta(overflow, owner, turnPlayer));
        }
    }

    /// <summary>The host permanent is still on the field (battle or breeding). Mirrors AS-IS
    /// IsExistOnBattleArea / IsExistOnBreedingAreaDigimon evaluated through the source's permanent — this runs
    /// BEFORE the top card leaves, so it holds on every deletion path.</summary>
    private static bool IsHostOnField(IZoneMover zoneMover, HeadlessPlayerId owner, HeadlessEntityId hostId) =>
        zoneMover is IZoneStateReader reader
        && (reader.GetCards(owner, ChoiceZone.BattleArea).Contains(hostId)
            || reader.GetCards(owner, ChoiceZone.BreedingArea).Contains(hostId));

    private static IReadOnlyList<HeadlessEntityId> ReadSources(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
            ? ids.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => new HeadlessEntityId(value)).ToArray()
            : Array.Empty<HeadlessEntityId>();
}
