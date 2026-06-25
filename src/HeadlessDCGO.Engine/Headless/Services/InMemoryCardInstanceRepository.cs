namespace HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryCardInstanceRepository : ICardInstanceRepository
{
    private readonly Dictionary<HeadlessEntityId, CardInstanceRecord> _instances = new();

    public void Upsert(CardInstanceRecord instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.InstanceId] = instance;
    }

    public bool TryGetInstance(HeadlessEntityId instanceId, out CardInstanceRecord? instance)
    {
        return _instances.TryGetValue(instanceId, out instance);
    }

    public IReadOnlyList<CardInstanceRecord> Snapshot()
    {
        return _instances.Values.ToArray();
    }

    public void Clear()
    {
        ResetMatchState();
    }

    public void ResetMatchState()
    {
        _instances.Clear();
    }
}
