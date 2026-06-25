namespace HeadlessDCGO.Engine.Headless.Runtime;

// TODO: Replace this clamp-only tracker with real memory handoff and cost handling.
public sealed class InMemoryHeadlessMemoryController : IHeadlessMemoryController
{
    private int _minimum = -10;
    private int _maximum = 10;

    public HeadlessMemoryState Current { get; private set; } = HeadlessMemoryState.Default;

    public void Initialize(int initialMemory, int minimum = -10, int maximum = 10)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        _minimum = minimum;
        _maximum = maximum;
        Current = new HeadlessMemoryState(
            Clamp(initialMemory),
            _minimum,
            _maximum);
    }

    public HeadlessMemoryState Set(int value)
    {
        Current = Current with { Current = Clamp(value) };
        return Current;
    }

    public HeadlessMemoryState Add(int amount)
    {
        return Set(Current.Current + amount);
    }

    public bool CanPay(int cost)
    {
        return cost >= 0 && Current.Current - cost >= _minimum;
    }

    public HeadlessMemoryState Pay(int cost)
    {
        if (cost < 0)
        {
            return Add(-cost);
        }

        return Set(Current.Current - cost);
    }

    public void ResetMatchState()
    {
        _minimum = HeadlessMemoryState.Default.Minimum;
        _maximum = HeadlessMemoryState.Default.Maximum;
        Current = HeadlessMemoryState.Default;
    }

    private int Clamp(int value)
    {
        return Math.Min(_maximum, Math.Max(_minimum, value));
    }
}
