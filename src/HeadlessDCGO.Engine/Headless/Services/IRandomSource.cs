namespace HeadlessDCGO.Engine.Headless.Services;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();

    void Shuffle<T>(IList<T> items);
}
