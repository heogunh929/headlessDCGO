namespace HeadlessDCGO.Engine.Headless.Services;

public sealed class NullLogSink : ILogSink
{
    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
