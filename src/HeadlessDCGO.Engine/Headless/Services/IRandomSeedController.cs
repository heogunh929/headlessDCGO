namespace HeadlessDCGO.Engine.Headless.Services;

public interface IRandomSeedController
{
    void ResetSeed(int seed);
}

public interface IRandomStateReader
{
    int CurrentSeed { get; }
}
