namespace HeadlessDCGO.Engine.Headless.Diagnostics;

public interface ITraceSink
{
    void Record(string category, string message, IReadOnlyDictionary<string, object?>? metadata = null);
}
