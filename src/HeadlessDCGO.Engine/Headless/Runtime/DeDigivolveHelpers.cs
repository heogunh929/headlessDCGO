namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-4 De-Digivolve / SelectPermanentEffect.Mode.Degenerate) Removes the top N cards of a Digimon's
/// digivolution stack, mirroring the AS-IS <c>CardController.IDegeneration.Degeneration</c>: each step
/// trashes the current top card and promotes the immediate under-source to be the new top (the
/// ArmorPurge promotion primitive, repeated). The Digimon "regresses" to a lower form; if the stack
/// runs out (no source to promote) or a level floor (rookie, level 3) is reached, removal stops early.
///
/// Static immunity: a card flagged <c>cannotBeDeDigivolved</c> (AS-IS <c>IImmuneFromDeDigivolveEffect</c>)
/// is skipped. Continuous-replacement immunity is a caller concern (it needs the effect registry).
/// Opens <see cref="TriggerTimings.WhenTopCardTrashed"/> once when at least one card was trashed.
/// </summary>
public static class DeDigivolveHelpers
{
    public const string SourceIdsKey = "sourceIds";
    public const string IsSuspendedKey = "isSuspended";
    public const string LevelKey = "level";
    public const string CannotBeDeDigivolvedKey = "cannotBeDeDigivolved";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DeletedByEffectKey = "deletedByEffect";

    /// <summary>Level at/below which a Digimon cannot be de-digivolved further (rookie floor).</summary>
    public const int LevelFloor = 3;

    /// <summary>
    /// (B1) The Armor Purge top-trash: trash ONLY the top card and promote the immediate under-source —
    /// the permanent survives (AS-IS ArmorPurgeProcess: <c>willBeRemoveField = false</c>). Unlike
    /// de-digivolve there is NO rookie floor and NO de-digivolve immunity (those guards belong to
    /// IDegeneration only), and a TOKEN top is removed without being trashed (AS-IS ArmorPurge.cs:54-57).
    /// Emits <see cref="TriggerTimings.WhenTopCardTrashed"/> (AS-IS :69-79). Returns false when there is no
    /// under-source to promote.
    /// </summary>
    public static async Task<bool> ArmorPurgeTopAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? top) || top is null)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> sources = ReadSourceIds(top.Metadata);
        if (sources.Count == 0 ||
            !repository.TryGetInstance(sources[0], out CardInstanceRecord? promoted) || promoted is null)
        {
            return false;
        }

        // The trashed top is NOT a deleted permanent (the deletion was replaced) — strip the deletion
        // markers so no OnDeletion/POST window opens for it, then move it out.
        var topMetadata = new Dictionary<string, object?>(top.Metadata, StringComparer.Ordinal);
        topMetadata.Remove(GameFlowProcessor.PendingDeletionKey);
        topMetadata.Remove(DeletedByBattleKey);
        topMetadata.Remove(DeletedByEffectKey);
        topMetadata.Remove(DeletionReplacementGate.DeletedByOwnEffectKey);
        bool isToken = ReadFlag(top.Metadata, "isToken");
        repository.Upsert(top with { Metadata = topMetadata });

        await zoneMover.MoveAsync(
            new ZoneMoveRequest(top.OwnerId, cardId, ChoiceZone.BattleArea, isToken ? ChoiceZone.None : ChoiceZone.Trash),
            cancellationToken).ConfigureAwait(false);
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(promoted.OwnerId, sources[0], ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        var metadata = new Dictionary<string, object?>(promoted.Metadata, StringComparer.Ordinal);
        string[] remaining = sources.Skip(1).Select(id => id.Value).ToArray();
        if (remaining.Length > 0)
        {
            metadata[SourceIdsKey] = remaining;
        }
        else
        {
            metadata.Remove(SourceIdsKey);
        }

        // The permanent persists: carry tap state to the new top (AS-IS SetChangedLocationTime — continuous
        // effects re-derive from the new top), no deletion markers.
        metadata[IsSuspendedKey] = ReadFlag(top.Metadata, IsSuspendedKey);
        metadata.Remove(DeletedByBattleKey);
        metadata.Remove(DeletedByEffectKey);
        repository.Upsert(promoted with { Metadata = metadata });

        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenTopCardTrashed, subject: sources[0]);
        }

        return true;
    }

    /// <summary>
    /// De-digivolve <paramref name="cardId"/> by up to <paramref name="count"/>. Returns the number of
    /// top cards actually trashed (0 if immune or no sources).
    /// </summary>
    public static async Task<int> DeDigivolveAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        int count,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (count < 1)
        {
            return 0;
        }

        HeadlessEntityId currentTopId = cardId;
        int removed = 0;
        while (removed < count)
        {
            if (!repository.TryGetInstance(currentTopId, out CardInstanceRecord? top) || top is null)
            {
                break;
            }

            // Static de-digivolve immunity (AS-IS ImmuneFromDeDigivolve) — applies to the live top.
            if (ReadFlag(top.Metadata, CannotBeDeDigivolvedKey))
            {
                break;
            }

            IReadOnlyList<HeadlessEntityId> sources = ReadSourceIds(top.Metadata);
            if (sources.Count == 0)
            {
                break; // no under-source to promote — the stack is a single card.
            }

            // Rookie floor: do not regress a level-3 (or lower) Digimon further, when level is known.
            int? level = ReadInt(top.Metadata, LevelKey);
            if (level is int lvl && lvl <= LevelFloor)
            {
                break;
            }

            HeadlessEntityId promotedId = sources[0];
            if (!repository.TryGetInstance(promotedId, out CardInstanceRecord? promoted) || promoted is null)
            {
                break;
            }

            // Trash the current top, then promote the immediate under-source to the new top.
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(top.OwnerId, currentTopId, ChoiceZone.BattleArea, ChoiceZone.Trash),
                cancellationToken).ConfigureAwait(false);
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(promoted.OwnerId, promotedId, ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true),
                cancellationToken).ConfigureAwait(false);

            var metadata = new Dictionary<string, object?>(promoted.Metadata, StringComparer.Ordinal);
            string[] remaining = sources.Skip(1).Select(id => id.Value).ToArray();
            if (remaining.Length > 0)
            {
                metadata[SourceIdsKey] = remaining;
            }
            else
            {
                metadata.Remove(SourceIdsKey);
            }

            // The permanent persists: carry tap state to the new top, clear any stale deletion markers.
            metadata[IsSuspendedKey] = ReadFlag(top.Metadata, IsSuspendedKey);
            metadata.Remove(DeletedByBattleKey);
            metadata.Remove(DeletedByEffectKey);
            repository.Upsert(promoted with { Metadata = metadata });

            currentTopId = promotedId;
            removed++;
        }

        if (removed > 0 && gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenTopCardTrashed, subject: currentTopId);
        }

        return removed;
    }

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>(),
        };
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

    private static int? ReadInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null,
        };
    }
}
