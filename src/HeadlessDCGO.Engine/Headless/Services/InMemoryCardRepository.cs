namespace HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryCardRepository : ICardRepository
{
    private readonly Dictionary<HeadlessEntityId, CardRecord> _cards = new();

    public void Upsert(CardRecord card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _cards[card.Id] = card;
    }

    public bool Remove(HeadlessEntityId id)
    {
        return _cards.Remove(id);
    }

    public bool TryGetCard(HeadlessEntityId id, out CardRecord? card)
    {
        return _cards.TryGetValue(id, out card);
    }

    public CardRecord GetCard(HeadlessEntityId id)
    {
        return TryGetCard(id, out CardRecord? card)
            ? card!
            : throw new KeyNotFoundException($"Card definition was not found: {id}.");
    }

    public IReadOnlyList<CardRecord> Query(CardQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _cards.Values
            .Where(query.Matches)
            .OrderBy(card => card.CardNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(card => card.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<CardRecord> Snapshot()
    {
        return _cards.Values
            .OrderBy(card => card.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public void Clear()
    {
        _cards.Clear();
    }
}
