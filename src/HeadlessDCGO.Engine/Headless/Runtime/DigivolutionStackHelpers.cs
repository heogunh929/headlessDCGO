namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-22 Save / C-23 Material Save / C-24 Training) Shared digivolution-stack mutations. All three
/// keywords build on the AS-IS <c>Permanent.AddDigivolutionCardsBottom</c> primitive — placing a card
/// under a permanent as a digivolution source (bottom of the stack). In the headless model the sources
/// live off-field (<see cref="ChoiceZone.None"/>) and are tracked by the top card's <c>sourceIds</c>
/// metadata (ordered top→bottom), so "add to bottom" appends to that list.
///
/// Save is wired as a post-deletion response (<see cref="DeletionReplacementGate.TrySaveAsync"/>);
/// Material Save and Training are ACTIVATED effects with no passive trigger point, so the engine exposes
/// the primitive here and the card-facing activation is authored at porting time.
/// </summary>
public static class DigivolutionStackHelpers
{
    public const string SourceIdsKey = "sourceIds";
    public const string IsSuspendedKey = "isSuspended";
    public const string CanSuspendKey = "canSuspend";

    /// <summary>Moves <paramref name="cards"/> from <paramref name="fromZone"/> off-field and appends them
    /// (in order) to the BOTTOM of <paramref name="targetId"/>'s digivolution stack.</summary>
    public static async Task AddSourcesBottomAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId targetId,
        IReadOnlyList<HeadlessEntityId> cards,
        ChoiceZone fromZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0 || !repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null)
        {
            return;
        }

        var appended = new List<string>();
        foreach (HeadlessEntityId cardId in cards)
        {
            if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
            {
                continue;
            }

            await zoneMover.MoveAsync(
                new ZoneMoveRequest(card.OwnerId, cardId, fromZone, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
            appended.Add(cardId.Value);
        }

        AppendSources(repository, target, appended);
    }

    /// <summary>(C-23 Material Save) Moves the first <paramref name="count"/> digivolution sources of
    /// <paramref name="fromId"/> to the bottom of <paramref name="toId"/>'s stack (pure re-parent — the
    /// source cards already live off-field). Returns true when at least one source moved.</summary>
    public static bool MoveSourcesBottom(
        ICardInstanceRepository repository,
        HeadlessEntityId fromId,
        HeadlessEntityId toId,
        int count)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (count < 1 ||
            fromId == toId ||
            !repository.TryGetInstance(fromId, out CardInstanceRecord? source) || source is null ||
            !repository.TryGetInstance(toId, out CardInstanceRecord? destination) || destination is null)
        {
            return false;
        }

        List<string> fromSources = ReadSourceIds(source.Metadata).Select(id => id.Value).ToList();
        if (fromSources.Count == 0)
        {
            return false;
        }

        List<string> moved = fromSources.Take(Math.Min(count, fromSources.Count)).ToList();
        List<string> remaining = fromSources.Skip(moved.Count).ToList();

        repository.Upsert(source with { Metadata = WithSources(source.Metadata, remaining) });
        AppendSources(repository, destination, moved);
        return true;
    }

    /// <summary>(C-24 Training) Suspends <paramref name="permanentId"/> (the cost) and places the top card
    /// of its owner's library at the bottom of its own stack. Returns true when trained.</summary>
    public static async Task<bool> TrainAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId permanentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(permanentId, out CardInstanceRecord? permanent) || permanent is null ||
            ReadFlag(permanent.Metadata, IsSuspendedKey) ||
            !ReadFlag(permanent.Metadata, CanSuspendKey, defaultValue: true) ||
            zoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> library = zones.GetCards(permanent.OwnerId, ChoiceZone.Library);
        if (library.Count == 0)
        {
            return false;
        }

        // Cost: suspend self.
        repository.Upsert(permanent with
        {
            Metadata = new Dictionary<string, object?>(permanent.Metadata, StringComparer.Ordinal)
            {
                [IsSuspendedKey] = true,
            }
        });

        await AddSourcesBottomAsync(repository, zoneMover, permanentId, new[] { library[0] }, ChoiceZone.Library, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>(B-10) Trash <paramref name="count"/> of <paramref name="hostId"/>'s digivolution sources
    /// (from the bottom/DigiEgg end by default) — move them off-field to the trash and drop them from the
    /// host's stack. Returns the number trashed.</summary>
    public static Task<int> TrashSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom = true,
        CancellationToken cancellationToken = default) =>
        RemoveSourcesAsync(repository, zoneMover, hostId, count, fromBottom, ChoiceZone.Trash, cancellationToken);

    /// <summary>(B-10) Return <paramref name="count"/> of <paramref name="hostId"/>'s digivolution sources
    /// to <paramref name="destination"/> (Hand / Library top via MoveToDeck-style zones). Returns the count moved.</summary>
    public static Task<int> ReturnSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        ChoiceZone destination,
        bool fromBottom = true,
        CancellationToken cancellationToken = default) =>
        RemoveSourcesAsync(repository, zoneMover, hostId, count, fromBottom, destination, cancellationToken);

    /// <summary>(G10-007) Remove a SPECIFIC digivolution source from <paramref name="hostId"/> and move it to
    /// <paramref name="destination"/> (e.g. the battle area, to play it as another Digimon). Returns true if
    /// the source belonged to the host and was moved.</summary>
    public static async Task<bool> PlaySpecificSourceAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId sourceId,
        ChoiceZone destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (sourceId.IsEmpty || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> sources = ReadSourceIds(host.Metadata).Select(id => id.Value).ToList();
        if (!sources.Remove(sourceId.Value))
        {
            return false;
        }

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, sources) });
        HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
            ? src.OwnerId
            : host.OwnerId;
        await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<int> RemoveSourcesAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        int count,
        bool fromBottom,
        ChoiceZone destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (count < 1 || !repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return 0;
        }

        List<string> sources = ReadSourceIds(host.Metadata).Select(id => id.Value).ToList();
        if (sources.Count == 0)
        {
            return 0;
        }

        int take = Math.Min(count, sources.Count);
        // Bottom (DigiEgg) is the end of the top→bottom list; top is the start.
        List<string> removed = fromBottom
            ? sources.Skip(sources.Count - take).ToList()
            : sources.Take(take).ToList();
        List<string> remaining = fromBottom
            ? sources.Take(sources.Count - take).ToList()
            : sources.Skip(take).ToList();

        repository.Upsert(host with { Metadata = WithSources(host.Metadata, remaining) });

        foreach (string sourceValue in removed)
        {
            var sourceId = new HeadlessEntityId(sourceValue);
            HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
                ? src.OwnerId
                : host.OwnerId;
            await zoneMover.MoveAsync(new ZoneMoveRequest(owner, sourceId, ChoiceZone.None, destination), cancellationToken).ConfigureAwait(false);
        }

        return removed.Count;
    }

    private static void AppendSources(ICardInstanceRepository repository, CardInstanceRecord target, IReadOnlyList<string> add)
    {
        if (add.Count == 0)
        {
            return;
        }

        // Re-read the target so an earlier append in the same operation is preserved.
        CardInstanceRecord current = repository.TryGetInstance(target.InstanceId, out CardInstanceRecord? latest) && latest is not null
            ? latest
            : target;
        List<string> sources = ReadSourceIds(current.Metadata).Select(id => id.Value).ToList();
        sources.AddRange(add);
        repository.Upsert(current with { Metadata = WithSources(current.Metadata, sources) });
    }

    private static Dictionary<string, object?> WithSources(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> sources)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (sources.Count > 0)
        {
            copy[SourceIdsKey] = sources.ToArray();
        }
        else
        {
            copy.Remove(SourceIdsKey);
        }

        return copy;
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
            _ => Array.Empty<HeadlessEntityId>()
        };
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key, bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return defaultValue;
        }

        return raw is bool value ? value : defaultValue;
    }
}
