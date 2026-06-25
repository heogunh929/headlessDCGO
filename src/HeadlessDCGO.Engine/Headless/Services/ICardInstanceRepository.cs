namespace HeadlessDCGO.Engine.Headless.Services;

public interface ICardInstanceRepository : IHeadlessMatchStateResettable
{
    void Upsert(CardInstanceRecord instance);

    bool TryGetInstance(HeadlessEntityId instanceId, out CardInstanceRecord? instance);

    IReadOnlyList<CardInstanceRecord> Snapshot();
}
