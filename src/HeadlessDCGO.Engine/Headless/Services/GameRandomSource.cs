namespace HeadlessDCGO.Engine.Headless.Services;

public sealed class GameRandomSource :
    IRandomSource,
    IRandomSeedController,
    IRandomStateReader
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public GameRandomSource(int seed = 0)
    {
        ResetSeed(seed);
    }

    public int CurrentSeed { get; private set; }

    public void ResetSeed(int seed)
    {
        CurrentSeed = seed;

        ulong state = unchecked((ulong)(long)seed);
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        _s2 = SplitMix64(ref state);
        _s3 = SplitMix64(ref state);

        if (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0)
        {
            _s0 = 1;
        }
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        ulong threshold = unchecked(0UL - range) % range;

        ulong raw;
        do
        {
            raw = NextUInt64();
        } while (raw < threshold);

        return (int)(minInclusive + (long)(raw % range));
    }

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = NextInt(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private ulong NextUInt64()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    private static ulong RotateLeft(ulong value, int offset)
    {
        return (value << offset) | (value >> (64 - offset));
    }

    private static ulong SplitMix64(ref ulong state)
    {
        ulong value = state += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
