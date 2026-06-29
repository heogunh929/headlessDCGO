namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-1 Link) Link-card attach/detach for a Digimon permanent. Mirrors the AS-IS <c>Permanent</c> link
/// model (<c>LinkedCards</c> list, <c>LinkedDP</c>, <c>LinkedMax</c>, <c>AddLinkCard</c>/
/// <c>RemoveLinkedCard</c>) using the same off-field-storage pattern as
/// <see cref="DigivolutionStackHelpers"/>: linked cards move to <see cref="ChoiceZone.None"/> and are
/// tracked on the host permanent's metadata (<c>linkedCardIds</c>, ordered newest-first like the original
/// which inserts at index 0). The accumulated link DP is kept in <c>linkedDp</c> for the DP calculator.
///
/// Timing windows (F-6.9): the caller opens <see cref="TriggerTimings.WhenLinked"/> after an attach and
/// <see cref="TriggerTimings.OnLinkCardDiscarded"/> when linked cards are trashed — both emitted here via
/// the game-event queue when one is supplied.
/// </summary>
public static class LinkHelpers
{
    public const string LinkedCardIdsKey = "linkedCardIds";
    public const string LinkedDpKey = "linkedDp";
    public const string LinkedMaxKey = "linkedMax";
    public const string LinkDpKey = "linkDp";

    /// <summary>Default maximum number of link cards a Digimon can hold (AS-IS <c>LinkedMax</c> default 1).</summary>
    public const int DefaultLinkedMax = 1;

    /// <summary>The link cards currently attached to <paramref name="metadata"/>'s host (newest first).</summary>
    public static IReadOnlyList<HeadlessEntityId> ReadLinkedCardIds(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!metadata.TryGetValue(LinkedCardIdsKey, out object? raw) || raw is null)
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

    /// <summary>The accumulated link DP on the host (sum of attached cards' LinkDP).</summary>
    public static int ReadLinkedDp(IReadOnlyDictionary<string, object?> metadata) =>
        ReadInt(metadata, LinkedDpKey) ?? 0;

    /// <summary>The host's link maximum (its <c>linkedMax</c> override, else <see cref="DefaultLinkedMax"/>).</summary>
    public static int ReadLinkedMax(IReadOnlyDictionary<string, object?> metadata) =>
        ReadInt(metadata, LinkedMaxKey) ?? DefaultLinkedMax;

    /// <summary>
    /// (AS-IS <c>Permanent.AddLinkCard</c>) Attach <paramref name="linkCardId"/> to <paramref name="hostId"/>:
    /// move the link card off-field, prepend it to the host's linked list, add its LinkDP, and open the
    /// WhenLinked window. Excess over the host's max is trashed first (AS-IS force-remove). Returns true
    /// when attached.
    /// </summary>
    public static async Task<bool> AddLinkCardAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId,
        ChoiceZone fromZone,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null ||
            !repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) || linkCard is null)
        {
            return false;
        }

        await zoneMover.MoveAsync(
            new ZoneMoveRequest(linkCard.OwnerId, linkCardId, fromZone, ChoiceZone.None),
            cancellationToken).ConfigureAwait(false);

        // Re-read host (the move may have touched state) and prepend (AS-IS insert at index 0).
        CardInstanceRecord current = repository.TryGetInstance(hostId, out CardInstanceRecord? latest) && latest is not null ? latest : host;
        List<string> linked = ReadLinkedCardIds(current.Metadata).Select(id => id.Value).ToList();
        linked.Insert(0, linkCardId.Value);
        int linkedDp = ReadLinkedDp(current.Metadata) + (ReadInt(linkCard.Metadata, LinkDpKey) ?? 0);
        repository.Upsert(current with { Metadata = WithLinked(current.Metadata, linked, linkedDp) });

        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenLinked, actor: current.OwnerId, subject: hostId);
        }

        // AS-IS: if over max, force-trash the oldest excess link cards.
        await EnforceLinkedMaxAsync(repository, zoneMover, hostId, gameEventQueue, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// (AS-IS <c>Permanent.RemoveLinkedCard</c>) Detach <paramref name="linkCardId"/> from
    /// <paramref name="hostId"/>: remove it from the linked list, subtract its LinkDP, optionally trash it
    /// (default), and open the OnLinkCardDiscarded window. Returns true when removed.
    /// </summary>
    public static async Task<bool> RemoveLinkCardAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        HeadlessEntityId linkCardId,
        bool trash = true,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return false;
        }

        List<string> linked = ReadLinkedCardIds(host.Metadata).Select(id => id.Value).ToList();
        if (!linked.Remove(linkCardId.Value))
        {
            return false;
        }

        int linkDp = repository.TryGetInstance(linkCardId, out CardInstanceRecord? linkCard) && linkCard is not null
            ? ReadInt(linkCard.Metadata, LinkDpKey) ?? 0
            : 0;
        int linkedDp = Math.Max(0, ReadLinkedDp(host.Metadata) - linkDp);
        repository.Upsert(host with { Metadata = WithLinked(host.Metadata, linked, linkedDp) });

        if (trash && linkCard is not null)
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(linkCard.OwnerId, linkCardId, ChoiceZone.None, ChoiceZone.Trash),
                cancellationToken).ConfigureAwait(false);
        }

        if (gameEventQueue is not null)
        {
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.OnLinkCardDiscarded, actor: host.OwnerId, subject: hostId);
        }

        return true;
    }

    /// <summary>(AS-IS auto-processing <c>IsDigimonLackLinkMaxCountProcess</c>) Trash the oldest link cards
    /// beyond the host's max. Returns the number trashed.</summary>
    public static async Task<int> EnforceLinkedMaxAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId hostId,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
        {
            return 0;
        }

        int max = ReadLinkedMax(host.Metadata);
        IReadOnlyList<HeadlessEntityId> linked = ReadLinkedCardIds(host.Metadata);
        if (linked.Count <= max)
        {
            return 0;
        }

        // Oldest cards are at the end of the newest-first list.
        var excess = linked.Skip(max).ToArray();
        int trashed = 0;
        foreach (HeadlessEntityId linkCardId in excess)
        {
            if (await RemoveLinkCardAsync(repository, zoneMover, hostId, linkCardId, trash: true, gameEventQueue, cancellationToken).ConfigureAwait(false))
            {
                trashed++;
            }
        }

        return trashed;
    }

    private static Dictionary<string, object?> WithLinked(IReadOnlyDictionary<string, object?> metadata, IReadOnlyList<string> linked, int linkedDp)
    {
        var copy = new Dictionary<string, object?>(metadata, StringComparer.Ordinal);
        if (linked.Count > 0)
        {
            copy[LinkedCardIdsKey] = linked.ToArray();
            copy[LinkedDpKey] = linkedDp;
        }
        else
        {
            copy.Remove(LinkedCardIdsKey);
            copy.Remove(LinkedDpKey);
        }

        return copy;
    }

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
            string s when int.TryParse(s, out int parsed) => parsed,
            _ => null,
        };
    }
}
