namespace HeadlessDCGO.Engine.Headless.Services;

public interface ILogSink
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
