namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class CardDatabase : ICardRepository
{
    private readonly InMemoryCardRepository _repository = new();

    public int Count => _repository.Snapshot().Count;

    public void Upsert(CardRecord card)
    {
        _repository.Upsert(card);
    }

    public void UpsertRange(IEnumerable<CardRecord> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        foreach (CardRecord card in cards)
        {
            Upsert(card);
        }
    }

    public bool TryGetCard(HeadlessEntityId id, out CardRecord? card)
    {
        return _repository.TryGetCard(id, out card);
    }

    public CardRecord GetCard(HeadlessEntityId id)
    {
        return _repository.GetCard(id);
    }

    public IReadOnlyList<CardRecord> Query(CardQuery query)
    {
        return _repository.Query(query);
    }

    public IReadOnlyList<CardRecord> Snapshot()
    {
        return _repository.Snapshot();
    }

    public void Clear()
    {
        _repository.Clear();
    }
}
