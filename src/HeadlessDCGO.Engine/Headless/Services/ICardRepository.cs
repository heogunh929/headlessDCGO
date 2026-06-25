namespace HeadlessDCGO.Engine.Headless.Services;

public interface ICardRepository
{
    CardRecord GetCard(HeadlessEntityId id);

    bool TryGetCard(HeadlessEntityId id, out CardRecord? card);

    IReadOnlyList<CardRecord> Query(CardQuery query);

    IReadOnlyList<CardRecord> Snapshot();
}
