namespace HeadlessDCGO.Engine.Headless.Diagnostics;

public sealed class NullTraceSink : ITraceSink
{
    public void Record(string category, string message, IReadOnlyDictionary<string, object?>? metadata = null)
    {
    }
}
