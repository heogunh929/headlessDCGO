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

        // Under-cards, deepest (DigiEgg) first.
        IReadOnlyList<HeadlessEntityId> underBottomToTop = ReadSourceIds(topInstance.Metadata)
            .Reverse()
            .ToArray();

        var stacked = new List<StackedCard>(underBottomToTop.Count + 1);
        for (int index = 0; index < underBottomToTop.Count; index++)
        {
            StackRole role = index == 0 ? StackRole.DigiEgg : StackRole.Digivolution;
            stacked.Add(BuildCard(instances, cards, underBottomToTop[index], role));
        }

        stacked.Add(BuildCard(instances, cards, topCardId, StackRole.Top, topInstance));
        return new DigivolutionStack(stacked);
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
            IEnumerable<HeadlessEntityId> entityIds => entityIds.ToArray(),
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
