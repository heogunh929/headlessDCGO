namespace HeadlessDCGO.Engine.Headless.Runtime;

// Memory state tracker. Clamp [-10,10] mirrors AS-IS MemoryObject.cs:141-148. Cost handling is live
// (CanPay/Pay, consumed by DigivolveAction/PlayCardAction). Turn handoff is the external
// unconditional negation Set(-Current) at TurnFlowPump.cs:323.
// Sign model: this is a handoff-negation SINGLE-value model (Current is always the CURRENT turn player's
// memory; the flip is TurnFlowPump:323). AS-IS Player.MaxMemoryCost (Player.cs:1127) is a PlayerID-absolute
// model (Abs(10-M)/Abs(-10-M)). These are EQUIVALENT: Player.MemoryForPlayer applies the turn-relative sign
// (TurnPlayerId==PlayerId ? Current : -Current), so MemoryForPlayer(P0)=-M / (P1)=+M and
// MaxMemoryCost=|MemoryForPlayer+10| reproduces BOTH AS-IS branches for BOTH players at every step (verified
// by stepwise trace 2026-07-24). CanPay's `cost <= Current+10` is the turn player's |MemoryForPlayer+10|;
// non-turn-player affordability is read via Player.MaxMemoryCost, which covers both seats.
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
