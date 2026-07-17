namespace HeadlessDCGO.Engine.Headless.State;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Projects the live <c>sourceIds</c> metadata storage into the typed <see cref="DigivolutionStack"/>
/// read-model (B1b integration). The metadata list remains the canonical storage written by the
/// digivolve action / source-stack port; this reader is the single typed *view* over it, so battle,
/// observation, and ported digivolution effects read <see cref="StackedCard"/>s (top card, base DP,
/// under-cards) instead of re-deriving the stack from untyped metadata each time.
///
/// Ordering note: <c>sourceIds</c> is stored newest-under-card first (the card just digivolved from
/// is index 0, the DigiEgg is last) — see <c>DigivolveAction.AttachTargetAsSource</c>. The stack model
/// wants bottom (DigiEgg) first, top Digimon last, so the under-cards are reversed before the live top
/// card is appended with the <see cref="StackRole.Top"/> role.
/// </summary>
public static class DigivolutionStackReader
{
    public const string SourceIdsKey = "sourceIds";
    public const string DpKey = "dp";
    public const string LevelKey = "level";

    // (RD-RLENV-03 / B2 substrate-only) Memoized read: the B2 profile put this reader (and the stack
    // ctor/projections under it) at ~76% of total CPU — it is re-invoked for every battle-area card on
    // every mirror effect scan (CardSource.PermanentOfThisCard et al.), while its output is a PURE
    // function of (top CardInstanceRecord, the under-cards' CardInstanceRecords, immutable definitions).
    // CardInstanceRecord is an immutable record (metadata copied into a ReadOnlyDictionary at init;
    // every mutation path Upserts a NEW record — and every sourceIds writer stores a fresh string[]),
    // so a cache keyed on the top record's object identity, VALIDATED against the current object
    // identity of every under-card record it was built from, provably returns the identical value the
    // un-cached rebuild would produce. Entries are held weakly per record (no cross-match leak; keys
    // die with the match's repository) and are immutable, so concurrent matches (RLB1-01) see no shared
    // mutable state.
    private sealed record CacheEntry(
        DigivolutionStack Stack,
        ICardInstanceRepository Repository,
        ICardRepository Definitions,
        HeadlessEntityId[] UnderIds,
        CardInstanceRecord?[] UnderRecords);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CardInstanceRecord, CacheEntry> Cache = new();

    public static DigivolutionStack Read(
        ICardInstanceRepository instances,
        ICardRepository cards,
        HeadlessEntityId topCardId)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(cards);

        if (topCardId.IsEmpty ||
            !instances.TryGetInstance(topCardId, out CardInstanceRecord? topInstance) ||
            topInstance is null)
        {
            return DigivolutionStack.Empty;
        }

        if (Cache.TryGetValue(topInstance, out CacheEntry? cached) &&
            ReferenceEquals(cached.Repository, instances) &&
            ReferenceEquals(cached.Definitions, cards) &&
            UnderRecordsUnchanged(instances, cached))
        {
            return cached.Stack;
        }

        // Under-cards, deepest (DigiEgg) first. (RD-RLENV-03 / B2 substrate-only) The former
        // Reverse().ToArray() + List<> buildup allocated three intermediate collections per read on the
        // hottest path in the engine; the projection is now a single exact-size array indexed in reverse
        // (identical order and roles: stored newest-under-card first, so stacked[0] = last stored id =
        // the DigiEgg).
        IReadOnlyList<HeadlessEntityId> underNewestFirst = ReadSourceIds(topInstance.Metadata);
        int underCount = underNewestFirst.Count;
        var stacked = new StackedCard[underCount + 1];
        var underIds = underCount == 0 ? Array.Empty<HeadlessEntityId>() : new HeadlessEntityId[underCount];
        var underRecords = underCount == 0 ? Array.Empty<CardInstanceRecord?>() : new CardInstanceRecord?[underCount];
        for (int index = 0; index < underCount; index++)
        {
            StackRole role = index == 0 ? StackRole.DigiEgg : StackRole.Digivolution;
            HeadlessEntityId underId = underNewestFirst[underCount - 1 - index];
            instances.TryGetInstance(underId, out CardInstanceRecord? underRecord);
            underIds[index] = underId;
            underRecords[index] = underRecord;
            stacked[index] = BuildCard(instances, cards, underId, role, underRecord);
        }

        stacked[underCount] = BuildCard(instances, cards, topCardId, StackRole.Top, topInstance);
        var stack = new DigivolutionStack(stacked);
        Cache.AddOrUpdate(topInstance, new CacheEntry(stack, instances, cards, underIds, underRecords));
        return stack;
    }

    // A cached entry is valid only while every under-card id still resolves to the SAME record object it
    // was built from (including "still missing" for ids that had no record). Any Upsert of an under card
    // replaces its record object and fails this check, forcing a rebuild.
    private static bool UnderRecordsUnchanged(ICardInstanceRepository instances, CacheEntry entry)
    {
        HeadlessEntityId[] ids = entry.UnderIds;
        CardInstanceRecord?[] records = entry.UnderRecords;
        for (int i = 0; i < ids.Length; i++)
        {
            instances.TryGetInstance(ids[i], out CardInstanceRecord? current);
            if (!ReferenceEquals(current, records[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static StackedCard BuildCard(
        ICardInstanceRepository instances,
        ICardRepository cards,
        HeadlessEntityId cardId,
        StackRole role,
        CardInstanceRecord? known = null)
    {
        CardInstanceRecord? instance = known;
        if (instance is null)
        {
            instances.TryGetInstance(cardId, out instance);
        }

        CardRecord? definition = null;
        if (instance is not null)
        {
            cards.TryGetCard(instance.DefinitionId, out definition);
        }

        IReadOnlyDictionary<string, object?>? meta = instance?.Metadata;
        IReadOnlyDictionary<string, object?>? defMeta = definition?.Metadata;

        int level = ReadInt(meta, LevelKey) ?? ReadInt(defMeta, LevelKey) ?? ReadInt(defMeta, "Level") ?? 0;
        int baseDp = ReadInt(meta, DpKey) ?? ReadInt(defMeta, DpKey) ?? ReadInt(defMeta, "DP") ?? 0;

        return new StackedCard(cardId, definition?.CardNumber ?? string.Empty, role, level, baseDp);
    }

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            // (RD-RLENV-03 / B2) The canonical storage is already an indexable id list — return it
            // uncopied. Only Read consumes this (locally, single-threaded, no escape), so dropping the
            // defensive ToArray is observation-equivalent.
            IReadOnlyList<HeadlessEntityId> entityIdList => entityIdList,
            IEnumerable<HeadlessEntityId> entityIds => entityIds.ToArray(),
            // (RD-RLENV-03 / B2) string[] is the canonical stored shape (every writer stores
            // Select(id => id.Value).ToArray()) — parse it with an exact-size loop instead of the LINQ
            // Where/Select/ToArray chain (LargeArrayBuilder was ~33% of total CPU at this call rate).
            // Identical filter (null/whitespace skipped) and order.
            string[] stringArray => ParseStringIds(stringArray),
            IEnumerable<string> stringIds => stringIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string value => value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => new HeadlessEntityId(part))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>(),
        };
    }

    private static IReadOnlyList<HeadlessEntityId> ParseStringIds(string[] values)
    {
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var ids = new HeadlessEntityId[count];
        int next = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                ids[next++] = new HeadlessEntityId(values[i]);
            }
        }

        return ids;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            double doubleValue when doubleValue % 1 == 0 && doubleValue is >= int.MinValue and <= int.MaxValue => (int)doubleValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null,
        };
    }
}
