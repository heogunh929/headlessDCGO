namespace HeadlessDCGO.Engine.Headless.Effects;

public sealed class EffectResolutionQueue
{
    private readonly Queue<PendingEffect> _effects = new();

    public int Count => _effects.Count;

    public void Enqueue(PendingEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects.Enqueue(effect);
    }

    public bool TryPeek(out PendingEffect? effect)
    {
        if (_effects.Count == 0)
        {
            effect = null;
            return false;
        }

        effect = _effects.Peek();
        return true;
    }

    public bool TryDequeue(out PendingEffect? effect)
    {
        if (_effects.Count == 0)
        {
            effect = null;
            return false;
        }

        effect = _effects.Dequeue();
        return true;
    }

    public IReadOnlyList<PendingEffect> Snapshot()
    {
        return Array.AsReadOnly(_effects.ToArray());
    }

    public int Clear()
    {
        int removedCount = _effects.Count;
        _effects.Clear();
        return removedCount;
    }
}
